import { trigger, useValue } from 'cs2/api';
import type { UISound } from 'cs2/ui';
import { lazyBindValue } from './lazy-value-binding';

// The game's own groups, not the mod's, which is why they live in their own module rather than in
// `common`.
const AUDIO_GROUP = 'audio';

const INPUT_GROUP = 'input';

const OVERLAY_GROUP = 'overlay';

/**
 * The game's `ControlScheme`: `0` is keyboard and mouse, `1` a gamepad.
 * Bound here and read as a number: `cs2/input` declares the enum in its types, but exports neither
 * it nor its binding at runtime.
 */
const controlScheme$ = lazyBindValue<number>(INPUT_GROUP, 'controlScheme', 0);

/**
 * The game's `OverlayScreen`, the screen drawn over every other: `0` is none, the others a splash,
 * a disclaimer, a wait, or a load. Bound here, as the game's module exporting it throws on access.
 */
const overlayScreen$ = lazyBindValue<number>(OVERLAY_GROUP, 'activeScreen', 0);

/**
 * Plays a Vanilla sound.
 */
export function playSound(sound: `${UISound}`, volume = 1): void {
  trigger(AUDIO_GROUP, 'playSound', sound, volume);
}

/**
 * Whether the player is on keyboard and mouse, not on a gamepad.
 */
export function useIsKeyboardAndMouse(): boolean {
  return useValue(controlScheme$()) == 0;
}

/**
 * Whether the game draws an overlay over the screen, a load above all: the game mode already reads
 * as the load's destination while it runs.
 */
export function useIsOverlayShown(): boolean {
  return useValue(overlayScreen$()) != 0;
}
