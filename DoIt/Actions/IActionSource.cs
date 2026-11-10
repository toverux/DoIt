using System;
using System.Collections.Generic;
using Game.UI.Localization;

namespace DoIt.Actions;

/// <summary>
/// A source of actions: the game, a bridge into a mod, or the registration source.
/// The registry rebuilds its whole index from every source when one raises <see cref="Changed"/>.
/// Every call into a source is foreign code, wrapped by the registry: a throw while listing marks
/// the source down, a throw in one action's delegate disables that action alone.
/// </summary>
internal interface IActionSource {
  ActionSourceInfo Info { get; }

  /// <summary>
  /// Raised when the set of actions <see cref="List"/> returns moves.
  /// </summary>
  event Action? Changed;

  IEnumerable<ActionDescriptor> List();
}

/// <summary>
/// Who stands behind a source: its id segment, the name the palette shows as its actions' default
/// category, and the mod name and assembly version a failure is logged with.
/// </summary>
internal sealed record ActionSourceInfo {
  internal ActionSourceInfo(
    string segment,
    LocalizedString displayName,
    string modName,
    string modVersion
  ) {
    // Validates the segment the same way an id does.
    _ = new ActionId(segment, "_");

    this.Segment = segment;
    this.DisplayName = displayName;
    this.ModName = modName;
    this.ModVersion = modVersion;
  }

  internal string Segment { get; }

  internal LocalizedString DisplayName { get; }

  internal string ModName { get; }

  internal string ModVersion { get; }
}
