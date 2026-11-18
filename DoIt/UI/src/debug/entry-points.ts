import type { Entity } from 'cs2/bindings';
import { type DependencyList, useEffect } from 'react';

declare global {
  /**
   * Debug entry points for the UI side, called from the gameface eval or from a scenario through
   * the view's script execution. Each is defined while the feature behind it is mounted.
   */
  var doItDebug: DoItDebug | undefined;
}

interface DoItDebug {
  /**
   * Runs an action of the latest results as the palette would; query it first with
   * `engine.trigger('doIt.actions.query', '<text>', <sequence>)`.
   */
  readonly runAction?: (id: string, entity?: Entity) => void;

  /**
   * Holds a subscription to the results or the pinned actions, or releases it, standing in for the
   * palette or the toolbar: C# re-reads them on its tick only while one is held.
   */
  readonly holdBinding?: (name: 'results' | 'pinnedLive', isHeld: boolean) => void;

  /**
   * Opens the palette, defined only while it can open on the screen shown.
   */
  readonly openPalette?: () => void;

  /**
   * Closes the palette, defined only while it can open on the screen shown.
   */
  readonly closePalette?: () => void;
}

/**
 * Defines the Debug entry points `define` returns on `globalThis.doItDebug`, and removes them when
 * the component unmounts or `deps` change; a Release bundle defines none.
 */
export function useDebugEntryPoints(define: () => DoItDebug, deps: DependencyList): void {
  // oxlint-disable-next-line react-hooks/exhaustive-deps - `deps` are the caller's, as for useEffect.
  useEffect(() => (DEBUG ? defineEntryPoints(define()) : undefined), deps);
}

/**
 * Defines the entry points, and gives the function that removes them.
 */
function defineEntryPoints(entries: DoItDebug): () => void {
  globalThis.doItDebug = { ...globalThis.doItDebug, ...entries };

  return () => {
    globalThis.doItDebug = Object.fromEntries(
      Object.entries(globalThis.doItDebug ?? {}).filter(([name]) => !(name in entries))
    );
  };
}
