using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Simulation;
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
    var simulationSystem = world.GetOrCreateSystemManaged<SimulationSystem>();
    var timeUISystem = world.GetOrCreateSystemManaged<TimeUISystem>();
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
        // The game handles this input action only while the simulation runs, the toggle resumes it.
        Channel = new InputActionChannel(pauseSimulation),
        State = new ToggleState {
          Read = () => simulationSystem.selectedSpeed is 0f,
          // Resumes at the speed the game's own controls resume at, which the time UI system
          // remembers privately and reports while paused.
          Set = isPaused => simulationSystem.selectedSpeed =
            isPaused ? 0f : GameActionSource.Speed(timeUISystem.GetSimulationSpeed())
        }
      },
      new ActionDescriptor {
        LocalId = "Simulation Speed",
        Label = LocalizedString.Id("Main.TOOLTIP_TITLE_GAME_SPEED"),
        Description = LocalizedString.Id("Main.TOOLTIP_DESCRIPTION_GAME_SPEED"),
        Icon = "Media/Glyphs/SimulationSpeed1.svg",
        KeyHint = new ProxyActionKeyHint("Shortcuts", "Switch Speed"),
        // Keyed by the game's speed index, labelled as its speed icons count, 1 to 3.
        State = new ChoiceState {
          Options = [
            new ActionChoice("0", LocalizedString.Value("1")),
            new ActionChoice("1", LocalizedString.Value("2")),
            new ActionChoice("2", LocalizedString.Value("3"))
          ],
          // The speed the game resumes at while paused, as its own speed control shows.
          Read = () => timeUISystem.GetSimulationSpeed().ToString(CultureInfo.InvariantCulture),
          // Unpauses, as the game's own speed control does.
          Set = key => simulationSystem.selectedSpeed =
            GameActionSource.Speed(int.Parse(key, CultureInfo.InvariantCulture))
        }
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

  /// <summary>
  /// The simulation speed of a speed index of the game's time controls, each one doubling it.
  /// </summary>
  private static float Speed(int index) => 1 << index;
}
