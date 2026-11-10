import type { Entity } from 'cs2/bindings';
import type { ModRegistrar } from 'cs2/modding';
import { type ReactNode, useEffect } from 'react';
import { holdActionBinding, readActionResults, useRunAction } from '../utils/bindings';

declare global {
  /**
   * Debug entry points for the UI side, called from the gameface eval or from a scenario through
   * the view's script execution.
   */
  var doItDebug: DoItDebug | undefined;
}

interface DoItDebug {
  /**
   * Runs an action of the latest results as the palette would; query it first with
   * `engine.trigger('doIt.actions.query', '<text>', <sequence>)`.
   */
  readonly runAction: (id: string, entity?: Entity) => void;
  /**
   * Holds a subscription to the results or the pinned actions, or releases it, standing in for the
   * palette or the toolbar: C# re-reads them on its tick only while one is held.
   */
  readonly holdBinding: (name: 'results' | 'pinnedLive', isHeld: boolean) => void;
}

/**
 * Debug entry point: mounts {@link ActionRunner} in the game's view, inside its input stack
 * provider, where the input actions dispatch.
 */
export const registerActionRunner: ModRegistrar = moduleRegistry => {
  moduleRegistry.append('Game', ActionRunner);
};

// oxlint-disable-next-line react/only-export-components - the game's UI has no fast refresh
function ActionRunner(): ReactNode {
  const runAction = useRunAction();

  useEffect(() => {
    // The subscriptions held, by binding name, released with the runner.
    const held = new Map<string, () => void>();

    globalThis.doItDebug = {
      // Reads the results at call time rather than subscribing, which would keep C# ticking.
      runAction(id, entity): void {
        const row = readActionResults().rows.find(candidate => candidate.id == id);

        if (!row) {
          throw new Error(`Action "${id}" is not in the latest results, query it first.`);
        }

        runAction(row, entity);
      },
      holdBinding(name, isHeld): void {
        held.get(name)?.();
        held.delete(name);

        if (isHeld) {
          held.set(name, holdActionBinding(name));
        }
      }
    };

    return () => {
      for (const release of held.values()) {
        release();
      }

      globalThis.doItDebug = undefined;
    };
  }, [runAction]);

  return null;
}
