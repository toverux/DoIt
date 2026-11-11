using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using DoIt.Actions;
using Game.UI.Localization;
using Xunit;

namespace DoIt.Tests.Actions.Benchmark;

/// <summary>
/// The bake-off between the candidate matchers, each behind the registry's real pipeline: their
/// quality on the case file, and their speed as a multiple of the substring baseline's.
/// Lives only while the choice is open.
/// </summary>
public sealed class SearchBakeOffTests {
  /// <summary>
  /// The most a candidate may cost, as a multiple of the baseline's time on the same run.
  /// </summary>
  private const double SpeedGate = 5;

  /// <summary>
  /// Timed rounds, the fastest one kept, so a busy moment of the machine does not count.
  /// </summary>
  private const int Rounds = 3;

  private static readonly (string Name, Func<IActionMatcher> Create)[] Candidates = [
    ("substring", static () => new SubstringActionMatcher()),
    ("fzf v2", static () => new FzfActionMatcher()),
    ("vs code, weak 1st", static () => new VsCodeActionMatcher())
  ];

  [Fact]
  public void BakeOff_ReportsEachCandidate_AndHoldsItWithinTheSpeedGate() {
    var corpus = SearchCorpus.Require();
    var cases = SearchCases.All;
    var stress = SearchBakeOffTests.StressRegistries(corpus);

    var rankings = SearchBakeOffTests.Candidates
      .Select(candidate => cases.Select(@case => @case.Rank(corpus, candidate.Create())).ToList())
      .ToList();

    var qualityTimes = SearchBakeOffTests.Candidates.Select(static _ => double.MaxValue).ToArray();
    var stressTimes = qualityTimes.ToArray();

    // The rounds interleave the candidates, so a slow stretch of the machine hits them alike.
    for (var round = 0; round < SearchBakeOffTests.Rounds; round++) {
      for (var i = 0; i < SearchBakeOffTests.Candidates.Length; i++) {
        var matcher = SearchBakeOffTests.Candidates[i].Create();
        var clock = new Stopwatch();

        foreach (var @case in cases) {
          @case.Rank(corpus, matcher, clock);
        }

        qualityTimes[i] = Math.Min(qualityTimes[i], clock.Elapsed.TotalMilliseconds);
        stressTimes[i] = Math.Min(
          stressTimes[i],
          SearchBakeOffTests.TimeStress(stress, cases, SearchBakeOffTests.Candidates[i].Create())
        );
      }
    }

    var report = SearchBakeOffTests.Report(cases, rankings, qualityTimes, stressTimes);

    TestContext.Current.TestOutputHelper?.WriteLine(report);

    Assert.All(
      Enumerable.Range(1, SearchBakeOffTests.Candidates.Length - 1),
      i => {
        var name = SearchBakeOffTests.Candidates[i].Name;

        Assert.True(
          qualityTimes[i] / qualityTimes[0] <= SearchBakeOffTests.SpeedGate,
          $"{name} is over the speed gate on the quality corpus."
        );
        Assert.True(
          stressTimes[i] / stressTimes[0] <= SearchBakeOffTests.SpeedGate,
          $"{name} is over the speed gate on the stress set."
        );
      }
    );
  }

  private static string Report(
    IReadOnlyList<SearchCase> cases,
    List<List<List<string>>> rankings,
    double[] qualityTimes,
    double[] stressTimes
  ) {
    var builder = new StringBuilder();
    var positives = cases.Count(static @case => @case.Expect.Count > 0);

    builder.AppendLine(
      $"Bake-off over {cases.Count} cases ({positives} with an expected label), " +
      $"the fastest of {SearchBakeOffTests.Rounds} rounds."
    );
    builder.AppendLine(
      "candidate          | first  | top 3  | miss rank | not found | lost | won | time ×  | stress ×"
    );

    for (var i = 0; i < SearchBakeOffTests.Candidates.Length; i++) {
      var ranked = rankings[i];
      var first = cases.Where((@case, n) => @case.IsPassedBy(ranked[n])).Count();

      var topThree = cases.Where((@case, n) => @case.Expect.Count is 0
          ? ranked[n].Count is 0
          : ranked[n].Take(3).Any(@case.Expect.Contains)
        )
        .Count();

      // The rank of each expected label that placed below first, and the cases that found none.
      var missRanks = cases
        .Select((@case, n) => (@case, Ranked: ranked[n]))
        .Where(static pair => pair.@case.Expect.Count > 0 && !pair.@case.IsPassedBy(pair.Ranked))
        .Select(static pair => pair.Ranked.FindIndex(key => pair.@case.Expect.Contains(key)) + 1)
        .ToList();

      var found = missRanks.Where(static rank => rank > 0).ToList();
      var lost = SearchBakeOffTests.Flips(cases, rankings[0], ranked).Count;
      var won = SearchBakeOffTests.Flips(cases, ranked, rankings[0]).Count;

      builder.AppendLine(
        $"{SearchBakeOffTests.Candidates[i].Name,-18} | {(double) first / cases.Count,6:P1} | " +
        $"{(double) topThree / cases.Count,6:P1} | " +
        $"{(found.Count > 0 ? found.Average() : 0),9:F1} | {missRanks.Count - found.Count,9} | " +
        $"{lost,4} | {won,3} | {qualityTimes[i] / qualityTimes[0],7:F2} | " +
        $"{stressTimes[i] / stressTimes[0],8:F2}"
      );
    }

    builder.AppendLine(
      $"Baseline time: {qualityTimes[0]:F0} ms on the quality corpus, {stressTimes[0]:F0} ms on " +
      "the stress set. Lost: cases the baseline passes and the candidate fails; won: the reverse."
    );

    builder.AppendLine();
    builder.AppendLine("First, by locale:");
    builder.AppendLine(
      $"{"candidate",-18} | " +
      string.Join(
        " | ",
        cases.Select(static @case => @case.Locale).Distinct().Select(static l => $"{l,7}")
      )
    );

    for (var i = 0; i < SearchBakeOffTests.Candidates.Length; i++) {
      var ranked = rankings[i];

      var byLocale = cases
        .Select((@case, n) => (@case.Locale, IsFirst: @case.IsPassedBy(ranked[n])))
        .GroupBy(static pair => pair.Locale)
        .Select(static group =>
          $"{(double) group.Count(static p => p.IsFirst) / group.Count(),7:P0}"
        );

      builder.AppendLine(
        $"{SearchBakeOffTests.Candidates[i].Name,-18} | {string.Join(" | ", byLocale)}"
      );
    }

    for (var i = 1; i < SearchBakeOffTests.Candidates.Length; i++) {
      builder.AppendLine();
      builder.AppendLine($"{SearchBakeOffTests.Candidates[i].Name}, lost:");

      foreach (var n in SearchBakeOffTests.Flips(cases, rankings[0], rankings[i])) {
        builder.AppendLine(
          $"  {cases[n]} → {string.Join(", ", rankings[i][n].Take(3).DefaultIfEmpty("(none)"))}"
        );
      }

      builder.AppendLine($"{SearchBakeOffTests.Candidates[i].Name}, still failing:");

      foreach (var n in Enumerable.Range(0, cases.Count)
        .Where(n =>
          !cases[n].IsPassedBy(rankings[i][n]) && cases[n].IsPassedBy(rankings[0][n]) is false
        )) {
        builder.AppendLine(
          $"  {cases[n]} [{cases[n].ExpectedMiss ?? "unflagged"}] → " +
          string.Join(", ", rankings[i][n].Take(3).DefaultIfEmpty("(none)"))
        );
      }
    }

    return builder.ToString();
  }

  /// <summary>
  /// The cases <paramref name="from"/> passes and <paramref name="to"/> fails.
  /// </summary>
  private static List<int> Flips(
    IReadOnlyList<SearchCase> cases,
    List<List<string>> from,
    List<List<string>> to
  ) =>
    Enumerable.Range(0, cases.Count)
      .Where(n => cases[n].IsPassedBy(from[n]) && !cases[n].IsPassedBy(to[n]))
      .ToList();

  /// <summary>
  /// The prefab names of each locale, as one source each, keyed by locale.
  /// </summary>
  private static Dictionary<string, FakeSource> StressRegistries(SearchCorpus corpus) =>
    corpus.Locales.Keys.ToDictionary(
      static locale => locale,
      locale => {
        var source = new FakeSource("stress");

        source.Descriptors.AddRange(
          corpus.Stress(locale)
            .Select(static label => new ActionDescriptor {
                LocalId = label.Key,
                Label = LocalizedString.Value(label.Label)
              }
            )
        );

        return source;
      }
    );

  /// <summary>
  /// The time every case query takes over its locale's prefab names, in milliseconds.
  /// </summary>
  private static double TimeStress(
    Dictionary<string, FakeSource> sources,
    IReadOnlyList<SearchCase> cases,
    IActionMatcher matcher
  ) {
    var clock = new Stopwatch();

    foreach (var locale in cases.GroupBy(static @case => @case.Locale)) {
      var fixture = new RegistryFixture(matcher);

      fixture.Registry.AddSource(sources[locale.Key]);

      foreach (var @case in locale) {
        clock.Start();
        fixture.Registry.Search(@case.Query, sequence: 0);
        clock.Stop();
      }
    }

    return clock.Elapsed.TotalMilliseconds;
  }
}
