import { type KeyboardEvent, type MouseEvent, type ReactNode, useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { openPaletteAction } from '../utils/bindings';
import { useCameraActionBarrier } from '../vanilla-modules/game-ui/common/hooks/use-camera-action-barrier';
import { isKey, matchesKeyBinding } from './key-binding';
import * as styles from './palette.module.scss';

interface PaletteProps {
  readonly onClose: () => void;
}

/**
 * The command palette: a text box in a panel, over a layer that covers the screen.
 *
 * The box holds the keyboard focus for the palette's whole life, which is what mutes the game's
 * keyboard: a focused text field makes the game drop the keyboard from every input action map.
 * So a press anywhere in the palette keeps the focus in the box, and the box losing it to anything
 * else closes the palette. The open key is muted with the rest, so the box closes on it itself.
 *
 * The layer takes every click outside the panel, which closes the palette and reaches nothing under
 * it, and it renders into the document's body, above the game's panels, whichever screen mounts it.
 * The game's tools ignore the mouse over any element, but its camera still reads the wheel, so the
 * palette also holds the camera barrier a vanilla modal holds.
 */
export function Palette({ onClose }: PaletteProps): ReactNode {
  useCameraActionBarrier();

  const openBinding = openPaletteAction.useInputBinding();

  const boxRef = useRef<HTMLInputElement>(null);

  // Whether the button held down was pressed on the layer itself, so a press that started in the
  // panel, a text selection dragged out of the box, closes nothing on its release.
  const isPressOnLayer = useRef(false);

  useEffect(() => {
    boxRef.current?.focus();
  }, []);

  const onMouseDown = (event: MouseEvent): void => {
    isPressOnLayer.current = event.target == event.currentTarget;

    // Pressing anything but the box itself would move the focus out of it.
    if (event.target != boxRef.current) {
      event.preventDefault();
    }
  };

  const onMouseUp = (event: MouseEvent): void => {
    if (isPressOnLayer.current && event.target == event.currentTarget) {
      onClose();
    }

    isPressOnLayer.current = false;
  };

  const onKeyDown = (event: KeyboardEvent): void => {
    if (isKey(event, 'escape') || matchesKeyBinding(event, openBinding)) {
      event.preventDefault();

      // A held key repeats, so the open key held down would close the palette it just opened.
      if (!event.repeat) {
        onClose();
      }
    } else if (isKey(event, 'tab')) {
      // Would move the focus out of the box.
      event.preventDefault();
    }
  };

  return createPortal(
    <div className={styles.palette} onMouseDown={onMouseDown} onMouseUp={onMouseUp}>
      <div className={styles.palettePanel}>
        <input
          ref={boxRef}
          type='text'
          className={styles.paletteBox}
          onKeyDown={onKeyDown}
          onBlur={onClose}
        />
      </div>
    </div>,
    document.body
  );
}
