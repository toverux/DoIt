using System;
using System.Collections.Generic;
using System.Linq;
using DoIt.Actions;
using Game.UI.Localization;
using Xunit;

namespace DoIt.Tests.Actions;

public sealed class ActionRegistryStateTests {
  private readonly RegistryFixture fixture = new();

  private readonly FakeSource source = new();

  [Fact]
  public void Search_WritesEachStateShapeWithItsValueOnItsRow() {
    this.fixture.Translations["Speed.Slow"] = "Slow";

    this.Add(
      Descriptors.State(
        "Paused",
        new ToggleState {
          Read = static () => true
        }
      ),
      Descriptors.State(
        "Volume",
        new NumberState {
          Read = static () => 0.5,
          Set = static _ => {},
          Min = 0,
          Max = 1
        }
      ),
      Descriptors.State(
        "Speed",
        new ChoiceState {
          Options = [
            new ActionChoice("1", LocalizedString.Id("Speed.Slow")), new ActionChoice("2", "Fast")
          ],
          Read = static () => "2",
          Set = static _ => {}
        }
      ),
      Descriptors.State(
        "City name",
        new TextState {
          Read = static () => "Lyon"
        }
      )
    );

    var states = this.fixture.Registry.Search("", 1).Rows.Select(static row => row.State).ToList();

    Assert.Equal(
      new ToggleValue(true) {
        IsSettable = false
      },
      states[0]
    );
    Assert.Equal(
      new NumberValue(0.5, 0, 1, null) {
        IsSettable = true
      },
      states[1]
    );
    Assert.Equal(
      new TextValue("Lyon") {
        IsSettable = false
      },
      states[3]
    );

    var choice = Assert.IsType<ChoiceValue>(states[2]);

    Assert.Equal("2", choice.Key);
    Assert.True(choice.IsSettable);
    Assert.Equal([new ChoiceOption("1", "Slow"), new ChoiceOption("2", "Fast")], choice.Options);
  }

  [Fact]
  public void SetState_ReachesTheSetterAndTheNextReadShowsTheChange() {
    var isPaused = false;

    this.Add(
      Descriptors.State(
        "Paused",
        new ToggleState {
          Read = () => isPaused,
          Set = value => isPaused = value
        }
      )
    );

    Assert.Equal(ActionSetOutcome.Set, this.fixture.Registry.SetState("test/Paused", "true"));
    Assert.Equal(
      new ToggleValue(true) {
        IsSettable = true
      },
      this.ReadState("test/Paused")
    );
    Assert.Equal(1, this.fixture.UseStore.Saved["test/Paused"].Count);
  }

  [Theory]
  [InlineData("0.25", true, 0.25)]
  [InlineData("2", false, 0.5)]
  [InlineData("loud", false, 0.5)]
  [InlineData("NaN", false, 0.5)]
  public void SetState_ParsesANumberInvariantlyAndRefusesOneOutOfBounds(
    string value,
    bool isSet,
    double volume
  ) {
    var current = 0.5;

    this.Add(
      Descriptors.State(
        "Volume",
        new NumberState {
          Read = () => current,
          Set = set => current = set,
          Min = 0,
          Max = 1
        }
      )
    );

    Assert.Equal(
      isSet ? ActionSetOutcome.Set : ActionSetOutcome.Invalid,
      this.fixture.Registry.SetState("test/Volume", value)
    );
    Assert.Equal(volume, current);
  }

  [Fact]
  public void SetState_RefusesAKeyNotAmongTheOptions() {
    var key = "1";

    this.Add(
      Descriptors.State(
        "Speed",
        new ChoiceState {
          Options = [new ActionChoice("1", "Slow"), new ActionChoice("2", "Fast")],
          Read = () => key,
          Set = set => key = set
        }
      )
    );

    Assert.Equal(ActionSetOutcome.Invalid, this.fixture.Registry.SetState("test/Speed", "3"));
    Assert.Equal(ActionSetOutcome.Set, this.fixture.Registry.SetState("test/Speed", "2"));
    Assert.Equal("2", key);
  }

  [Fact]
  public void SetState_RefusesAReadOnlyStateAndAnActionWithNone() {
    this.Add(
      Descriptors.State(
        "Paused",
        new ToggleState {
          Read = static () => false
        }
      ),
      Descriptors.Call("Bulldoze")
    );

    Assert.Equal(ActionSetOutcome.ReadOnly, this.fixture.Registry.SetState("test/Paused", "true"));
    Assert.Equal(ActionSetOutcome.ReadOnly, this.fixture.Registry.SetState("test/Bulldoze", "x"));
    Assert.Empty(this.fixture.UseStore.Saved);
  }

  [Fact]
  public void SetState_RefusesAnIdTheReaderDisabledSinceItsRowWasWritten() {
    var isEnabled = true;
    var sets = 0;

    this.Add(
      Descriptors.State(
        "Name",
        new TextState {
          Read = static () => "",
          Set = _ => sets++
        }
      ) with {
        Availability = () => isEnabled ? ActionAvailability.Enabled : ActionAvailability.Disabled()
      }
    );

    Assert.True(Assert.Single(this.fixture.Registry.Search("name", 1).Rows).IsEnabled);

    isEnabled = false;

    Assert.Equal(ActionSetOutcome.Refused, this.fixture.Registry.SetState("test/Name", "Lyon"));
    Assert.Equal(0, sets);
    Assert.Empty(this.fixture.UseStore.Saved);
  }

  [Fact]
  public void SetState_DisablesTheActionWhoseSetterThrows() {
    this.Add(
      Descriptors.State(
        "Paused",
        new ToggleState {
          Read = static () => false,
          Set = static _ => throw new InvalidOperationException("The setter failed.")
        }
      )
    );

    Assert.Equal(ActionSetOutcome.Failed, this.fixture.Registry.SetState("test/Paused", "true"));

    var row = Assert.Single(this.fixture.Registry.Search("paused", 1).Rows);

    Assert.False(row.IsEnabled);
    Assert.Null(row.State);
    Assert.Single(this.fixture.Log.Calls);
  }

  [Fact]
  public void Search_DisablesTheActionWhoseTextOrChoiceReaderAnswersNull() {
    // Foreign code can break the reader's annotation, and a null string written to the UI would
    // change the value's shape.
    this.Add(
      Descriptors.State(
        "City name",
        new TextState {
          Read = static () => null!
        }
      ),
      Descriptors.State(
        "Speed",
        new ChoiceState {
          Options = [new ActionChoice("1", "1")],
          Read = static () => null!
        }
      )
    );

    var rows = this.fixture.Registry.Search("", 1).Rows;

    Assert.All(
      rows,
      static row => {
        Assert.False(row.IsEnabled);
        Assert.Null(row.State);
      }
    );

    Assert.Equal(2, rows.Count);
    Assert.Equal(2, this.fixture.Log.Calls.Count);
  }

  [Fact]
  public void Invoke_AnswersNoInvokeForAnActionWithAStateAlone() {
    this.Add(
      Descriptors.State(
        "Paused",
        new ToggleState {
          Read = static () => false
        }
      )
    );

    Assert.Equal(ActionInvokeOutcome.NoInvoke, this.fixture.Registry.Invoke("test/Paused"));
    Assert.Equal(ActionSetOutcome.Unknown, this.fixture.Registry.SetState("test/Nothing", "true"));
  }

  [Fact]
  public void ReadLive_ShowsTheConsequenceOfASetOnACoupledState() {
    // A source whose speed setter unpauses, a rule the registry knows nothing of.
    var speed = 0;

    this.Add(
      Descriptors.State(
        "Paused",
        new ToggleState {
          Read = () => speed is 0,
          Set = paused => speed = paused ? 0 : 1
        }
      ),
      Descriptors.State(
        "Speed",
        new ChoiceState {
          Options = [new ActionChoice("1", "1"), new ActionChoice("2", "2")],
          Read = () => speed is 0 ? "1" : speed.ToString(),
          Set = key => speed = int.Parse(key)
        }
      )
    );

    this.fixture.Registry.SetState("test/Speed", "2");

    Assert.Equal(
      new ToggleValue(false) {
        IsSettable = true
      },
      this.ReadState("test/Paused")
    );
  }

  [Fact]
  public void ReadLive_ReadsTheGivenIdsAloneInTheirOrder() {
    var reads = new List<string>();

    foreach (var label in new[] { "A", "B", "C" }) {
      this.source.Descriptors.Add(
        Descriptors.Call(label) with {
          Availability = () => {
            reads.Add(label);

            return label is "B" ? ActionAvailability.Hidden : ActionAvailability.Enabled;
          }
        }
      );
    }

    this.fixture.Registry.AddSource(this.source);

    var lives = this.fixture.Registry.ReadLive(["test/C", "test/B", "test/Nothing", "test/C"]);

    Assert.Equal(["test/C", "test/C"], lives.Select(static live => live.Id));
    Assert.Equal(["C", "B", "C"], reads);
  }

  [Fact]
  public void Refresh_RereadsTheRowsKeepingTheirRangesAndSequence() {
    var isPaused = false;
    var isHidden = false;

    this.Add(
      Descriptors.State(
        "Paused",
        new ToggleState {
          Read = () => isPaused
        }
      ),
      Descriptors.Call("Pause menu") with {
        Availability = () => isHidden ? ActionAvailability.Hidden : ActionAvailability.Enabled
      }
    );

    var results = this.fixture.Registry.Search("pause", 7);

    Assert.Equal(results, this.fixture.Registry.Refresh(results));

    isPaused = true;
    isHidden = true;

    var refreshed = this.fixture.Registry.Refresh(results);

    Assert.Equal(7, refreshed.Sequence);

    var row = Assert.Single(refreshed.Rows);

    Assert.Equal(
      new ToggleValue(true) {
        IsSettable = false
      },
      row.State
    );
    Assert.Equal(results.Rows[0].Matches, row.Matches);
  }

  private void Add(params ActionDescriptor[] descriptors) {
    this.source.Descriptors.AddRange(descriptors);
    this.fixture.Registry.AddSource(this.source);
  }

  private ActionStateValue? ReadState(string id) =>
    Assert.Single(this.fixture.Registry.ReadLive([id])).State;
}
