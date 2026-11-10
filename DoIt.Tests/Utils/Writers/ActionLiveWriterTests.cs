using System.Linq;
using DoIt.Actions;
using DoIt.Utils.Writers;
using Xunit;

namespace DoIt.Tests.Utils.Writers;

public sealed class ActionLiveWriterTests {
  [Fact]
  public void Write_EmitsOneShapePerTypeName_AcrossEveryAvailabilityAndState() {
    ActionLive[] lives = [
      new("game/Pause Simulation", true, null, null),
      new("game/Bulldoze Tool", false, "Select something first.", null),
      .. StateValues.All.Select(static (state, index) =>
        new ActionLive($"test/{index}", index % 2 is 0, null, state)
      )
    ];

    var writer = new ShapeRecordingJsonWriter();

    new ActionLiveWriter().Write(writer, lives);
    new ActionLiveWriter().Write(writer, []);

    Assert.Empty(writer.ConflictingTypeNames);

    string[] typeNames = ["DoIt.Actions.ActionLive", .. StateValues.TypeNames];

    Assert.Equal(typeNames.Order(), writer.Shapes.Keys.Order());
  }
}
