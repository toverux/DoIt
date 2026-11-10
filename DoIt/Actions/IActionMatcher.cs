using System.Collections.Generic;

namespace DoIt.Actions;

/// <summary>
/// Scores a label against a query, the registry's search seam.
/// </summary>
internal interface IActionMatcher {
  /// <returns>
  /// The match, or <c>null</c> when the label does not match.
  /// </returns>
  ActionMatch? Match(string query, string label);
}

/// <summary>
/// A higher <see cref="Score"/> ranks first; <see cref="Ranges"/> are the matched spans of the
/// label, which the UI highlights.
/// </summary>
internal sealed record ActionMatch(int Score, IReadOnlyList<MatchRange> Ranges);

internal readonly record struct MatchRange(int Start, int Length);
