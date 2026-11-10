using System;
using System.Linq;
using DoIt.Actions;
using Xunit;

namespace DoIt.Tests.Actions;

public sealed class ActionRegistryFaultTests {
  private readonly RegistryFixture fixture = new();

  [Fact]
  public void Rebuild_LeavesOutASourceThatThrowsListing_AndKeepsTheOthers() {
    var broken = new FakeSource("broken") {
      ListThrows = true
    };

    var sound = new FakeSource("sound");

    broken.Descriptors.Add(Descriptors.Call("Tool A"));
    sound.Descriptors.Add(Descriptors.Call("Tool B"));

    this.fixture.Registry.AddSource(broken);
    this.fixture.Registry.AddSource(sound);

    Assert.Equal(
      ["Tool B"],
      this.fixture.Registry.Search("tool", 1).Rows.Select(static row => row.Label)
    );

    Assert.Equal(
      [("broken", true), ("sound", false)],
      this.fixture.Registry.Sources.Select(static status => (status.Info.Segment, status.IsDown))
    );
  }

  [Fact]
  public void Rebuild_LogsADownSourceOnce_WithItsModAndVersion() {
    var broken = new FakeSource("broken") {
      ListThrows = true
    };

    this.fixture.Registry.AddSource(broken);
    this.fixture.Registry.Rebuild();

    var warning = Assert.Single(this.fixture.Log.Calls, static call => call.Method == "Warn");

    Assert.IsType<InvalidOperationException>(warning.Args[0]);
    Assert.Contains("Fake Mod 1.2.3.4", (string?) warning.Args[1]);
  }

  [Fact]
  public void Changed_RebuildsTheCatalogue_BringingADownSourceBack() {
    var source = new FakeSource {
      ListThrows = true
    };

    source.Descriptors.Add(Descriptors.Call("Tool A"));
    this.fixture.Registry.AddSource(source);

    source.ListThrows = false;
    source.RaiseChanged();

    Assert.Single(this.fixture.Registry.Search("tool", 1).Rows);
    Assert.False(Assert.Single(this.fixture.Registry.Sources).IsDown);
  }

  [Fact]
  public void Search_DisablesTheOneActionWhoseReaderThrows_AndLogsItOnce() {
    var source = new FakeSource();

    source.Descriptors.AddRange(
      [
        Descriptors.Call("Tool A") with {
          Availability = static () => throw new InvalidOperationException("The reader failed.")
        },
        Descriptors.Call("Tool B")
      ]
    );

    this.fixture.Registry.AddSource(source);

    this.fixture.Registry.Search("tool", 1);

    var rows = this.fixture.Registry.Search("tool", 2).Rows;

    Assert.Equal(
      [("Tool A", false, ActionRegistry.FailedReason.id), ("Tool B", true, null)],
      rows.Select(static row => (row.Label, row.IsEnabled, row.DisabledReason))
    );

    var warning = Assert.Single(this.fixture.Log.Calls, static call => call.Method == "Warn");

    Assert.Contains("test/Tool A", (string?) warning.Args[1]);
    Assert.Contains("Fake Mod 1.2.3.4", (string?) warning.Args[1]);
  }

  [Fact]
  public void Invoke_DisablesAnActionWhoseDelegateThrows_AndLogsItOnce() {
    var source = new FakeSource();

    source.Descriptors.Add(
      Descriptors.Call("Tool A", static () => throw new InvalidOperationException("Invoke failed."))
    );

    this.fixture.Registry.AddSource(source);

    Assert.Equal(ActionInvokeOutcome.Failed, this.fixture.Registry.Invoke("test/Tool A"));
    Assert.Equal(ActionInvokeOutcome.Refused, this.fixture.Registry.Invoke("test/Tool A"));
    Assert.False(Assert.Single(this.fixture.Registry.Search("tool", 1).Rows).IsEnabled);
    Assert.Single(this.fixture.Log.Calls, static call => call.Method == "Warn");
  }
}
