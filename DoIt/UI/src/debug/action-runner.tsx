import type { Entity } from 'cs2/bindings';
import type { ModRegistrar } from 'cs2/modding';
import { type ReactNode, useEffect } from 'react';
import { useActionResults, useRunAction } from '../utils/bindings';

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
  const { rows } = useActionResults();

  useEffect(() => {
    globalThis.doItDebug = {
      runAction(id, entity): void {
        const row = rows.find(candidate => candidate.id == id);

        if (!row) {
          throw new Error(`Action "${id}" is not in the latest results, query it first.`);
        }

        runAction(row, entity);
      }
    };

    return () => {
      globalThis.doItDebug = undefined;
    };
  }, [rows, runAction]);

  return null;
}
