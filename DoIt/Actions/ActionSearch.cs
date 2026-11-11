using System;
using System.Collections.Generic;
using System.Linq;

namespace DoIt.Actions;

/// <summary>
/// The ranking pipeline of the registry's search, around the matcher seam: the query is folded and
/// split into terms, each term scored on its own through the matcher against every field of an
/// action and placed in its best one, every term required.
/// A row is placed by its weakest term's field group, then tier, then field, then by frecency, the
/// matcher's score breaking a frecency tie, and the shorter title a score tie.
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

    // [PERF] One buffer for every action, copied for those kept, in place of an array per action.
    var hits = new TermHit?[terms.Count];

    foreach (var action in actions) {
      var isWhole = true;
      var isRetryable = true;

      for (var i = 0; i < terms.Count && isRetryable; i++) {
        hits[i] = ActionSearch.BestHit(terms[i], action.Fields, matcher, isTypo: false);

        if (hits[i] is null) {
          isWhole = false;

          // [PERF] A miss past the cap is dropped, in place of kept for a typo pass that no longer
          // runs.
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

      var text = field.Folded.Text;

      // A description is prose, where a term's letters scatter by chance: such hits would fill the
      // cap and keep the typo pass from running, so a description is hit by a run of the term
      // alone, wherever the matcher's alignment would go, and the matcher only scores it.
      var run = field.Kind is ActionField.Description
        ? ActionSearch.RunOf(term, text)
        : null;

      // [PERF] A search for the run, in place of the matcher's alignment, on a description the term
      // cannot hit.
      if ((field.Kind is ActionField.Description && run is null) ||
        matcher.Match(term, text) is not {} match) {
        continue;
      }

      var ranges = run is {} span ? [span] : match.Ranges;

      var tier = isTypo ? MatchTier.Typo : ActionSearch.ReadTier(text, ranges);

      var hit = new TermHit(i, new HitRank(field.Kind, tier), match.Score, ranges);

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
  /// The term's first place in <paramref name="text"/> at a word start, else its first place, or
  /// none when the text does not hold it.
  /// A consonant typed alone in the term finds a syllable opening on it.
  /// </summary>
  private static MatchRange? RunOf(string term, string text) {
    MatchRange? first = null;

    for (var at = HangulInitials.IndexOf(text, term, 0);
      at >= 0;
      at = HangulInitials.IndexOf(text, term, at + 1)) {
      if (ActionSearch.IsWordStart(text, at)) {
        return new MatchRange(at, term.Length);
      }

      first ??= new MatchRange(at, term.Length);
    }

    return first;
  }

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
    // [PERF] The empty query: one shared empty array, in place of one per action.
    if (hits.Length is 0) {
      return [];
    }

    // [PERF] A loop, in place of LINQ.
    var whole = new TermHit[hits.Length];

    for (var i = 0; i < hits.Length; i++) {
      whole[i] = hits[i] ?? throw new InvalidOperationException("A term missed.");
    }

    return whole;
  }

  /// <summary>
  /// The kind of hit a term's ranges make on a folded text, the same reading for every matcher.
  /// A prefix is one range at the text's start; an abbreviation like <c>ps</c> for Pause Simulation
  /// is a hit on word starts, and the initials when it hits every word.
  /// </summary>
  private static MatchTier ReadTier(string text, IReadOnlyList<MatchRange> ranges) {
    if (ranges is [{ Start: 0 }]) {
      return MatchTier.Prefix;
    }

    var isOnWordStarts = ranges.Count > 0;

    foreach (var range in ranges) {
      isOnWordStarts &= ActionSearch.IsWordStart(text, range.Start);
    }

    if (isOnWordStarts) {
      return ActionSearch.IsInitials(text, ranges) ? MatchTier.Initials : MatchTier.WordStarts;
    }

    return ranges.Count is 1 ? MatchTier.OneRun : MatchTier.Scattered;
  }

  /// <summary>
  /// Whether ranges on word starts are the initials of <paramref name="text"/>: one character on
  /// the start of each of its words, a word opening on a letter, a mark, or a number, so a symbol
  /// standing alone, such as an ampersand, is no word to hit.
  /// An apostrophe joins an elided article to its word, which then opens twice: Panneau
  /// d'informations has the initials <c>pd</c>, <c>pi</c>, and <c>pdi</c>.
  /// A lone letter after an apostrophe, a possessive's s, is no word to hit: Driver's License has
  /// the initials <c>dl</c>.
  /// A word start followed by more of the word is not an initial: <c>econ</c> hitting Electricity
  /// Consumption as <c>e</c> and <c>con</c> stays below a word prefix.
  /// </summary>
  private static bool IsInitials(string text, IReadOnlyList<MatchRange> ranges) {
    foreach (var range in ranges) {
      var width = char.IsSurrogatePair(text, range.Start) ? 2 : 1;

      if (range.Length != width || !CodePointText.IsWordCharacter(text, range.Start)) {
        return false;
      }
    }

    // The next range to meet, and whether the word being read is hit yet.
    var next = 0;
    var isWordHit = true;

    // [PERF] A one-character hit on a word start of a long description: an exit at the first word
    // no range hits, in place of reading every word of the text.
    for (var i = 0; i < text.Length; i++) {
      if (!CodePointText.IsWordCharacter(text, i) || !ActionSearch.IsWordStart(text, i)) {
        continue;
      }

      // An apostrophe after a word joins an elided article to its word; after no word, it quotes.
      var isJoined = i >= 1 &&
        text[i - 1] is '\'' or '\u2019' &&
        !ActionSearch.IsWordStart(text, i - 1);

      if (isJoined && ActionSearch.IsLoneLetter(text, i)) {
        continue;
      }

      if (!isJoined) {
        if (!isWordHit) {
          return false;
        }

        isWordHit = false;
      }

      if (next < ranges.Count && ranges[next].Start == i) {
        isWordHit = true;
        next++;
      }
    }

    return isWordHit && next == ranges.Count;
  }

  /// <summary>
  /// Whether the word character at <paramref name="index"/> stands alone, with no word character
  /// after it.
  /// </summary>
  private static bool IsLoneLetter(string text, int index) {
    var after = index + (char.IsSurrogatePair(text, index) ? 2 : 1);

    return after >= text.Length || !CodePointText.IsWordCharacter(text, after);
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

    var before = index >= 2 && char.IsSurrogatePair(text[index - 2], text[index - 1])
      ? index - 2
      : index - 1;

    return !CodePointText.IsWordCharacter(text, before);
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

    // [PERF] A loop, in place of LINQ's Max and Sum.
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
  /// </summary>
  // [PERF] One integer, in place of a tuple of enums.
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

  /// <summary>
  /// One character on the start of every word, an abbreviation of the whole label.
  /// </summary>
  Initials,

  WordStarts,

  OneRun,

  Scattered,

  /// <summary>
  /// A hit of the term with one character dropped, the registry's own.
  /// </summary>
  Typo
}
