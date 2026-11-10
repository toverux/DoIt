namespace DoIt.Actions;

/// <summary>
/// What became of a set, for the Debug entry points and the tests: the UI reads the outcome off the
/// row, never from the set.
/// </summary>
internal enum ActionSetOutcome {
  Set,

  /// <summary>
  /// No action has the id in the current game mode.
  /// </summary>
  Unknown,

  /// <summary>
  /// The action has no state, or a state with no setter.
  /// </summary>
  ReadOnly,

  /// <summary>
  /// The action read disabled or hidden, so its setter was not called.
  /// </summary>
  Refused,

  /// <summary>
  /// The value does not parse for the state's shape, or falls outside its bounds or options.
  /// </summary>
  Invalid,

  /// <summary>
  /// The setter threw, which disabled the action.
  /// </summary>
  Failed
}
