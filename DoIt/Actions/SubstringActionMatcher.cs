using System;

namespace DoIt.Actions;

/// <summary>
/// The first matcher: a text matches when it holds the term.
/// A match at the text's start ranks above one at a word's start, which ranks above one inside a
/// word, and an earlier match ranks above a later one.
/// The registry reads tiers from the ranges, so this score orders what the tiers leave level: typo
/// hits, and a multi-term row's stronger terms.
/// </summary>
internal sealed class SubstringActionMatcher : IActionMatcher {
  private const int PrefixScore = 3000;

  private const int WordStartScore = 2000;

  private const int InsideScore = 1000;

  public ActionMatch? Match(string term, string text) {
    // Ordinal, as a culture-aware comparison differs between the game's Mono and .NET; both sides
    // come folded, case included.
    var start = text.IndexOf(term, StringComparison.Ordinal);

    if (start < 0) {
      return null;
    }

    var tier = start is 0
      ? SubstringActionMatcher.PrefixScore
      : char.IsLetterOrDigit(text[start - 1])
        ? SubstringActionMatcher.InsideScore
        : SubstringActionMatcher.WordStartScore;

    // The position only orders matches within a tier.
    return new ActionMatch(
      tier - Math.Min(start, SubstringActionMatcher.InsideScore - 1),
      [new MatchRange(start, term.Length)]
    );
  }
}
