import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, renderHook } from '@testing-library/react';
import { InputStack } from 'cs2/input';
import { createElement, type ReactNode } from 'react';
import { getTriggers, resetBindings, setBinding } from '../../testing/game-setup';
import { InputRootContext } from '../../vanilla-modules/game-ui/common/input-events/input-controller';
import {
  type ActionResults,
  type ActionRow,
  invokeAction,
  queryActions,
  useActionResults,
  useRunAction
} from './actions';

afterEach(() => {
  cleanup();
  resetBindings();
});

describe('the actions bindings', () => {
  it(`sends a query with its sequence number`, () => {
    const sequence = 3;

    queryActions('bulld', sequence);

    expect(actionTriggers()).toEqual([{ event: 'doIt.actions.query', args: ['bulld', sequence] }]);
  });

  it(`invokes an action bare with the null entity`, () => {
    invokeAction('game/Bulldoze Tool');

    expect(actionTriggers()).toEqual([
      { event: 'doIt.actions.invoke', args: ['game/Bulldoze Tool', { index: 0, version: 0 }] }
    ]);
  });

  it(`invokes an action on an explicit entity`, () => {
    invokeAction('test/Follow', { index: 9, version: 2 });

    expect(actionTriggers()).toEqual([
      { event: 'doIt.actions.invoke', args: ['test/Follow', { index: 9, version: 2 }] }
    ]);
  });

  it(`reads the results C# wrote`, () => {
    const results: ActionResults = { sequence: 3, rows: [] };

    setBinding('doIt.actions', 'results', results);

    expect(renderHook(() => useActionResults()).result.current).toEqual(results);
  });
});

describe('running an action from its row', () => {
  const economyPanel = row({
    id: 'game/Game.UI.InGame.EconomyPanel',
    channel: {
      kind: 'trigger',
      group: 'game',
      name: 'togglePanel',
      args: ['Game.UI.InGame.EconomyPanel']
    }
  });

  const pause = row({
    id: 'game/Pause Simulation',
    channel: { kind: 'inputAction', name: 'Pause Simulation' }
  });

  const follow = row({
    id: 'test/Follow',
    subject: 'entity',
    channel: { kind: 'trigger', group: 'camera', name: 'focusEntity', args: [] }
  });

  it(`fires a trigger row's path with its fixed arguments, then the invoked note`, () => {
    runAction(economyPanel);

    expect(firedTriggers()).toEqual([
      { event: 'game.togglePanel', args: ['Game.UI.InGame.EconomyPanel'] },
      { event: 'doIt.actions.invoked', args: ['game/Game.UI.InGame.EconomyPanel'] }
    ]);
  });

  it(`fills a bare entity trigger row from the game's selection`, () => {
    setBinding('selectedInfo', 'selectedEntity', { index: 5, version: 1 });

    runAction(follow);

    expect(firedTriggers()).toEqual([
      { event: 'camera.focusEntity', args: [{ index: 5, version: 1 }] },
      { event: 'doIt.actions.invoked', args: ['test/Follow'] }
    ]);
  });

  it(`passes an explicit entity over the selection`, () => {
    setBinding('selectedInfo', 'selectedEntity', { index: 5, version: 1 });

    runAction(follow, { index: 9, version: 2 });

    expect(firedTriggers()[0]).toEqual({
      event: 'camera.focusEntity',
      args: [{ index: 9, version: 2 }]
    });
  });

  it(`fires nothing for a bare entity row with nothing to take`, () => {
    setBinding('selectedInfo', 'selectedEntity', { index: 0, version: 0 });

    runAction(follow);
    runAction({ ...follow, fillsFromSelection: false });

    expect(firedTriggers()).toEqual([]);
  });

  it(`dispatches an input action row on the root input stack, then the invoked note`, () => {
    const dispatched: string[] = [];

    runAction(pause, undefined, stack => {
      stack.push('Pause Simulation', 'test', () => dispatched.push('Pause Simulation'));
    });

    expect(dispatched).toEqual(['Pause Simulation']);
    expect(firedTriggers()).toEqual([
      { event: 'doIt.actions.invoked', args: ['game/Pause Simulation'] }
    ]);
  });

  it(`sends no note for an input action no handler took`, () => {
    runAction(pause);

    expect(firedTriggers()).toEqual([]);
  });

  it(`invokes a C# call row in C#, which needs no note`, () => {
    runAction(row({ id: 'game/Bulldoze Tool', channel: { kind: 'csharpCall' } }));

    expect(firedTriggers()).toEqual([
      { event: 'doIt.actions.invoke', args: ['game/Bulldoze Tool', { index: 0, version: 0 }] }
    ]);
  });

  it(`fires nothing for a disabled row`, () => {
    const dispatched: string[] = [];

    for (const disabled of [economyPanel, pause, row({ channel: { kind: 'csharpCall' } })]) {
      runAction({ ...disabled, isEnabled: false }, undefined, stack => {
        stack.push('Pause Simulation', 'test', () => dispatched.push('Pause Simulation'));
      });
    }

    expect(dispatched).toEqual([]);
    expect(firedTriggers()).toEqual([]);
  });
});

/**
 * Runs `actionRow` through {@link useRunAction} under a root input controller whose stack holds
 * what `fillStack` pushes.
 */
function runAction(
  actionRow: ActionRow,
  entity?: Parameters<ReturnType<typeof useRunAction>>[1],
  fillStack?: (stack: InputStack) => void
): void {
  const stack = new InputStack();

  fillStack?.(stack);

  const wrapper = ({ children }: { readonly children: ReactNode }): ReactNode =>
    createElement(InputRootContext.Provider, { value: { stack } }, children);

  renderHook(() => useRunAction(), { wrapper }).result.current(actionRow, entity);
}

function row(overrides: Partial<ActionRow> & Pick<ActionRow, 'channel'>): ActionRow {
  return {
    id: 'test/Action',
    label: 'Action',
    matches: [],
    description: null,
    category: 'Test',
    icon: null,
    keyBinding: null,
    keyText: null,
    isEnabled: true,
    disabledReason: null,
    subject: 'none',
    fillsFromSelection: true,
    ...overrides
  };
}

/**
 * The triggers the tests fire, leaving out those another test file's components fire late, such as
 * their sounds.
 */
function firedTriggers(): ReturnType<typeof getTriggers> {
  return getTriggers().filter(recorded => !recorded.event.startsWith('audio.'));
}

/**
 * The triggers of the actions group, leaving out those another test file's components fire late,
 * such as their sounds.
 */
function actionTriggers(): ReturnType<typeof getTriggers> {
  return getTriggers().filter(recorded => recorded.event.startsWith('doIt.actions.'));
}
