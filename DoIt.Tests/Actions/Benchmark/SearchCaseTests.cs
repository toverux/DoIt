using System;
using System.Collections.Generic;
using System.Linq;
using DoIt.Actions;
using Xunit;

namespace DoIt.Tests.Actions.Benchmark;

/// <summary>
/// Every case of the case file, searched through the registry's real pipeline against the shipped
/// matcher over the game's own labels, one row per case named after its locale first.
/// </summary>
public sealed class SearchCaseTests {
  private static readonly string[] Locales = [
    "de-DE",
    "en-US",
    "es-ES",
    "fr-FR",
    "it-IT",
    "ja-JP",
    "ko-KR",
    "pl-PL",
    "pt-BR",
    "ru-RU",
    "zh-HANS",
    "zh-HANT"
  ];

  /// <summary>
  /// Why a case misses today: the shipped matcher cannot make the hit (<c>baseline</c>), the typo
  /// pass forgives one edit per term (<c>two typos</c>), a bare Hangul consonant composes nothing
  /// (<c>jamo</c>), nothing stems a word (<c>inflection</c>), or a word no label or declared alias
  /// holds (<c>synonym</c>).
  /// </summary>
  private static readonly string[] MissReasons = [
    "baseline", "two typos", "jamo", "inflection", "synonym"
  ];

  /// <summary>
  /// A row per case, in the file's order, which groups them by locale; without the game install,
  /// one row skipped with the reason, so the run says once that the benchmark did not run.
  /// </summary>
  public static IEnumerable<TheoryDataRow<int>> Cases() {
    if (SearchCorpus.NotRunReason is {} why) {
      return [
        new TheoryDataRow<int>(-1) {
          Skip = why,
          TestDisplayName = "Search cases"
        }
      ];
    }

    return SearchCases.All.Select(static (@case, index) =>
      new TheoryDataRow<int>(index) {
        TestDisplayName = @case.ToString()
      }.WithTrait("Locale", @case.Locale)
    );
  }

  [Theory]
  [MemberData(nameof(SearchCaseTests.Cases))]
  public void Search_RanksTheExpectedLabelFirst(int index) {
    var corpus = SearchCorpus.Require();
    var @case = SearchCases.All[index];
    var ranked = @case.Rank(corpus, new VsCodeActionMatcher());
    var top = string.Join(", ", ranked.Take(3));

    if (@case.ExpectedMiss is {} reason) {
      Assert.False(
        @case.IsPassedBy(ranked),
        $"Expected a miss ({reason}), but the case passes now: drop its flag. Ranked: {top}."
      );
    }
    else {
      Assert.True(@case.IsPassedBy(ranked), $"Ranked: {top}.");
    }
  }

  [Fact]
  public void Cases_FlagEveryMissWithAKnownReason_AndSayWhetherAHabitWasReviewed() =>
    Assert.All(
      SearchCases.All,
      static @case => {
        Assert.Contains(@case.Locale, SearchCaseTests.Locales);

        if (@case.ExpectedMiss is {} reason) {
          Assert.Contains(reason, SearchCaseTests.MissReasons);
        }

        // A habit case, a query a player of the language would type, waits on a fluent reader.
        if (@case.Note.StartsWith("habit", StringComparison.Ordinal)) {
          Assert.Matches("^habit, (reviewed|unreviewed): ", @case.Note);
        }
      }
    );

  [Fact]
  public void Cases_NameEveryLabelByAKeyOfItsLocale() {
    var corpus = SearchCorpus.Require();

    Assert.All(
      SearchCases.All,
      @case => {
        var keys = corpus.Quality(@case.Locale).Select(static label => label.Key).ToHashSet();

        Assert.All(
          @case.Expect.Concat(@case.Uses.Keys).Concat(@case.Aliases.Keys),
          key => Assert.Contains(key, keys)
        );
      }
    );
  }
}
