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
using JetBrains.Annotations;

namespace DoIt.Scenarios;

/// <summary>
/// Drives the actions bindings through their real triggers, fired from the UI's script context as
/// the UI fires them.
/// </summary>
[TestDescriptor("DoIt: Actions", ScenarioRegistry.ScenarioCategory)]
internal sealed class ActionsScenario : ModTestScenario {
  private static readonly Hash128 Archipelago = Hash128.Parse("bf8036b291428535b986c757fda3e627");

  [Test]
  [UsedImplicitly]
  private async Task QueryAndInvoke_ActivateTheBulldozer() {
    // The new game screen sets the starting year, which a scenario's load does not: left at zero,
    // reading the city's date throws, for the game and every mod reading it.
    this.World.GetExistingSystemManaged<TimeSystem>().startingYear = DateTime.Now.Year;

    await this.StartNewCity(ActionsScenario.Archipelago, "Do It Actions");

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
