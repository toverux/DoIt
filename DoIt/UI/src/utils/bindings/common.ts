import { trigger, useValue } from 'cs2/api';
import { lazyBindValue } from './lazy-value-binding';

const GROUP = 'doIt.common';

/**
 * The settings the UI reads, see C# `DoIt.Settings` for documentation.
 */
// oxlint-disable-next-line typescript/no-empty-interface, typescript/no-empty-object-type - the next setting the UI reads goes here.
export interface ModSettings {}

const settings$ = lazyBindValue<ModSettings>(GROUP, 'settings', {});

export function useModSettings(): ModSettings {
  return useValue(settings$());
}

const locale$ = lazyBindValue<string>(GROUP, 'locale', 'en-US');

/**
 * Subscribes to the game's current locale (e.g. `en-US`).
 */
export function useLocale(): string {
  return useValue(locale$());
}

/**
 * Opens the mod's settings page at the given tab, e.g. `General`.
 */
export function openModSettings(tab: string): void {
  trigger(GROUP, 'openModSettings', tab);
}

/**
 * Opens a web page in the player's default browser.
 */
export function openWebPage(url: string): void {
  trigger(GROUP, 'openWebPage', url);
}

/**
 * Logs a JavaScript error to the mod's C# logs (not only the UI console), and shows an error dialog
 * when {@link fatal} is set.
 */
export function logJavaScriptError(fatal: boolean, errorString: string | undefined): void {
  trigger(GROUP, 'logJavaScriptError', fatal, errorString);
}
