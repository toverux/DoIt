namespace DoIt.Actions;

/// <summary>
/// What became of an invoke, for the Debug entry points and the tests: the UI reads the outcome off
/// the row, never from the invoke.
/// </summary>
internal enum ActionInvokeOutcome {
  Invoked,

  /// <summary>
  /// No action has the id in the current game mode.
  /// </summary>
  Unknown,

  /// <summary>
  /// The action fires from the UI side.
  /// </summary>
  NotACSharpCall,

  /// <summary>
  /// The action read disabled or hidden, so its delegate was not called.
  /// </summary>
  Refused,

  /// <summary>
  /// The delegate threw, which disabled the action.
  /// </summary>
  Failed
}
