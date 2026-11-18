using System.Linq;
using System.Threading.Tasks;
using Colossal.TestFramework;
using DoIt.Actions;
using DoIt.Systems;
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

  [Test]
  [UsedImplicitly]
  private async Task SetState_PausesAndShowsTheSpeed() {
    await this.StartCity();

    var actionsSystem = this.World.GetExistingSystemManaged<ActionsUISystem>();
    var simulationSystem = this.World.GetExistingSystemManaged<SimulationSystem>();
    var pauseId = new ActionId("game", "Pause Simulation").ToString();
    var speedId = new ActionId("game", "Simulation Speed").ToString();

    // Runs at the second speed, the game's index 1, so the speed row has a value to show.
    simulationSystem.selectedSpeed = 2;

    await this.Trigger("query", "''", "43");

    // The set rewrites the current rows at once, with no subscriber holding the results.
    await this.Trigger("setState", $"'{pauseId}'", "'true'");

    var rows = actionsSystem.Results.Rows;
    var pause = rows.FirstOrDefault(row => row.Id == pauseId);
    var speed = rows.FirstOrDefault(row => row.Id == speedId);

    this.Check(
      simulationSystem.selectedSpeed is 0f,
      $"The simulation is paused: selected speed {simulationSystem.selectedSpeed}."
    );

    this.Check(
      pause?.State is ToggleValue { Value: true, IsSettable: true },
      $"The pause row reads paused: {pause?.State}."
    );

    this.Check(
      speed?.State is ChoiceValue { Key: "1", IsSettable: true },
      $"The speed row shows the speed it resumes at: {speed?.State}."
    );

    await this.Trigger("setState", $"'{pauseId}'", "'false'");

    this.Check(
      simulationSystem.selectedSpeed is 2f,
      $"The simulation resumes at its speed: selected speed {simulationSystem.selectedSpeed}."
    );

    simulationSystem.selectedSpeed = 1;

    await this.Finish();
  }

  private Task StartCity() => this.StartNewCity(ModTestScenario.Archipelago, "Do It Actions");

  /// <summary>
  /// Runs an action of the latest results as the palette would, through the UI's Debug entry point,
  /// then waits a few frames for the view to deliver what it fired.
  /// </summary>
  private Task RunInTheUI(string id) => this.RunScript($"doItDebug.runAction('{id}');");

  /// <summary>
  /// Fires a trigger of the actions group from the UI's script context, its arguments written as
  /// JavaScript, then waits a few frames: the view delivers the trigger on its own update.
  /// </summary>
  private Task Trigger(string name, params string[] args) =>
    this.RunScript($"engine.trigger('doIt.actions.{name}', {string.Join(", ", args)});");
}
