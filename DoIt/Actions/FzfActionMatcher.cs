// Ported from fzf's FuzzyMatchV2, src/algo/algo.go at c7bf2b8eea25952dd257a466c0afa72faae206fb
// (https://github.com/junegunn/fzf).
// Copyright (c) 2013-2026 Junegunn Choi. MIT License, its full text in THIRD-PARTY-NOTICES.md.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DoIt.Actions;

/// <summary>
/// <para>
/// fzf's V2 algorithm: the best-scoring alignment of the term's characters in the text, in order,
/// by a Smith-Waterman pass with an affine gap penalty, a bonus for a character that starts a word,
/// and a bonus for a run of consecutive characters.
/// </para>
/// <para>
/// Both sides come folded, so the port neither lower-cases nor normalizes, and it indexes code
/// points. A mark counts as a letter, so the word starts it rewards are the registry's own.
/// fzf's fast paths for one and two characters, and its greedy fallback above its matrix budget,
/// none of which changes a result on a label, are left out.
/// </para>
/// <para>
/// Not thread-safe: its buffers are reused from one call to the next.
/// </para>
/// </summary>
internal sealed class FzfActionMatcher : IActionMatcher {
  private const int ScoreMatch = 16;

  private const int ScoreGapStart = -3;

  private const int ScoreGapExtension = -1;

  /// <summary>
  /// Cancelled by a gap of over eight characters, about the length of a word, so a longer acronym
  /// does not always win over a shorter fuzzy match.
  /// </summary>
  private const int BonusBoundary = FzfActionMatcher.ScoreMatch / 2;

  private const int BonusNonWord = FzfActionMatcher.ScoreMatch / 2;

  /// <summary>
  /// One less than a boundary, as a camel hump comes with no one-character gap.
  /// </summary>
  private const int BonusCamel123 =
    FzfActionMatcher.BonusBoundary + FzfActionMatcher.ScoreGapExtension;

  private const int BonusConsecutive =
    -(FzfActionMatcher.ScoreGapStart + FzfActionMatcher.ScoreGapExtension);

  private const int BonusFirstCharMultiplier = 2;

  private const int BonusBoundaryWhite = FzfActionMatcher.BonusBoundary + 2;

  private const int BonusBoundaryDelimiter = FzfActionMatcher.BonusBoundary + 1;

  private const string DelimiterChars = "/,:;|";

  private const string WhiteChars = " \t\n\v\f\r";

  private static readonly int[,] BonusMatrix = FzfActionMatcher.ComputeBonusMatrix();

  private readonly CodePointText pattern = new();

  private readonly CodePointText text = new();

  /// <summary>
  /// The first row of the score matrix and of the run lengths, and each character's bonus.
  /// </summary>
  private int[] h0 = [], c0 = [], bonuses = [];

  /// <summary>
  /// The first place of each term character, taken in order.
  /// </summary>
  private int[] firsts = [];

  /// <summary>
  /// The score matrix and the run lengths, a row per term character over the scope of the match.
  /// </summary>
  private int[] h = [], c = [];

  private enum CharClass {
    White,

    NonWord,

    Delimiter,

    Lower,

    Upper,

    Letter,

    Number
  }

  public ActionMatch? Match(string term, string text) {
    // Phase 1: the scope a match can lie in, most texts rejected before any is decoded.
    if (CodePointText.Scope(term, text) is not var (start, end)) {
      return null;
    }

    this.pattern.Decode(term);
    this.text.Decode(text, start, end);

    var m = this.pattern.Length;
    var n = this.text.Length;

    if (m is 0 || m > n) {
      return null;
    }

    FzfActionMatcher.Ensure(ref this.h0, n);
    FzfActionMatcher.Ensure(ref this.c0, n);
    FzfActionMatcher.Ensure(ref this.bonuses, n);
    FzfActionMatcher.Ensure(ref this.firsts, m);

    // Phase 2: each character's bonus, the first place of each term character, and the first row.
    var maxScore = 0;
    var maxScorePos = 0;
    var pidx = 0;
    var lastIdx = 0;
    var pchar0 = this.pattern[0];
    var pchar = pchar0;
    var prevH0 = 0;
    var prevClass = CharClass.White;
    var isInGap = false;

    for (var off = 0; off < n; off++) {
      var character = this.text[off];
      var charClass = FzfActionMatcher.ClassOf(character);
      var bonus = FzfActionMatcher.BonusMatrix[(int) prevClass, (int) charClass];

      this.bonuses[off] = bonus;
      prevClass = charClass;

      if (character == pchar) {
        if (pidx < m) {
          this.firsts[pidx] = off;
          pidx++;
          pchar = this.pattern[Math.Min(pidx, m - 1)];
        }

        lastIdx = off;
      }

      if (character == pchar0) {
        var score = FzfActionMatcher.ScoreMatch +
          (bonus * FzfActionMatcher.BonusFirstCharMultiplier);

        this.h0[off] = score;
        this.c0[off] = 1;

        if (m is 1 && score > maxScore) {
          maxScore = score;
          maxScorePos = off;

          if (bonus >= FzfActionMatcher.BonusBoundary) {
            break;
          }
        }

        isInGap = false;
      }
      else {
        var gap = isInGap ? FzfActionMatcher.ScoreGapExtension : FzfActionMatcher.ScoreGapStart;

        this.h0[off] = Math.Max(prevH0 + gap, 0);
        this.c0[off] = 0;
        isInGap = true;
      }

      prevH0 = this.h0[off];
    }

    if (pidx != m) {
      return null;
    }

    if (m is 1) {
      return new ActionMatch(maxScore, this.text.ToRanges([maxScorePos]));
    }

    // Phase 3: the score matrix, over the scope from the first term character's first place to
    // the last place of the last one.
    var f0 = this.firsts[0];
    var width = lastIdx - f0 + 1;

    FzfActionMatcher.Ensure(ref this.h, width * m);
    FzfActionMatcher.Ensure(ref this.c, width * m);
    Array.Copy(this.h0, f0, this.h, 0, width);
    Array.Copy(this.c0, f0, this.c, 0, width);

    for (pidx = 1; pidx < m; pidx++) {
      var f = this.firsts[pidx];
      var rowChar = this.pattern[pidx];
      var row = pidx * width;
      var hLeft = row + f - f0 - 1;
      var diag = hLeft - width;

      isInGap = false;
      this.h[hLeft] = 0;

      for (var col = f; col <= lastIdx; col++) {
        var k = col - f;
        var s1 = 0;
        var consecutive = 0;

        var s2 = this.h[hLeft + k] +
          (isInGap ? FzfActionMatcher.ScoreGapExtension : FzfActionMatcher.ScoreGapStart);

        if (rowChar == this.text[col]) {
          s1 = this.h[diag + k] + FzfActionMatcher.ScoreMatch;

          var b = this.bonuses[col];

          consecutive = this.c[diag + k] + 1;

          if (consecutive > 1) {
            var fb = this.bonuses[col - consecutive + 1];

            // A stronger boundary inside a run starts a new chunk.
            if (b >= FzfActionMatcher.BonusBoundary && b > fb) {
              consecutive = 1;
            }
            else {
              b = Math.Max(b, Math.Max(FzfActionMatcher.BonusConsecutive, fb));
            }
          }

          if (s1 + b < s2) {
            s1 += this.bonuses[col];
            consecutive = 0;
          }
          else {
            s1 += b;
          }
        }

        this.c[hLeft + 1 + k] = consecutive;
        isInGap = s1 < s2;

        var cell = Math.Max(Math.Max(s1, s2), 0);

        if (pidx == m - 1 && cell > maxScore) {
          maxScore = cell;
          maxScorePos = col;
        }

        this.h[hLeft + 1 + k] = cell;
      }
    }

    var positions = this.Backtrace(m, f0, width, lastIdx, maxScorePos);

    return new ActionMatch(maxScore, this.text.ToRanges(positions));
  }

  /// <summary>
  /// Phase 4: the places of the term's characters, read back from the best cell of the last row,
  /// ascending; a tie goes to the match that extends a run.
  /// </summary>
  private List<int> Backtrace(int m, int f0, int width, int lastIdx, int maxScorePos) {
    var positions = new List<int>(m);
    var i = m - 1;
    var j = maxScorePos;
    var isMatchPreferred = true;

    while (true) {
      var rowStart = i * width;
      var j0 = j - f0;
      var s = this.h[rowStart + j0];
      var s1 = 0;
      var s2 = 0;

      if (i > 0 && j >= this.firsts[i]) {
        s1 = this.h[rowStart - width + j0 - 1];
      }

      if (j > this.firsts[i]) {
        s2 = this.h[rowStart + j0 - 1];
      }

      var row = i;

      if (s > s1 && (s > s2 || (s == s2 && isMatchPreferred))) {
        positions.Add(j);

        if (i is 0) {
          break;
        }

        i--;
      }

      // The row below is only written from its first place on.
      isMatchPreferred = this.c[rowStart + j0] > 1 ||
        (row + 1 < m &&
          j < lastIdx &&
          j + 1 >= this.firsts[row + 1] &&
          this.c[rowStart + width + j0 + 1] > 0);

      j--;
    }

    positions.Reverse();

    return positions;
  }

  private static void Ensure(ref int[] buffer, int length) {
    if (buffer.Length < length) {
      buffer = new int[length];
    }
  }

  private static CharClass ClassOf(int point) {
    if (point < 128) {
      return point switch {
        >= 'a' and <= 'z' => CharClass.Lower,
        >= 'A' and <= 'Z' => CharClass.Upper,
        >= '0' and <= '9' => CharClass.Number,
        _ when FzfActionMatcher.WhiteChars.IndexOf((char) point) >= 0 => CharClass.White,
        _ when FzfActionMatcher.DelimiterChars.IndexOf((char) point) >= 0 => CharClass.Delimiter,
        _ => CharClass.NonWord
      };
    }

    return CharUnicodeInfo.GetUnicodeCategory(point) switch {
      UnicodeCategory.LowercaseLetter => CharClass.Lower,
      UnicodeCategory.UppercaseLetter => CharClass.Upper,
      UnicodeCategory.DecimalDigitNumber or
        UnicodeCategory.LetterNumber or
        UnicodeCategory.OtherNumber => CharClass.Number,
      // fzf counts a mark as a non-word character, a boundary before the next letter.
      _ when CodePointText.IsWordCharacter(point) => CharClass.Letter,
      _ when point < 0x10000 && char.IsWhiteSpace((char) point) => CharClass.White,
      _ => CharClass.NonWord
    };
  }

  private static int[,] ComputeBonusMatrix() {
    var count = Enum.GetValues(typeof(CharClass)).Length;
    var matrix = new int[count, count];

    for (var prev = 0; prev < count; prev++) {
      for (var current = 0; current < count; current++) {
        matrix[prev, current] = FzfActionMatcher.BonusFor((CharClass) prev, (CharClass) current);
      }
    }

    return matrix;
  }

  private static int BonusFor(CharClass prev, CharClass current) {
    if (current >= CharClass.NonWord) {
      switch (prev) {
        case CharClass.White: return FzfActionMatcher.BonusBoundaryWhite;
        case CharClass.Delimiter: return FzfActionMatcher.BonusBoundaryDelimiter;
        case CharClass.NonWord: return FzfActionMatcher.BonusBoundary;
      }
    }

    if ((prev is CharClass.Lower && current is CharClass.Upper) ||
      (prev is not CharClass.Number && current is CharClass.Number)) {
      return FzfActionMatcher.BonusCamel123;
    }

    return current switch {
      CharClass.NonWord or CharClass.Delimiter => FzfActionMatcher.BonusNonWord,
      CharClass.White => FzfActionMatcher.BonusBoundaryWhite,
      _ => 0
    };
  }
}
