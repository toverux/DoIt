using System.Collections.Generic;
using DoIt.Actions;
using Game.UI.Localization;
using Unity.Entities;
using Xunit;

namespace DoIt.Tests.Actions;

public sealed class ActionRegistryInvokeTests {
  private static readonly Entity Selected = new() {
    Index = 5,
    Version = 1
  };

  private static readonly Entity Explicit = new() {
    Index = 9,
    Version = 2
  };

  private readonly RegistryFixture fixture = new();

  private readonly FakeSource source = new();

  [Fact]
  public void Invoke_CallsAnEnabledCSharpCall() {
    var calls = 0;

    this.Add(Descriptors.Call("Bulldozer", () => calls++));

    Assert.Equal(ActionInvokeOutcome.Invoked, this.fixture.Registry.Invoke("test/Bulldozer"));
    Assert.Equal(1, calls);
  }

  [Fact]
  public void Invoke_RefusesAnUnknownId() =>
    Assert.Equal(ActionInvokeOutcome.Unknown, this.fixture.Registry.Invoke("test/Nothing"));

  [Fact]
  public void Invoke_RefusesAnIdTheReaderDisabledSinceItsRowWasWritten() {
    var isEnabled = true;
    var calls = 0;

    this.Add(
      Descriptors.Call("Bulldozer", () => calls++) with {
        Availability = () => isEnabled ? ActionAvailability.Enabled : ActionAvailability.Disabled()
      }
    );

    Assert.True(Assert.Single(this.fixture.Registry.Search("bulldozer", 1).Rows).IsEnabled);

    isEnabled = false;

    Assert.Equal(ActionInvokeOutcome.Refused, this.fixture.Registry.Invoke("test/Bulldozer"));
    Assert.Equal(0, calls);
  }

  [Fact]
  public void Invoke_FillsABareEntityActionFromTheSelection() {
    var received = this.AddEntityAction(fillsFromSelection: true);

    this.fixture.Selection = ActionRegistryInvokeTests.Selected;

    Assert.Equal(ActionInvokeOutcome.Invoked, this.fixture.Registry.Invoke("test/Follow"));
    Assert.Equal([ActionRegistryInvokeTests.Selected], received);
  }

  [Fact]
  public void Invoke_PassesAnExplicitEntityOverTheSelection() {
    var received = this.AddEntityAction(fillsFromSelection: true);

    this.fixture.Selection = ActionRegistryInvokeTests.Selected;

    this.fixture.Registry.Invoke("test/Follow", ActionRegistryInvokeTests.Explicit);

    Assert.Equal([ActionRegistryInvokeTests.Explicit], received);
  }

  [Fact]
  public void Invoke_RefusesTheSelectionToAnActionThatOptedOut() {
    var received = this.AddEntityAction(fillsFromSelection: false);

    this.fixture.Selection = ActionRegistryInvokeTests.Selected;

    Assert.Equal(ActionInvokeOutcome.Refused, this.fixture.Registry.Invoke("test/Follow"));
    Assert.Empty(received);

    this.fixture.Registry.Invoke("test/Follow", ActionRegistryInvokeTests.Explicit);

    Assert.Equal([ActionRegistryInvokeTests.Explicit], received);
  }

  [Fact]
  public void Search_DisablesABareEntityActionWithNothingSelected_WithTheSelectFirstReason() {
    this.AddEntityAction(fillsFromSelection: true);

    var row = Assert.Single(this.fixture.Registry.Search("follow", 1).Rows);

    Assert.False(row.IsEnabled);
    Assert.Equal(ActionRegistry.SelectFirstReason.id, row.DisabledReason);
    Assert.Equal(ActionInvokeOutcome.Refused, this.fixture.Registry.Invoke("test/Follow"));

    this.fixture.Selection = ActionRegistryInvokeTests.Selected;

    Assert.True(Assert.Single(this.fixture.Registry.Search("follow", 2).Rows).IsEnabled);
  }

  private List<Entity> AddEntityAction(bool fillsFromSelection) {
    var received = new List<Entity>();

    this.Add(
      new ActionDescriptor {
        LocalId = "Follow",
        Label = LocalizedString.Id("Follow"),
        Subject = ActionSubject.Entity,
        FillsFromSelection = fillsFromSelection,
        Channel = ActionChannel.CSharpCall(received.Add)
      }
    );

    return received;
  }

  private void Add(ActionDescriptor descriptor) {
    this.source.Descriptors.Add(descriptor);
    this.fixture.Registry.AddSource(this.source);
  }
}
