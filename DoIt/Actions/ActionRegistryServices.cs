using System;
using Colossal.UI.Binding;
using DoIt.Logging;
using Game;
using Game.UI.Localization;
using Unity.Entities;

namespace DoIt.Actions;

/// <summary>
/// The collaborators an <see cref="ActionRegistry"/> reads the running game through, so the
/// registry itself holds no engine dependency.
/// </summary>
internal sealed class ActionRegistryServices {
  internal required IModLog Log { get; init; }

  /// <summary>
  /// Resolves a label, a description, a category, or a reason to the text the player reads.
  /// </summary>
  internal required Func<LocalizedString, string> ResolveText { get; init; }

  /// <summary>
  /// Reads the current keyboard binding of a <c>ProxyAction</c> by map and name, written to the row
  /// as is, or <c>null</c> when the action or its binding does not exist.
  /// </summary>
  internal required Func<string, string, IJsonWritable?> ReadKeyBinding { get; init; }

  /// <summary>
  /// The selected entity, <see cref="Entity.Null"/> when none.
  /// </summary>
  internal required Func<Entity> ReadSelection { get; init; }

  internal required Func<GameMode> ReadGameMode { get; init; }

  internal required Func<DateTime> Now { get; init; }

  internal required IActionUseStore UseStore { get; init; }

  internal IActionMatcher Matcher { get; init; } = new SubstringActionMatcher();
}
