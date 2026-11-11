using System;

namespace DoIt.Actions;

/// <summary>
/// <para>
/// Reads a Hangul syllable by its initial consonant, so a Korean player typing a word's initial
/// consonants alone, <c>ㅂㄷㅈ</c> for 불도저, finds it.
/// A bare consonant folds into its conjoining initial (U+1100 to U+1112), and a syllable of the
/// precomposed block encodes its initial as its offset divided by its 21 vowels times 28 finals.
/// </para>
/// <para>
/// The rule sits in the comparison of one character of the term with one of the text, so a term
/// mixing syllables and consonants (<c>다ㄹ</c>, <c>도로ㄷ</c>) aligns wherever both fit.
/// </para>
/// </summary>
internal static class HangulInitials {
  private const int SyllablesPerInitial = 21 * 28;

  /// <summary>
  /// Whether a term's code point matches a text's: the same code point, or a consonant typed alone
  /// and a syllable opening on it.
  /// </summary>
  internal static bool IsMatch(int termPoint, int textPoint) =>
    termPoint == textPoint ||
    (HangulInitials.IsInitial(termPoint) &&
      textPoint is >= '가' and <= '힣' &&
      termPoint == 'ᄀ' + ((textPoint - '가') / HangulInitials.SyllablesPerInitial));

  /// <summary>
  /// The first place at or after <paramref name="startIndex"/> where <paramref name="text"/> holds
  /// <paramref name="term"/> under <see cref="IsMatch"/>, or -1.
  /// </summary>
  internal static int IndexOf(string text, string term, int startIndex) {
    // [PERF] The framework's ordinal search for a term with no consonant typed alone, in place of
    // the comparison loop for every term.
    if (!HangulInitials.HasInitial(term)) {
      return text.IndexOf(term, startIndex, StringComparison.Ordinal);
    }

    for (var at = startIndex; at <= text.Length - term.Length; at++) {
      var matched = 0;

      while (matched < term.Length && HangulInitials.IsMatch(term[matched], text[at + matched])) {
        matched++;
      }

      if (matched == term.Length) {
        return at;
      }
    }

    return -1;
  }

  /// <summary>
  /// The first place at or after <paramref name="startIndex"/> where <paramref name="text"/> holds
  /// a character <paramref name="character"/> matches under <see cref="IsMatch"/>, or -1.
  /// </summary>
  internal static int IndexOf(string text, char character, int startIndex) {
    // [PERF] The framework's search for a unit no syllable stands for, in place of the comparison
    // loop for every unit.
    if (!HangulInitials.IsInitial(character)) {
      return text.IndexOf(character, startIndex);
    }

    for (var at = startIndex; at < text.Length; at++) {
      if (HangulInitials.IsMatch(character, text[at])) {
        return at;
      }
    }

    return -1;
  }

  /// <summary>
  /// Whether <paramref name="term"/> holds a consonant typed alone.
  /// </summary>
  private static bool HasInitial(string term) {
    foreach (var character in term) {
      if (HangulInitials.IsInitial(character)) {
        return true;
      }
    }

    return false;
  }

  /// <summary>
  /// Whether <paramref name="point"/> is a conjoining initial, a consonant typed alone once folded.
  /// </summary>
  private static bool IsInitial(int point) => point is >= 'ᄀ' and <= 'ᄒ';
}
