using System;
using System.Collections.Generic;
using Game;
using Game.UI.Localization;

namespace DoIt.Actions;

/// <summary>
/// One action as its source describes it: immutable data plus the delegates the registry calls.
/// A bridge builds it from lambdas over reflection handles resolved once at load.
/// </summary>
internal sealed record ActionDescriptor {
  /// <summary>
  /// The key the source keeps stable; the source's segment completes it into an
  /// <see cref="ActionId"/>.
  /// </summary>
  internal required string LocalId { get; init; }

  internal required LocalizedString Label { get; init; }

  /// <summary>
  /// Other words players know the action by (<c>demolish</c> for Bulldozer), searched beside the
  /// label and ranked below it at equal match.
  /// </summary>
  internal IReadOnlyList<LocalizedString> Aliases { get; init; } = [];

  internal LocalizedString? Description { get; init; }

  /// <summary>
  /// The category the row shows; the source's display name when none.
  /// </summary>
  internal LocalizedString? Category { get; init; }

  /// <summary>
  /// An image path the UI loads as is (<c>Media/Game/Icons/Bulldozer.svg</c>), or none.
  /// </summary>
  internal string? Icon { get; init; }

  internal ActionKeyHint? KeyHint { get; init; }

  /// <summary>
  /// The game modes the action exists in, the only thing that hides an action by data: outside
  /// them, search never sees it.
  /// </summary>
  internal GameMode Modes { get; init; } = GameMode.Game;

  /// <summary>
  /// Answers whether the action is enabled, disabled, or hidden right now; always enabled when
  /// none.
  /// </summary>
  internal Func<ActionAvailability>? Availability { get; init; }

  internal ActionSubject Subject { get; init; } = ActionSubject.None;

  /// <summary>
  /// Whether an entity action fired bare takes the current selection; off for an action that must
  /// always pick its entity.
  /// </summary>
  internal bool FillsFromSelection { get; init; } = true;

  /// <summary>
  /// How the action is invoked, or none for an action that only shows and sets its
  /// <see cref="State"/>.
  /// </summary>
  internal ActionChannel? Channel { get; init; }

  /// <summary>
  /// The one live state the action shows, or none.
  /// </summary>
  internal ActionState? State { get; init; }
}

/// <summary>
/// What the player supplies at the moment of use.
/// </summary>
internal enum ActionSubject {
  None,

  Entity
}
