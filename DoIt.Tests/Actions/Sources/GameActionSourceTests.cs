using DoIt.Actions.Sources;
using Xunit;

namespace DoIt.Tests.Actions.Sources;

public sealed class GameActionSourceTests {
  private readonly RegistryFixture fixture = new();

  [Fact]
  public void AddSource_MarksTheGameSourceDown_WhenItsGameInternalsBreak() {
    // No world stands in for a game update breaking the systems the source reads.
    this.fixture.Registry.AddSource(new GameActionSource(null!));

    Assert.True(Assert.Single(this.fixture.Registry.Sources).IsDown);
    Assert.Contains(
      "Cities: Skylines II",
      (string?) Assert.Single(this.fixture.Log.Calls, static call => call.Method == "Warn").Args[1]
    );
  }
}
