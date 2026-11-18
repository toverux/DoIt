import { afterEach, beforeEach, describe, expect, it, mock } from 'bun:test';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { resetBindings, setBinding } from '../testing/game-setup';
import { Palette } from './palette';

// The codes the engine sends for these keys, by which the palette tells them.
const code = { tab: 9, escape: 27, arrowDown: 40, backspace: 8, keyK: 75 } as const;

const mouseButton = { left: 0, middle: 1, right: 2 } as const;

const ctrlK = {
  binding: { device: 'Keyboard', name: 'k', displayName: 'K' },
  modifiers: [{ device: 'Keyboard', name: 'ctrl', displayName: 'Ctrl' }]
};

const onClose = mock(() => {
  // Counted only.
});

beforeEach(() => {
  setBinding('doIt.actions', 'openPalette.binding', ctrlK);
});

afterEach(() => {
  cleanup();
  resetBindings();
  onClose.mockClear();
});

describe('Palette', () => {
  it(`takes the focus into its box`, () => {
    const { box } = renderPalette();

    expect(document.activeElement).toBe(box);
  });

  it(`closes on Escape and on the open key, which it keeps from the box`, () => {
    const { box } = renderPalette();

    expect(fireEvent.keyDown(box, { keyCode: code.escape })).toBe(false);
    expect(fireEvent.keyDown(box, { keyCode: code.keyK, ctrlKey: true })).toBe(false);
    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it(`stays open while the open key is held from the press that opened it`, () => {
    const { box } = renderPalette();

    expect(fireEvent.keyDown(box, { keyCode: code.keyK, ctrlKey: true, repeat: true })).toBe(false);
    expect(onClose).not.toHaveBeenCalled();
  });

  it(`keeps Tab from moving the focus out of the box`, () => {
    const { box } = renderPalette();

    expect(fireEvent.keyDown(box, { keyCode: code.tab })).toBe(false);
    expect(onClose).not.toHaveBeenCalled();
  });

  it(`leaves every other key to the box`, () => {
    const { box } = renderPalette();

    expect(fireEvent.keyDown(box, { keyCode: code.arrowDown })).toBe(true);
    expect(fireEvent.keyDown(box, { keyCode: code.backspace })).toBe(true);
    expect(fireEvent.keyDown(box, { keyCode: code.keyK })).toBe(true);
    expect(onClose).not.toHaveBeenCalled();
  });

  it(`closes when its box loses the focus`, () => {
    const { box } = renderPalette();

    act(() => {
      box.blur();
    });

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it(`closes on a click of any button on the layer around its panel`, () => {
    const { layer } = renderPalette();

    const buttons = Object.values(mouseButton);

    for (const button of buttons) {
      expect(fireEvent.mouseDown(layer, { button })).toBe(false);
      fireEvent.mouseUp(layer, { button });
    }

    expect(onClose).toHaveBeenCalledTimes(buttons.length);
  });

  it(`keeps the focus in its box on a press in its panel`, () => {
    const { box, panel } = renderPalette();

    expect(fireEvent.mouseDown(panel)).toBe(false);
    fireEvent.mouseUp(panel);

    expect(document.activeElement).toBe(box);
    expect(onClose).not.toHaveBeenCalled();
  });

  it(`stays open when a press in its box is released on the layer`, () => {
    const { box, layer } = renderPalette();

    // The box takes the press itself, to place the caret or start a selection.
    expect(fireEvent.mouseDown(box)).toBe(true);
    fireEvent.mouseUp(layer);

    expect(onClose).not.toHaveBeenCalled();
  });
});

/**
 * Renders the palette, and finds its box, the panel around it, and the layer around the panel.
 */
function renderPalette(): { box: HTMLElement; panel: HTMLElement; layer: HTMLElement } {
  render(<Palette onClose={onClose} />);

  const box = screen.getByRole('textbox');
  const panel = box.parentElement;
  const layer = panel?.parentElement;

  if (!panel || !layer) {
    throw new Error(`The palette's box is not in a panel over a layer.`);
  }

  return { box, panel, layer };
}
