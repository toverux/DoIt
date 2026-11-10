using System.Collections.Generic;
using DoIt.Actions;

namespace DoIt.Tests.Actions;

/// <summary>
/// A fake fuzzy matcher: a text matches when it holds the term's characters in order, each taken at
/// its first place after the previous one.
/// A tighter match scores higher: the score is minus the span from the first matched character to
/// the last.
/// </summary>
internal sealed class SubsequenceMatcher : IActionMatcher {
  public ActionMatch? Match(string term, string text) {
    var ranges = new List<MatchRange>();
    var from = 0;

    foreach (var character in term) {
      var at = text.IndexOf(character, from);

      if (at < 0) {
        return null;
      }

      if (ranges.Count > 0 && ranges[^1].Start + ranges[^1].Length == at) {
        ranges[^1] = ranges[^1] with {
          Length = ranges[^1].Length + 1
        };
      }
      else {
        ranges.Add(new MatchRange(at, 1));
      }

      from = at + 1;
    }

    return new ActionMatch(ranges[0].Start - from, ranges);
  }
}
