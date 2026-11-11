// Ported from Visual Studio Code's fuzzyScore, src/vs/base/common/filters.ts at b779fd689299
// (https://github.com/microsoft/vscode).
// Copyright (c) 2015 - present Microsoft Corporation. MIT License, its full text in
// THIRD-PARTY-NOTICES.md.

using System;
using System.Collections.Generic;

namespace DoIt.Actions;

/// <summary>
/// <para>
/// Visual Studio Code's fuzzy scorer: the best alignment of the term's characters in the text, in
/// order, by dynamic programming over a band of the text, rewarding a prefix, a character after a
/// separator, and a run.
/// It runs with the upstream's option that lets the term's first character land inside a word, so
/// <c>tor</c> finds <c>constructor</c>: the registry ranks such a hit in its own tier.
/// </para>
/// <para>
/// Both sides come folded, so the port drops the upstream's camel-hump and case-match branches,
/// which never fire on lower-cased text, and it indexes code points. A separator is any character
/// other than a letter, a mark, and a number, where the upstream lists ASCII ones: the registry's
/// own reading of a boundary. Its tables grow with the text, where the upstream cuts both sides at
/// 128 characters, which would drop the end of a long description.
/// </para>
/// <para>
/// Not thread-safe: its buffers are reused from one call to the next.
/// </para>
/// </summary>
internal sealed class VsCodeActionMatcher : IActionMatcher {
  private const int Diag = 1;

  private const int Left = 2;

  private const int LeftLeft = 3;

  private const int NoScore = int.MinValue;

  private readonly CodePointText pattern = new();

  private readonly CodePointText word = new();

  /// <summary>
  /// The earliest and the latest place in the text each term character can match at, reassigned
  /// when a longer term needs larger buffers.
  /// </summary>
  private int[] minWordMatchPos = [], maxWordMatchPos = [];

  /// <summary>
  /// The scores, the arrows a backtrace follows, and the length of the diagonal run ending in each
  /// cell, a row per term character plus an empty first one, a column per text character plus one,
  /// reassigned when a longer term or text needs larger buffers.
  /// </summary>
  private int[] table = [], arrows = [], diag = [];

  public ActionMatch? Match(string term, string text) {
    // Hot path, every field of every action: a scan of the UTF-16 units, in place of decoding the
    // text for IsPatternInWord to reject.
    if (CodePointText.Scope(term, text) is null) {
      return null;
    }

    this.pattern.Decode(term);
    this.word.Decode(text);

    var patternLen = this.pattern.Length;
    var wordLen = this.word.Length;

    if (patternLen is 0 || patternLen > wordLen) {
      return null;
    }

    VsCodeActionMatcher.Ensure(ref this.minWordMatchPos, patternLen);
    VsCodeActionMatcher.Ensure(ref this.maxWordMatchPos, patternLen);

    if (!this.IsPatternInWord(patternLen, wordLen)) {
      return null;
    }

    this.FillInMaxWordMatchPos(patternLen, wordLen);

    var stride = wordLen + 1;
    var cells = (patternLen + 1) * stride;

    VsCodeActionMatcher.Ensure(ref this.table, cells);
    VsCodeActionMatcher.Ensure(ref this.arrows, cells);
    VsCodeActionMatcher.Ensure(ref this.diag, cells);

    // The upstream's first row is never written: zeroed here, as the buffers are reused.
    Array.Clear(this.table, 0, stride);
    Array.Clear(this.diag, 0, stride);

    for (var patternPos = 0; patternPos < patternLen; patternPos++) {
      var row = (patternPos + 1) * stride;
      var above = row - stride;
      var minPos = this.minWordMatchPos[patternPos];
      var maxPos = this.maxWordMatchPos[patternPos];

      var nextMaxPos = patternPos + 1 < patternLen
        ? this.maxWordMatchPos[patternPos + 1]
        : wordLen;

      for (var wordPos = minPos; wordPos < nextMaxPos; wordPos++) {
        var column = wordPos + 1;
        var score = VsCodeActionMatcher.NoScore;

        if (wordPos <= maxPos) {
          score = this.DoScore(patternPos, wordPos, this.diag[above + column - 1] is 0);
        }

        var canComeDiag = score is not VsCodeActionMatcher.NoScore;
        var diagScore = canComeDiag ? score + this.table[above + column - 1] : 0;
        var canComeLeft = wordPos > minPos;

        // A gap opened after a diagonal run costs five.
        var leftScore = canComeLeft
          ? this.table[row + column - 1] + (this.diag[row + column - 1] > 0 ? -5 : 0)
          : 0;

        var canComeLeftLeft = wordPos > minPos + 1 && this.diag[row + column - 1] > 0;

        var leftLeftScore = canComeLeftLeft
          ? this.table[row + column - 2] + (this.diag[row + column - 2] > 0 ? -5 : 0)
          : 0;

        // Going left places the match earlier in the text, so it wins a tie.
        if (canComeLeftLeft &&
          (!canComeLeft || leftLeftScore >= leftScore) &&
          (!canComeDiag || leftLeftScore >= diagScore)) {
          this.table[row + column] = leftLeftScore;
          this.arrows[row + column] = VsCodeActionMatcher.LeftLeft;
          this.diag[row + column] = 0;
        }
        else if (canComeLeft && (!canComeDiag || leftScore >= diagScore)) {
          this.table[row + column] = leftScore;
          this.arrows[row + column] = VsCodeActionMatcher.Left;
          this.diag[row + column] = 0;
        }
        else if (canComeDiag) {
          this.table[row + column] = diagScore;
          this.arrows[row + column] = VsCodeActionMatcher.Diag;
          this.diag[row + column] = this.diag[above + column - 1] + 1;
        }
        else {
          throw new InvalidOperationException("No way into a cell of the score table.");
        }
      }
    }

    return this.Backtrace(patternLen, wordLen, stride);
  }

  /// <summary>
  /// The match read back from the last cell, with the upstream's final adjustments: a boost when
  /// the term is the whole text, and a point off per text character skipped before the last match.
  /// </summary>
  private ActionMatch Backtrace(int patternLen, int wordLen, int stride) {
    var row = patternLen;
    var column = wordLen;
    var score = this.table[(row * stride) + column];
    var backwardsDiagLength = 0;
    var maxMatchColumn = 0;
    var positions = new List<int>(patternLen);

    while (row >= 1) {
      var diagColumn = column;

      do {
        var arrow = this.arrows[(row * stride) + diagColumn];

        if (arrow is VsCodeActionMatcher.LeftLeft) {
          diagColumn -= 2;
        }
        else if (arrow is VsCodeActionMatcher.Left) {
          diagColumn -= 1;
        }
        else {
          break;
        }
      } while (diagColumn >= 1);

      // Keep the backwards diagonal when it makes a run of three or more longer than the forwards
      // one.
      if (backwardsDiagLength > 1 &&
        this.pattern[row - 1] == this.word[column - 1] &&
        backwardsDiagLength + 1 > this.diag[(row * stride) + diagColumn]) {
        diagColumn = column;
      }

      backwardsDiagLength = diagColumn == column ? backwardsDiagLength + 1 : 1;

      if (maxMatchColumn is 0) {
        maxMatchColumn = diagColumn;
      }

      row--;
      column = diagColumn - 1;
      positions.Add(column);
    }

    if (wordLen == patternLen) {
      score += 2;
    }

    score -= maxMatchColumn - patternLen;
    positions.Reverse();

    return new ActionMatch(score, this.word.ToRanges(positions));
  }

  /// <summary>
  /// Whether the term's characters occur in order in the text, noting the earliest place of each.
  /// </summary>
  private bool IsPatternInWord(int patternLen, int wordLen) {
    var patternPos = 0;

    for (var wordPos = 0; patternPos < patternLen && wordPos < wordLen; wordPos++) {
      if (this.pattern[patternPos] == this.word[wordPos]) {
        this.minWordMatchPos[patternPos] = wordPos;
        patternPos++;
      }
    }

    return patternPos == patternLen;
  }

  private void FillInMaxWordMatchPos(int patternLen, int wordLen) {
    var patternPos = patternLen - 1;

    for (var wordPos = wordLen - 1; patternPos >= 0 && wordPos >= 0; wordPos--) {
      if (this.pattern[patternPos] == this.word[wordPos]) {
        this.maxWordMatchPos[patternPos] = wordPos;
        patternPos--;
      }
    }
  }

  /// <summary>
  /// The score of matching the term's character at <paramref name="patternPos"/> to the text's at
  /// <paramref name="wordPos"/>, or <see cref="NoScore"/> when they differ.
  /// </summary>
  private int DoScore(int patternPos, int wordPos, bool isNewMatchStart) {
    if (this.pattern[patternPos] != this.word[wordPos]) {
      return VsCodeActionMatcher.NoScore;
    }

    var wordLen = this.word.Length;
    var score = 1;
    var isGapLocation = false;

    if (wordPos == patternPos) {
      // A common prefix.
      score = 7;
    }
    else if (this.IsSeparatorAt(wordPos) && (wordPos is 0 || !this.IsSeparatorAt(wordPos - 1))) {
      score = 5;
    }
    else if (this.IsSeparatorAt(wordPos - 1)) {
      score = 5;
      isGapLocation = true;
    }

    if (!isGapLocation) {
      isGapLocation = this.IsSeparatorAt(wordPos - 1);
    }

    if (patternPos is 0) {
      // The first term character away from the text's start pays for the gap before it.
      if (wordPos > 0) {
        score -= isGapLocation ? 3 : 5;
      }
    }
    else if (isNewMatchStart) {
      score += isGapLocation ? 2 : 0;
    }
    else {
      score += isGapLocation ? 0 : 1;
    }

    // A gap is pretended after the text's last character, so matching it gains nothing unfair.
    if (wordPos + 1 == wordLen) {
      score -= isGapLocation ? 3 : 5;
    }

    return score;
  }

  private bool IsSeparatorAt(int index) =>
    index >= 0 &&
    index < this.word.Length &&
    !CodePointText.IsWordCharacter(this.word[index]);

  private static void Ensure(ref int[] buffer, int length) {
    if (buffer.Length < length) {
      buffer = new int[length];
    }
  }
}
