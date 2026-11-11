using System.Linq;
using DoIt.Actions;
using Xunit;

namespace DoIt.Tests.Actions;

/// <summary>
/// The fzf port against fzf's own test cases, lower-cased as the registry hands it folded text.
/// </summary>
public sealed class FzfActionMatcherTests {
  private readonly FzfActionMatcher matcher = new();

  [Theory]
  // fzf's TestFuzzyMatch (src/algo/algo_test.go), each score as fzf spells it out.
  [InlineData("fbb", "foo bar baz", (16 * 3) + (10 * 2) + (10 * 2) - (3 * 2) - 4, "0+1 4+1 8+1")]
  [InlineData("zshc", "/man1/zshcompctl.1", (16 * 4) + (9 * 2) + (9 * 3), "6+4")]
  [InlineData("zshc", "/.oh-my-zsh/cache", (16 * 4) + (8 * 2) + (8 * 2) - 3 + 9, "8+3 12+1")]
  [InlineData("fbb", "foo/bar/baz", (16 * 3) + (10 * 2) + (9 * 2) - (3 * 2) - 4, "0+1 4+1 8+1")]
  [InlineData("fbb", "foo barbaz", (16 * 3) + (10 * 2) + 10 - (3 * 2) - 3, "0+1 4+1 7+1")]
  [InlineData("o-ba", "foo-bar", (16 * 4) + (8 * 3), "2+4")]
  [InlineData(".vimrc", ".vimrc", (16 * 6) + (10 * (2 + 5)), "0+6")]
  [InlineData("12356", "ab0123 456", (16 * 5) + (4 * 3) - 3 - 1, "3+3 8+2")]
  [InlineData("12356", "abc123 456", (16 * 5) + (7 * 2) + (7 * 2) + 4 - 3 - 1, "3+3 8+2")]
  public void Match_ScoresAndPlacesAsFzfDoes(string term, string text, int score, string ranges) {
    var match = this.matcher.Match(term, text);

    Assert.NotNull(match);
    Assert.Equal(score, match.Score);
    Assert.Equal(ranges, FzfActionMatcherTests.Format(match));
  }

  [Theory]
  [InlineData("foobarbazz", "foobarbaz")]
  [InlineData("fbbx", "foo bar baz")]
  public void Match_FindsNothing_WhenTheTermIsNotASubsequence(string term, string text) =>
    Assert.Null(this.matcher.Match(term, text));

  [Fact]
  public void Match_IndexesCharacters_NeverHalfASurrogatePair() {
    // U+20000, a han character outside the basic plane, written as a surrogate pair.
    const string han = "\U00020000";

    Assert.Equal(
      "0+2 3+1",
      FzfActionMatcherTests.Format(this.matcher.Match($"{han}b", $"{han} bar"))
    );
    Assert.Null(this.matcher.Match("\uDC00", han));
  }

  [Fact]
  public void Match_RewardsAWordStart_AfterAnyBoundaryButAMark() {
    var afterLetter = this.matcher.Match("b", "ab")?.Score;

    // A katakana middle dot, punctuation outside fzf's ASCII lists.
    Assert.True(this.matcher.Match("b", "a\u30FBb")?.Score > afterLetter);
    Assert.Equal(afterLetter, this.matcher.Match("b", "a\u0308b")?.Score);
  }

  private static string Format(ActionMatch? match) =>
    string.Join(" ", match?.Ranges.Select(static range => $"{range.Start}+{range.Length}") ?? []);
}
