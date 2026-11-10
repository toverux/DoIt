using System.Collections.Generic;

namespace DoIt.Actions;

/// <summary>
/// Scores one term of a query against one text, the registry's search seam: both come folded by
/// <see cref="TextFold"/>, the term never empty and holding no space.
/// The registry reads the tier of a match from its ranges and scores every field of an action
/// through it, so a matcher knows nothing of fields, tiers, or use.
/// </summary>
internal interface IActionMatcher {
  /// <returns>
  /// The match, or <c>null</c> when the text does not match.
  /// </returns>
  ActionMatch? Match(string term, string text);
}

/// <summary>
/// A higher <see cref="Score"/> ranks first among matches of the same tier and use; the
/// <see cref="Ranges"/> are the matched spans of the text, in order, adjacent spans merged into
/// one.
/// </summary>
internal sealed record ActionMatch(int Score, IReadOnlyList<MatchRange> Ranges);

internal readonly record struct MatchRange(int Start, int Length);
