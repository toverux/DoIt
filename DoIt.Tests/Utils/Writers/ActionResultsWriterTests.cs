using System.Linq;
using DoIt.Actions;
using DoIt.Tests.Actions;
using DoIt.Utils.Writers;
using Xunit;

namespace DoIt.Tests.Utils.Writers;

public sealed class ActionResultsWriterTests {
  [Fact]
  public void Write_EmitsOneShapePerTypeName_AcrossEveryAvailabilityChannelAndState() {
    var enabled = new ActionRow {
      Id = "game/Bulldoze Tool",
      Label = "Bulldozer",
      Matches = [new MatchRange(0, 5)],
      Description = "Deletes things.",
      Category = "Game",
      Icon = "Media/Game/Icons/Bulldozer.svg",
      KeyBinding = new FakeKeyBinding("Shortcuts/Bulldozer"),
      IsEnabled = true,
      Channel = ActionChannel.CSharpCall(static () => {}),
      State = null,
      Subject = ActionSubject.None,
      FillsFromSelection = true
    };

    var disabled = enabled with {
      Matches = [],
      Description = null,
      Icon = null,
      KeyBinding = null,
      KeyText = "Ctrl+B",
      IsEnabled = false,
      DisabledReason = "Select something first.",
      Channel = new TriggerChannel("game", "togglePanel", ["Game.UI.InGame.EconomyPanel", 2, true]),
      Subject = ActionSubject.Entity,
      FillsFromSelection = false
    };

    var inputAction = enabled with {
      Channel = new InputActionChannel("Pause Simulation")
    };

    // A row for each state shape, with and without a setter, the last with no invoke.
    var stateRows = StateValues.All.Select(state => disabled with {
        State = state,
        Channel = null
      }
    );

    var writer = new ShapeRecordingJsonWriter();

    new ActionResultsWriter().Write(
      writer,
      new ActionResults(1, [enabled, disabled, inputAction, .. stateRows])
    );

    new ActionResultsWriter().Write(writer, ActionResults.Empty);

    Assert.Empty(writer.ConflictingTypeNames);

    // Each kind of channel and of state writes under its own type name.
    string[] typeNames = [
      "DoIt.Actions.ActionResults",
      "DoIt.Actions.ActionRow",
      "DoIt.Actions.MatchRange",
      nameof(FakeKeyBinding),
      "DoIt.Actions.CSharpCallChannel",
      "DoIt.Actions.TriggerChannel",
      "DoIt.Actions.InputActionChannel",
      .. StateValues.TypeNames
    ];

    Assert.Equal(typeNames.Order(), writer.Shapes.Keys.Order());
  }
}
