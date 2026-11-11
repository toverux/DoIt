using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace DoIt.Actions;

/// <summary>
/// Turns a string into the form the search compares, so a query and a label typed or written
/// differently still meet: compatibility-decomposed, lower-cased, accents dropped from Latin
/// letters only, the Latin letters that do not decompose replaced, the consonant clusters a Korean
/// IME composes split into their two consonants, katakana turned into hiragana, and what is left
/// recomposed.
/// </summary>
internal static class TextFold {
  /// <summary>
  /// Each code point's compatibility decomposition, as the framework gives it.
  /// The framework only normalizes whole strings, which loses the position of each character, so
  /// the fold walks the text one code point at a time and asks the framework once per code point.
  /// </summary>
  private static readonly ConcurrentDictionary<int, string> Decompositions = new();

  /// <summary>
  /// The character a base and a mark compose into, or <c>\0</c> when they stay apart, keyed by the
  /// base in the high half and the mark in the low half.
  /// </summary>
  private static readonly ConcurrentDictionary<int, char> Compositions = new();

  /// <summary>
  /// The scratch space every fold on this thread writes into, so a fold allocates only its result.
  /// Mutable as it is created on a thread's first fold: a thread-static initializer only runs on
  /// the first thread.
  /// </summary>
  [ThreadStatic]
  private static Buffer? threadBuffer;

  internal static FoldedText Fold(string text) {
    var buffer = TextFold.threadBuffer ??= new Buffer();
    var isAfterLatinLetter = false;

    buffer.Clear();

    for (var index = 0; index < text.Length; index++) {
      // [PERF] ASCII has no decomposition: lower-cased directly, in place of a lookup in the cache.
      if (text[index] < 0x80) {
        var lower = char.ToLowerInvariant(text[index]);

        buffer.Append(lower, index);
        isAfterLatinLetter = TextFold.IsLatinLetter(lower);

        continue;
      }

      // A Hangul syllable decomposes into letters, not marks, and the fold rejoins marks only, so
      // the syllable is kept whole.
      // A lone surrogate, broken text in a mod's label, has no code point to normalize.
      if (text[index] is >= '가' and <= '힣' ||
        (char.IsSurrogate(text[index]) && !char.IsSurrogatePair(text, index))) {
        buffer.Append(text[index], index);
        isAfterLatinLetter = false;

        continue;
      }

      var start = index;

      var codePoint = char.IsSurrogatePair(text, index)
        ? char.ConvertToUtf32(text[index], text[++index])
        : text[index];

      var decomposition = TextFold.Decompositions.GetOrAdd(
        codePoint,
        static key => TextFold.NormalizeOrKeep(char.ConvertFromUtf32(key), NormalizationForm.FormKD)
      );

      foreach (var decomposed in decomposition) {
        // Lower-cased after the decomposition, which can yield capitals (㎒ gives MHz).
        var character = char.ToLowerInvariant(decomposed);

        if (TextFold.IsMark(character)) {
          if (isAfterLatinLetter) {
            continue;
          }

          if (buffer.Length > 0 && TextFold.TryCompose(buffer.Last, character, out var composed)) {
            buffer.Last = composed;

            continue;
          }
        }
        else {
          isAfterLatinLetter = TextFold.IsLatinLetter(character);
        }

        if (TextFold.ReplaceLetter(character) is {} replacement) {
          foreach (var replacing in replacement) {
            buffer.Append(replacing, start);
          }

          continue;
        }

        buffer.Append(TextFold.ToHiragana(character), start);
      }
    }

    return buffer.ToFoldedText();
  }

  private static bool TryCompose(char character, char mark, out char composed) {
    composed = TextFold.Compositions.GetOrAdd(
      (character << 16) | mark,
      static pair => {
        var pairText = new string([(char) (pair >> 16), (char) pair]);

        return TextFold.NormalizeOrKeep(pairText, NormalizationForm.FormC) is [var single]
          ? single
          : '\0';
      }
    );

    return composed is not '\0';
  }

  /// <summary>
  /// The text in the given normalization form, or as it is where the framework rejects it.
  /// </summary>
  private static string NormalizeOrKeep(string text, NormalizationForm form) {
    try {
      return text.Normalize(form);
    }
    catch (ArgumentException) {
      // CoreCLR rejects a lone surrogate and U+FFFE (run off-engine); the game's Mono keeps both.
      return text;
    }
  }

  /// <summary>
  /// <para>
  /// The Latin letters that hold their accent or ligature in the letter itself, which a
  /// decomposition cannot drop.
  /// </para>
  /// <para>
  /// The Hangul clusters a Korean IME composes from two consonants typed with no vowel, <c>ㄳ</c>
  /// from <c>ㄱ</c> then <c>ㅅ</c>, read as those two initials: decomposed, a cluster is a final
  /// consonant or an archaic initial, which no initial matches.
  /// </para>
  /// </summary>
  private static string? ReplaceLetter(char character) =>
    character switch {
      'ł' => "l",
      'ø' => "o",
      'đ' => "d",
      'ß' => "ss",
      // The game's Mono keeps this capital under invariant lower-casing (run on Mono 6.14).
      'ẞ' => "ss",
      'æ' => "ae",
      'œ' => "oe",
      'ᆪ' => "ᄀᄉ",
      'ᆬ' => "ᄂᄌ",
      'ᆭ' => "ᄂᄒ",
      'ᆰ' => "ᄅᄀ",
      'ᆱ' => "ᄅᄆ",
      'ᆲ' => "ᄅᄇ",
      'ᆳ' => "ᄅᄉ",
      'ᆴ' => "ᄅᄐ",
      'ᆵ' => "ᄅᄑ",
      'ᄚ' => "ᄅᄒ",
      'ᄡ' => "ᄇᄉ",
      _ => null
    };

  /// <summary>
  /// A Japanese IME shows hiragana until the player commits a conversion, and the game writes loan
  /// words in katakana: both kana blocks share one layout, a fixed offset apart.
  /// The katakana with no hiragana twin (the middle dot, the long vowel mark) stay.
  /// </summary>
  private static char ToHiragana(char character) =>
    character is >= 'ァ' and <= 'ヶ' or 'ヽ' or 'ヾ' ? (char) (character - 0x60) : character;

  private static bool IsMark(char character) =>
    CharUnicodeInfo.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark or
      UnicodeCategory.SpacingCombiningMark or
      UnicodeCategory.EnclosingMark;

  /// <summary>
  /// The ASCII letters, lower-cased by every caller, and the letters of Latin-1 Supplement through
  /// Latin Extended-B, whose span holds two symbols, × and ÷.
  /// </summary>
  private static bool IsLatinLetter(char character) =>
    character is >= 'a' and <= 'z' or >= 'À' and <= 'ɏ' and not '×' and not '÷';

  /// <summary>
  /// The folded text being written and its position map, grown to the longest text folded on its
  /// thread and kept.
  /// </summary>
  private sealed class Buffer {
    // Both replaced by larger copies when a fold outgrows them.
    private char[] text = new char[64];

    private int[] map = new int[64];

    internal int Length { get; private set; }

    internal char Last {
      get => this.text[this.Length - 1];
      set => this.text[this.Length - 1] = value;
    }

    internal void Clear() => this.Length = 0;

    internal void Append(char character, int origin) {
      if (this.Length == this.text.Length) {
        Array.Resize(ref this.text, this.Length * 2);
        Array.Resize(ref this.map, this.Length * 2);
      }

      this.text[this.Length] = character;
      this.map[this.Length] = origin;
      this.Length++;
    }

    internal FoldedText ToFoldedText() {
      var origins = new int[this.Length];

      Array.Copy(this.map, origins, this.Length);

      return new FoldedText {
        Text = new string(this.text, 0, this.Length),
        Map = origins
      };
    }
  }
}

/// <summary>
/// A folded string and, for each of its positions, the index in the original string of the
/// character it came from, so a match on the folded text highlights what the player sees.
/// Every position a surrogate pair or a decomposed character gives maps to where it starts.
/// </summary>
internal readonly struct FoldedText {
  internal required string Text { get; init; }

  internal required int[] Map { get; init; }
}
