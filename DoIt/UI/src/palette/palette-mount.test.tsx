import { afterEach, beforeEach, describe, expect, it } from 'bun:test';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { resetBindings, setBinding } from '../testing/game-setup';
import { CityPaletteMount, ScreenPaletteMount } from './palette-mount';

// The game's values for the screens and states the palette opens on or not.
const gameScreen = { main: 0, pauseMenu: 10 } as const;
const overlayScreen = { none: 0, loading: 13 } as const;
const controlScheme = { keyboardAndMouse: 0, gamepad: 1 } as const;

const ESCAPE_CODE = 27;

beforeEach(() => {
  // Every gate is set for each test: a binding a test changed keeps its value into the next one.
  setBinding('doIt.actions', 'openPalette.phase', 'Waiting');
  setBinding('doIt.actions', 'openPalette.binding', {
    binding: { device: 'Keyboard', name: 'k', displayName: 'K' },
    modifiers: [{ device: 'Keyboard', name: 'ctrl', displayName: 'Ctrl' }]
  });
  setBinding('overlay', 'activeScreen', overlayScreen.none);
  setBinding('input', 'controlScheme', controlScheme.keyboardAndMouse);
  setBinding('game', 'activeScreen', gameScreen.main);
});

afterEach(() => {
  cleanup();
  resetBindings();
});

describe('ScreenPaletteMount', () => {
  it(`opens the palette on a press of the open key`, () => {
    render(<ScreenPaletteMount />);

    expect(isOpen()).toBe(false);

    pressOpenKey();

    expect(isOpen()).toBe(true);
  });

  it(`opens on a press released in the frame it came in`, () => {
    render(<ScreenPaletteMount />);

    // One input update delivers both phases, and React renders once after them.
    act(() => {
      setBinding('doIt.actions', 'openPalette.phase', 'Performed');
      setBinding('doIt.actions', 'openPalette.phase', 'Canceled');
    });

    expect(isOpen()).toBe(true);
  });

  it(`does not open on a press that came before it mounted`, () => {
    setBinding('doIt.actions', 'openPalette.phase', 'Performed');

    render(<ScreenPaletteMount />);

    expect(isOpen()).toBe(false);
  });

  it(`closes when the palette asks`, () => {
    render(<ScreenPaletteMount />);

    pressOpenKey();
    fireEvent.keyDown(screen.getByRole('textbox'), { keyCode: ESCAPE_CODE });

    expect(isOpen()).toBe(false);
  });

  it(`closes under an overlay, and does not open there`, () => {
    render(<ScreenPaletteMount />);

    pressOpenKey();

    act(() => {
      setBinding('overlay', 'activeScreen', overlayScreen.loading);
    });

    expect(isOpen()).toBe(false);

    pressOpenKey();

    expect(isOpen()).toBe(false);
  });

  it(`does not open on a gamepad`, () => {
    setBinding('input', 'controlScheme', controlScheme.gamepad);

    render(<ScreenPaletteMount />);

    pressOpenKey();

    expect(isOpen()).toBe(false);
  });

  it(`defines the Debug entry points only while the palette can open`, () => {
    render(<ScreenPaletteMount />);

    act(() => {
      globalThis.doItDebug?.openPalette?.();
    });

    expect(isOpen()).toBe(true);

    act(() => {
      globalThis.doItDebug?.closePalette?.();
    });

    expect(isOpen()).toBe(false);

    act(() => {
      setBinding('overlay', 'activeScreen', overlayScreen.loading);
    });

    expect(globalThis.doItDebug?.openPalette).toBeUndefined();
    expect(globalThis.doItDebug?.closePalette).toBeUndefined();
  });
});

describe('CityPaletteMount', () => {
  it(`opens the palette on the city's main screen alone`, () => {
    setBinding('game', 'activeScreen', gameScreen.pauseMenu);

    render(<CityPaletteMount />);

    pressOpenKey();

    expect(isOpen()).toBe(false);

    act(() => {
      setBinding('game', 'activeScreen', gameScreen.main);
    });

    pressOpenKey();

    expect(isOpen()).toBe(true);
  });
});

/**
 * Presses and releases the open key, as the game reports its action's phase.
 */
function pressOpenKey(): void {
  act(() => {
    setBinding('doIt.actions', 'openPalette.phase', 'Performed');
  });

  act(() => {
    setBinding('doIt.actions', 'openPalette.phase', 'Waiting');
  });
}

function isOpen(): boolean {
  return screen.queryByRole('textbox') != null;
}
