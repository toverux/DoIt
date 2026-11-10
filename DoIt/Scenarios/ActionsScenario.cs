using System;
using System.Linq;
using System.Threading.Tasks;
using Colossal;
using Colossal.TestFramework;
using DoIt.Actions;
using DoIt.Systems;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using Game.UI.InGame;
using JetBrains.Annotations;

namespace DoIt.Scenarios;

/// <summary>
/// Drives the actions bindings through their real triggers, fired from the UI's script context as
/// the UI fires them, and the UI-side channels through the UI's own executors.
/// </summary>
[TestDescriptor("DoIt: Actions", ScenarioRegistry.ScenarioCategory)]
internal sealed class ActionsScenario : ModTestScenario {
  private static readonly Hash128 Archipelago = Hash128.Parse("bf8036b291428535b986c757fda3e627");

  [Test]
  [UsedImplicitly]
  private async Task QueryAndInvoke_ActivateTheBulldozer() {
    await this.StartCity();

    var actionsSystem = this.World.GetExistingSystemManaged<ActionsUISystem>();
    var toolSystem = this.World.GetExistingSystemManaged<ToolSystem>();
    var bulldozeTool = this.World.GetExistingSystemManaged<BulldozeToolSystem>();
    var id = new ActionId("game", bulldozeTool.toolID).ToString();

    await this.Trigger("query", "'bulld'", "41");

    var results = actionsSystem.Results;
    var row = results.Rows.FirstOrDefault(candidate => candidate.Id == id);

    this.Check(results.Sequence is 41, $"The results answer query 41: {results.Sequence}.");

    this.Check(
      row is { IsEnabled: true, KeyBinding: not null },
      $"The bulldozer row is enabled with a key: {row?.Label}, {row?.KeyBinding}."
    );

    await this.Trigger("invoke", $"'{id}'", "{ index: 0, version: 0 }");

    this.Check(
      toolSystem.activeTool == bulldozeTool,
      $"The active tool is the bulldozer: {toolSystem.activeTool?.toolID}."
    );

    toolSystem.activeTool = this.World.GetExistingSystemManaged<DefaultToolSystem>();

    await this.Finish();
  }

  [Test]
  [UsedImplicitly]
  private async Task RunInTheUI_OpensTheEconomyPanelAndPauses() {
    await this.StartCity();

    var panelSystem = this.World.GetExistingSystemManaged<GamePanelUISystem>();
    var simulationSystem = this.World.GetExistingSystemManaged<SimulationSystem>();
    // Never null for a non-generic type, which the panel is.
    var economyPanel = typeof(EconomyPanel).FullName!;

    // The UI runs an action from the latest results, which the empty query lists whole.
    await this.Trigger("query", "''", "42");

    await this.RunInTheUI(new ActionId("game", economyPanel).ToString());

    this.Check(
      panelSystem.activePanel is EconomyPanel,
      $"The active panel is the economy panel: {panelSystem.activePanel?.GetType().Name}."
    );

    panelSystem.ClosePanel(economyPanel);

    // The game loads a city paused under the player's paused-after-loading setting (decompile,
    // SimulationSystem), so the check below proves something only once the simulation runs, which
    // also lets the game's UI bind its pause handler.
    simulationSystem.selectedSpeed = 1;

    await this.WaitFrames(5);

    await this.RunInTheUI(new ActionId("game", "Pause Simulation").ToString());

    this.Check(
      simulationSystem.selectedSpeed is 0,
      $"The simulation is paused: selected speed {simulationSystem.selectedSpeed}."
    );

    simulationSystem.selectedSpeed = 1;

    await this.Finish();
  }

  private async Task StartCity() {
    // The new game screen sets the starting year, which a scenario's load does not: left at zero,
    // reading the city's date throws, for the game and every mod reading it.
    this.World.GetExistingSystemManaged<TimeSystem>().startingYear = DateTime.Now.Year;

    await this.StartNewCity(ActionsScenario.Archipelago, "Do It Actions");
  }

  /// <summary>
  /// Runs an action of the latest results as the palette would, through the UI's Debug entry point,
  /// then waits a few frames for the view to deliver what it fired.
  /// </summary>
  private async Task RunInTheUI(string id) {
    GameManager.instance.userInterface.view.View.ExecuteScript($"doItDebug.runAction('{id}');");

    await this.WaitFrames(5);
  }

  /// <summary>
  /// Fires a trigger of the actions group from the UI's script context, its arguments written as
  /// JavaScript, then waits a few frames: the view delivers the trigger on its own update.
  /// </summary>
  private async Task Trigger(string name, params string[] args) {
    GameManager.instance.userInterface.view.View.ExecuteScript(
      $"engine.trigger('doIt.actions.{name}', {string.Join(", ", args)});"
    );

    await this.WaitFrames(5);
  }
}
