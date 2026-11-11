using System.Linq;
using DoIt.Actions;
using Xunit;

namespace DoIt.Tests.Actions;

/// <summary>
/// The Visual Studio Code port against its own test cases, lower-cased as the registry hands it
/// folded text.
/// </summary>
public sealed class VsCodeActionMatcherTests {
  private readonly VsCodeActionMatcher matcher = new();

  [Theory]
  // Visual Studio Code's fuzzyScore tests (src/vs/base/test/common/filters.test.ts).
  [InlineData("gp", "git: pull", "0+1 5+1")]
  [InlineData("fo", "bar_foo", "4+2")]
  [InlineData("fo", "bar foo", "4+2")]
  [InlineData("fo", "bar.foo", "4+2")]
  [InlineData("bkn", "the_black_knight", "4+1 10+2")]
  [InlineData("bt", "the_black_knight", "4+1 15+1")]
  [InlineData("fob", "foobar", "0+2 3+1")]
  [InlineData("form", "editor.formatonsave", "7+4")]
  [InlineData("myvable", "myvariable", "0+4 7+3")]
  [InlineData("moza", "-moz-animation", "1+3 5+1")]
  [InlineData("tit", "win.tit", "4+3")]
  [InlineData("is", "isvalid", "0+2")]
  public void Match_PlacesAsVisualStudioCodeDoes(string term, string text, string ranges) =>
    Assert.Equal(ranges, VsCodeActionMatcherTests.Format(this.matcher.Match(term, text)));

  [Theory]
  // Its first-match-can-be-weak tests: a first character inside a word.
  [InlineData("tor", "constructor", "8+3")]
  [InlineData("ur", "constructor", "6+1 10+1")]
  public void Match_LetsTheFirstCharacterLandInsideAWord(string term, string text, string ranges) =>
    Assert.Equal(ranges, VsCodeActionMatcherTests.Format(this.matcher.Match(term, text)));

  [Theory]
  [InlineData("fobz", "foobar")]
  [InlineData("bax", "foobar")]
  public void Match_FindsNothing_WhenTheTermIsNotASubsequence(string term, string text) =>
    Assert.Null(this.matcher.Match(term, text));

  [Fact]
  public void Match_ScoresAMatchAfterASeparatorHighest() {
    string[] texts = [
      "css.lint.importstatement",
      "css.colordecorators.enable",
      "workbench.quickopen.closeonfocusout"
    ];

    var scores = texts.Select(text => this.matcher.Match("close", text)?.Score).ToArray();

    Assert.Equal(scores.Max(), scores[2]);
    Assert.True(scores[2] > scores[0] && scores[2] > scores[1]);
  }

  [Fact]
  public void Match_IndexesCharacters_NeverHalfASurrogatePair() {
    // U+20000, a han character outside the basic plane, written as a surrogate pair.
    const string han = "\U00020000";

    Assert.Equal(
      "0+2 3+1",
      VsCodeActionMatcherTests.Format(this.matcher.Match($"{han}b", $"{han} bar"))
    );
    Assert.Null(this.matcher.Match("\uDC00", han));
  }

  [Fact]
  public void Match_RewardsAWordStart_AfterAnyBoundaryButAMark() {
    // Each text one character longer than "barfoo", as a skipped character costs a point.
    var insideWord = this.matcher.Match("fo", "barxfoo")?.Score;

    // A katakana middle dot, punctuation outside the upstream's ASCII list.
    Assert.True(this.matcher.Match("fo", "bar\u30FBfoo")?.Score > insideWord);
    Assert.Equal(insideWord, this.matcher.Match("fo", "bar\u0301foo")?.Score);
  }

  [Fact]
  public void Match_ReachesPastTheUpstreamsHundredAndTwentyEightCharacters() {
    var text = $"{new string('a', 200)} end";

    Assert.Equal("201+3", VsCodeActionMatcherTests.Format(this.matcher.Match("end", text)));
  }

  private static string Format(ActionMatch? match) =>
    string.Join(" ", match?.Ranges.Select(static range => $"{range.Start}+{range.Length}") ?? []);
}
