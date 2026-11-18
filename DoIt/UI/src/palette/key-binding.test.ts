import { describe, expect, it } from 'bun:test';
import type { ControlPath } from 'cs2/input';
import type { ProxyBinding } from '../utils/bindings';
import { isKey, type KeyStroke, matchesKeyBinding } from './key-binding';

// The codes and locations the engine sends for these keys.
const code = { keyK: 75, keyL: 76, enter: 13, escape: 27, f2: 113, digit0: 48 } as const;
const location = { standard: 0, numpad: 3 } as const;

describe('matching a keydown with a key binding', () => {
  const ctrlK = binding('k', 'ctrl');

  it(`matches the key with its modifier held`, () => {
    expect(matchesKeyBinding(stroke(code.keyK, { ctrlKey: true }), ctrlK)).toBe(true);
  });

  it(`matches no other key with the same modifier`, () => {
    expect(matchesKeyBinding(stroke(code.keyL, { ctrlKey: true }), ctrlK)).toBe(false);
  });

  it(`matches neither the bare key nor the key with an extra modifier`, () => {
    expect(matchesKeyBinding(stroke(code.keyK), ctrlK)).toBe(false);
    expect(matchesKeyBinding(stroke(code.keyK, { ctrlKey: true, shiftKey: true }), ctrlK)).toBe(
      false
    );
  });

  it(`reads a modifier named for one side as that modifier`, () => {
    const sidedCtrlShiftK = binding('k', 'leftCtrl', 'rightShift');

    expect(
      matchesKeyBinding(stroke(code.keyK, { ctrlKey: true, shiftKey: true }), sidedCtrlShiftK)
    ).toBe(true);
  });

  it(`matches a named key by the code the engine sends for it`, () => {
    expect(matchesKeyBinding(stroke(code.f2, { altKey: true }), binding('f2', 'alt'))).toBe(true);
    expect(matchesKeyBinding(stroke(code.digit0), binding('0'))).toBe(true);
  });

  it(`tells the numpad's Enter from the main one by its location`, () => {
    const numpadEnter = stroke(code.enter, { location: location.numpad });

    expect(matchesKeyBinding(numpadEnter, binding('numpadEnter'))).toBe(true);
    expect(matchesKeyBinding(numpadEnter, binding('enter'))).toBe(false);
    expect(matchesKeyBinding(stroke(code.enter), binding('numpadEnter'))).toBe(false);
  });

  it(`matches nothing for a key the engine sends no code for`, () => {
    expect(matchesKeyBinding(stroke(0), binding('backquote'))).toBe(false);
    expect(
      matchesKeyBinding(stroke(0, { location: location.numpad }), binding('numpadEquals'))
    ).toBe(false);
    expect(matchesKeyBinding(stroke(0), binding(''))).toBe(false);
  });
});

describe('telling a keydown is a key', () => {
  it(`ignores the modifiers held`, () => {
    expect(isKey(stroke(code.escape, { shiftKey: true }), 'escape')).toBe(true);
  });
});

function stroke(keyCode: number, overrides: Partial<KeyStroke> = {}): KeyStroke {
  return {
    // oxlint-disable-next-line typescript/no-deprecated - the engine names keys by it, see KeyStroke.
    keyCode,
    location: location.standard,
    ctrlKey: false,
    shiftKey: false,
    altKey: false,
    ...overrides
  };
}

function binding(name: string, ...modifiers: readonly string[]): ProxyBinding {
  return {
    binding: controlPath(name),
    modifiers: modifiers.map(modifier => controlPath(modifier))
  };
}

function controlPath(name: string): ControlPath {
  return { device: 'Keyboard', name, displayName: name };
}
