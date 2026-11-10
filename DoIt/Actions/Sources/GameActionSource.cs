using System;
using System.Collections.Generic;
using Game.Tools;
using Game.UI.InGame;
using Game.UI.Localization;
using Unity.Entities;
using Version = Game.Version;

namespace DoIt.Actions.Sources;

/// <summary>
/// The game's own actions, reached through the public members of its systems, resolved on the first
/// listing: a game update breaking them then marks the source down instead of failing the mod.
/// </summary>
internal sealed class GameActionSource(World world) : IActionSource {
  // Mutable, as the actions are resolved on the first listing that succeeds, then kept.
  private ActionDescriptor[]? descriptors;

  // The game's actions do not change while it runs.
  public event Action? Changed { add {} remove {} }

  public ActionSourceInfo Info { get; } = new(
    "game",
    LocalizedString.Id("DoIt.Actions.SOURCE_GAME"),
    "Cities: Skylines II",
    Version.current.shortVersion
  );

  public IEnumerable<ActionDescriptor> List() =>
    this.descriptors ??= GameActionSource.Resolve(world);

  private static ActionDescriptor[] Resolve(World world) {
    var toolUISystem = world.GetOrCreateSystemManaged<ToolUISystem>();
    var bulldozeTool = world.GetOrCreateSystemManaged<BulldozeToolSystem>();
    // Never null for a non-generic type, which the panel is.
    var economyPanel = typeof(EconomyPanel).FullName!;

    // The UI input action's name, which is also its Shortcuts action's.
    const string pauseSimulation = "Pause Simulation";

    return [
      new ActionDescriptor {
        // The tool's id, which the game's own tool trigger names it by.
        LocalId = bulldozeTool.toolID,
        Label = LocalizedString.Id($"Assets.NAME[{bulldozeTool.toolID}]"),
        Description = LocalizedString.Id($"Assets.DESCRIPTION[{bulldozeTool.toolID}]"),
        Icon = "Media/Game/Icons/Bulldozer.svg",
        KeyHint = new ProxyActionKeyHint("Shortcuts", "Bulldozer"),
        // The game's own tool button goes through this member, which also sets the bulldoze prefab
        // the tool needs.
        Channel = ActionChannel.CSharpCall(() => toolUISystem.SelectTool(bulldozeTool))
      },
      new ActionDescriptor {
        LocalId = pauseSimulation,
        Label = LocalizedString.Id("Main.TOOLTIP_TITLE_PAUSE"),
        Description = LocalizedString.Id("Main.TOOLTIP_DESCRIPTION_PAUSE"),
        Icon = "Media/Glyphs/SimulationPause.svg",
        KeyHint = new ProxyActionKeyHint("Shortcuts", pauseSimulation),
        Channel = new InputActionChannel(pauseSimulation)
      },
      new ActionDescriptor {
        // The panel's type name, which the game's panel triggers name it by.
        LocalId = economyPanel,
        Label = LocalizedString.Id("Main.TOOLTIP_TITLE_CITY_ECONOMY"),
        Description = LocalizedString.Id("Main.TOOLTIP_DESCRIPTION_CITY_ECONOMY"),
        Icon = "Media/Game/Icons/Economy.svg",
        KeyHint = new ProxyActionKeyHint("Shortcuts", "Economy Panel"),
        Channel = new TriggerChannel("game", "togglePanel", [economyPanel])
      }
    ];
  }
}
