using System.IO;
using System.Linq;
using Xunit;

namespace DoIt.Tests.Actions.Benchmark;

/// <summary>
/// The game's labels the search benchmark reads from the install, and how it says it did not run.
/// </summary>
public sealed class SearchCorpusTests {
  [Fact]
  public void Locales_LoadEveryLocale_WithAsManyEntriesAsItsFileDeclares() {
    var corpus = SearchCorpus.Require();

    Assert.Equal(
      [
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
      ],
      corpus.Locales.Keys.Order()
    );

    Assert.All(
      corpus.Locales.Values,
      static locale => Assert.Equal(locale.DeclaredCount, locale.Entries.Count)
    );

    Assert.Equal("Bulldoze", corpus.Locales["en-US"].Entries["Common.ACTION[Bulldoze]"]);
  }

  [Fact]
  public void Quality_HoldsTheActionLikeFamilies_EachLabelWithItsKey() {
    var corpus = SearchCorpus.Require();
    var labels = corpus.Quality("en-US").ToDictionary(static label => label.Key);

    Assert.Equal("Bulldoze", labels["Common.ACTION[Bulldoze]"].Label);
    Assert.Equal(
      "Pause Simulation",
      labels["Options.OPTION[Shortcuts/Pause Simulation/binding]"].Label
    );
    Assert.Equal("Electricity", labels["Infoviews.INFOVIEW[Electricity]"].Label);
    Assert.Equal("Land Value", labels["Infoviews.INFOMODE[LandValueInfomode]"].Label);
    Assert.Equal("Simple Curve", labels["Tools.TOOL_MODE[SimpleCurve]"].Label);
    Assert.Equal("Bulldoze", labels["Editor.TOOL[BulldozeTool]"].Label);
    Assert.Equal("Event Journal", labels["EventJournal.PANEL_TITLE"].Label);
    Assert.Equal("City Economy", labels["Main.TOOLTIP_TITLE_CITY_ECONOMY"].Label);

    // Not actions: a key's name, a setting, a prefab, and a label with a placeholder.
    Assert.DoesNotContain("Options.INPUT_CONTROL[Keyboard.alt]", labels.Keys);
    Assert.DoesNotContain("Options.OPTION[InputSettings.elevationDraggingEnabled]", labels.Keys);
    Assert.DoesNotContain("Assets.NAME[Small Road]", labels.Keys);
    Assert.DoesNotContain("LifePath.DETAIL_PANEL_TITLE", labels.Keys);
  }

  [Fact]
  public void Quality_DescribesALabelByItsSiblingKey_WhenItHasOne() {
    var corpus = SearchCorpus.Require();
    var entries = corpus.Locales["en-US"].Entries;
    var labels = corpus.Quality("en-US").ToDictionary(static label => label.Key);

    Assert.Equal(
      entries["Main.TOOLTIP_DESCRIPTION_CITY_ECONOMY"],
      labels["Main.TOOLTIP_TITLE_CITY_ECONOMY"].Description
    );
    Assert.Equal(
      entries["Infoviews.INFOMODE_TOOLTIP[LandValueInfomode]"],
      labels["Infoviews.INFOMODE[LandValueInfomode]"].Description
    );
    Assert.Equal(
      entries["Options.OPTION_DESCRIPTION[Shortcuts/Pause Simulation/binding]"],
      labels["Options.OPTION[Shortcuts/Pause Simulation/binding]"].Description
    );
    Assert.Null(labels["Common.ACTION[Bulldoze]"].Description);
  }

  [Theory]
  [InlineData(null, "CSII_INSTALLATIONPATH is not set")]
  [InlineData("", "CSII_INSTALLATIONPATH is not set")]
  [InlineData("/nowhere", "no locale archive at /nowhere/Cities2_Data/Content/Game/Locale.cok")]
  public void Load_WithoutTheArchive_SaysInOneLineThatTheBenchmarkDidNotRun(
    string? installPath,
    string reason
  ) {
    var (corpus, why) = SearchCorpus.Load(installPath?.Replace('/', Path.DirectorySeparatorChar));

    Assert.Null(corpus);
    Assert.Equal(
      $"The search benchmark did not run: {reason.Replace('/', Path.DirectorySeparatorChar)}.",
      why
    );
  }
}
