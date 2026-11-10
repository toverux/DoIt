using System.Linq;
using DoIt.Actions;
using Xunit;

namespace DoIt.Tests.Actions;

public sealed class ActionRegistryUseTests {
  private readonly RegistryFixture fixture = new(new EqualMatcher());

  private readonly FakeSource source = new();

  [Fact]
  public void RecordInvoked_RecordsAUseOfAnEnabledAction() {
    this.Add(Descriptors.Trigger("Economy"));

    Assert.Equal(ActionInvokeOutcome.Invoked, this.fixture.Registry.RecordInvoked("test/Economy"));

    Assert.Equal(
      new ActionUse(1, RegistryFixture.Now),
      this.fixture.UseStore.Saved["test/Economy"]
    );
  }

  [Fact]
  public void RecordInvoked_RecordsAnActionItsOwnRunDisabled() {
    var isEnabled = true;

    this.Add(
      Descriptors.Trigger("Economy") with {
        Availability = () => isEnabled ? ActionAvailability.Enabled : ActionAvailability.Disabled()
      }
    );

    Assert.True(Assert.Single(this.fixture.Registry.Search("economy", 1).Rows).IsEnabled);

    isEnabled = false;

    // The note lands after the effect, so the reader judges the state the action itself made.
    Assert.Equal(ActionInvokeOutcome.Invoked, this.fixture.Registry.RecordInvoked("test/Economy"));
    Assert.Equal(1, this.fixture.UseStore.Saved["test/Economy"].Count);
  }

  [Fact]
  public void RecordInvoked_RecordsAnEntityActionTheUIFiredWithNothingSelected() {
    this.Add(
      Descriptors.Trigger("Follow") with {
        Subject = ActionSubject.Entity
      }
    );

    Assert.Equal(ActionInvokeOutcome.Invoked, this.fixture.Registry.RecordInvoked("test/Follow"));
    Assert.Equal(1, this.fixture.UseStore.Saved["test/Follow"].Count);
  }

  [Fact]
  public void RecordInvoked_RecordsNothingForAnUnknownId() {
    Assert.Equal(ActionInvokeOutcome.Unknown, this.fixture.Registry.RecordInvoked("test/Nothing"));
    Assert.Empty(this.fixture.UseStore.Saved);
  }

  [Fact]
  public void Search_OrdersEqualScoresByRecordedUse() {
    this.Add(Descriptors.Trigger("Alpha"), Descriptors.Trigger("Bravo"), Descriptors.Call("Delta"));

    this.fixture.Registry.RecordInvoked("test/Bravo");
    this.fixture.Registry.Invoke("test/Delta");
    this.fixture.Registry.Invoke("test/Delta");

    Assert.Equal(
      ["test/Delta", "test/Bravo", "test/Alpha"],
      this.fixture.Registry.Search("", 1).Rows.Select(static row => row.Id)
    );
  }

  [Fact]
  public void Search_RanksAHigherScoreOverMoreUse() {
    var lengthFixture = new RegistryFixture(new LengthMatcher());

    this.source.Descriptors.AddRange([Descriptors.Trigger("Alpha"), Descriptors.Trigger("Bravo!")]);
    lengthFixture.Registry.AddSource(this.source);

    lengthFixture.Registry.RecordInvoked("test/Alpha");

    Assert.Equal(
      ["test/Bravo!", "test/Alpha"],
      lengthFixture.Registry.Search("", 1).Rows.Select(static row => row.Id)
    );
  }

  [Fact]
  public void Search_OrdersByTheUsesAnEarlierRegistrySaved() {
    this.Add(Descriptors.Trigger("Alpha"), Descriptors.Trigger("Bravo"));

    this.fixture.Registry.RecordInvoked("test/Bravo");

    var restarted = new RegistryFixture(new EqualMatcher(), this.fixture.UseStore);

    restarted.Registry.AddSource(this.source);

    Assert.Equal(
      ["test/Bravo", "test/Alpha"],
      restarted.Registry.Search("", 1).Rows.Select(static row => row.Id)
    );
  }

  private void Add(params ActionDescriptor[] descriptors) {
    this.source.Descriptors.AddRange(descriptors);
    this.fixture.Registry.AddSource(this.source);
  }

  /// <summary>
  /// Matches every label with the same score.
  /// </summary>
  private sealed class EqualMatcher : IActionMatcher {
    public ActionMatch Match(string query, string label) => new(1, []);
  }

  /// <summary>
  /// Matches every label, a longer one ranking first.
  /// </summary>
  private sealed class LengthMatcher : IActionMatcher {
    public ActionMatch Match(string query, string label) => new(label.Length, []);
  }
}
