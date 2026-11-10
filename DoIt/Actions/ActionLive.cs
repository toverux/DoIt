using System.Collections.Generic;

namespace DoIt.Actions;

/// <summary>
/// The live parts of an action, read afresh from its source: what a pinned button shows.
/// A hidden action has none.
/// </summary>
internal sealed record ActionLive(
  string Id,
  bool IsEnabled,
  string? DisabledReason,
  ActionStateValue? State
);

/// <summary>
/// A state as read for the UI: its shape, its current value, and whether the player can set it.
/// </summary>
internal abstract record ActionStateValue {
  internal required bool IsSettable { get; init; }
}

internal sealed record ToggleValue(bool Value) : ActionStateValue;

internal sealed record NumberValue(double Value, double? Min, double? Max, double? Step)
  : ActionStateValue;

/// <summary>
/// The current key among the options, their labels resolved in the current locale.
/// The options list is the catalogue's own, so two reads of an unchanged choice compare equal.
/// </summary>
internal sealed record ChoiceValue(IReadOnlyList<ChoiceOption> Options, string Key)
  : ActionStateValue;

internal sealed record ChoiceOption(string Key, string Label);

internal sealed record TextValue(string Value) : ActionStateValue;
