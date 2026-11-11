using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DoIt.Tests.Actions.Benchmark;

/// <summary>
/// The game's own labels in every locale, which the search benchmark searches, read from the game
/// install at test time: no game text is committed.
/// </summary>
internal sealed class SearchCorpus {
  private const string InstallVariable = "CSII_INSTALLATIONPATH";

  /// <summary>
  /// The families of labels that name something a player does: shortcuts, the actions of the game's
  /// input hints, info views and their modes, tool modes, editor tools, and panel titles.
  /// </summary>
  private static readonly Regex QualityFamilies = new(
    @"^Options\.OPTION\[[^/\]]+/[^/\]]+/[^\]]+\]$|" +
    @"^Common\.ACTION\[|" +
    @"^Infoviews\.(INFOVIEW|INFOMODE)\[|" +
    @"^Tools\.TOOL_MODE\[|" +
    @"^Editor\.TOOL\[|" +
    @"^\w+\.\w*PANEL_TITLE$|^Main\.TOOLTIP_TITLE_"
  );

  private static readonly Regex StressFamily = new(@"^Assets\.NAME\[");

  /// <summary>
  /// The key prefix of a label family, and the one of its descriptions, keyed alike past it.
  /// </summary>
  private static readonly (string Label, string Description)[] DescriptionSiblings = [
    ("Options.OPTION[", "Options.OPTION_DESCRIPTION["),
    ("Infoviews.INFOMODE[", "Infoviews.INFOMODE_TOOLTIP["),
    ("Main.TOOLTIP_TITLE_", "Main.TOOLTIP_DESCRIPTION_")
  ];

  private static readonly Lazy<(SearchCorpus? Corpus, string Why)> Installed =
    new(static () => SearchCorpus.Load(SearchCorpus.ReadInstallPath()));

  // Hot path: every case searches its locale's labels, so each family is read once per locale
  // rather than filtered and sorted from the whole locale on each call.
  private readonly ConcurrentDictionary<(string Locale, Regex Family), List<CorpusLabel>> labels =
    new();

  private SearchCorpus(IReadOnlyDictionary<string, GameLocale> locales) {
    this.Locales = locales;
  }

  /// <summary>
  /// Why the benchmark cannot run on this machine, or <c>null</c> when the game install is there.
  /// </summary>
  internal static string? NotRunReason =>
    SearchCorpus.Installed.Value.Corpus is null ? SearchCorpus.Installed.Value.Why : null;

  internal IReadOnlyDictionary<string, GameLocale> Locales { get; }

  /// <summary>
  /// The corpus of the game install, or a skip of the calling test saying why there is none, so a
  /// machine without the game never passes a benchmark it did not run.
  /// </summary>
  internal static SearchCorpus Require() {
    var (corpus, why) = SearchCorpus.Installed.Value;

    Assert.SkipWhen(corpus is null, why);

    return corpus;
  }

  /// <summary>
  /// The corpus of the install at <paramref name="installPath"/>, or none and why.
  /// </summary>
  internal static (SearchCorpus? Corpus, string Why) Load(string? installPath) {
    if (string.IsNullOrEmpty(installPath)) {
      return (null, SearchCorpus.NotRun($"{SearchCorpus.InstallVariable} is not set"));
    }

    var archive = Path.Combine(installPath, "Cities2_Data", "Content", "Game", "Locale.cok");

    return File.Exists(archive)
      ? (new SearchCorpus(LocaleArchive.Read(archive)), "")
      : (null, SearchCorpus.NotRun($"no locale archive at {archive}"));
  }

  /// <summary>
  /// The action-like labels of <paramref name="locale"/>, which the benchmark judges the ranking
  /// on, in key order.
  /// </summary>
  internal IReadOnlyList<CorpusLabel> Quality(string locale) =>
    this.Labels(locale, SearchCorpus.QualityFamilies);

  /// <summary>
  /// The prefab names of <paramref name="locale"/>, which the benchmark times the search on alone,
  /// in key order.
  /// </summary>
  internal IReadOnlyList<CorpusLabel> Stress(string locale) =>
    this.Labels(locale, SearchCorpus.StressFamily);

  /// <summary>
  /// The labels of a family, a label holding a placeholder left out, as no action shows one.
  /// </summary>
  private List<CorpusLabel> Labels(string locale, Regex family) =>
    this.labels.GetOrAdd((locale, family), this.ReadLabels);

  private List<CorpusLabel> ReadLabels((string Locale, Regex Family) family) {
    var entries = this.Locales[family.Locale].Entries;

    return entries
      .Where(entry => family.Family.IsMatch(entry.Key) &&
        entry.Value.Length > 0 &&
        !entry.Value.Contains('{')
      )
      .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
      .Select(entry => new CorpusLabel(
          entry.Key,
          entry.Value,
          SearchCorpus.DescriptionKey(entry.Key) is {} key &&
          entries.TryGetValue(key, out var description)
            ? description
            : null
        )
      )
      .ToList();
  }

  private static string? DescriptionKey(string key) =>
    SearchCorpus.DescriptionSiblings
      .Where(sibling => key.StartsWith(sibling.Label, StringComparison.Ordinal))
      .Select(sibling => $"{sibling.Description}{key[sibling.Label.Length..]}")
      .FirstOrDefault();

  private static string NotRun(string reason) => $"The search benchmark did not run: {reason}.";

  /// <summary>
  /// Read user scope first, then from the process, as Linux has no user scope.
  /// </summary>
  private static string? ReadInstallPath() {
    const string name = SearchCorpus.InstallVariable;

    return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) ??
      Environment.GetEnvironmentVariable(name);
  }
}

/// <summary>
/// A label of the game, keyed by its locale key, with the description the game shows beside it.
/// </summary>
internal sealed record CorpusLabel(string Key, string Label, string? Description);
