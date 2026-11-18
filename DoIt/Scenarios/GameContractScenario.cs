using System.Collections.Generic;
using System.Threading.Tasks;
using Colossal.TestFramework;
using Colossal.UI.Binding;
using DoIt.Utils;
using Game.SceneFlow;
using JetBrains.Annotations;

namespace DoIt.Scenarios;

/// <summary>
/// Reads from the running game what the mod takes from it with no failure of its own to show when a
/// game update moves it: a fallback stands in silently.
/// Each check says, when it fails, what to change.
/// </summary>
[TestDescriptor("DoIt: Game contract", ScenarioRegistry.ScenarioCategory)]
internal sealed class GameContractScenario : ModTestScenario {
  /// <summary>
  /// The game's bindings <c>utils/bindings/vanilla.ts</c> binds with a default, which stands in for
  /// a binding the game no longer has: the palette would then open during a load or on a gamepad.
  /// </summary>
  private static readonly string[] DefaultedBindings = [
    "overlay.activeScreen", "input.controlScheme"
  ];

  [Test]
  [UsedImplicitly]
  private async Task TheBindingDialogKeys_ReadTheOptionStrings() {
    var dictionary = GameManager.instance.localizationManager.activeDictionary;

    // Read again past RegisterKeyBindings, which LocaleLoader.Setup runs before.
    var aliases = LocaleLoader.ListAliases(Mod.Settings);

    // The input map's alias comes first, then one per key binding.
    this.Check(aliases.Count > 1, $"The settings hold key bindings to alias: {aliases.Count - 1}.");

    foreach (var (key, sourceKey) in aliases) {
      dictionary.TryGetValue(key, out var text);
      dictionary.TryGetValue(sourceKey, out var sourceText);

      this.Check(
        text is not null && text == sourceText,
        $"{key} reads \"{text}\", as {sourceKey}. If not: the binding's title or map changed " +
        "after LocaleLoader.Setup read them in Mod.OnLoad; set the locales up once they are final."
      );
    }

    await this.Finish();
  }

  [Test]
  [UsedImplicitly]
  private async Task TheBindingsTheUIReads_AreTheGames() {
    var paths = new HashSet<string>();

    GameContractScenario.CollectPaths(GameManager.instance.userInterface.bindings, paths);

    foreach (var path in GameContractScenario.DefaultedBindings) {
      this.Check(
        paths.Contains(path),
        $"The game binds {path}. If not: find what the game's UI reads for it now, and bind that " +
        "in vanilla.ts."
      );
    }

    await this.Finish();
  }

  /// <summary>
  /// Collects the path of every binding a registry holds, through the composites systems nest.
  /// </summary>
  private static void CollectPaths(IBindingRegistry registry, ISet<string> paths) {
    if (registry is not CompositeBinding composite) {
      return;
    }

    foreach (var binding in composite.bindings) {
      if (binding is BindingBase named) {
        paths.Add(named.path);
      }

      if (binding is IBindingRegistry nested) {
        GameContractScenario.CollectPaths(nested, paths);
      }
    }
  }
}
