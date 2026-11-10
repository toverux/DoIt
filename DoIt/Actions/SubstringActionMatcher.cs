using System;

namespace DoIt.Actions;

/// <summary>
/// The first matcher: a label matches when it holds the query, case-folded.
/// A match at the label's start ranks above one at a word's start, which ranks above one inside a
/// word, and an earlier match ranks above a later one.
/// An empty query matches every label, unranked.
/// </summary>
internal sealed class SubstringActionMatcher : IActionMatcher {
  private const int PrefixScore = 3000;

  private const int WordStartScore = 2000;

  private const int InsideScore = 1000;

  public ActionMatch? Match(string query, string label) {
    query = query.Trim();

    if (query.Length is 0) {
      return new ActionMatch(0, []);
    }

    // Ordinal, as a culture-aware comparison differs between the game's Mono and .NET.
    var start = label.IndexOf(query, StringComparison.OrdinalIgnoreCase);

    if (start < 0) {
      return null;
    }

    var tier = start is 0
      ? SubstringActionMatcher.PrefixScore
      : char.IsLetterOrDigit(label[start - 1])
        ? SubstringActionMatcher.InsideScore
        : SubstringActionMatcher.WordStartScore;

    // The position only orders matches within a tier.
    return new ActionMatch(
      tier - Math.Min(start, SubstringActionMatcher.InsideScore - 1),
      [new MatchRange(start, query.Length)]
    );
  }
}
