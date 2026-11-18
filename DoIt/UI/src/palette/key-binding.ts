import type { ProxyBinding } from '../utils/bindings';
import keyCodeTable from './key-codes.json';

/**
 * The parts of a `keydown` that tell its key and modifiers.
 * The engine sends a key as its `keyCode` and `location` alone, and derives `key` and `code` from
 * them, neither of which names every key: `key` is the character of the code's number, so the left
 * arrow, 37, reads `%`, and `code` makes up a name for a key the engine has no code for.
 */
export type KeyStroke = Pick<
  KeyboardEvent,
  'keyCode' | 'location' | 'ctrlKey' | 'shiftKey' | 'altKey'
>;

/**
 * The `keyCode` the engine gives a `keydown`, by the name the Unity Input System gives the same
 * key, the name a binding holds.
 * Both name the physical key, never the character the keyboard layout puts on it: the game maps
 * each Unity key to a Windows virtual key code through a fixed table (decompile,
 * `Colossal.UI.UIKeyboardModule`), which `key-codes.json` copies and an off-engine C# test holds to
 * the game's, so a binding matches whatever the layout.
 * A key the game maps to no code, the backquote among them, reaches the UI with a `keyCode` of 0,
 * which no name here matches.
 */
const keyCodes: ReadonlyMap<string, number> = new Map(Object.entries(keyCodeTable));

/**
 * The `location` of a key on the numpad, which tells the numpad's Enter from the main one: both
 * have the same `keyCode`.
 */
const NUMPAD_LOCATION = 3;

/**
 * Whether a `keydown` is the key a Unity Input System name stands for, whatever modifiers are held.
 */
export function isKey(stroke: KeyStroke, name: string): boolean {
  return (
    // oxlint-disable-next-line typescript/no-deprecated - the engine names keys by it, see KeyStroke.
    stroke.keyCode == keyCodes.get(name) &&
    (stroke.location == NUMPAD_LOCATION) == name.startsWith('numpad')
  );
}

/**
 * Whether a `keydown` is the binding's key, with exactly the binding's modifiers held.
 * A binding naming a key the engine sends no `keyCode` for matches nothing.
 */
export function matchesKeyBinding(stroke: KeyStroke, binding: ProxyBinding): boolean {
  // A modifier is named as its key, either side (`ctrl`), or one side alone (`leftCtrl`); the
  // engine's modifier flags do not tell the sides apart.
  const modifiers = binding.modifiers.map(modifier => modifier.name.toLowerCase());

  return (
    isKey(stroke, binding.binding.name) &&
    stroke.ctrlKey == modifiers.some(name => name.endsWith('ctrl')) &&
    stroke.shiftKey == modifiers.some(name => name.endsWith('shift')) &&
    stroke.altKey == modifiers.some(name => name.endsWith('alt'))
  );
}
