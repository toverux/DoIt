using System.Threading.Tasks;
using Colossal.TestFramework;
using DoIt.Actions;
using Game;
using Game.Input;
using Game.SceneFlow;
using Game.Simulation;
using Game.UI.InGame;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoIt.Scenarios;

/// <summary>
/// Drives the palette in a city, through the UI's Debug entry points and through the keyboard and
/// pointer, and reads from the game what reached it.
/// Each check of a game behaviour the palette relies on says, when it fails, what to change.
/// The tests share one city, and each leaves the palette closed.
/// </summary>
[TestDescriptor("DoIt: Palette", ScenarioRegistry.ScenarioCategory)]
internal sealed class PaletteScenario : ModTestScenario {
  private static bool IsBoxFocused => InputManager.instance.hasInputFieldFocus;

  [Test]
  [UsedImplicitly]
  private async Task OpenInACity_MutesTheKeyboardAndHoldsTheCamera() {
    await this.EnsureCity();

    var openAction = Mod.Settings.GetAction(Settings.OpenPaletteAction);
    var cameraMap = InputManager.instance.FindActionMap("Camera");

    this.Check(
      openAction.enabled && cameraMap.enabled,
      "The open key and the camera are live in the city: " +
      $"{openAction.enabled}, {cameraMap.enabled}."
    );

    await this.RunScript("doItDebug.openPalette();");

    this.Check(PaletteScenario.IsBoxFocused, "The box holds the focus.");

    this.Check(
      !openAction.enabled && openAction.shouldBeEnabled,
      $"The focus mutes the open key with every other: {openAction.enabled}, " +
      $"{openAction.shouldBeEnabled}."
    );

    this.Check(!cameraMap.enabled, $"The camera is held: {cameraMap.enabled}.");

    await this.RunScript("doItDebug.closePalette();");

    this.Check(
      !PaletteScenario.IsBoxFocused && openAction.enabled && cameraMap.enabled,
      $"Closing gives the keyboard and the camera back: {PaletteScenario.IsBoxFocused}, " +
      $"{openAction.enabled}, {cameraMap.enabled}."
    );

    await this.Finish();
  }

  [Test]
  [UsedImplicitly]
  private async Task TheOpenKey_OpensAndClosesTheBox_HeldOrNot() {
    await this.EnsureCity();

    using var input = new NativeInput();

    input.HoldKeys(Key.LeftCtrl, Key.K);
    await this.WaitFrames(5);

    this.Check(PaletteScenario.IsBoxFocused, "Ctrl+K opens the box.");

    // Past the delay after which the game's UI repeats a held key.
    await this.WaitSeconds(1);

    this.Check(
      PaletteScenario.IsBoxFocused,
      "Held, the open key keeps the box it opened. If not: the game no longer flags the repeats " +
      "of a held key on `keydown.repeat`, by which the box ignores the open key's; find how the " +
      "game's keyboard module (Colossal.UI.UIKeyboardModule) repeats a key now, and tell its " +
      "repeats from a press that way in palette.tsx."
    );

    input.HoldKeys();
    await this.WaitFrames(5);

    input.HoldKeys(Key.LeftCtrl, Key.K);
    await this.WaitFrames(5);

    this.Check(!PaletteScenario.IsBoxFocused, "Pressed again, the open key closes the box.");

    await this.WaitSeconds(1);

    this.Check(
      !PaletteScenario.IsBoxFocused,
      "Held after the close, the open key leaves the box closed. If not: the game now fires an " +
      "action enabled again while its key is held, which the box's mute gives back on close; " +
      "keep the open action from opening the box until its key is released after a close."
    );

    input.HoldKeys();
    await this.WaitFrames(5);

    await this.Finish();
  }

  [Test]
  [UsedImplicitly]
  private async Task Escape_ClosesTheBox_AndOpensNoPauseMenu() {
    await this.EnsureCity();

    var gameScreen = this.World.GetExistingSystemManaged<GameScreenUISystem>();

    using var input = new NativeInput();

    await this.RunScript("doItDebug.openPalette();");

    input.HoldKeys(Key.Escape);
    await this.WaitFrames(5);

    this.Check(!PaletteScenario.IsBoxFocused, "Escape closes the box.");

    input.HoldKeys();
    await this.WaitFrames(10);

    this.Check(
      gameScreen.activeScreen is GameScreenUISystem.GameScreen.Main,
      "Released after the close, Escape opens no pause menu. If not: the game now acts on a key " +
      "enabled again while held, or on its release; keep the game's keys muted after the close " +
      $"until Escape is released. Screen: {gameScreen.activeScreen}."
    );

    // The control: with the palette closed, the same press opens the pause menu.
    await this.PressEscape(input);

    this.Check(
      gameScreen.activeScreen is GameScreenUISystem.GameScreen.PauseMenu,
      "With the palette closed, Escape opens the pause menu, so the check above reads the " +
      $"palette's mute. Screen: {gameScreen.activeScreen}."
    );

    await this.PressEscape(input);

    this.Check(
      gameScreen.activeScreen is GameScreenUISystem.GameScreen.Main,
      $"Escape closes the pause menu. Screen: {gameScreen.activeScreen}."
    );

    await this.Finish();
  }

  [Test]
  [UsedImplicitly]
  private async Task ThePointerOnTheLayer_IsOverTheGamesUI() {
    await this.EnsureCity();

    using var input = new NativeInput();

    await this.RunScript("doItDebug.openPalette();");

    // Low on the screen, on the layer around the palette's panel and clear of the game's toolbar.
    input.MovePointer(new Vector2(Screen.width * 0.5f, Screen.height * 0.25f));
    await this.WaitFrames(5);

    this.Check(
      InputManager.instance.mouseOverUI,
      "On the layer, the pointer is over the game's UI, which holds the tools' mouse actions " +
      "(decompile, ToolSystem). If not: a click on the layer reaches the tool under it; hold the " +
      "Tool map's mouse actions while the palette is open, as it holds the camera."
    );

    await this.RunScript("doItDebug.closePalette();");

    // A point beside the first: cohtml hit-tests again only once the pointer moves.
    input.MovePointer(new Vector2(Screen.width * 0.5f, Screen.height * 0.3f));
    await this.WaitFrames(5);

    this.Check(
      !InputManager.instance.mouseOverUI,
      "With the palette closed, the pointer there is off the UI, so the check above reads the " +
      "layer."
    );

    await this.Finish();
  }

  [Test]
  [UsedImplicitly]
  private async Task AnActionRunWithTheBoxFocused_ReachesTheGame() {
    await this.EnsureCity();

    var simulationSystem = this.World.GetExistingSystemManaged<SimulationSystem>();

    // A city loads paused under the player's paused-after-loading setting: running, the pause below
    // proves the action reached the game.
    simulationSystem.selectedSpeed = 1;

    await this.RunScript("doItDebug.openPalette();");
    await this.RunScript("engine.trigger('doIt.actions.query', '', 42);");

    await this.RunScript($"doItDebug.runAction('{new ActionId("game", "Pause Simulation")}');");

    this.Check(
      PaletteScenario.IsBoxFocused && simulationSystem.selectedSpeed is 0,
      "An input action run with the box focused reaches the game's root input stack. If not: " +
      "the box's focus now keeps the game's UI actions from the root; close the palette before " +
      $"running an action. Focused: {PaletteScenario.IsBoxFocused}, " +
      $"speed {simulationSystem.selectedSpeed}."
    );

    simulationSystem.selectedSpeed = 1;

    await this.Finish();
  }

  /// <summary>
  /// Closes the palette a test left open, so the next one starts with it closed.
  /// </summary>
  [TestCleanup]
  [UsedImplicitly]
  private Task ClosePalette() => this.RunScript("doItDebug.closePalette?.();");

  /// <summary>
  /// Presses and releases Escape, then waits for the game to act on the release.
  /// </summary>
  private async Task PressEscape(NativeInput input) {
    input.HoldKeys(Key.Escape);
    await this.WaitFrames(5);

    input.HoldKeys();
    await this.WaitFrames(10);
  }

  /// <summary>
  /// Starts the city the tests share, unless an earlier test did.
  /// </summary>
  private async Task EnsureCity() {
    if (GameManager.instance.gameMode is GameMode.Game) {
      return;
    }

    await this.StartNewCity(ModTestScenario.Archipelago, "Do It Palette");
  }
}
