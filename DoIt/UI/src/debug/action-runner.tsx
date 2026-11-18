import type { ModRegistrar } from 'cs2/modding';
import { type ReactNode, useEffect } from 'react';
import { holdActionBinding, readActionResults, useRunAction } from '../utils/bindings';
import { useDebugEntryPoints } from './entry-points';

/**
 * Debug entry point: mounts {@link ActionRunner} in the game's view, inside its input stack
 * provider, where the input actions dispatch.
 */
export const registerActionRunner: ModRegistrar = moduleRegistry => {
  moduleRegistry.append('Game', ActionRunner);
};

/**
 * The subscriptions `holdBinding` holds, by binding name, for the one runner the city's slot
 * mounts.
 */
const heldBindings = new Map<string, () => void>();

// oxlint-disable-next-line react/only-export-components - the game's UI has no fast refresh
function ActionRunner(): ReactNode {
  const runAction = useRunAction();

  useDebugEntryPoints(
    () => ({
      // [PERF] Reads the results at call time, in place of a subscription, which would keep C#
      // ticking.
      runAction(id, entity): void {
        const row = readActionResults().rows.find(candidate => candidate.id == id);

        if (!row) {
          throw new Error(`Action "${id}" is not in the latest results, query it first.`);
        }

        runAction(row, entity);
      },
      holdBinding(name, isHeld): void {
        heldBindings.get(name)?.();
        heldBindings.delete(name);

        if (isHeld) {
          heldBindings.set(name, holdActionBinding(name));
        }
      }
    }),
    [runAction]
  );

  // Releases the subscriptions with the runner, which leaves with the city.
  useEffect(
    () => () => {
      for (const release of heldBindings.values()) {
        release();
      }

      heldBindings.clear();
    },
    []
  );

  return null;
}
