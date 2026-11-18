using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Colossal.UI;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using Xunit;

namespace DoIt.Tests.UI;

/// <summary>
/// Holds the palette's key table, <c>key-codes.json</c>, to the one the game sends keys to its UI
/// with (decompile, <c>Colossal.UI.UIKeyboardModule</c>), key by key, under the names the Unity
/// Input System gives the keys.
/// A lookup of a game member by name returns null once an update renames the member, and the test
/// fails on a null reference at that line: find the member's new name in the decompile.
/// </summary>
public sealed class KeyCodesTests {
  [Fact]
  public void TheTable_IsTheOneTheGameSendsKeysToItsUIWith() {
    var gameCodes = KeyCodesTests.ReadGameKeys()
      .Where(static key => key.KeyCode is not 0)
      .ToDictionary(static key => key.Name, static key => key.KeyCode);

    var tableCodes = JsonSerializer.Deserialize<Dictionary<string, int>>(
      File.ReadAllText(Path.Combine("UI", "key-codes.json"))
    )!;

    var differences = gameCodes.Keys
      .Union(tableCodes.Keys)
      .Where(name => gameCodes.GetValueOrDefault(name) != tableCodes.GetValueOrDefault(name))
      .Select(name =>
        $"{name}: the game {gameCodes.GetValueOrDefault(name)}, " +
        $"the table {tableCodes.GetValueOrDefault(name)}"
      )
      .ToList();

    Assert.True(
      differences.Count is 0,
      "The game sends its UI other key codes since an update: copy them into key-codes.json, and " +
      "where a key the game sent no code for now has one, the backquote among them, drop the " +
      $"notes saying it has none (key-binding.ts, the spec). {string.Join("; ", differences)}"
    );
  }

  /// <summary>
  /// The palette reads a key named <c>numpad…</c> as one the game sends on the numpad's location,
  /// and every other as one it does not.
  /// </summary>
  [Fact]
  public void TheNumpadKeys_AreTheOnesTheGameSendsOnTheNumpadLocation() {
    var isNumpadKey = typeof(UIKeyboardModule).GetMethod(
      "IsNumPadKey",
      BindingFlags.NonPublic | BindingFlags.Instance
    )!;

    // A method reading its argument alone, called on an instance no constructor ran for.
    var module = RuntimeHelpers.GetUninitializedObject(typeof(UIKeyboardModule));

    var misplaced = KeyCodesTests.ReadGameKeys()
      .Where(key =>
        (bool) isNumpadKey.Invoke(module, [key.Bit])! != key.Name.StartsWith("numpad")
      )
      .Select(static key => key.Name)
      .ToList();

    Assert.True(
      misplaced.Count is 0,
      "The game sends other keys on the numpad's location since an update: make isKey " +
      $"(key-binding.ts) tell them by another rule than their name. {string.Join(", ", misplaced)}"
    );
  }

  /// <summary>
  /// Lists every key of the keyboard by its Unity name, its bit in the keyboard's state, which is
  /// its <c>Key</c> value, and the code the game sends for it, 0 for none.
  /// </summary>
  private static List<(string Name, int Bit, int KeyCode)> ReadGameKeys() {
    var keysMap = (Dictionary<int, int>) typeof(UIKeyboardModule)
      .GetNestedType("KeyCodeMapping", BindingFlags.NonPublic)!
      .GetField("keysMap")!
      .GetValue(null)!;

    return typeof(KeyboardState)
      .GetField(nameof(KeyboardState.keys))!
      .GetCustomAttributes<InputControlAttribute>()
      .Where(static control => control.layout is "Key")
      .Select(control => (
          control.name,
          (int) control.bit,
          keysMap.GetValueOrDefault((int) control.bit)
        )
      )
      .ToList();
  }
}
