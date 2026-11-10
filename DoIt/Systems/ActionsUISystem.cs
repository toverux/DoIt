using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Colossal.Localization;
using Colossal.UI.Binding;
using DoIt.Actions;
using DoIt.Actions.Sources;
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
/// UI System owning the actions binding group: it builds the <see cref="ActionRegistry"/> over the
/// running game, searches it on the query trigger, runs its C# calls on the invoke trigger, and
/// records the use of an action the UI fired on its side on the invoked trigger.
/// </summary>
internal sealed partial class ActionsUISystem : UISystemBase {
  private const string BindingGroup = "doIt.actions";

  private readonly LocalizationManager localizationManager =
    GameManager.instance.localizationManager;

  // Created in OnCreate, which the game runs before any other member: hence mutable, and null!
  // until then.
  private ValueBinding<ActionResults> resultsBinding = null!;

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
      // No need to OnUpdate: the bindings are written on the triggers.
      this.Enabled = false;

      var toolSystem = this.World.GetOrCreateSystemManaged<ToolSystem>();

      this.Registry = new ActionRegistry(
        new ActionRegistryServices {
          Log = Mod.Log,
          ResolveText = ActionsUISystem.ResolveText,
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

      this.AddBinding(
        new TriggerBinding<string, int>(ActionsUISystem.BindingGroup, "query", this.OnQuery)
      );

      this.AddBinding(
        new TriggerBinding<string, Entity>(ActionsUISystem.BindingGroup, "invoke", this.OnInvoke)
      );

      this.AddBinding(
        new TriggerBinding<string>(ActionsUISystem.BindingGroup, "invoked", this.OnInvoked)
      );

      this.localizationManager.onActiveDictionaryChanged += this.OnActiveDictionaryChanged;

      this.Registry.AddSource(new GameActionSource(this.World));
    }
    catch (Exception ex) {
      Mod.Log.ErrorFatal(ex);
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
  /// The first keyboard binding of a <c>ProxyAction</c>, read on each row written, so a rebind
  /// shows without a restart.
  /// </summary>
  private static IJsonWritable? ReadKeyBinding(string map, string name) =>
    InputManager.instance.FindAction(map, name)
      ?.bindings
      .Where(static binding => binding.isKeyboard && binding.isSet)
      .Select(static binding => (IJsonWritable) binding)
      .FirstOrDefault();

  private void OnQuery(string query, int sequence) =>
    this.resultsBinding.Update(this.Registry.Search(query, sequence));

  // The UI sends Entity.Null for a bare invoke: a trigger takes no optional argument.
  private void OnInvoke(string id, Entity entity) => this.Registry.Invoke(id, entity);

  private void OnInvoked(string id) => this.Registry.RecordInvoked(id);

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
  internal static ActionInvokeOutcome DebugInvoke(string id) =>
    ActionsUISystem.Instance.Registry.Invoke(id);

  /// <summary>
  /// Debug entry point: an action's row with its live parts read afresh, or nothing when it is
  /// unknown or reads hidden.
  /// </summary>
  internal static string DebugReadLive(string id) =>
    ActionsUISystem.Format(ActionsUISystem.Instance.Registry.ReadLive([id]));

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

  private static string Format(IEnumerable<ActionRow> rows) =>
    string.Join(
      "\n",
      rows.Select(static row =>
        $"{row.Id} | {row.Label} | {row.Category} | {row.Icon} | " +
        $"key {(row.KeyBinding is ProxyBinding binding ? binding.path : row.KeyText)} | " +
        $"{(row.IsEnabled ? "enabled" : $"disabled: {row.DisabledReason}")}"
      )
    );
  #endif
}
