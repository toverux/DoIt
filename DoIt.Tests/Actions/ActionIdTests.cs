using System;
using DoIt.Actions;
using Xunit;

namespace DoIt.Tests.Actions;

public sealed class ActionIdTests {
  [Fact]
  public void ToString_JoinsSourceAndLocalWithASlash() =>
    Assert.Equal("game/Bulldoze Tool", new ActionId("game", "Bulldoze Tool").ToString());

  [Fact]
  public void Parse_SplitsAtTheFirstSlash_SoTheLocalSegmentKeepsItsOwn() {
    var id = ActionId.Parse("bridge:Traffic/tool/Lane Connector Tool");

    Assert.Equal(new ActionId("bridge:Traffic", "tool/Lane Connector Tool"), id);
    Assert.Equal("bridge:Traffic/tool/Lane Connector Tool", id.ToString());
  }

  [Theory]
  [InlineData("game")]
  [InlineData("/Bulldoze Tool")]
  [InlineData("game/")]
  public void TryParse_RefusesAStringMissingASegment(string text) =>
    Assert.False(ActionId.TryParse(text, out _));

  [Fact]
  public void Constructor_RefusesASourceSegmentHoldingASlash() =>
    Assert.Throws<ArgumentException>(static () => new ActionId("api:My/Mod", "key"));
}
