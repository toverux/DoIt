using System;
using System.Linq;
using DoIt.Actions;
using Game.UI.Localization;
using Xunit;

namespace DoIt.Tests.Actions;

/// <summary>
/// How a search finds and ranks actions, over the fake <see cref="SubsequenceMatcher"/>.
/// </summary>
public sealed class ActionRegistrySearchTests {
  private readonly MemoryUseStore useStore = new();

  private readonly FakeSource source = new();

  /// <summary>
  /// Built on first use, so a test seeds the use store before the registry loads it.
  /// </summary>
  private RegistryFixture Fixture =>
    field ??= new RegistryFixture(new SubsequenceMatcher(), this.useStore);

  [Fact]
  public void Search_MatchesTermsInAnyOrder_EachOneRequired() {
    this.Add(Descriptors.Call("Pause Simulation"), Descriptors.Call("Economy"));

    Assert.Equal(["Pause Simulation"], this.Labels("simulation pause"));
    Assert.Empty(this.Labels("pause economy"));
  }

  [Theory]
  [InlineData("Économie", "economie", "Title 0+8")]
  [InlineData("Économie", "conom", "Title 1+5")]
  [InlineData("Łódź", "lodz", "Title 0+4")]
  // A mark the fold drops is highlighted with its letter.
  [InlineData("E\u0301conomie", "eco", "Title 0+4")]
  // A letter the fold turns into two is highlighted whole, even when half of it matched.
  [InlineData("Straße", "stras", "Title 0+5")]
  public void Search_FoldsQueryAndLabel_AndHighlightsTheOriginalCharacters(
    string label,
    string query,
    string hit
  ) {
    this.Add(Descriptors.Call(label));

    Assert.Equal([hit], ActionRegistrySearchTests.Hits(this.Single(query)));
  }

  [Fact]
  public void Search_FindsAKatakanaLabelFromHiragana_KeepingDakutenDistinct() {
    this.Add(Descriptors.Call("カメラ"));

    // The provisional hiragana a Japanese IME shows, and the katakana it converts to.
    Assert.Equal(["カメラ"], this.Labels("かめ"));
    Assert.Equal(["カメラ"], this.Labels("カメ"));
    Assert.Empty(this.Labels("が"));
  }

  [Fact]
  public void Search_RanksByTier_PrefixWordStartsOneRunThenScattered() {
    this.Add(
      Descriptors.Call("Dock Zone Exit"),
      Descriptors.Call("Bulldozer"),
      Descriptors.Call("Big Dozer"),
      Descriptors.Call("Dozer")
    );

    Assert.Equal(["Dozer", "Big Dozer", "Bulldozer", "Dock Zone Exit"], this.Labels("doze"));
  }

  [Fact]
  public void Search_RanksAMatchOnWordStartsAboveATighterScatteredOne() {
    this.Add(Descriptors.Call("Abide"), Descriptors.Call("Big Dozer"));

    Assert.Equal(["Big Dozer", "Abide"], this.Labels("bd"));
  }

  [Fact]
  public void Search_OrdersATierByFrecency_UseHalvingEveryTwoWeeks() {
    // 2 × ½^(13/14) ≈ 1.05, then 1, then 2 × ½^(15/14) ≈ 0.95.
    this.Seed("Dozer C", count: 2, daysAgo: 15);
    this.Seed("Dozer B", count: 1, daysAgo: 0);
    this.Seed("Dozer A", count: 2, daysAgo: 13);
    this.Add(Descriptors.Call("Dozer C"), Descriptors.Call("Dozer B"), Descriptors.Call("Dozer A"));

    Assert.Equal(["Dozer A", "Dozer B", "Dozer C"], this.Labels("doz"));
  }

  [Fact]
  public void Search_BreaksAFrecencyTieByTheMatchersScore() {
    // Both scattered, the tighter Dozer scoring higher.
    this.Add(Descriptors.Call("Dazzler"), Descriptors.Call("Dozer"));

    Assert.Equal(["Dozer", "Dazzler"], this.Labels("dzr"));
  }

  [Fact]
  public void Search_BreaksAScoreTieByTheShorterTitle() {
    // Both prefixes, scored alike: the whole label above the longer one opening with it.
    this.Add(Descriptors.Call("Pause Menu"), Descriptors.Call("Pause"));

    Assert.Equal(["Pause", "Pause Menu"], this.Labels("pa"));
  }

  [Fact]
  public void Search_ListsUnusedActionsInCatalogueOrderOnAnEmptyQuery_WhateverTheirLength() {
    this.Add(Descriptors.Call("Pause Menu"), Descriptors.Call("Pause"));

    Assert.Equal(["Pause Menu", "Pause"], this.Labels(""));
  }

  [Fact]
  public void Search_RanksAUsedActionAboveAHigherScoreInItsTier() {
    this.Seed("Dazzler", count: 1, daysAgo: 30);
    this.Add(Descriptors.Call("Dazzler"), Descriptors.Call("Dozer"));

    Assert.Equal(["Dazzler", "Dozer"], this.Labels("dzr"));
  }

  [Fact]
  public void Search_ListsTheCatalogueInFrecencyOrderOnAnEmptyQuery_UpToTheCap() {
    // 5 × ½^5 ≈ 0.16, below a single use today.
    this.Seed("Old", count: 5, daysAgo: 70);
    this.Seed("Recent", count: 1, daysAgo: 0);
    this.Add(
      Descriptors.Call("Unused A"),
      Descriptors.Call("Old"),
      Descriptors.Call("Unused B"),
      Descriptors.Call("Recent")
    );

    Assert.Equal(
      ["Recent", "Old", "Unused A"],
      this.Fixture.Registry.Search(" ", 1, rowCap: 3).Rows.Select(static row => row.Label)
    );
  }

  [Fact]
  public void Search_RanksAnyNameHitAboveAnyDetailHit() {
    this.Add(
      Descriptors.Call("Panel") with {
        Description = LocalizedString.Id("Economy overview")
      },
      Descriptors.Call("Electric Coverage")
    );

    // A scattered title hit above a description's prefix.
    Assert.Equal(["Electric Coverage", "Panel"], this.Labels("eco"));
  }

  [Fact]
  public void Search_FindsAWordOfADescription_WhereTheMatchersAlignmentScatters() {
    this.Add(
      Descriptors.Call("Menu") with {
        Description = LocalizedString.Id("Opens a panel to use the pause menu")
      }
    );

    // The fake matcher takes the p of "Opens" first.
    Assert.Equal(["Description 25+5"], ActionRegistrySearchTests.Hits(this.Single("pause")));
  }

  [Fact]
  public void Search_IgnoresATermScatteredAcrossADescription() {
    this.Add(
      Descriptors.Call("Menu") with {
        Description = LocalizedString.Id("Opens a panel to use the pause menu")
      }
    );

    Assert.Empty(this.Labels("pnpm"));
  }

  [Fact]
  public void Search_FindsAChoiceByAnOptionLabel_InTheDetailGroup_WithTheOptionsKey() {
    this.Add(
      Descriptors.State(
        "Game Speed",
        new ChoiceState {
          Options = [new ActionChoice("1", "Slow"), new ActionChoice("3", "Fast Forward")],
          Read = static () => "1"
        }
      ),
      Descriptors.Call("Menu") with {
        Description = LocalizedString.Id("Fast travel")
      },
      Descriptors.Call("Breakfast")
    );

    // A title's run inside a word, then an option's prefix above a description's.
    Assert.Equal(["Breakfast", "Game Speed", "Menu"], this.Labels("fast"));
    Assert.Equal(["Option 3 8+4"], ActionRegistrySearchTests.Hits(this.Single("ward")));
  }

  [Fact]
  public void Search_RanksATitleHitAboveAnAliasHitOfTheSameTier_HoweverUsed() {
    this.Seed("Bulldozer", count: 10, daysAgo: 0);
    this.Add(
      Descriptors.Call("Bulldozer") with {
        Aliases = [LocalizedString.Id("Demolish")]
      },
      Descriptors.Call("Demolition")
    );

    Assert.Equal(["Demolition", "Bulldozer"], this.Labels("demo"));
  }

  [Fact]
  public void Search_RanksAnAliasHitOfAStrongerTierAboveATitleHit() {
    this.Add(
      Descriptors.Call("Old Demolition"),
      Descriptors.Call("Bulldozer") with {
        Aliases = [LocalizedString.Id("Demolish")]
      }
    );

    Assert.Equal(["Bulldozer", "Old Demolition"], this.Labels("demo"));
  }

  [Fact]
  public void Search_PlacesAMultiTermRowInItsWeakestTermsGroup() {
    this.Add(
      Descriptors.Call("Pause") with {
        Description = LocalizedString.Id("Simple")
      },
      Descriptors.Call("Pause Simulation")
    );

    // Both terms on the title, above a title prefix with a description prefix.
    Assert.Equal(["Pause Simulation", "Pause"], this.Labels("pause sim"));
  }

  [Fact]
  public void Search_PlacesAMultiTermRowInItsWeakestTermsTier() {
    this.Add(Descriptors.Call("Pastis"), Descriptors.Call("Big Park Site"));

    // Both terms on word starts, above a prefix with a scattered hit.
    Assert.Equal(["Big Park Site", "Pastis"], this.Labels("pa si"));
  }

  [Fact]
  public void Search_ReportsADeclaredAliasHit_WithTheAliasText() {
    this.Add(
      Descriptors.Call("Bulldozer") with {
        Aliases = [LocalizedString.Id("Raze"), LocalizedString.Id("Demolish")]
      }
    );

    var row = this.Single("demolish");

    Assert.Equal("Demolish", row.Alias);
    Assert.Equal(["Alias 0+8"], ActionRegistrySearchTests.Hits(row));
    Assert.Null(this.Single("bull").Alias);
  }

  [Fact]
  public void Search_PlacesATypoHitBelowEveryCleanHitOfItsGroup_AboveTheDetailGroup() {
    this.Add(
      Descriptors.Call("Menu") with {
        Description = LocalizedString.Id("Pauze the game")
      },
      Descriptors.Call("Pause"),
      Descriptors.Call("Pause Zone Editor")
    );

    // A title's typo hit, below a scattered title hit, above a description's prefix.
    Assert.Equal(["Pause Zone Editor", "Pause", "Menu"], this.Labels("pauze"));
  }

  [Fact]
  public void Search_RunsTheTypoPassOnlyWhenTheCleanPassFellShortOfTheCap() {
    this.Add(
      Descriptors.Call("Pause"),
      Descriptors.Call("Menu") with {
        Description = LocalizedString.Id("Pauze the game")
      }
    );

    // The title's typo hit would rank above the description's clean hit, so only the gate keeps it
    // out at a cap of one.
    Assert.Equal(["Menu"], this.Labels("pauze", rowCap: 1));
    Assert.Equal(["Pause", "Menu"], this.Labels("pauze", rowCap: 2));
  }

  [Fact]
  public void Search_RunsTheTypoPassOnlyOnTermsOfFourCharactersOrMore() {
    this.Add(Descriptors.Call("Bulldozer"));

    Assert.Empty(this.Labels("bux"));
    Assert.Equal(["Bulldozer"], this.Labels("bulx"));
  }

  [Fact]
  public void Search_FindsATypoInEachOfTwoTerms() {
    this.Add(Descriptors.Call("Bulldozer Tool"));

    var row = this.Single("bulldoser tolo");

    Assert.Equal(["Title 0+6,7+2,10+3"], ActionRegistrySearchTests.Hits(row));
  }

  [Fact]
  public void Search_FindsAnActionByItsSourceLocaleTitle_OnAnotherLocale() {
    this.Fixture.Translations["Economy"] = "Économie";
    this.Add(Descriptors.Call("Economy"));

    var row = this.Single("economy");

    Assert.Equal("Économie", row.Label);
    Assert.Equal("Economy", row.Alias);
    Assert.Equal(["Alias 0+7"], ActionRegistrySearchTests.Hits(row));
  }

  [Fact]
  public void Search_ReportsNoSourceLocaleAlias_WhenItFoldsEqualToTheTitle() {
    this.Fixture.Translations["Cafe"] = "Café";
    this.Add(Descriptors.Call("Cafe"));

    var row = this.Single("cafe");

    Assert.Null(row.Alias);
    Assert.Equal(["Title 0+4"], ActionRegistrySearchTests.Hits(row));
  }

  [Fact]
  public void Search_ReportsNoSourceLocaleAlias_OnTheSourceLocale() {
    this.Add(Descriptors.Call("Economy"));

    var row = this.Single("economy");

    Assert.Null(row.Alias);
    Assert.Equal(["Title 0+7"], ActionRegistrySearchTests.Hits(row));
  }

  [Fact]
  public void Search_ReportsEveryFieldATermHit_InFieldOrder() {
    this.Add(
      Descriptors.Call("Bulldozer") with {
        Aliases = [LocalizedString.Id("Demolish")],
        Description = LocalizedString.Id("Clears buildings")
      }
    );

    var row = this.Single("clears demolish bull");

    Assert.Equal("Demolish", row.Alias);
    Assert.Equal(
      ["Title 0+4", "Alias 0+8", "Description 0+6"],
      ActionRegistrySearchTests.Hits(row)
    );
  }

  [Fact]
  public void Search_ReportsTheFirstAliasHit_WhenTermsHitTwo() {
    this.Add(
      Descriptors.Call("Bulldozer") with {
        Aliases = [LocalizedString.Id("Raze"), LocalizedString.Id("Demolish")]
      }
    );

    var row = this.Single("demolish raze");

    Assert.Equal("Raze", row.Alias);
    Assert.Equal(["Alias 0+4"], ActionRegistrySearchTests.Hits(row));
  }

  private void Seed(string label, int count, int daysAgo) =>
    this.useStore.Saved[$"test/{label}"] =
      new ActionUse(count, RegistryFixture.Now - TimeSpan.FromDays(daysAgo));

  private void Add(params ActionDescriptor[] descriptors) {
    this.source.Descriptors.AddRange(descriptors);
    this.Fixture.Registry.AddSource(this.source);
  }

  private string[] Labels(string query, int rowCap = ActionRegistry.DefaultRowCap) =>
    this.Fixture.Registry.Search(query, 1, rowCap).Rows.Select(static row => row.Label).ToArray();

  private ActionRow Single(string query) =>
    Assert.Single(this.Fixture.Registry.Search(query, 1).Rows);

  /// <summary>
  /// Each hit as its field, its option key when it has one, then its ranges as start+length.
  /// </summary>
  private static string[] Hits(ActionRow row) =>
    row.Hits
      .Select(static hit => string.Join(
          " ",
          [
            hit.Field.ToString(),
            .. hit.OptionKey is {} key ? [key] : (string[]) [],
            string.Join(",", hit.Ranges.Select(static range => $"{range.Start}+{range.Length}"))
          ]
        )
      )
      .ToArray();
}
