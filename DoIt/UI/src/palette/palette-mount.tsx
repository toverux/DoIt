import { useValue } from 'cs2/api';
import { game } from 'cs2/bindings';
import { type ReactNode, useCallback, useState } from 'react';
import { useDebugEntryPoints } from '../debug/entry-points';
import { openPaletteAction, useIsKeyboardAndMouse, useIsOverlayShown } from '../utils/bindings';
import { Palette } from './palette';

/**
 * The palette's mount in the city's slot, which stays mounted under the pause menu and the screens
 * it opens, only hidden: the palette opens on the city's main screen alone.
 */
export function CityPaletteMount(): ReactNode {
  const isMainScreen = useValue(game.activeGameScreen$) == game.GameScreen.main;

  return <PaletteMount isScreenShown={isMainScreen} />;
}

/**
 * The palette's mount in the editor's or the main menu's slot, which is unmounted with its screen,
 * whatever replaces it.
 */
export function ScreenPaletteMount(): ReactNode {
  return <PaletteMount isScreenShown={true} />;
}

interface PaletteMountProps {
  readonly isScreenShown: boolean;
}

/**
 * Opens and closes the palette on one screen, where it can open: the screen shown, no load or other
 * overlay over it, and the player on keyboard and mouse. The open key is live there only.
 */
function PaletteMount({ isScreenShown }: PaletteMountProps): ReactNode {
  const isOverlayShown = useIsOverlayShown();

  const isKeyboardAndMouse = useIsKeyboardAndMouse();

  const canOpen = isScreenShown && !isOverlayShown && isKeyboardAndMouse;

  const [isOpen, setIsOpen] = useState(false);

  // Closed with the gate, so the palette does not open again by itself when the gate opens.
  if (isOpen && !canOpen) {
    setIsOpen(false);
  }

  const open = useCallback(() => setIsOpen(true), []);

  const close = useCallback(() => setIsOpen(false), []);

  useDebugEntryPoints(
    () => (canOpen ? { openPalette: open, closePalette: close } : {}),
    [canOpen, open, close]
  );

  return (
    canOpen && (
      <>
        <OpenKeyListener onPress={open} />
        {isOpen && <Palette onClose={close} />}
      </>
    )
  );
}

interface OpenKeyListenerProps {
  readonly onPress: () => void;
}

/**
 * Calls `onPress` on each press of the open key.
 * Subscribing to the key's phase is what enables its action, so it is mounted only where the
 * palette can open. While the palette is open, its box mutes the key with every other, and closes
 * on it itself.
 */
function OpenKeyListener({ onPress }: OpenKeyListenerProps): null {
  openPaletteAction.useInputPress(onPress);

  return null;
}
