import { trigger, useValue } from 'cs2/api';
import { type Entity, selectedInfo } from 'cs2/bindings';
import type { InputAction } from 'cs2/input';
import { useCallback, useContext } from 'react';
import {
  InputRootContext,
  type RootInputController
} from '../../vanilla-modules/game-ui/common/input-events/input-controller';
import type { ProxyBinding } from './input-action';
import { lazyBindValue } from './lazy-value-binding';

const GROUP = 'doIt.actions';

/**
 * The rows answering one query, see C# `DoIt.Actions.ActionResults`.
 * Drop a result whose `sequence` is older than the latest query sent.
 */
export interface ActionResults {
  readonly sequence: number;
  readonly rows: readonly ActionRow[];
}

/**
 * One action, complete, see C# `DoIt.Actions.ActionRow`.
 */
export interface ActionRow {
  /**
   * The canonical id, opaque to the UI.
   */
  readonly id: string;
  readonly label: string;
  /**
   * The spans of `label` the query matched.
   */
  readonly matches: readonly MatchRange[];
  readonly description: string | null;
  readonly category: string;
  /**
   * An image path, loaded as is.
   */
  readonly icon: string | null;
  /**
   * The current keyboard binding of the action's game or mod input action.
   */
  readonly keyBinding: ProxyBinding | null;
  /**
   * A localized key hint, for an action with no input action.
   */
  readonly keyText: string | null;
  readonly isEnabled: boolean;
  readonly disabledReason: string | null;
  /**
   * Null for an action with a state and no invoke.
   */
  readonly channel: ActionChannel | null;
  readonly state: ActionState | null;
  readonly subject: 'none' | 'entity';
  /**
   * Whether an entity action fired bare takes the current selection.
   */
  readonly fillsFromSelection: boolean;
}

export interface MatchRange {
  readonly start: number;
  readonly length: number;
}

/**
 * The live parts of a pinned action, see C# `DoIt.Actions.ActionLive`.
 */
export interface ActionLive {
  readonly id: string;
  readonly isEnabled: boolean;
  readonly disabledReason: string | null;
  readonly state: ActionState | null;
}

/**
 * An action's one live state, set with {@link setActionState} when `isSettable`.
 */
export type ActionState = ToggleState | NumberState | ChoiceState | TextState;

export interface ToggleState {
  readonly kind: 'toggle';
  readonly value: boolean;
  readonly isSettable: boolean;
}

/**
 * A set outside `min` and `max` is refused in C#.
 */
export interface NumberState {
  readonly kind: 'number';
  readonly value: number;
  readonly min: number | null;
  readonly max: number | null;
  readonly step: number | null;
  readonly isSettable: boolean;
}

/**
 * `value` is the key of the current option; keys are opaque to the UI.
 */
export interface ChoiceState {
  readonly kind: 'choice';
  readonly options: readonly ChoiceOption[];
  readonly value: string;
  readonly isSettable: boolean;
}

export interface ChoiceOption {
  readonly key: string;
  readonly label: string;
}

export interface TextState {
  readonly kind: 'text';
  readonly value: string;
  readonly isSettable: boolean;
}

export type ActionChannel = CSharpCallChannel | TriggerChannel | InputActionChannel;

/**
 * Run in C# through {@link invokeAction}.
 */
export interface CSharpCallChannel {
  readonly kind: 'csharpCall';
}

/**
 * A cohtml trigger fired from the UI side, its fixed arguments first.
 */
export interface TriggerChannel {
  readonly kind: 'trigger';
  readonly group: string;
  readonly name: string;
  readonly args: ReadonlyArray<boolean | number | string>;
}

/**
 * One of the game's named UI input actions, dispatched on its root input stack.
 */
export interface InputActionChannel {
  readonly kind: 'inputAction';
  readonly name: InputAction;
}

// What C# reads as Entity.Null, a bare invoke: a trigger takes no optional argument.
const nullEntity: Entity = { index: 0, version: 0 };

const results$ = lazyBindValue<ActionResults>(GROUP, 'results', { sequence: 0, rows: [] });

const pinnedLive$ = lazyBindValue<readonly ActionLive[]>(GROUP, 'pinnedLive', []);

/**
 * Subscribes to the results of the latest query C# answered.
 * C# re-reads their live parts on a fixed tick while anything subscribes: hold it while the palette
 * is open only.
 */
export function useActionResults(): ActionResults {
  return useValue(results$());
}

/**
 * Reads the results of the latest query once, without holding a subscription, so C# does not tick.
 */
export function readActionResults(): ActionResults {
  return results$().value;
}

/**
 * Subscribes to the live parts of the pinned actions, in pin order, which C# re-reads on a fixed
 * tick while anything subscribes.
 */
export function usePinnedLive(): readonly ActionLive[] {
  return useValue(pinnedLive$());
}

/**
 * Holds a subscription to the results or the pinned actions until the returned function releases
 * it, which keeps C#'s tick running: for the Debug entry points, which render nothing.
 */
export function holdActionBinding(name: 'results' | 'pinnedLive'): () => void {
  const subscription = (name == 'results' ? results$() : pinnedLive$()).subscribe();

  return () => subscription.dispose();
}

/**
 * Searches the actions; C# answers on {@link useActionResults} with the same `sequence`.
 */
export function queryActions(query: string, sequence: number): void {
  trigger(GROUP, 'query', query, sequence);
}

/**
 * Runs a C# call action, on `entity` or on what its rules give it when none.
 * A disabled action is refused in C#.
 */
export function invokeAction(id: string, entity: Entity = nullEntity): void {
  trigger(GROUP, 'invoke', id, entity);
}

/**
 * Sets an action's state, sent as a string whatever its shape, so a wrong type never reaches
 * cohtml. A disabled action, a read-only state, or a value out of its bounds or options is refused
 * in C#.
 */
export function setActionState(id: string, value: boolean | number | string): void {
  trigger(GROUP, 'setState', id, String(value));
}

/**
 * Gives the function that runs an action from its row, whichever side fires it.
 * A C# call goes to C# through {@link invokeAction}. A trigger or an input action fires here, then
 * tells C# with the `invoked` note, which records its use. A disabled row, or one with a state and
 * no invoke, fires nothing.
 *
 * A trigger row taking an entity fires on `entity`, else on the game's selection unless the action
 * opted out of it, read at fire time from the binding the game's info panel reads; with neither, it
 * fires nothing.
 *
 * An input action is dispatched on the game's root input stack, reached through the game's
 * `InputRootContext`: it holds the root controller at any depth, where `useInputController` would
 * attach a controller and only reach the stack on the next rebuild. The game unregisters its
 * shortcut handlers while a modal is up, so an input action fires nothing from inside one.
 */
export function useRunAction(): (row: ActionRow, entity?: Entity) => void {
  const root = useContext(InputRootContext);

  return useCallback((row, entity) => runAction(row, entity, root), [root]);
}

function runAction(row: ActionRow, entity: Entity | undefined, root: RootInputController): void {
  const { channel } = row;

  if (!row.isEnabled || !channel) {
    return;
  }

  switch (channel.kind) {
    case 'csharpCall': {
      invokeAction(row.id, entity);

      return;
    }
    case 'trigger': {
      const args: Array<boolean | number | string | Entity> = [...channel.args];

      if (row.subject == 'entity') {
        const subject =
          entity ?? (row.fillsFromSelection ? selectedInfo.selectedEntity$.value : undefined);

        if (subject == null || subject.index == nullEntity.index) {
          return;
        }

        args.push(subject);
      }

      trigger(channel.group, channel.name, ...args);

      break;
    }
    case 'inputAction': {
      // False when no handler took it: the game registers none for a shortcut that cannot run now,
      // `Pause Simulation` while paused for one.
      if (!root.stack?.dispatchInputEvent(channel.name, null)) {
        return;
      }

      break;
    }
    default: {
      const unknown: never = channel;

      throw new Error(`Unknown action channel ${JSON.stringify(unknown)}.`);
    }
  }

  trigger(GROUP, 'invoked', row.id);
}
