using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DoIt.Actions;
using Game.UI.Localization;

namespace DoIt.Tests.Actions.Benchmark;

/// <summary>
/// The checked-in case file, which pins every ranking behaviour the search decided against the
/// game's own labels.
/// </summary>
internal static class SearchCases {
  private static readonly Lazy<IReadOnlyList<SearchCase>> Cases = new(SearchCases.Load);

  internal static IReadOnlyList<SearchCase> All => SearchCases.Cases.Value;

  private static IReadOnlyList<SearchCase> Load() {
    var path = Path.Combine(AppContext.BaseDirectory, "Actions", "Benchmark", "search-cases.json");

    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
      RespectNullableAnnotations = true,
      RespectRequiredConstructorParameters = true,
      UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    return JsonSerializer.Deserialize<SearchCaseFile>(File.ReadAllText(path), options)?.Cases ??
      throw new InvalidDataException($"{path} holds no case file.");
  }

  private sealed record SearchCaseFile(IReadOnlyList<SearchCase> Cases);
}

/// <summary>
/// <para>
/// One query on one locale's labels, and the locale keys of which one must rank first, or none when
/// the query must find nothing.
/// The expected keys hold every label the case cannot tell apart, the same text under two keys.
/// </para>
/// <para>
/// An expected miss is a case the pipeline fails today and why, asserted to fail, so a fix shows.
/// </para>
/// </summary>
internal sealed record SearchCase(
  string Locale,
  string Query,
  IReadOnlyList<string> Expect,
  string Note
) {
  /// <summary>
  /// The locale whose labels a player on another locale finds actions by too.
  /// </summary>
  private const string SourceLocale = "en-US";

  private const string Segment = "bench";

  public string? ExpectedMiss { get; init; }

  /// <summary>
  /// The uses recorded before the search, by locale key.
  /// </summary>
  public IReadOnlyDictionary<string, CaseUse> Uses { get; init; } =
    new Dictionary<string, CaseUse>();

  /// <summary>
  /// The aliases a source would declare, by locale key, as the game declares none.
  /// </summary>
  public IReadOnlyDictionary<string, IReadOnlyList<string>> Aliases { get; init; } =
    new Dictionary<string, IReadOnlyList<string>>();

  /// <summary>
  /// The locale keys of the rows a search over the locale's quality corpus writes, in rank order,
  /// with <paramref name="matcher"/> behind the registry's real pipeline; <paramref name="clock"/>,
  /// when given, runs during the search alone.
  /// </summary>
  internal List<string> Rank(
    SearchCorpus corpus,
    IActionMatcher matcher,
    Stopwatch? clock = null
  ) =>
    this.Search(corpus, matcher, clock)
      .Rows
      .Select(static row => ActionId.Parse(row.Id).Local)
      .ToList();

  /// <summary>
  /// The rows a search over the locale's quality corpus writes, as <see cref="Rank"/> reads them.
  /// </summary>
  internal ActionResults Search(
    SearchCorpus corpus,
    IActionMatcher matcher,
    Stopwatch? clock = null
  ) {
    var english = corpus.Locales[SearchCase.SourceLocale].Entries;
    var useStore = new MemoryUseStore();

    useStore.Save(
      this.Uses.ToDictionary(
        static use => new ActionId(SearchCase.Segment, use.Key).ToString(),
        static use => new ActionUse(
          use.Value.Count,
          RegistryFixture.Now - TimeSpan.FromDays(use.Value.DaysAgo)
        )
      )
    );

    var fixture = new RegistryFixture(
      matcher,
      useStore,
      text => text.id is {} id && english.TryGetValue(id, out var label) ? label : null
    );

    var source = new FakeSource(SearchCase.Segment);

    foreach (var label in corpus.Quality(this.Locale)) {
      source.Descriptors.Add(
        new ActionDescriptor {
          LocalId = label.Key,
          // Keyed for the source-locale title, valued for the fixture's resolver.
          Label = LocalizedString.IdWithFallback(label.Key, label.Label),
          Description = label.Description is {} description
            ? LocalizedString.Value(description)
            : (LocalizedString?) null,
          Aliases = this.Aliases.TryGetValue(label.Key, out var aliases)
            ? aliases.Select(LocalizedString.Value).ToList()
            : []
        }
      );
    }

    fixture.Registry.AddSource(source);
    clock?.Start();

    var results = fixture.Registry.Search(this.Query, sequence: 0);

    clock?.Stop();

    return results;
  }

  /// <summary>
  /// Whether <paramref name="ranked"/>, the keys a search wrote, ranks an expected key first, or is
  /// empty when none is expected.
  /// </summary>
  internal bool IsPassedBy(IReadOnlyList<string> ranked) =>
    this.Expect.Count is 0
      ? ranked.Count is 0
      : ranked.Count > 0 && this.Expect.Contains(ranked[0]);

  public override string ToString() => $"{this.Locale} · {this.Query} · {this.Note}";
}

/// <summary>
/// A use count, and how many days ago the last use was.
/// </summary>
internal sealed record CaseUse(int Count, double DaysAgo);
