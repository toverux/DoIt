using System;
using System.Collections.Generic;
using Game.UI.Localization;

namespace DoIt.Actions;

/// <summary>
/// <para>
/// The one live state an action shows and the player changes in place, read from the source each
/// time a row is written and never stored, so Do It needs no rule for a mod's coupled states to
/// display right.
/// </para>
/// <para>
/// Each shape has a reader; a state with no setter is read-only.
/// A throw in a reader or a setter disables the action, as a throw in its availability reader does.
/// </para>
/// </summary>
internal abstract record ActionState {
  internal abstract bool IsSettable { get; }
}

internal sealed record ToggleState : ActionState {
  internal required Func<bool> Read { get; init; }

  internal Action<bool>? Set { get; init; }

  internal override bool IsSettable => this.Set is not null;
}

/// <summary>
/// A number, with optional bounds and step the UI's control follows; a set outside the bounds is
/// refused.
/// </summary>
internal sealed record NumberState : ActionState {
  internal required Func<double> Read { get; init; }

  internal Action<double>? Set { get; init; }

  internal override bool IsSettable => this.Set is not null;

  internal double? Min { get; init; }

  internal double? Max { get; init; }

  internal double? Step { get; init; }
}

/// <summary>
/// One of a fixed list of labelled options, keyed by strings opaque to Do It, so a registrant
/// passes its modes and sub-actions through; a set of a key not in the list is refused.
/// A source whose options move raises <see cref="IActionSource.Changed"/>.
/// </summary>
internal sealed record ChoiceState : ActionState {
  internal required IReadOnlyList<ActionChoice> Options { get; init; }

  /// <summary>
  /// Reads the key of the current option.
  /// </summary>
  internal required Func<string> Read { get; init; }

  internal Action<string>? Set { get; init; }

  internal override bool IsSettable => this.Set is not null;
}

internal sealed record ActionChoice(string Key, LocalizedString Label);

internal sealed record TextState : ActionState {
  internal required Func<string> Read { get; init; }

  internal Action<string>? Set { get; init; }

  internal override bool IsSettable => this.Set is not null;
}
