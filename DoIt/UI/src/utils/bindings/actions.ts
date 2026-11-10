import { trigger, useValue } from 'cs2/api';
import type { Entity } from 'cs2/bindings';
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
  readonly channel: ActionChannel;
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
  readonly name: string;
}

// What C# reads as Entity.Null, a bare invoke: a trigger takes no optional argument.
const nullEntity: Entity = { index: 0, version: 0 };

const results$ = lazyBindValue<ActionResults>(GROUP, 'results', { sequence: 0, rows: [] });

/**
 * Subscribes to the results of the latest query C# answered.
 */
export function useActionResults(): ActionResults {
  return useValue(results$());
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
