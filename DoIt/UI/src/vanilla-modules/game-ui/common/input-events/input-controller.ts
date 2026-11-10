import type { InputController, InputStack } from 'cs2/input';
import { type Context, createContext } from 'react';
import { getModuleExport } from '../../../../utils/vanilla-modules';

/**
 * The game's root input controller, as its input stack provider hands it to every component below
 * it, at any depth. Unlike `InputContext`, which holds the nearest controller, this one always
 * holds the root, and reading it attaches no controller of one's own.
 * Outside the provider it holds the game's placeholder controller, which has no `stack`.
 */
export const InputRootContext = getModuleExport<Context<RootInputController>>(
  'game-ui/common/input-events/input-controller.ts',
  'InputRootContext',
  (value): value is Context<RootInputController> =>
    value != null && typeof value == 'object' && 'Provider' in value,
  createContext<RootInputController>({})
);

/**
 * The game's `RootInputControllerImpl`, whose `stack` the `cs2/input` typings declare private, yet
 * is a plain property: the one input stack every UI input action is dispatched on.
 */
export interface RootInputController extends Partial<InputController> {
  readonly stack?: InputStack;
}
