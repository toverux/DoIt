using System;
using DoIt.Actions;
using Game;
using Xunit;

namespace DoIt.Tests.Actions;

public sealed class RegistrationActionSourceTests {
  private readonly RegistryFixture fixture = new();

  private readonly RegistrationActionSource source = new("MyMod", "2.0.0.0");

  public RegistrationActionSourceTests() => this.fixture.Registry.AddSource(this.source);

  [Fact]
  public void Register_MapsPlainTypesOntoAnAction_ThatSearchFinds() {
    var calls = 0;

    this.source.Register(
      new ActionRegistration {
        Key = "open",
        LabelId = "MyMod.OPEN",
        LabelFallback = "Open My Mod",
        Icon = "coui://mymod/icon.svg",
        CategoryId = "MyMod.CATEGORY",
        Modes = (int) (GameMode.Game | GameMode.MainMenu),
        Invoke = () => calls++
      }
    );

    this.fixture.GameMode = GameMode.MainMenu;

    var row = Assert.Single(this.fixture.Registry.Search("open my", 1).Rows);

    Assert.Equal("api:MyMod/open", row.Id);
    Assert.Equal("Open My Mod", row.Label);
    Assert.Equal("coui://mymod/icon.svg", row.Icon);
    Assert.Equal("MyMod.CATEGORY", row.Category);
    Assert.Equal(ActionInvokeOutcome.Invoked, this.fixture.Registry.Invoke(row.Id));
    Assert.Equal(1, calls);
  }

  [Fact]
  public void Register_FallsBackToTheRegistrantsNameAsCategory() {
    this.source.Register(
      new ActionRegistration {
        Key = "open",
        LabelId = "Open",
        Invoke = static () => {}
      }
    );

    Assert.Equal("MyMod", Assert.Single(this.fixture.Registry.Search("open", 1).Rows).Category);
  }

  [Fact]
  public void Unregister_RemovesTheAction() {
    this.source.Register(
      new ActionRegistration {
        Key = "open",
        LabelId = "Open",
        Invoke = static () => {}
      }
    );
    this.source.Unregister("open");

    Assert.Empty(this.fixture.Registry.Search("open", 1).Rows);
  }

  [Fact]
  public void Register_RefusesAnEmptyKey() =>
    Assert.Throws<ArgumentException>(() =>
      this.source.Register(
        new ActionRegistration {
          Key = "",
          LabelId = "Open",
          Invoke = static () => {}
        }
      )
    );

  [Fact]
  public void Constructor_RefusesARegistrantNameHoldingASlash() =>
    Assert.Throws<ArgumentException>(static () => new RegistrationActionSource("My/Mod", "1.0"));
}
