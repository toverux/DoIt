using System;
using Game.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DoIt.Scenarios;

/// <summary>
/// Sends the game keys and pointer moves through the Unity Input System, as the player's keyboard
/// and mouse do, whether or not the game's window has the focus; disposing it gives the window's
/// focus rules back.
/// An event takes effect on the input system's next update: wait a few frames before reading what
/// it changed, and between a press and its release.
/// </summary>
internal sealed class NativeInput : IDisposable {
  private readonly InputSettings.BackgroundBehavior backgroundBehavior =
    InputSystem.settings.backgroundBehavior;

  /// <summary>
  /// Lets the input system read the devices with the window in the background, which disables them
  /// otherwise, and the game take the pointer as on its screen.
  /// </summary>
  internal NativeInput() {
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    InputManager.instance.OnFocusChanged(true);
  }

  /// <summary>
  /// Holds the keys given down, and releases every other: no key releases them all.
  /// </summary>
  internal void HoldKeys(params Key[] keys) =>
    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));

  /// <summary>
  /// Moves the pointer to a point of the screen, in pixels from its bottom left corner.
  /// </summary>
  internal void MovePointer(Vector2 position) {
    var delta = position - Mouse.current.position.ReadValue();

    // Cohtml hit-tests the UI, and the game updates `mouseOverUI`, only on an event that moves the
    // pointer, which the delta tells and the position alone does not.
    InputSystem.QueueStateEvent(
      Mouse.current,
      new MouseState {
        position = position,
        delta = delta == Vector2.zero ? Vector2.one : delta
      }
    );
  }

  public void Dispose() {
    this.HoldKeys();

    // The devices stay enabled: the input system disables them itself on the window's next loss of
    // focus, where a device disabled here would stay disabled once the window has the focus again.
    InputSystem.settings.backgroundBehavior = this.backgroundBehavior;

    InputManager.instance.OnFocusChanged(Application.isFocused);
  }
}
