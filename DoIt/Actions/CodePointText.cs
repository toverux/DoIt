using System.Collections.Generic;
using System.Globalization;

namespace DoIt.Actions;

/// <summary>
/// A text decoded into its code points, in buffers a matcher reuses from one call to the next, so
/// the matcher compares whole characters, a surrogate pair included, and allocates no buffer per
/// call.
/// </summary>
internal sealed class CodePointText {
  /// <summary>
  /// The code points, reassigned when a longer text needs a larger buffer.
  /// </summary>
  private int[] points = [];

  /// <summary>
  /// The index in the text of each code point, then the text's length, reassigned when a longer
  /// text needs a larger buffer.
  /// </summary>
  private int[] starts = [0];

  internal int Length { get; private set; }

  internal int this[int index] => this.points[index];

  /// <summary>
  /// Whether a code point belongs to a word: a letter, a mark, or a number.
  /// A word starts after any other character, the registry's own reading of a boundary, so a
  /// matcher's word-start bonus lands where the registry reads a word-starts tier.
  /// The letters, the marks, and the numbers are the first eleven categories.
  /// </summary>
  internal static bool IsWordCharacter(int point) =>
    CharUnicodeInfo.GetUnicodeCategory(point) <= UnicodeCategory.OtherNumber;

  /// <summary>
  /// Whether the code point at <paramref name="index"/> belongs to a word.
  /// The low half of a surrogate pair and a lone surrogate belong to none.
  /// </summary>
  internal static bool IsWordCharacter(string text, int index) =>
    CharUnicodeInfo.GetUnicodeCategory(text, index) <= UnicodeCategory.OtherNumber;

  /// <summary>
  /// Whether <paramref name="text"/> holds the characters of <paramref name="term"/> in order, a
  /// consonant typed alone held by a syllable opening on it.
  /// It reads UTF-16 units, which hold in order whenever the code points do, so it rejects a text
  /// before decoding it.
  /// </summary>
  internal static bool HoldsInOrder(string term, string text) {
    var at = -1;

    foreach (var unit in term) {
      at = HangulInitials.IndexOf(text, unit, at + 1);

      if (at < 0) {
        return false;
      }
    }

    return true;
  }

  internal void Decode(string text) {
    if (this.points.Length < text.Length) {
      this.points = new int[text.Length];
      this.starts = new int[text.Length + 1];
    }

    var count = 0;

    for (var i = 0; i < text.Length; i++) {
      this.starts[count] = i;

      if (i + 1 < text.Length && char.IsSurrogatePair(text[i], text[i + 1])) {
        this.points[count] = char.ConvertToUtf32(text[i], text[i + 1]);
        i++;
      }
      else {
        this.points[count] = text[i];
      }

      count++;
    }

    this.starts[count] = text.Length;
    this.Length = count;
  }

  /// <summary>
  /// The spans of the text the code points at <paramref name="positions"/> cover, the positions
  /// ascending, adjacent spans merged into one.
  /// </summary>
  internal List<MatchRange> ToRanges(IReadOnlyList<int> positions) {
    var ranges = new List<MatchRange>(positions.Count);

    foreach (var position in positions) {
      var start = this.starts[position];
      var length = this.starts[position + 1] - start;

      if (ranges.Count > 0 && ranges[^1].Start + ranges[^1].Length == start) {
        ranges[^1] = ranges[^1] with {
          Length = ranges[^1].Length + length
        };
      }
      else {
        ranges.Add(new MatchRange(start, length));
      }
    }

    return ranges;
  }
}
