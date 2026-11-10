using System.Collections.Generic;
using System.Linq;
using DoIt.Actions;
using Game;
using Game.UI.Localization;
using Xunit;

namespace DoIt.Tests.Actions;

public sealed class ActionRegistryTests {
  [Fact]
  public void Search_WritesAMatchingActionAsACompleteRow() {
    var fixture = new RegistryFixture();
    var source = new FakeSource("game");

    source.Descriptors.Add(
      Descriptors.Call("Bulldozer") with {
        LocalId = "Bulldoze Tool",
        Description = LocalizedString.Id("Bulldozer.Description"),
        Icon = "Media/Game/Icons/Bulldozer.svg",
        KeyHint = new ProxyActionKeyHint("Shortcuts", "Bulldozer")
      }
    );

    fixture.Registry.AddSource(source);

    var results = fixture.Registry.Search("bulld", 7);

    Assert.Equal(7, results.Sequence);

    var row = Assert.Single(results.Rows);

    Assert.Equal("game/Bulldoze Tool", row.Id);
    Assert.Equal("Bulldozer", row.Label);
    Assert.Equal([new MatchRange(0, 5)], row.Matches);
    Assert.Equal("Bulldozer.Description", row.Description);
    Assert.Equal("Source[game]", row.Category);
    Assert.Equal("Media/Game/Icons/Bulldozer.svg", row.Icon);
    Assert.Equal(new FakeKeyBinding("Shortcuts/Bulldozer"), row.KeyBinding);
    Assert.True(row.IsEnabled);
    Assert.IsType<CSharpCallChannel>(row.Channel);
  }

  [Fact]
  public void Search_LeavesOutALabelNotHoldingTheQuery() {
    var fixture = new RegistryFixture();
    var source = new FakeSource();

    source.Descriptors.AddRange([Descriptors.Call("Bulldozer"), Descriptors.Call("Pause")]);
    fixture.Registry.AddSource(source);

    Assert.Equal(["Pause"], fixture.Registry.Search("aus", 1).Rows.Select(static row => row.Label));
  }

  [Fact]
  public void Search_LeavesOutAnActionWhoseModesMissTheCurrentOne() {
    var fixture = new RegistryFixture();
    var source = new FakeSource();

    source.Descriptors.Add(
      Descriptors.Call("Edit Terrain") with {
        Modes = GameMode.Editor
      }
    );
    fixture.Registry.AddSource(source);

    Assert.Empty(fixture.Registry.Search("terrain", 1).Rows);

    fixture.GameMode = GameMode.Editor;

    Assert.Single(fixture.Registry.Search("terrain", 2).Rows);
  }

  [Fact]
  public void Search_WritesEachReaderAnswerOnTheRow() {
    var fixture = new RegistryFixture();
    var source = new FakeSource();

    source.Descriptors.AddRange(
      [
        Descriptors.Call("Tool Enabled") with {
          Availability = static () => ActionAvailability.Enabled
        },
        Descriptors.Call("Tool Disabled") with {
          Availability = static () => ActionAvailability.Disabled(LocalizedString.Id("Reason"))
        },
        Descriptors.Call("Tool Hidden") with {
          Availability = static () => ActionAvailability.Hidden
        }
      ]
    );

    fixture.Registry.AddSource(source);

    var rows = fixture.Registry.Search("tool", 1).Rows;

    Assert.Equal(
      [("Tool Enabled", true, null), ("Tool Disabled", false, "Reason")],
      rows.Select(static row => (row.Label, row.IsEnabled, row.DisabledReason))
    );
  }

  [Fact]
  public void Search_GivesAHiddenMatchsPlaceToTheNextOne() {
    var fixture = new RegistryFixture();
    var source = new FakeSource();

    source.Descriptors.AddRange(
      [
        Descriptors.Call("Tool A") with {
          Availability = static () => ActionAvailability.Hidden
        },
        Descriptors.Call("Tool B"),
        Descriptors.Call("Tool C")
      ]
    );

    fixture.Registry.AddSource(source);

    Assert.Equal(
      ["Tool B", "Tool C"],
      fixture.Registry.Search("tool", 1, rowCap: 2).Rows.Select(static row => row.Label)
    );
  }

  [Fact]
  public void Search_StopsAtItsRowCap_AndReadsNoReaderPastIt() {
    var fixture = new RegistryFixture();
    var source = new FakeSource();
    var reads = 0;

    for (var i = 0; i < 5; i++) {
      source.Descriptors.Add(
        Descriptors.Call($"Tool {i}") with {
          Availability = () => {
            reads++;

            return ActionAvailability.Enabled;
          }
        }
      );
    }

    fixture.Registry.AddSource(source);

    Assert.Equal(3, fixture.Registry.Search("tool", 1, rowCap: 3).Rows.Count);
    Assert.Equal(3, reads);
  }

  [Fact]
  public void Search_RanksAMatchAtTheLabelsStartFirst() {
    var fixture = new RegistryFixture();
    var source = new FakeSource();

    source.Descriptors.AddRange(
      [
        Descriptors.Call("Unpause Game"),
        Descriptors.Call("Game Pause"),
        Descriptors.Call("Pause Game")
      ]
    );

    fixture.Registry.AddSource(source);

    Assert.Equal(
      ["Pause Game", "Game Pause", "Unpause Game"],
      fixture.Registry.Search("PAUSE", 1).Rows.Select(static row => row.Label)
    );
  }

  [Fact]
  public void Rebuild_ResolvesLabelsAgain_SoSearchMatchesTheNewLocale() {
    var fixture = new RegistryFixture();
    var source = new FakeSource();

    source.Descriptors.Add(Descriptors.Call("Bulldozer"));
    fixture.Registry.AddSource(source);

    fixture.Translations["Bulldozer"] = "Bulldozer (fr)";
    fixture.Registry.Rebuild();

    Assert.Equal("Bulldozer (fr)", Assert.Single(fixture.Registry.Search("(fr)", 1).Rows).Label);
  }

  [Fact]
  public void Search_RanksByTheMatchersScore() {
    var fixture = new RegistryFixture(new LengthMatcher());
    var source = new FakeSource();

    source.Descriptors.AddRange(
      [Descriptors.Call("B"), Descriptors.Call("CCC"), Descriptors.Call("AA")]
    );

    fixture.Registry.AddSource(source);

    Assert.Equal(
      ["CCC", "AA", "B"],
      fixture.Registry.Search("anything", 1).Rows.Select(static row => row.Label)
    );
  }

  [Fact]
  public void Search_WritesALocalizedKeyHintAsText() {
    var fixture = new RegistryFixture();
    var source = new FakeSource();

    source.Descriptors.Add(
      Descriptors.Call("Bulldozer") with {
        KeyHint = new TextKeyHint(LocalizedString.Id("Hint"))
      }
    );

    fixture.Registry.AddSource(source);

    var row = Assert.Single(fixture.Registry.Search("bulldozer", 1).Rows);

    Assert.Equal("Hint", row.KeyText);
    Assert.Null(row.KeyBinding);
  }

  [Fact]
  public void Invoke_RecordsTheUse_ThroughTheUseStore() {
    var fixture = new RegistryFixture();
    var source = new FakeSource();

    source.Descriptors.Add(Descriptors.Call("Bulldozer"));
    fixture.Registry.AddSource(source);

    fixture.Registry.Invoke("test/Bulldozer");
    fixture.Registry.Invoke("test/Bulldozer");

    Assert.Equal(
      new Dictionary<string, ActionUse> {
        ["test/Bulldozer"] = new(2, RegistryFixture.Now)
      },
      fixture.UseStore.Saved
    );
  }

  [Fact]
  public void ReadLive_WritesTheRowsOfTheGivenIdsAlone_LeavingTheHiddenOnesOut() {
    var fixture = new RegistryFixture();
    var source = new FakeSource();

    source.Descriptors.AddRange(
      [
        Descriptors.Call("Tool A"),
        Descriptors.Call("Tool B"),
        Descriptors.Call("Tool C") with {
          Availability = static () => ActionAvailability.Hidden
        }
      ]
    );

    fixture.Registry.AddSource(source);

    Assert.Equal(
      ["test/Tool B"],
      fixture.Registry
        .ReadLive(["test/Tool B", "test/Tool C", "test/Nothing"])
        .Select(static row => row.Id)
    );
  }

  /// <summary>
  /// Matches every label, a longer one ranking first.
  /// </summary>
  private sealed class LengthMatcher : IActionMatcher {
    public ActionMatch Match(string query, string label) => new(label.Length, []);
  }
}
