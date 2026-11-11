using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Game.UI.Localization;
using Unity.Entities;

namespace DoIt.Actions;

/// <summary>
/// <para>
/// The one catalogue of actions, fed by sources and searched for the UI.
/// It holds no engine dependency: it reads the game through <see cref="ActionRegistryServices"/>.
/// </para>
/// <para>
/// Every call into a source is wrapped: a throw while listing marks the source down until a later
/// listing succeeds, a throw in an action's reader, invoke, or setter disables that action for the
/// session.
/// Each is logged once, with the mod name and version behind the source.
/// </para>
/// </summary>
internal sealed class ActionRegistry {
  /// <summary>
  /// The most rows a search writes when its caller sets no cap, which bounds every re-read of the
  /// rows on the tick.
  /// Measured in game 1.6.4f1 over 500 actions (<c>ActionsMeasurement</c>), the CPU held at 2 GHz,
  /// a search costs about 1.8 ms, one the typo pass runs over in full about 4 ms, and a re-read of
  /// 20 rows about 80 µs.
  /// </summary>
  internal const int DefaultRowCap = 20;

  /// <summary>
  /// The reason an entity action fired bare is disabled with when it has no entity to take, until a
  /// pick tool can ask the player for one.
  /// </summary>
  internal static readonly LocalizedString SelectFirstReason =
    LocalizedString.Id("DoIt.Actions.REASON_SELECT_FIRST");

  /// <summary>
  /// The reason an action whose reader, invoke, or setter threw is disabled with.
  /// </summary>
  internal static readonly LocalizedString FailedReason =
    LocalizedString.Id("DoIt.Actions.REASON_FAILED");

  private readonly ActionRegistryServices services;

  private readonly List<IActionSource> sources = [];

  // The sources whose last listing threw, logged when they enter the set.
  private readonly HashSet<IActionSource> downSources = [];

  // The actions whose reader, invoke, or setter threw, disabled for the session and logged when
  // they enter the set, so a source fixing itself on a rebuild does not bring them back.
  private readonly HashSet<string> faultedIds = [];

  // The ids listed twice, logged when they enter the set.
  private readonly HashSet<string> duplicateIds = [];

  // How often and when each action ran, by canonical id, saved through the use store on each use.
  private readonly Dictionary<string, ActionUse> uses;

  // Rebuilt whole from every source on any change, see Rebuild.
  private IReadOnlyList<IndexedAction> catalogue = [];

  // The catalogue by canonical id, rebuilt with it.
  private IReadOnlyDictionary<string, IndexedAction> catalogueById =
    new Dictionary<string, IndexedAction>();

  internal ActionRegistry(ActionRegistryServices services) {
    this.services = services;
    this.uses = new Dictionary<string, ActionUse>(services.UseStore.Load());
  }

  /// <summary>
  /// Every source, whether its last listing threw, and how many actions it lists.
  /// </summary>
  internal IReadOnlyList<ActionSourceStatus> Sources =>
    this.sources
      .Select(source => new ActionSourceStatus(
          source.Info,
          this.downSources.Contains(source),
          this.catalogue.Count(action => action.Source == source.Info)
        )
      )
      .ToList();

  /// <summary>
  /// Adds a source, rebuilding on its <see cref="IActionSource.Changed"/> from now on.
  /// </summary>
  internal void AddSource(IActionSource source) {
    this.sources.Add(source);

    source.Changed += this.Rebuild;

    this.Rebuild();
  }

  /// <summary>
  /// Lists every source again and resolves every display text, so search matches what the player
  /// reads in the current locale.
  /// Measured in game 1.6.4f1 over 500 actions (<c>ActionsMeasurement</c>), the CPU held at 2 GHz,
  /// it costs about 3.5 ms, paid on a source's change or a locale change alone.
  /// </summary>
  internal void Rebuild() {
    var catalogue = new List<IndexedAction>();
    var ids = new HashSet<string>();

    foreach (var source in this.sources) {
      if (this.TryList(source) is not {} actions) {
        continue;
      }

      foreach (var action in actions) {
        if (ids.Add(action.Id)) {
          catalogue.Add(action);
        }
        else if (this.duplicateIds.Add(action.Id)) {
          this.services.Log.Warn(
            $"{nameof(ActionRegistry)}: {ActionRegistry.Describe(action)} is listed twice, the " +
            "second one is left out."
          );
        }
      }
    }

    this.catalogue = catalogue;
    this.catalogueById = catalogue.ToDictionary(static action => action.Id);
  }

  /// <summary>
  /// Ranks the actions every term of <paramref name="query"/> hits, as <see cref="ActionSearch"/>
  /// places them, and writes at most <paramref name="rowCap"/> rows, reading the live parts of
  /// those rows alone.
  /// An empty query lists every action, the most used of late first.
  /// </summary>
  internal ActionResults Search(
    string query,
    int sequence,
    int rowCap = ActionRegistry.DefaultRowCap
  ) {
    var mode = this.services.ReadGameMode();
    var selection = this.services.ReadSelection();
    var now = this.services.Now();

    var matches = ActionSearch.Find(
      ActionSearch.Terms(query),
      this.catalogue.Where(action => (action.Descriptor.Modes & mode) is not 0),
      rowCap,
      this.services.Matcher
    );

    var rows = new List<ActionRow>();

    // A stable sort: rows equal on every key keep the order found, the catalogue's, with the typo
    // pass's rows after the clean ones.
    // The shorter title breaks a score tie alone, so a whole label ranks above a longer one opening
    // with it; the empty query, where every row ties, keeps catalogue order.
    var ranked = matches
      .OrderBy(static match => match.Rank)
      .ThenByDescending(match => ActionSearch.Frecency(this.UseOf(match.Action), now))
      .ThenByDescending(static match => match.Score)
      .ThenBy(static match => match.Terms.Count is 0 ? 0 : match.Action.Label.Length);

    foreach (var match in ranked) {
      if (rows.Count >= rowCap) {
        break;
      }

      if (this.ReadLiveParts(match.Action, selection) is {} live) {
        var (hits, alias) = ActionSearch.Hits(match);

        rows.Add(this.ToRow(match.Action, hits, alias, live));
      }
    }

    return new ActionResults(sequence, rows);
  }

  /// <summary>
  /// Rewrites the rows of <paramref name="results"/> with their live parts read afresh, keeping
  /// their hits and the query's sequence number.
  /// A row whose action reads hidden now, or no longer exists in the current game mode, leaves the
  /// list and the rows below it move up: no other match takes its place.
  /// </summary>
  internal ActionResults Refresh(ActionResults results) {
    var selection = this.services.ReadSelection();
    var rows = new List<ActionRow>();

    foreach (var row in results.Rows) {
      if (this.TryFind(row.Id, out var action) &&
        this.ReadLiveParts(action, selection) is {} live) {
        rows.Add(this.ToRow(action, row.Hits, row.Alias, live));
      }
    }

    return results with {
      Rows = rows
    };
  }

  /// <summary>
  /// Reads the live parts of <paramref name="ids"/> afresh, in the order given, leaving out an id
  /// that does not exist in the current game mode or reads hidden.
  /// </summary>
  internal List<ActionLive> ReadLive(IEnumerable<string> ids) {
    var selection = this.services.ReadSelection();
    var lives = new List<ActionLive>();

    foreach (var id in ids) {
      if (this.TryFind(id, out var action) && this.ReadLiveParts(action, selection) is {} live) {
        lives.Add(live);
      }
    }

    return lives;
  }

  /// <summary>
  /// Runs a C# call action, after reading its availability afresh: a disabled or hidden answer
  /// refuses it without calling its delegate.
  /// An entity action takes <paramref name="entity"/>, else the selection unless it opted out.
  /// </summary>
  internal ActionInvokeOutcome Invoke(string id, Entity entity = default) {
    if (!this.TryFind(id, out var action)) {
      return ActionInvokeOutcome.Unknown;
    }

    if (action.Descriptor.Channel is null) {
      return ActionInvokeOutcome.NoInvoke;
    }

    if (action.Descriptor.Channel is not CSharpCallChannel call) {
      return ActionInvokeOutcome.NotACSharpCall;
    }

    var subject = ActionRegistry.Subject(action, this.services.ReadSelection(), entity);

    if (this.ReadAvailability(action, subject).Kind is not ActionAvailabilityKind.Enabled) {
      return ActionInvokeOutcome.Refused;
    }

    try {
      call.Invoke(subject);
    }
    // Foreign code: whatever it throws is contained to its action.
    catch (Exception ex) {
      this.Fault(action, ex, "invoked");

      return ActionInvokeOutcome.Failed;
    }

    this.RecordUse(action.Id);

    return ActionInvokeOutcome.Invoked;
  }

  /// <summary>
  /// Sets an action's state from the UI's string form of a value, after reading its availability
  /// afresh: a disabled or hidden answer refuses it without calling its setter.
  /// A toggle takes <c>true</c> or <c>false</c>, a number an invariant-culture number within its
  /// bounds, a choice one of its keys, a text anything.
  /// </summary>
  internal ActionSetOutcome SetState(string id, string value) {
    if (!this.TryFind(id, out var action)) {
      return ActionSetOutcome.Unknown;
    }

    if (action.Descriptor.State is not { IsSettable: true } state) {
      return ActionSetOutcome.ReadOnly;
    }

    if (ActionRegistry.Setter(state, value) is not {} set) {
      return ActionSetOutcome.Invalid;
    }

    var subject = ActionRegistry.Subject(action, this.services.ReadSelection());

    if (this.ReadAvailability(action, subject).Kind is not ActionAvailabilityKind.Enabled) {
      return ActionSetOutcome.Refused;
    }

    try {
      set();
    }
    // Foreign code: whatever it throws is contained to its action.
    catch (Exception ex) {
      this.Fault(action, ex, "setting its state");

      return ActionSetOutcome.Failed;
    }

    this.RecordUse(action.Id);

    return ActionSetOutcome.Set;
  }

  /// <summary>
  /// Records the use of an action the UI fired on its side, whatever its reader answers now: the
  /// note lands after the effect, so a fresh answer judges the state the action itself made.
  /// The UI fires no disabled row, and sends no note for an input action no handler took.
  /// </summary>
  internal ActionInvokeOutcome RecordInvoked(string id) {
    if (!this.TryFind(id, out var action)) {
      return ActionInvokeOutcome.Unknown;
    }

    this.RecordUse(action.Id);

    return ActionInvokeOutcome.Invoked;
  }

  /// <summary>
  /// Lists a source's actions and resolves their texts, or marks it down when it throws: nothing it
  /// said can be trusted then.
  /// </summary>
  private List<IndexedAction>? TryList(IActionSource source) {
    try {
      var actions = source.List()
        .Select(descriptor => this.Index(source.Info, descriptor))
        .ToList();

      this.downSources.Remove(source);

      return actions;
    }
    // Foreign code: whatever it throws is contained to its source.
    catch (Exception ex) {
      if (this.downSources.Add(source)) {
        var info = source.Info;

        this.services.Log.Warn(
          ex,
          $"{nameof(ActionRegistry)}: Source \"{info.Segment}\" of {info.ModName} " +
          $"{info.ModVersion} threw while listing its actions, they are left out until it lists " +
          "again."
        );
      }

      return null;
    }
  }

  private IndexedAction Index(ActionSourceInfo source, ActionDescriptor descriptor) {
    var resolve = this.services.ResolveText;

    var label = resolve(descriptor.Label);
    var description = descriptor.Description is {} text ? resolve(text) : null;

    var options = descriptor.State is ChoiceState choice
      ? choice.Options.Select(option => new ChoiceOption(option.Key, resolve(option.Label)))
        .ToList()
      : [];

    var title = new SearchField(ActionField.Title, label);

    List<SearchField> fields = [
      title,
      .. descriptor.Aliases.Select(alias => new SearchField(ActionField.Alias, resolve(alias)))
    ];

    // The title in the source locale, so a player on another one finds the action by its English
    // name too; on the source locale, it is the title.
    if (this.services.ResolveSourceText(descriptor.Label) is {} sourceLabel) {
      var sourceTitle = new SearchField(ActionField.Alias, sourceLabel);

      if (sourceTitle.Folded.Text != title.Folded.Text) {
        fields.Add(sourceTitle);
      }
    }

    fields.AddRange(
      options.Select(static option =>
        new SearchField(ActionField.Option, option.Label, option.Key)
      )
    );

    if (description is not null) {
      fields.Add(new SearchField(ActionField.Description, description));
    }

    return new IndexedAction(
      new ActionId(source.Segment, descriptor.LocalId).ToString(),
      source,
      descriptor,
      label,
      description,
      resolve(descriptor.Category ?? source.DisplayName),
      options
    ) {
      Fields = fields
    };
  }

  /// <summary>
  /// The entity an action receives: none for an action taking none, else the explicit one, else the
  /// selection unless the action opted out of it.
  /// </summary>
  private static Entity Subject(IndexedAction action, Entity selection, Entity entity = default) =>
    action.Descriptor.Subject switch {
      ActionSubject.None => Entity.Null,
      _ when entity != Entity.Null => entity,
      _ when action.Descriptor.FillsFromSelection => selection,
      _ => Entity.Null
    };

  /// <summary>
  /// The call setting a settable state to <paramref name="value"/>, or none when the value does not
  /// parse for its shape or falls outside its bounds or options.
  /// </summary>
  private static Action? Setter(ActionState state, string value) {
    switch (state) {
      case ToggleState { Set: {} set }:
        return bool.TryParse(value, out var toggle) ? () => set(toggle) : null;
      case NumberState { Set: {} set } number:
        var isNumber = double.TryParse(
          value,
          NumberStyles.Float,
          CultureInfo.InvariantCulture,
          out var parsed
        );

        // NaN fails every bound comparison, hence its own check.
        return isNumber &&
          !double.IsNaN(parsed) &&
          !double.IsInfinity(parsed) &&
          !(parsed < number.Min) &&
          !(parsed > number.Max)
            ? () => set(parsed)
            : null;
      case ChoiceState { Set: {} set } choice:
        return choice.Options.Any(option => option.Key == value) ? () => set(value) : null;
      case TextState { Set: {} set }: return () => set(value);
      default: return null;
    }
  }

  /// <summary>
  /// Finds an action of the catalogue that exists in the current game mode.
  /// </summary>
  private bool TryFind(string id, [NotNullWhen(true)] out IndexedAction? action) {
    action = this.catalogueById.TryGetValue(id, out var found) &&
      (found.Descriptor.Modes & this.services.ReadGameMode()) is not 0
        ? found
        : null;

    return action is not null;
  }

  /// <summary>
  /// The availability and the state of an action read afresh, or none when it reads hidden, whose
  /// state is then left unread.
  /// A state reader that throws disables the action, as any of its readers does.
  /// </summary>
  private ActionLive? ReadLiveParts(IndexedAction action, Entity selection) {
    var availability = this.ReadAvailability(action, ActionRegistry.Subject(action, selection));

    if (availability.Kind is ActionAvailabilityKind.Hidden) {
      return null;
    }

    var state = this.ReadState(action);

    if (this.faultedIds.Contains(action.Id)) {
      availability = ActionAvailability.Disabled(ActionRegistry.FailedReason);
    }

    return new ActionLive(
      action.Id,
      availability.Kind is ActionAvailabilityKind.Enabled,
      availability.Reason is {} reason ? this.services.ResolveText(reason) : null,
      state
    );
  }

  /// <summary>
  /// The action's state as its reader answers now, or none for an action with no state or one that
  /// faulted.
  /// </summary>
  private ActionStateValue? ReadState(IndexedAction action) {
    if (action.Descriptor.State is not {} state || this.faultedIds.Contains(action.Id)) {
      return null;
    }

    try {
      return state switch {
        ToggleState toggle => new ToggleValue(toggle.Read()) {
          IsSettable = state.IsSettable
        },
        NumberState number => new NumberValue(number.Read(), number.Min, number.Max, number.Step) {
          IsSettable = state.IsSettable
        },
        ChoiceState choice =>
          new ChoiceValue(action.Options, ActionRegistry.NotNull(choice.Read())) {
            IsSettable = state.IsSettable
          },
        TextState text => new TextValue(ActionRegistry.NotNull(text.Read())) {
          IsSettable = state.IsSettable
        },
        _ => throw new InvalidOperationException($"No reading for a {state.GetType()}.")
      };
    }
    // Foreign code: whatever it throws is contained to its action.
    catch (Exception ex) {
      this.Fault(action, ex, "reading its state");

      return null;
    }
  }

  // Foreign code can answer null despite the annotation, and a null string written to the UI drops
  // the property, which changes the value's shape.
  private static string NotNull(string? read) =>
    read ?? throw new InvalidOperationException("The reader answered null.");

  /// <summary>
  /// The reader's answer, an enabled one turned disabled for an entity action with no entity to
  /// take; a hidden or disabled answer passes through.
  /// </summary>
  private ActionAvailability ReadAvailability(IndexedAction action, Entity subject) {
    var availability = this.ReadReader(action);

    return availability.Kind is ActionAvailabilityKind.Enabled &&
      action.Descriptor.Subject is ActionSubject.Entity &&
      subject == Entity.Null
        ? ActionAvailability.Disabled(ActionRegistry.SelectFirstReason)
        : availability;
  }

  /// <summary>
  /// The reader's own answer, enabled for an action with none, and disabled for one that faulted.
  /// </summary>
  private ActionAvailability ReadReader(IndexedAction action) {
    if (this.faultedIds.Contains(action.Id)) {
      return ActionAvailability.Disabled(ActionRegistry.FailedReason);
    }

    try {
      return action.Descriptor.Availability?.Invoke() ?? ActionAvailability.Enabled;
    }
    // Foreign code: whatever it throws is contained to its action.
    catch (Exception ex) {
      this.Fault(action, ex, "reading its availability");

      return ActionAvailability.Disabled(ActionRegistry.FailedReason);
    }
  }

  private ActionUse UseOf(IndexedAction action) =>
    this.uses.TryGetValue(action.Id, out var use) ? use : default;

  private void RecordUse(string id) {
    this.uses.TryGetValue(id, out var use);
    this.uses[id] = new ActionUse(use.Count + 1, this.services.Now());

    this.services.UseStore.Save(this.uses);
  }

  /// <summary>
  /// Disables an action for the session, logging it the first time.
  /// </summary>
  private void Fault(IndexedAction action, Exception ex, string doing) {
    if (this.faultedIds.Add(action.Id)) {
      this.services.Log.Warn(
        ex,
        $"{nameof(ActionRegistry)}: {ActionRegistry.Describe(action)} threw when {doing}, it is " +
        "disabled until the game restarts."
      );
    }
  }

  private static string Describe(IndexedAction action) =>
    $"Action \"{action.Id}\" of {action.Source.ModName} {action.Source.ModVersion}";

  private ActionRow ToRow(
    IndexedAction action,
    IReadOnlyList<ActionHit> hits,
    string? alias,
    ActionLive live
  ) {
    var descriptor = action.Descriptor;

    return new ActionRow {
      Id = action.Id,
      Label = action.Label,
      Hits = hits,
      Alias = alias,
      Description = action.Description,
      Category = action.Category,
      Icon = descriptor.Icon,
      KeyBinding = descriptor.KeyHint is ProxyActionKeyHint proxy
        ? this.services.ReadKeyBinding(proxy.Map, proxy.Name)
        : null,
      KeyText =
        descriptor.KeyHint is TextKeyHint text ? this.services.ResolveText(text.Text) : null,
      IsEnabled = live.IsEnabled,
      DisabledReason = live.DisabledReason,
      Channel = descriptor.Channel,
      State = live.State,
      Subject = descriptor.Subject,
      FillsFromSelection = descriptor.FillsFromSelection
    };
  }
}

internal sealed record ActionSourceStatus(ActionSourceInfo Info, bool IsDown, int ActionCount);

/// <summary>
/// An action of the catalogue, its display texts and its choice options resolved in the current
/// locale, and the fields a search reaches, folded once.
/// </summary>
internal sealed record IndexedAction(
  string Id,
  ActionSourceInfo Source,
  ActionDescriptor Descriptor,
  string Label,
  string? Description,
  string Category,
  IReadOnlyList<ChoiceOption> Options
) {
  internal required IReadOnlyList<SearchField> Fields { get; init; }
}
