import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, renderHook } from '@testing-library/react';
import { getTriggers, resetBindings, setBinding } from '../../testing/game-setup';
import { type ActionResults, invokeAction, queryActions, useActionResults } from './actions';

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

/**
 * The triggers of the actions group, leaving out those another test file's components fire late,
 * such as their sounds.
 */
function actionTriggers(): ReturnType<typeof getTriggers> {
  return getTriggers().filter(recorded => recorded.event.startsWith('doIt.actions.'));
}
