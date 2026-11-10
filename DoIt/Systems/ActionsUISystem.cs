using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Colossal.Localization;
using Colossal.UI.Binding;
using DoIt.Actions;
using DoIt.Actions.Sources;
using DoIt.Reflection;
using DoIt.Utils;
using DoIt.Utils.Writers;
using Game.Input;
using Game.SceneFlow;
using Game.Tools;
using Game.UI;
using Game.UI.Localization;
using Unity.Entities;
using UnityEngine;

namespace DoIt.Systems;

/// <summary>
/// <para>
/// UI System owning the actions binding group: it builds the <see cref="ActionRegistry"/> over the
/// running game, searches it on the query trigger, runs its C# calls on the invoke trigger, sets a
/// state on the set-state trigger, and records the use of an action the UI fired on its side on the
/// invoked trigger.
/// </para>
/// <para>
/// It re-reads the live parts of the current rows and of the pinned actions right after each of
/// those triggers, and on a fixed tick while the UI subscribes to them: the result rows while a
/// component holds the results binding, which the palette does only while open, the pinned actions
/// while one holds the pinned-live binding and any action is pinned.
/// Nothing subscribed means no reads on the tick.
/// </para>
/// </summary>
internal sealed partial class ActionsUISystem : UISystemBase {
  private const string BindingGroup = "doIt.actions";

  /// <summary>
  /// The seconds between two live re-reads on the tick: a change made outside Do It shows within
  /// it, under the delay a player reads as instant.
  /// Measured in game 1.6.2f1 (<c>ActionsMeasurement</c>), a tick re-reading 20 rows and 10 pins
  /// costs about 0.1 ms.
  /// </summary>
  private const double TickInterval = 0.1;

  private readonly LocalizationManager localizationManager =
    GameManager.instance.localizationManager;

  // Created in OnCreate, which the game runs before any other member: hence mutable, and null!
  // until then.
  private ValueBinding<ActionResults> resultsBinding = null!;

  private ValueBinding<IReadOnlyList<ActionLive>> pinnedLiveBinding = null!;

  // Not UnityEngine.Time: naming it in a system wakes the Entities source generator, which emits
  // its partial of the system in the global namespace, breaking the build.
  private readonly Stopwatch clock = Stopwatch.StartNew();

  // Mutable, as each tick sets the next one.
  private double nextTickTime;

  internal ActionRegistry Registry { get; private set; } = null!;

  /// <summary>
  /// The results last written to the UI.
  /// </summary>
  internal ActionResults Results => this.resultsBinding.value;

  /// <summary>
  /// The use record, beside the settings file.
  /// </summary>
  private static string UsesPath =>
    Path.Combine(Application.persistentDataPath, Settings.Folder, "ActionUses.txt");

  protected override void OnCreate() {
    base.OnCreate();

    try {
      var toolSystem = this.World.GetOrCreateSystemManaged<ToolSystem>();

      this.Registry = new ActionRegistry(
        new ActionRegistryServices {
          Log = Mod.Log,
          ResolveText = ActionsUISystem.ResolveText,
          ResolveSourceText = ActionsUISystem.SourceTextResolver(this.localizationManager),
          ReadKeyBinding = ActionsUISystem.ReadKeyBinding,
          ReadSelection = () => toolSystem.selected,
          ReadGameMode = static () => GameManager.instance.gameMode,
          Now = static () => DateTime.UtcNow,
          UseStore = new FileActionUseStore(ActionsUISystem.UsesPath, Mod.Log)
        }
      );

      this.resultsBinding = new ValueBinding<ActionResults>(
        ActionsUISystem.BindingGroup,
        "results",
        ActionResults.Empty,
        new ActionResultsWriter()
      );

      this.AddBinding(this.resultsBinding);

      this.pinnedLiveBinding = new ValueBinding<IReadOnlyList<ActionLive>>(
        ActionsUISystem.BindingGroup,
        "pinnedLive",
        [],
        new ActionLiveWriter(),
        new SequenceEqualityComparer<ActionLive>()
      );

      this.AddBinding(this.pinnedLiveBinding);

      this.AddBinding(
        new TriggerBinding<string, int>(ActionsUISystem.BindingGroup, "query", this.OnQuery)
      );

      this.AddBinding(
        new TriggerBinding<string, Entity>(ActionsUISystem.BindingGroup, "invoke", this.OnInvoke)
      );

      this.AddBinding(
        new TriggerBinding<string>(ActionsUISystem.BindingGroup, "invoked", this.OnInvoked)
      );

      this.AddBinding(
        new TriggerBinding<string, string>(
          ActionsUISystem.BindingGroup,
          "setState",
          this.OnSetState
        )
      );

      this.localizationManager.onActiveDictionaryChanged += this.OnActiveDictionaryChanged;

      this.Registry.AddSource(new GameActionSource(this.World));
    }
    catch (Exception ex) {
      Mod.Log.ErrorFatal(ex);

      // The bindings OnUpdate reads may not exist, and the game logs each of its throws as a
      // critical error, one per tick (seen in the running game).
      this.Enabled = false;
    }
  }

  protected override void OnUpdate() {
    base.OnUpdate();

    var time = this.clock.Elapsed.TotalSeconds;

    if (time < this.nextTickTime) {
      return;
    }

    this.nextTickTime = time + ActionsUISystem.TickInterval;

    if (this.resultsBinding.active) {
      this.RereadResults();
    }

    if (this.pinnedLiveBinding.active) {
      this.RereadPinned();
    }
  }

  protected override void OnDestroy() {
    base.OnDestroy();

    this.localizationManager.onActiveDictionaryChanged -= this.OnActiveDictionaryChanged;
  }

  // A method of the system, not the registry's: OnCreate may fail before building the registry, and
  // unsubscribing then must not throw.
  private void OnActiveDictionaryChanged() => this.Registry.Rebuild();

  /// <summary>
  /// Resolves a localized string through the active dictionary, falling back to its value, then to
  /// its id; its arguments are not substituted.
  /// </summary>
  private static string ResolveText(LocalizedString text) =>
    text.id is {} id ? id.Translate(text.value) : text.value ?? "";

  /// <summary>
  /// Resolves a localized string by its id through the game's fallback dictionary, which holds the
  /// source locale whatever the active one; a string with no id has no source text.
  /// When a game update moved that dictionary, it resolves nothing and the actions are no longer
  /// found by their English name, logged once.
  /// </summary>
  private static Func<LocalizedString, string?> SourceTextResolver(LocalizationManager manager) {
    if (LocalizationManagerAccessor.FallbackDictionary(manager) is not {} fallback) {
      Mod.Log.Warn(
        $"{nameof(ActionsUISystem)}: The game's fallback dictionary is out of reach, actions are " +
        "not found by their source-locale name."
      );

      return static _ => null;
    }

    return text => text.id is {} id && fallback.TryGetValue(id, out var value) ? value : null;
  }

  /// <summary>
  /// The first keyboard binding of a <c>ProxyAction</c>, read on each row written, so a rebind
  /// shows without a restart.
  /// </summary>
  private static IJsonWritable? ReadKeyBinding(string map, string name) =>
    InputManager.instance.FindAction(map, name)
      ?.bindings
      .Where(static binding => binding.isKeyboard && binding.isSet)
      .Select(static binding => (IJsonWritable) binding)
      .FirstOrDefault();

  // The view calls a trigger's handler within the UI's own trigger call, and the results binding's
  // update reaches the UI before that call returns: a keystroke's rows land in its own frame.
  private void OnQuery(string query, int sequence) =>
    this.resultsBinding.Update(this.Registry.Search(query, sequence));

  // The UI sends Entity.Null for a bare invoke: a trigger takes no optional argument.
  private void OnInvoke(string id, Entity entity) {
    this.Registry.Invoke(id, entity);
    this.RereadAll();
  }

  private void OnInvoked(string id) {
    this.Registry.RecordInvoked(id);
    this.RereadAll();
  }

  // The value comes as a string whatever the state's shape, so a wrong type never reaches cohtml.
  private void OnSetState(string id, string value) {
    this.Registry.SetState(id, value);
    this.RereadAll();
  }

  /// <summary>
  /// Re-reads the current rows and the pinned actions, subscribed or not, so a binding read next
  /// shows the change an invoke or a set made.
  /// </summary>
  private void RereadAll() {
    this.RereadResults();
    this.RereadPinned();
  }

  private void RereadResults() =>
    this.resultsBinding.Update(this.Registry.Refresh(this.resultsBinding.value));

  // With nothing pinned, ReadLive reads nothing.
  private void RereadPinned() =>
    this.pinnedLiveBinding.Update(this.Registry.ReadLive(Mod.Settings.PinnedActions));

  #if DEBUG
  private static ActionsUISystem Instance =>
    World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<ActionsUISystem>();

  /// <summary>
  /// Debug entry point: the rows a search writes, one line each, without writing the results
  /// binding.
  /// </summary>
  internal static string DebugSearch(string query) =>
    ActionsUISystem.Format(ActionsUISystem.Instance.Registry.Search(query, 0).Rows);

  /// <summary>
  /// Debug entry point: runs a C# call action, as the invoke trigger does.
  /// </summary>
  internal static ActionInvokeOutcome DebugInvoke(string id) {
    var outcome = ActionsUISystem.Instance.Registry.Invoke(id);

    ActionsUISystem.Instance.RereadAll();

    return outcome;
  }

  /// <summary>
  /// Debug entry point: sets an action's state, as the set-state trigger does.
  /// </summary>
  internal static ActionSetOutcome DebugSetState(string id, string value) {
    var outcome = ActionsUISystem.Instance.Registry.SetState(id, value);

    ActionsUISystem.Instance.RereadAll();

    return outcome;
  }

  /// <summary>
  /// Debug entry point: pins an action, or unpins it, at the end of the pin list, until the toolbar
  /// writes the list.
  /// </summary>
  internal static void DebugPin(string id, bool isPinned = true) {
    var settings = Mod.Settings;

    settings.PinnedActions = isPinned
      ? [.. settings.PinnedActions.Where(pinned => pinned != id), id]
      : [.. settings.PinnedActions.Where(pinned => pinned != id)];

    settings.ApplyAndSave();
    ActionsUISystem.Instance.RereadPinned();
  }

  /// <summary>
  /// Debug entry point: the pinned actions' live parts last written to the UI, one line each.
  /// </summary>
  internal static string DebugPinnedLive() =>
    ActionsUISystem.FormatLive(ActionsUISystem.Instance.pinnedLiveBinding.value);

  /// <summary>
  /// Debug entry point: an action's live parts read afresh, or nothing when it is unknown or reads
  /// hidden.
  /// </summary>
  internal static string DebugReadLive(string id) =>
    ActionsUISystem.FormatLive(ActionsUISystem.Instance.Registry.ReadLive([id]));

  /// <summary>
  /// Debug entry point: every source, whether it is down, and how many actions it lists.
  /// </summary>
  internal static string DebugListSources() =>
    string.Join(
      "\n",
      ActionsUISystem.Instance.Registry.Sources.Select(static status =>
        $"{status.Info.Segment} | {status.Info.ModName} {status.Info.ModVersion} | " +
        $"{(status.IsDown ? "down" : "up")} | {status.ActionCount} actions"
      )
    );

  private static string FormatLive(IEnumerable<ActionLive> lives) =>
    string.Join(
      "\n",
      lives.Select(static live =>
        $"{live.Id} | {(live.IsEnabled ? "enabled" : $"disabled: {live.DisabledReason}")} | " +
        $"{live.State}"
      )
    );

  /// <summary>
  /// Each hit as its field, its option key in brackets when it has one, then its ranges as
  /// start+length.
  /// </summary>
  private static string FormatHits(IEnumerable<ActionHit> hits) =>
    string.Join(
      " ",
      hits.Select(static hit =>
        $"{hit.Field}{(hit.OptionKey is {} key ? $"[{key}]" : "")} " +
        string.Join(",", hit.Ranges.Select(static range => $"{range.Start}+{range.Length}"))
      )
    );

  private static string Format(IEnumerable<ActionRow> rows) =>
    string.Join(
      "\n",
      rows.Select(static row =>
        $"{row.Id} | {row.Label} | hits {ActionsUISystem.FormatHits(row.Hits)} | " +
        $"alias {row.Alias} | {row.Category} | {row.Icon} | " +
        $"key {(row.KeyBinding is ProxyBinding binding ? binding.path : row.KeyText)} | " +
        $"{(row.IsEnabled ? "enabled" : $"disabled: {row.DisabledReason}")} | {row.State}"
      )
    );
  #endif
}
