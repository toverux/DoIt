import type { ModRegistrar } from 'cs2/modding';
import { CityPaletteMount, ScreenPaletteMount } from './palette-mount';

/**
 * Mounts the palette on the three screens it opens on, each through the screen's own slot: a city,
 * the editor and the main menu.
 */
export const registerPalette: ModRegistrar = moduleRegistry => {
  moduleRegistry.append('Game', CityPaletteMount);
  moduleRegistry.append('Editor', ScreenPaletteMount);
  moduleRegistry.append('Menu', ScreenPaletteMount);
};
