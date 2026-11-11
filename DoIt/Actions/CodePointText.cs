using System;
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
  /// </summary>
  internal static bool IsWordCharacter(int point) =>
    CharUnicodeInfo.GetUnicodeCategory(point) <= UnicodeCategory.OtherNumber;

  /// <summary>
  /// The span of <paramref name="text"/> a match of <paramref name="term"/> can lie in, from the
  /// character before the first place of the term's first character to the last place of its last
  /// one, or none when the text does not hold the term's characters in order.
  /// It reads UTF-16 units, which hold in order whenever the code points do, so it rejects a text
  /// before decoding it.
  /// </summary>
  internal static (int Start, int End)? Scope(string term, string text) {
    var at = -1;
    var first = -1;

    foreach (var unit in term) {
      at = text.IndexOf(unit, at + 1);

      if (at < 0) {
        return null;
      }

      if (first < 0) {
        first = at;
      }
    }

    var start = Math.Max(first - 1, 0);

    // Never half a surrogate pair.
    if (start > 0 && char.IsLowSurrogate(text[start])) {
      start--;
    }

    return (start, text.LastIndexOf(term[^1]) + 1);
  }

  internal void Decode(string text) => this.Decode(text, 0, text.Length);

  /// <summary>
  /// Decodes the span of <paramref name="text"/> from <paramref name="start"/> to
  /// <paramref name="end"/>, the ranges it reports still placed in the whole text.
  /// </summary>
  internal void Decode(string text, int start, int end) {
    if (this.points.Length < end - start) {
      this.points = new int[end - start];
      this.starts = new int[end - start + 1];
    }

    var count = 0;

    for (var i = start; i < end; i++) {
      this.starts[count] = i;

      if (i + 1 < end && char.IsSurrogatePair(text[i], text[i + 1])) {
        this.points[count] = char.ConvertToUtf32(text[i], text[i + 1]);
        i++;
      }
      else {
        this.points[count] = text[i];
      }

      count++;
    }

    this.starts[count] = end;
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
