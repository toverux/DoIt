using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
/// listing succeeds, a throw in an action's reader or invoke disables that action for the session.
/// Each is logged once, with the mod name and version behind the source.
/// </para>
/// </summary>
internal sealed class ActionRegistry {
  /// <summary>
  /// The most rows a search writes when its caller sets no cap.
  /// </summary>
  internal const int DefaultRowCap = 20;

  /// <summary>
  /// The reason an entity action fired bare is disabled with when it has no entity to take, until a
  /// pick tool can ask the player for one.
  /// </summary>
  internal static readonly LocalizedString SelectFirstReason =
    LocalizedString.Id("DoIt.Actions.REASON_SELECT_FIRST");

  /// <summary>
  /// The reason an action whose reader or invoke threw is disabled with.
  /// </summary>
  internal static readonly LocalizedString FailedReason =
    LocalizedString.Id("DoIt.Actions.REASON_FAILED");

  private readonly ActionRegistryServices services;

  private readonly List<IActionSource> sources = [];

  // The sources whose last listing threw, logged when they enter the set.
  private readonly HashSet<IActionSource> downSources = [];

  // The actions whose reader or invoke threw, disabled for the session and logged when they enter
  // the set, so a source fixing itself on a rebuild does not bring them back.
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
  /// Ranks the actions whose label matches <paramref name="query"/> and writes at most
  /// <paramref name="rowCap"/> rows, reading the live parts of those rows alone.
  /// </summary>
  internal ActionResults Search(
    string query,
    int sequence,
    int rowCap = ActionRegistry.DefaultRowCap
  ) {
    var mode = this.services.ReadGameMode();
    var selection = this.services.ReadSelection();
    var matches = new List<(IndexedAction Action, ActionMatch Match)>();

    foreach (var action in this.catalogue) {
      if ((action.Descriptor.Modes & mode) is not 0 &&
        this.services.Matcher.Match(query, action.Label) is {} match) {
        matches.Add((action, match));
      }
    }

    var rows = new List<ActionRow>();

    // Recorded use breaks a tie in score, the more uses then the latest first; a stable sort, so
    // the rest keep the catalogue's order.
    var ranked = matches
      .OrderByDescending(static match => match.Match.Score)
      .ThenByDescending(match => this.UseOf(match.Action).Count)
      .ThenByDescending(match => this.UseOf(match.Action).LastUse);

    foreach (var (action, match) in ranked) {
      if (rows.Count >= rowCap) {
        break;
      }

      var availability = this.ReadAvailability(action, ActionRegistry.Subject(action, selection));

      if (availability.Kind is not ActionAvailabilityKind.Hidden) {
        rows.Add(this.ToRow(action, match.Ranges, availability));
      }
    }

    return new ActionResults(sequence, rows);
  }

  /// <summary>
  /// Writes the rows of <paramref name="ids"/> with their live parts read afresh, in the order
  /// given, leaving out an id that does not exist in the current game mode or reads hidden.
  /// </summary>
  internal IReadOnlyList<ActionRow> ReadLive(IEnumerable<string> ids) {
    var selection = this.services.ReadSelection();
    var rows = new List<ActionRow>();

    foreach (var id in ids) {
      if (!this.TryFind(id, out var action)) {
        continue;
      }

      var availability = this.ReadAvailability(action, ActionRegistry.Subject(action, selection));

      if (availability.Kind is not ActionAvailabilityKind.Hidden) {
        rows.Add(this.ToRow(action, [], availability));
      }
    }

    return rows;
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
  /// Records the use of an action the UI fired on its side, after calling its reader afresh: a
  /// disabled or hidden answer records nothing, as the UI does not fire a disabled row.
  /// The select-first rule is left out, as the UI fires an entity action only on an entity it
  /// holds, which the note does not carry.
  /// </summary>
  internal ActionInvokeOutcome RecordInvoked(string id) {
    if (!this.TryFind(id, out var action)) {
      return ActionInvokeOutcome.Unknown;
    }

    if (this.ReadReader(action).Kind is not ActionAvailabilityKind.Enabled) {
      return ActionInvokeOutcome.Refused;
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

    return new IndexedAction(
      new ActionId(source.Segment, descriptor.LocalId).ToString(),
      source,
      descriptor,
      resolve(descriptor.Label),
      descriptor.Description is {} description ? resolve(description) : null,
      resolve(descriptor.Category ?? source.DisplayName)
    );
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
    IReadOnlyList<MatchRange> matches,
    ActionAvailability availability
  ) {
    var descriptor = action.Descriptor;

    return new ActionRow {
      Id = action.Id,
      Label = action.Label,
      Matches = matches,
      Description = action.Description,
      Category = action.Category,
      Icon = descriptor.Icon,
      KeyBinding = descriptor.KeyHint is ProxyActionKeyHint proxy
        ? this.services.ReadKeyBinding(proxy.Map, proxy.Name)
        : null,
      KeyText =
        descriptor.KeyHint is TextKeyHint text ? this.services.ResolveText(text.Text) : null,
      IsEnabled = availability.Kind is ActionAvailabilityKind.Enabled,
      DisabledReason = availability.Reason is {} reason ? this.services.ResolveText(reason) : null,
      Channel = descriptor.Channel,
      Subject = descriptor.Subject,
      FillsFromSelection = descriptor.FillsFromSelection
    };
  }

  /// <summary>
  /// An action of the catalogue, its display texts resolved in the current locale.
  /// </summary>
  private sealed record IndexedAction(
    string Id,
    ActionSourceInfo Source,
    ActionDescriptor Descriptor,
    string Label,
    string? Description,
    string Category
  );
}

internal sealed record ActionSourceStatus(ActionSourceInfo Info, bool IsDown, int ActionCount);
