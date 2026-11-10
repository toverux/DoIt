using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DoIt.Actions;

/// <summary>
/// The ranking pipeline of the registry's search, around the matcher seam: the query is folded and
/// split into terms, each term scored on its own through the matcher against every field of an
/// action and placed in its best one, every term required.
/// A row is placed by its weakest term's field group, then tier, then field, then by frecency, the
/// matcher's score breaking a frecency tie alone.
/// </summary>
internal static class ActionSearch {
  /// <summary>
  /// The shortest term the typo pass retries: shorter, a dropped character leaves too little to
  /// tell a typo from a guess, and a short query stays sharp.
  /// </summary>
  private const int TypoTermMinLength = 4;

  /// <summary>
  /// The time over which a use counts half as much in <see cref="Frecency"/>, so a habit fades
  /// within weeks of being dropped.
  /// </summary>
  private static readonly TimeSpan HalfLife = TimeSpan.FromDays(14);

  /// <summary>
  /// How much an action was used, recently and often: its use count, halved for each
  /// <see cref="HalfLife"/> since its last use.
  /// </summary>
  internal static double Frecency(ActionUse use, DateTime now) {
    // A clock set back does not lift a use above its count.
    var age = Math.Max(0, (now - use.LastUse).TotalDays);

    return use.Count * Math.Pow(0.5, age / ActionSearch.HalfLife.TotalDays);
  }

  /// <summary>
  /// The folded query split on spaces, the empty terms left out.
  /// </summary>
  internal static string[] Terms(string query) =>
    TextFold.Fold(query).Text.Split([' '], StringSplitOptions.RemoveEmptyEntries);

  /// <summary>
  /// The actions every term hits, unranked.
  /// A term that hits no field of an action is retried with one character dropped, which a wrong,
  /// an extra, and a swapped character all reduce to, when the clean pass found fewer actions than
  /// <paramref name="rowCap"/> and the term is long enough; its hit then ranks in the typo tier.
  /// </summary>
  internal static List<SearchMatch> Find(
    IReadOnlyList<string> terms,
    IEnumerable<IndexedAction> actions,
    int rowCap,
    IActionMatcher matcher
  ) {
    var matches = new List<SearchMatch>();

    // The actions the typo pass may still find, with the hits of their other terms.
    var misses = new List<(IndexedAction Action, TermHit?[] Hits)>();

    // Hot path, once per action: one buffer for every action, copied for those kept, in place of an
    // array per action.
    var hits = new TermHit?[terms.Count];

    foreach (var action in actions) {
      var isWhole = true;
      var isRetryable = true;

      for (var i = 0; i < terms.Count && isRetryable; i++) {
        hits[i] = ActionSearch.BestHit(terms[i], action.Fields, matcher, isTypo: false);

        if (hits[i] is null) {
          isWhole = false;

          // Hot path, once per missed action: a miss past the cap is dropped, in place of kept for
          // a typo pass that no longer runs.
          isRetryable = matches.Count < rowCap && terms[i].Length >= ActionSearch.TypoTermMinLength;
        }
      }

      if (isWhole) {
        matches.Add(new SearchMatch(action, ActionSearch.Whole(hits)));
      }
      else if (isRetryable) {
        misses.Add((action, [.. hits]));
      }
    }

    if (matches.Count >= rowCap) {
      return matches;
    }

    // Each term's variants, made on its first retry.
    var variants = new List<string>?[terms.Count];

    foreach (var (action, termHits) in misses) {
      var isFound = true;

      for (var i = 0; i < terms.Count && isFound; i++) {
        if (termHits[i] is null) {
          var termVariants = variants[i] ??= ActionSearch.Variants(terms[i]);

          termHits[i] = ActionSearch.BestTypoHit(termVariants, action.Fields, matcher);

          isFound = termHits[i] is not null;
        }
      }

      if (isFound) {
        matches.Add(new SearchMatch(action, ActionSearch.Whole(termHits)));
      }
    }

    return matches;
  }

  /// <summary>
  /// The fields of an action its match hit, in field order, with their ranges in the original text,
  /// and the alias among them.
  /// The row shows one alias, the first one a term hit: a hit on another alias is left out.
  /// </summary>
  internal static (IReadOnlyList<ActionHit> Hits, string? Alias) Hits(SearchMatch match) {
    var fields = match.Action.Fields;
    var hits = new List<ActionHit>();
    string? alias = null;

    var indexes = match.Terms
      .Select(static term => term.Field)
      .Distinct()
      .OrderBy(static index => index);

    foreach (var index in indexes) {
      var field = fields[index];

      if (field.Kind is ActionField.Alias) {
        if (alias is not null) {
          continue;
        }

        alias = field.Text;
      }

      var ranges = match.Terms
        .Where(term => term.Field == index)
        .SelectMany(static term => term.Ranges);

      hits.Add(
        new ActionHit(
          field.Kind,
          field.OptionKey,
          ActionSearch.ToOriginal(field.Folded, field.Text.Length, ranges)
        )
      );
    }

    return (hits, alias);
  }

  /// <summary>
  /// The group of each field, which places a row before its tier: any name hit ranks above any
  /// detail hit.
  /// </summary>
  internal static FieldGroup GroupOf(ActionField field) =>
    field switch {
      ActionField.Title or ActionField.Alias => FieldGroup.Name,
      ActionField.Option or ActionField.Description => FieldGroup.Detail,
      _ => throw new InvalidOperationException($"No group for the {field} field.")
    };

  /// <summary>
  /// The term's best hit among <paramref name="fields"/>, or none when it hits none.
  /// A typo hit, of one of the term's variants, ranks in the typo tier whatever its ranges.
  /// </summary>
  private static TermHit? BestHit(
    string term,
    IReadOnlyList<SearchField> fields,
    IActionMatcher matcher,
    bool isTypo
  ) {
    var strongestTier = isTypo ? MatchTier.Typo : MatchTier.Prefix;
    TermHit? best = null;

    for (var i = 0; i < fields.Count; i++) {
      var field = fields[i];

      // The fields come in rank order: once the best hit outranks a field's strongest hit, no later
      // field can beat it.
      if (best?.Rank.CompareTo(new HitRank(field.Kind, strongestTier)) < 0) {
        break;
      }

      if (matcher.Match(term, field.Folded.Text) is not {} match) {
        continue;
      }

      var tier = isTypo ? MatchTier.Typo : ActionSearch.ReadTier(field.Folded.Text, match.Ranges);

      var hit = new TermHit(i, new HitRank(field.Kind, tier), match.Score, match.Ranges);

      if (ActionSearch.IsBetter(hit, best)) {
        best = hit;
      }
    }

    return best;
  }

  /// <summary>
  /// The best hit of any of a term's variants among <paramref name="fields"/>, or none.
  /// </summary>
  private static TermHit? BestTypoHit(
    IEnumerable<string> variants,
    IReadOnlyList<SearchField> fields,
    IActionMatcher matcher
  ) {
    TermHit? best = null;

    foreach (var variant in variants) {
      if (ActionSearch.BestHit(variant, fields, matcher, isTypo: true) is {} hit &&
        ActionSearch.IsBetter(hit, best)) {
        best = hit;
      }
    }

    return best;
  }

  /// <summary>
  /// Whether <paramref name="hit"/> places before <paramref name="best"/>: a rank that sorts first,
  /// or a higher score at equal rank.
  /// </summary>
  private static bool IsBetter(TermHit hit, TermHit? best) =>
    best is not {} current ||
    hit.Rank.CompareTo(current.Rank) < 0 ||
    (hit.Rank == current.Rank && hit.Score > current.Score);

  /// <summary>
  /// The term with one character dropped, a surrogate pair whole.
  /// </summary>
  private static List<string> Variants(string term) {
    var variants = new List<string>(term.Length);

    for (var i = 0; i < term.Length; i++) {
      var length = char.IsSurrogatePair(term, i) ? 2 : 1;

      // Dropping either of two equal neighbours makes the same variant.
      if (i is 0 || term[i] != term[i - 1]) {
        variants.Add(term.Remove(i, length));
      }

      i += length - 1;
    }

    return variants;
  }

  /// <summary>
  /// The hits of an action every term hit.
  /// </summary>
  private static TermHit[] Whole(TermHit?[] hits) {
    // Hot path, the empty query: one shared empty array, in place of one per action.
    if (hits.Length is 0) {
      return [];
    }

    // Hot path, once per matching action: a loop, in place of LINQ.
    var whole = new TermHit[hits.Length];

    for (var i = 0; i < hits.Length; i++) {
      whole[i] = hits[i] ?? throw new InvalidOperationException("A term missed.");
    }

    return whole;
  }

  /// <summary>
  /// The kind of hit a term's ranges make on a folded text, the same reading for every matcher.
  /// A prefix is one range at the text's start; an abbreviation like <c>ps</c> for Pause Simulation
  /// is a hit on word starts.
  /// </summary>
  private static MatchTier ReadTier(string text, IReadOnlyList<MatchRange> ranges) {
    if (ranges is [{ Start: 0 }]) {
      return MatchTier.Prefix;
    }

    var isOnWordStarts = ranges.Count > 0;

    foreach (var range in ranges) {
      isOnWordStarts &= ActionSearch.IsWordStart(text, range.Start);
    }

    return isOnWordStarts
      ? MatchTier.WordStarts
      : ranges.Count is 1
        ? MatchTier.OneRun
        : MatchTier.Scattered;
  }

  /// <summary>
  /// Whether a word starts at <paramref name="index"/>: at the text's start, or after a character
  /// other than a letter, a mark, and a number, such as a space or a punctuation sign.
  /// A han label has no other boundary, as nothing segments CJK.
  /// </summary>
  private static bool IsWordStart(string text, int index) {
    if (index is 0) {
      return true;
    }

    var before = index >= 2 && char.IsLowSurrogate(text[index - 1]) ? index - 2 : index - 1;

    // The letters, the marks, and the numbers are the first eleven categories.
    return CharUnicodeInfo.GetUnicodeCategory(text, before) > UnicodeCategory.OtherNumber;
  }

  /// <summary>
  /// The ranges of a folded text as spans of its original text, sorted and merged.
  /// A span covers whole original characters: a letter the fold turned into two is highlighted
  /// whole when either half matched, and the marks the fold dropped after a matched letter go with
  /// it.
  /// </summary>
  private static List<MatchRange> ToOriginal(
    FoldedText folded,
    int originalLength,
    IEnumerable<MatchRange> ranges
  ) {
    var spans = new List<MatchRange>();

    foreach (var range in ranges) {
      var start = folded.Map[range.Start];
      var last = folded.Map[range.Start + range.Length - 1];
      var next = range.Start + range.Length;

      // Past the other halves of the last original character.
      while (next < folded.Map.Length && folded.Map[next] <= last) {
        next++;
      }

      var end = next < folded.Map.Length ? folded.Map[next] : originalLength;

      spans.Add(new MatchRange(start, end - start));
    }

    spans.Sort(static (a, b) => a.Start.CompareTo(b.Start));

    var merged = new List<MatchRange>(spans.Count);

    foreach (var span in spans) {
      if (merged.Count > 0 && span.Start <= merged[^1].Start + merged[^1].Length) {
        var end = Math.Max(merged[^1].Start + merged[^1].Length, span.Start + span.Length);

        merged[^1] = merged[^1] with {
          Length = end - merged[^1].Start
        };
      }
      else {
        merged.Add(span);
      }
    }

    return merged;
  }
}

/// <summary>
/// A text of an action a search reaches, folded once when the registry rebuilds.
/// An action lists its fields in rank order: the title, the aliases, the option labels, then the
/// description.
/// </summary>
internal sealed record SearchField(ActionField Kind, string Text, string? OptionKey = null) {
  internal FoldedText Folded { get; } = TextFold.Fold(Text);
}

/// <summary>
/// An action every term of the query hit, placed by its weakest term.
/// </summary>
internal sealed class SearchMatch {
  internal SearchMatch(IndexedAction action, TermHit[] terms) {
    this.Action = action;
    this.Terms = terms;
    this.Rank = new HitRank(ActionField.Title, MatchTier.Prefix);

    // Hot path, once per matching action: a loop, in place of LINQ's Max and Sum.
    foreach (var term in terms) {
      if (term.Rank.CompareTo(this.Rank) > 0) {
        this.Rank = term.Rank;
      }

      this.Score += term.Score;
    }
  }

  internal IndexedAction Action { get; }

  internal IReadOnlyList<TermHit> Terms { get; }

  /// <summary>
  /// The rank of the weakest term: a row is only as good as the term it had to reach furthest for.
  /// </summary>
  internal HitRank Rank { get; }

  /// <summary>
  /// The matcher's scores summed over the terms, which breaks a tie in rank and in use alone.
  /// </summary>
  internal int Score { get; }
}

/// <summary>
/// Where one term of the query hit an action: the index of the field among the action's fields, and
/// its ranges on that field's folded text.
/// </summary>
internal readonly record struct TermHit(
  int Field,
  HitRank Rank,
  int Score,
  IReadOnlyList<MatchRange> Ranges
);

/// <summary>
/// What places a hit, the lower first: its field's group, then its tier, then its field.
/// </summary>
internal readonly record struct HitRank(ActionField Field, MatchTier Tier) : IComparable<HitRank> {
  /// <summary>
  /// The group, the tier, and the field, one byte each.
  /// Hot path, compared thousands of times by a search's sort: one integer, in place of a tuple of
  /// enums.
  /// </summary>
  private int Order =>
    ((int) ActionSearch.GroupOf(this.Field) << 16) | ((int) this.Tier << 8) | (int) this.Field;

  public int CompareTo(HitRank other) => this.Order.CompareTo(other.Order);
}

/// <summary>
/// The groups of fields, the stronger first.
/// </summary>
internal enum FieldGroup {
  /// <summary>
  /// The title and the aliases.
  /// </summary>
  Name,

  /// <summary>
  /// The option labels and the description.
  /// </summary>
  Detail
}

/// <summary>
/// The kinds of hit, the strongest first.
/// </summary>
internal enum MatchTier {
  Prefix,

  WordStarts,

  OneRun,

  Scattered,

  /// <summary>
  /// A hit of the term with one character dropped, the registry's own.
  /// </summary>
  Typo
}
