using Game.UI.Localization;

namespace DoIt.Actions;

/// <summary>
/// An availability reader's answer.
/// Hidden is for an action that does not exist in this context: its row leaves the list.
/// Disabled is for one that exists but cannot run now: its row stays findable, greyed with its
/// reason, and it is never fired.
/// Bridge authors follow this convention.
/// </summary>
internal readonly record struct ActionAvailability(
  ActionAvailabilityKind Kind,
  LocalizedString? Reason = null
) {
  internal static ActionAvailability Enabled => new(ActionAvailabilityKind.Enabled);

  internal static ActionAvailability Hidden => new(ActionAvailabilityKind.Hidden);

  internal static ActionAvailability Disabled(LocalizedString? reason = null) =>
    new(ActionAvailabilityKind.Disabled, reason);
}

internal enum ActionAvailabilityKind {
  Enabled,

  Disabled,

  Hidden
}
