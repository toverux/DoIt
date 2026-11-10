using System;
using System.Linq;
using DoIt.Actions;
using Xunit;

namespace DoIt.Tests.Actions;

public sealed class TextFoldTests {
  [Fact]
  public void Fold_DropsTheAccentsOfLatinLetters_AndMapsEachPositionBack() {
    var folded = TextFold.Fold("Économie");

    Assert.Equal("economie", folded.Text);
    Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7], folded.Map);
  }

  [Fact]
  public void Fold_ReplacesTheLatinLettersThatDoNotDecompose() {
    var folded = TextFold.Fold("Łódź Straße Øre Đak Æble Œuvre");

    Assert.Equal("lodz strasse ore dak aeble oeuvre", folded.Text);
  }

  [Fact]
  public void Fold_MapsBothLettersOfAReplacementToTheLetterTheyReplace() {
    var folded = TextFold.Fold("Straße");

    Assert.Equal([0, 1, 2, 3, 4, 4, 5], folded.Map);
  }

  [Fact]
  public void Fold_TurnsKatakanaIntoHiragana() => Assert.Equal("めにゅー", TextFold.Fold("メニュー").Text);

  [Fact]
  public void Fold_LeavesHiraganaUnchanged_VoicedKanaRecomposed() {
    var folded = TextFold.Fold("ひらがな");

    Assert.Equal("ひらがな", folded.Text);
    Assert.Equal([0, 1, 2, 3], folded.Map);
  }

  [Fact]
  public void Fold_TurnsVoicedKatakanaIntoVoicedHiragana() =>
    Assert.Equal("がいど", TextFold.Fold("ガイド").Text);

  [Fact]
  public void Fold_TurnsFullWidthLatinIntoPlainLatin() =>
    Assert.Equal("pause", TextFold.Fold("Ｐａｕｓｅ").Text);

  [Fact]
  public void Fold_TurnsHalfWidthKatakanaIntoHiragana_AVoicedMarkJoiningTheKanaBeforeIt() {
    var folded = TextFold.Fold("ｶﾞｲﾄﾞ");

    Assert.Equal("がいど", folded.Text);
    Assert.Equal([0, 2, 3], folded.Map);
  }

  [Theory]
  [InlineData("が", "が")]
  [InlineData("Й", "й")]
  [InlineData("Ё", "ё")]
  public void Fold_KeepsTheMarksOfOtherScripts_SoAVoicedOrAccentedLetterStaysDistinct(
    string text,
    string expected
  ) {
    var folded = TextFold.Fold(text);

    Assert.Equal(expected, folded.Text);
    Assert.Equal([0], folded.Map);
  }

  [Fact]
  public void Fold_KeepsHangulSyllablesComposed_OnePositionPerSyllable() {
    var folded = TextFold.Fold("한국어");

    Assert.Equal("한국어", folded.Text);
    Assert.Equal([0, 1, 2], folded.Map);
  }

  [Fact]
  public void Fold_KeepsSpacesAndPunctuation_MappedToTheirOriginalPositions() {
    var folded = TextFold.Fold("Straße, (Ort)!");

    Assert.Equal("strasse, (ort)!", folded.Text);
    Assert.Equal([0, 1, 2, 3, 4, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13], folded.Map);
  }

  [Fact]
  public void Fold_ReadsASurrogatePairAsOneCharacter_BothHalvesMappedToItsStart() {
    var folded = TextFold.Fold("𝐀 😀");

    Assert.Equal("a 😀", folded.Text);
    Assert.Equal([0, 2, 3, 3], folded.Map);
  }

  [Fact]
  public void Fold_KeepsALoneSurrogateAsItIs() =>
    Assert.Equal("a\uD83Db", TextFold.Fold("A\uD83DB").Text);

  // A fact, as theory data travels serialized and a lone surrogate does not survive the trip.
  [Fact]
  public void Fold_KeepsWhatTheFrameworkCannotNormalize_AMarkAfterASurrogateOrANoncharacter() {
    Assert.Equal("\uD83D\u0301", TextFold.Fold("\uD83D\u0301").Text);
    Assert.Equal("\U0001F3D7\uFE0F build", TextFold.Fold("\U0001F3D7\uFE0F Build").Text);
    Assert.Equal("\uFFFE\u0301", TextFold.Fold("\uFFFE\u0301").Text);
  }

  [Theory]
  [InlineData("Économie, Région")]
  [InlineData("Łódź Straße Œuvre")]
  [InlineData("メニュー ガイド ｶﾞｲﾄﾞ ヴ")]
  [InlineData("Ｐａｕｓｅ ㎒ Ⅻ")]
  [InlineData("ЙЁ Пауза")]
  [InlineData("한국어 일시 정지")]
  public void Fold_LeavesFoldedTextUnchanged(string text) {
    var once = TextFold.Fold(text).Text;

    var twice = TextFold.Fold(once);

    Assert.Equal(once, twice.Text);
    Assert.Equal(Enumerable.Range(0, once.Length), twice.Map);
  }

  [Fact]
  public void Fold_AllocatesNothingBeyondItsResult_OnceItsCachesHoldTheText() {
    const string text = "Économie Straße ｶﾞｲﾄﾞ Пауза 한국어 𝐀 😀";

    TextFold.Fold(text);

    var before = GC.GetAllocatedBytesForCurrentThread();
    var folded = TextFold.Fold(text);
    var foldBytes = GC.GetAllocatedBytesForCurrentThread() - before;

    before = GC.GetAllocatedBytesForCurrentThread();
    var resultText = new string('x', folded.Text.Length);
    var resultMap = new int[folded.Map.Length];
    var resultBytes = GC.GetAllocatedBytesForCurrentThread() - before;

    GC.KeepAlive(resultText);
    GC.KeepAlive(resultMap);

    Assert.InRange(foldBytes, 0, resultBytes);
  }
}
