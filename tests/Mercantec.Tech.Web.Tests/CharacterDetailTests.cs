using Bunit;
using Mercantec.Tech.Web.Characters;
using CharacterDetail = Mercantec.Tech.Web.Pages.Characters.Detail;
using CharacterIndex = Mercantec.Tech.Web.Pages.Characters.Index;

namespace Mercantec.Tech.Web.Tests;

public sealed class CharacterDetailTests
{
    [Fact]
    public async Task ChoosingAnAppearanceUpdatesThePortraitAndPressedState()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));

        page.FindAll(".character-appearance-picker button")[2].Click();

        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));
        var selected = Assert.Single(page.FindAll(".character-appearance-picker button[aria-pressed='true']"));
        Assert.Contains("Festtøj", selected.TextContent);
        Assert.Equal(3, page.FindAll(".character-appearance-picker button[aria-pressed='false']").Count);
    }

    [Fact]
    public async Task RaymondArtworkCanBeOpenedAndDownloadedWithoutReplacingTheSelectedPortrait()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.FindAll(".character-appearance-picker button")[2].Click();

        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(6, page.FindAll(".character-scenes figure").Count);
        var artworks = CharacterCatalog.Find("raymond")!.Reactions.Concat(CharacterCatalog.Find("raymond")!.Scenes).ToArray();
        var figures = page.FindAll(".character-artwork-gallery figure");
        Assert.Equal(artworks.Length, figures.Count);
        for (var index = 0; index < artworks.Length; index++)
        {
            var expected = artworks[index];
            var figure = figures[index];
            var image = figure.QuerySelector("img")!;
            var preview = figure.QuerySelector(".character-artwork-preview")!;
            var download = figure.QuerySelector(".character-artwork-download")!;
            var filePath = expected.Image.Split('?')[0];
            Assert.Equal(expected.Preview, image.GetAttribute("src"));
            Assert.Equal(expected.Alt, image.GetAttribute("alt"));
            Assert.StartsWith("/images/characters/raymond/", filePath);
            Assert.EndsWith(".png", filePath);
            Assert.Equal(expected.Image, preview.GetAttribute("href"));
            Assert.Equal("_blank", preview.GetAttribute("target"));
            Assert.Contains("noopener", preview.GetAttribute("rel"));
            Assert.Equal(expected.Image, download.GetAttribute("href"));
            Assert.Equal($"raymond-{Path.GetFileName(filePath)}", download.GetAttribute("download"));
        }
        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Equal(4, page.FindAll(".character-appearance-picker button").Count);
        Assert.Equal(54, page.FindAll(".character-artwork-preview").Select(link => link.GetAttribute("href")).Distinct().Count());
    }

    [Fact]
    public async Task RaymondArtworkDoesNotFollowTheUserToOtherProfiles()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        Assert.Equal(2, page.FindAll(".character-artwork-gallery").Count);

        page.Render(parameters => parameters.Add(p => p.Slug, "lupp"));
        Assert.Equal(2, page.FindAll(".character-artwork-gallery").Count);
        Assert.Empty(page.FindAll(".character-animation-card"));
        Assert.Equal(2, page.FindAll(".character-scenes figure").Count);
        Assert.All(page.FindAll(".character-scenes img"), image => Assert.StartsWith("/images/characters/lupp/scenes/", image.GetAttribute("src")));
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.All(page.FindAll(".character-reactions img"), image => Assert.StartsWith("/images/characters/lupp/", image.GetAttribute("src")));
        Assert.Equal(24, page.FindAll(".character-gallery figure").Count);

        page.Render(parameters => parameters.Add(p => p.Slug, "vatson"));
        Assert.Empty(page.FindAll(".character-gallery"));
        Assert.Equal(2, page.FindAll(".character-scenes figure").Count);
        Assert.All(page.FindAll(".character-scenes img"), image => Assert.StartsWith("/images/characters/vatson/scenes/", image.GetAttribute("src")));
        Assert.Equal(63, page.FindAll(".character-reactions figure").Count);
        Assert.All(page.FindAll(".character-reactions img"), image => Assert.StartsWith("/images/characters/vatson/", image.GetAttribute("src")));

        page.Render(parameters => parameters.Add(p => p.Slug, "raymond"));
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(6, page.FindAll(".character-scenes figure").Count);
        Assert.EndsWith("/neutral.webp", page.Find(".character-main-art img").GetAttribute("src"));
    }

    [Fact]
    public async Task VatsonsLargeGalleryContainsAllExistingEmojisWithPublicOriginalDownloads()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "vatson"));

        Assert.Single(page.FindAll(".character-reactions-large"));
        Assert.Equal(63, page.FindAll(".character-reactions figure").Count);
        Assert.Contains("63 af 63", page.Find(".character-artwork-count").TextContent);
        Assert.All(page.FindAll(".character-reactions figure"), figure =>
        {
            var image = figure.QuerySelector("img")!;
            var preview = figure.QuerySelector(".character-artwork-preview")!;
            var download = figure.QuerySelector(".character-artwork-download")!;
            var source = image.GetAttribute("src")!;
            Assert.StartsWith("/images/characters/vatson/emojis/", source);
            Assert.EndsWith(".png", source);
            Assert.DoesNotContain("ticket", source, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(image.GetAttribute("alt")));
            Assert.Equal(source, preview.GetAttribute("href"));
            Assert.Equal("_blank", preview.GetAttribute("target"));
            Assert.Contains("noopener", preview.GetAttribute("rel"));
            Assert.Equal(source, download.GetAttribute("href"));
            Assert.Equal($"vatson-{Path.GetFileName(source)}", download.GetAttribute("download"));
        });
        Assert.Equal(63, page.FindAll(".character-reactions img").Select(image => image.GetAttribute("src")).Distinct().Count());
        Assert.Contains(page.FindAll(".character-links a"), link => link.GetAttribute("href") == "https://vatek.dk/");
        Assert.Contains(page.FindAll(".character-links a"), link => link.GetAttribute("href") == "https://videnscenterportalen.dk/vat/");
    }

    [Fact]
    public async Task VatsonsEmojiSearchFiltersByMotifAndCanBeClearedOrResetByProfileNavigation()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "vatson"));

        page.Find(".character-artwork-search input").Input("kaffe");
        Assert.Single(page.FindAll(".character-reactions figure"));
        Assert.EndsWith("/kaffe.png", page.Find(".character-reactions img").GetAttribute("src"));
        Assert.Contains("1 af 63", page.Find(".character-artwork-count").TextContent);

        page.Find(".character-artwork-search input").Input("PLC");
        Assert.Equal(2, page.FindAll(".character-reactions figure").Count);

        page.Find(".character-artwork-search input").Input("findes-ikke-her");
        Assert.Empty(page.FindAll(".character-reactions figure"));
        Assert.Contains("Ingen emojis matcher", page.Find(".character-artwork-empty").TextContent);

        page.Find(".character-artwork-search button").Click();
        Assert.Equal(63, page.FindAll(".character-reactions figure").Count);
        Assert.Empty(page.FindAll(".character-artwork-empty"));
        page.Find(".character-artwork-search input").Input("kaffe");

        page.Render(parameters => parameters.Add(p => p.Slug, "raymond"));
        Assert.Single(page.FindAll(".character-artwork-search"));
        Assert.Single(page.FindAll(".character-reactions-large"));
        Assert.Equal("", page.Find(".character-artwork-search input").GetAttribute("value"));
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);

        page.Render(parameters => parameters.Add(p => p.Slug, "vatson"));
        Assert.Equal(63, page.FindAll(".character-reactions figure").Count);
        Assert.Equal("", page.Find(".character-artwork-search input").GetAttribute("value"));
    }

    [Fact]
    public async Task RaymondAnimationsStartOnlyOnRequestAndCanBeStoppedWithoutChangingThePortrait()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.FindAll(".character-appearance-picker button")[2].Click();

        Assert.Equal(3, page.FindAll(".character-animation-card").Count);
        Assert.All(page.FindAll(".character-animation-screen img"), image => Assert.EndsWith(".png", image.GetAttribute("src")));
        Assert.All(page.FindAll(".character-animation-actions button"), button => Assert.Equal("false", button.GetAttribute("aria-pressed")));
        Assert.All(page.FindAll(".character-animation-actions a"), link =>
        {
            Assert.StartsWith("/images/characters/raymond/animations/", link.GetAttribute("href"));
            Assert.EndsWith(".gif", link.GetAttribute("href"));
            Assert.EndsWith(".gif", link.GetAttribute("download"));
        });

        page.Find("button[aria-label='Afspil Forbind tabeller']").Click();
        Assert.EndsWith("/join-tables.gif", page.Find("#animation-join-tables img").GetAttribute("src"));
        Assert.EndsWith("/sort-rows.png", page.Find("#animation-sort-rows img").GetAttribute("src"));
        Assert.Equal("true", page.Find("button[aria-label='Stop Forbind tabeller']").GetAttribute("aria-pressed"));
        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));

        page.Find("button[aria-label='Stop Forbind tabeller']").Click();
        Assert.EndsWith("/join-tables.png", page.Find("#animation-join-tables img").GetAttribute("src"));
        Assert.Equal("false", page.Find("button[aria-label='Afspil Forbind tabeller']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public async Task AnimationPlaybackIsResetWhenLeavingRaymondsProfile()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.Find("button[aria-label='Afspil Find den manglende række']").Click();

        page.Render(parameters => parameters.Add(p => p.Slug, "lupp"));
        Assert.Empty(page.FindAll(".character-animations"));
        Assert.Equal(24, page.FindAll(".character-gallery figure").Count);

        page.Render(parameters => parameters.Add(p => p.Slug, "raymond"));
        Assert.Equal(3, page.FindAll(".character-animation-card").Count);
        Assert.All(page.FindAll(".character-animation-screen img"), image => Assert.EndsWith(".png", image.GetAttribute("src")));
        Assert.Empty(page.FindAll(".character-animation-actions button[aria-pressed='true']"));
    }

    [Fact]
    public async Task MovingBetweenProfilesResetsTheAppearanceWithoutShowingThePreviousPortrait()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.FindAll(".character-appearance-picker button")[3].Click();

        page.Render(parameters => parameters.Add(p => p.Slug, "lupp"));

        Assert.Equal("LUPP", page.Find("h1").TextContent);
        Assert.EndsWith("/lupp-game-design.png", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Equal(2, page.FindAll(".character-appearance-picker button").Count);
        Assert.Equal("Vælg udtryk for LUPP", page.Find(".character-appearance-picker").GetAttribute("aria-label"));

        page.Render(parameters => parameters.Add(p => p.Slug, "raymond"));

        Assert.EndsWith("/neutral.webp", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Contains("Portræt", page.Find(".character-appearance-picker button[aria-pressed='true']").TextContent);
        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        Assert.All(page.FindAll(".character-gallery img"), image => Assert.StartsWith("/images/characters/raymond/infographics/", image.GetAttribute("src")));
    }

    [Theory]
    [InlineData("beaver", "Bæver", "Blå team", "robuste")]
    [InlineData("fox", "Ræv", "Rød team", "etisk")]
    public async Task SecurityTeachersHaveTheirOwnPortraitRoleAndAnimalExplanation(string slug, string name, string team, string explanation)
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, slug));

        Assert.Equal(name, page.Find("h1").TextContent);
        Assert.Contains(team, page.Find(".character-subtitle").TextContent);
        Assert.Equal($"/images/characters/{slug}/portrait.png", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Contains(explanation, page.Find(".character-features").TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(page.FindAll(".character-gallery, .character-status"));
        Assert.Equal(12, page.FindAll(".character-reactions figure").Count);
        var reactions = CharacterCatalog.Find(slug)!.Reactions;
        var figures = page.FindAll(".character-reactions figure");
        for (var index = 0; index < reactions.Count; index++)
        {
            var expected = reactions[index];
            var figure = figures[index];
            var image = figure.QuerySelector("img")!;
            var preview = figure.QuerySelector(".character-artwork-preview")!;
            var download = figure.QuerySelector(".character-artwork-download")!;
            Assert.Equal(expected.Preview, image.GetAttribute("src"));
            Assert.Equal(expected.Alt, image.GetAttribute("alt"));
            Assert.Equal(expected.Image, preview.GetAttribute("href"));
            Assert.Equal(expected.Image, download.GetAttribute("href"));
            Assert.StartsWith($"/images/characters/{slug}/reactions/", expected.Image);
            Assert.EndsWith(".png", expected.Image);
            Assert.EndsWith(".webp", expected.Preview);
            Assert.Equal($"{slug}-{Path.GetFileName(expected.Image)}", download.GetAttribute("download"));
        }
    }

    [Fact]
    public async Task GameDesignIllustrationsAreSeparateFromPortraitsAndRemainLocallyHosted()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "lupp"));

        Assert.Contains("Underviser i Game Design", page.Find(".character-subtitle").TextContent);
        Assert.Equal(2, page.FindAll(".character-appearance-picker button").Count);
        var figures = page.FindAll(".character-gallery figure");
        Assert.Equal(24, figures.Count);
        Assert.Equal(15, figures.Count(figure => figure.QuerySelector("img")!.GetAttribute("src")!.EndsWith(".webp")));
        Assert.All(figures, figure =>
        {
            var img = figure.QuerySelector("img")!;
            var preview = figure.QuerySelector(".character-illustration-preview")!;
            var download = figure.QuerySelector(".character-illustration-download")!;
            var source = img.GetAttribute("src");
            Assert.StartsWith("/images/characters/lupp/", source);
            Assert.False(string.IsNullOrWhiteSpace(img.GetAttribute("alt")));
            Assert.Equal(Path.ChangeExtension(source, ".png"), preview.GetAttribute("href"));
            Assert.Equal(preview.GetAttribute("href"), download.GetAttribute("href"));
            Assert.Equal("_blank", preview.GetAttribute("target"));
            Assert.Contains("noopener", preview.GetAttribute("rel"));
        });
        Assert.Contains(page.FindAll(".character-links a"), link => link.GetAttribute("href") == "/courses/template/game-design");
        Assert.Contains(page.FindAll(".character-links a"), link => link.GetAttribute("href") == "https://app.notion.com/p/Game-Design-3c6123f464aa8158906ae7c7a3643328");

        page.FindAll(".character-appearance-picker button")[1].Click();
        Assert.EndsWith("/lup-fireworks-player.png", page.Find(".character-main-art img").GetAttribute("src"));

        page.Render(parameters => parameters.Add(p => p.Slug, "fox"));
        Assert.Empty(page.FindAll(".character-gallery, .character-appearance-picker"));
        Assert.Equal("/images/characters/fox/portrait.png", page.Find(".character-main-art img").GetAttribute("src"));
    }

    [Fact]
    public async Task Lupps48EmojisUseWebpPreviewsAndPngDownloadsWithoutChangingTheSelectedPortrait()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "lupp"));
        page.FindAll(".character-appearance-picker button")[1].Click();

        var reactions = CharacterCatalog.Find("lupp")!.Reactions;
        Assert.Equal(48, reactions.Count);
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(48, page.FindAll(".character-reactions .character-artwork-download").Select(link => link.GetAttribute("href")).Distinct().Count());
        Assert.Equal(24, page.FindAll(".character-gallery figure").Count);
        Assert.Contains("Game Design", page.Find(".character-reactions").TextContent);
        Assert.Contains("character-reactions-large", page.Find(".character-reactions").ClassList);
        foreach (var (figure, expected) in page.FindAll(".character-reactions figure").Zip(reactions))
        {
            var image = figure.QuerySelector("img")!;
            var preview = figure.QuerySelector(".character-artwork-preview")!;
            var download = figure.QuerySelector(".character-artwork-download")!;
            Assert.Equal(expected.Preview, image.GetAttribute("src"));
            Assert.Equal(expected.Alt, image.GetAttribute("alt"));
            Assert.StartsWith("/images/characters/lupp/reactions/", expected.Image);
            Assert.EndsWith(".webp", expected.Preview);
            Assert.EndsWith(".png", expected.Image);
            Assert.Equal(expected.Image, preview.GetAttribute("href"));
            Assert.Equal(expected.Image, download.GetAttribute("href"));
            Assert.Equal($"lupp-{Path.GetFileName(expected.Image)}", download.GetAttribute("download"));
        }

        page.Find(".character-reactions .character-artwork-search input").Input("Godmorgen");
        Assert.Single(page.FindAll(".character-reactions figure"));
        Assert.Contains("1 af 48", page.Find(".character-reactions .character-artwork-count").TextContent);
        Assert.Equal("/images/characters/lupp/reactions/good-morning.png", page.Find(".character-reactions .character-artwork-download").GetAttribute("href"));
        Assert.EndsWith("/lup-fireworks-player.png", page.Find(".character-main-art img").GetAttribute("src"));

        page.Find(".character-reactions .character-artwork-search input").Input("ingen-match-her-987");
        Assert.Empty(page.FindAll(".character-reactions figure"));
        Assert.Contains("Ingen emojis", page.Find(".character-reactions .character-artwork-empty").TextContent);
        page.Find(".character-reactions .character-artwork-search button").Click();

        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Contains("48 af 48", page.Find(".character-reactions .character-artwork-count").TextContent);
        Assert.EndsWith("/lup-fireworks-player.png", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Equal(24, page.FindAll(".character-gallery figure").Count);
    }

    [Fact]
    public async Task RaymondInfographicsCoverTheSuppliedObjectivesWithAccessibleTextAndOriginalDownloads()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.FindAll(".character-appearance-picker button")[2].Click();
        var illustrations = CharacterCatalog.Find("raymond")!.Illustrations;

        Assert.Equal(Enumerable.Range(1, 24), illustrations.SelectMany(item => item.Objectives).Distinct().Order());
        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        Assert.Contains("16474", page.Find(".character-gallery header").TextContent);
        Assert.DoesNotContain("Game Design", page.Find(".character-gallery header").TextContent);
        foreach (var (figure, item) in page.FindAll(".character-gallery figure").Zip(illustrations))
        {
            Assert.Equal(item.Preview, figure.QuerySelector("img")!.GetAttribute("src"));
            Assert.EndsWith(".webp", item.Preview);
            Assert.Equal(item.Image, figure.QuerySelector(".character-illustration-preview")!.GetAttribute("href"));
            var download = figure.QuerySelector(".character-illustration-download")!;
            Assert.Equal(item.Image, download.GetAttribute("href"));
            Assert.EndsWith(".png", item.Image);
            Assert.Equal($"raymond-{Path.GetFileName(item.Image)}", download.GetAttribute("download"));
            Assert.False(string.IsNullOrWhiteSpace(figure.QuerySelector("img")!.GetAttribute("alt")));
            Assert.Contains(item.Stage!, figure.QuerySelector(".character-illustration-objectives")!.TextContent);
            Assert.All(item.Explanation, paragraph => Assert.Contains(paragraph, figure.TextContent));
            Assert.True(item.Explanation.Count >= 3);
            Assert.NotEmpty(item.Sources);
            foreach (var source in figure.QuerySelectorAll(".character-illustration-sources a"))
            {
                Assert.StartsWith("https://", source.GetAttribute("href"));
                Assert.Equal("_blank", source.GetAttribute("target"));
                Assert.Contains("noopener", source.GetAttribute("rel"));
            }
        }
        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(6, page.FindAll(".character-scenes figure").Count);
        Assert.Equal(3, page.FindAll(".character-animation-card").Count);
    }

    [Fact]
    public async Task InfographicFiltersKeepThePortraitAndResetWhenChangingProfiles()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.FindAll(".character-appearance-picker button")[2].Click();
        page.FindAll(".character-illustration-stages button").Single(button => button.TextContent == "H3").Click();
        Assert.Equal(3, page.FindAll(".character-gallery figure").Count);
        Assert.All(page.FindAll(".character-illustration-objectives"), text => Assert.StartsWith("H3", text.TextContent));
        Assert.Contains("3 af 16", page.Find(".character-illustration-count").TextContent);

        page.Find("#character-illustration-search").Input("findes-ikke-987");
        Assert.Empty(page.FindAll(".character-gallery figure"));
        Assert.Contains("Ingen infografikker", page.Find(".character-illustration-empty").TextContent);
        page.Find(".character-illustration-search button").Click();
        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));

        page.FindAll(".character-illustration-stages button").Single(button => button.TextContent == "H2").Click();
        Assert.Equal(3, page.FindAll(".character-gallery figure").Count);
        page.Render(parameters => parameters.Add(p => p.Slug, "lupp"));
        Assert.Equal(24, page.FindAll(".character-gallery figure").Count);
        Assert.Empty(page.FindAll(".character-illustration-tools, .character-illustration-explanation"));
        Assert.All(page.FindAll(".character-gallery img"), image => Assert.StartsWith("/images/characters/lupp/", image.GetAttribute("src")));
        Assert.Equal(24, page.FindAll(".character-illustration-download").Count);

        page.Render(parameters => parameters.Add(p => p.Slug, "raymond"));
        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        Assert.Equal("", page.Find("#character-illustration-search").GetAttribute("value"));
        Assert.Equal("Alle", page.Find(".character-illustration-stages button[aria-pressed='true']").TextContent);
        Assert.EndsWith("/neutral.webp", page.Find(".character-main-art img").GetAttribute("src"));
    }

    [Fact]
    public async Task OverviewIncludesAllTenCharactersAndUsesStableAnimalRoutes()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterIndex>();

        Assert.Equal(10, page.FindAll(".character-card").Count);
        Assert.Equal(10, CharacterCatalog.All.Select(character => character.Path).Distinct().Count());
        Assert.Equal("Bæver", page.Find("a[href='/characters/beaver'] h2").TextContent);
        Assert.Equal("Ræv", page.Find("a[href='/characters/fox'] h2").TextContent);
        Assert.Equal("Docker", page.Find("a[href='/characters/whale'] h2").TextContent);
        Assert.Equal("Gummianden", page.Find("a[href='/characters/gummianden'] h2").TextContent);
        Assert.Equal("Cyberbien", page.Find("a[href='/characters/bee'] h2").TextContent);
        Assert.Equal("Næsebladet", page.Find("a[href='/characters/bat'] h2").TextContent);
    }

    [Fact]
    public async Task WhaleProfileLinksAllDevNetObjectivesAndKeepsPortraitWhenFiltering()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "whale"));

        Assert.Equal("Docker", page.Find("h1").TextContent);
        Assert.Equal(4, page.FindAll(".character-appearance-picker button").Count);
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(6, page.FindAll(".character-scenes figure").Count);
        Assert.Equal(3, page.FindAll(".character-animation-card").Count);
        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        Assert.Equal(Enumerable.Range(1, 7), CharacterCatalog.Find("whale")!.Illustrations
            .SelectMany(item => item.Objectives).Distinct().Order());
        Assert.Contains(page.FindAll(".character-links a"), link =>
            link.GetAttribute("href") == "https://maalpinde.lup.dk/vuf/#deploy-or-die");

        page.FindAll(".character-appearance-picker button")[2].Click();
        page.Find("#character-illustration-search").Input("rollback");
        Assert.NotEmpty(page.FindAll(".character-gallery figure"));
        Assert.True(page.FindAll(".character-gallery figure").Count < 16);
        Assert.EndsWith("/vaerksted.webp", page.Find(".character-main-art img").GetAttribute("src"));
    }

    [Fact]
    public async Task DuckProfileKeepsTheSelectedDuckWhileFindingAndPlayingDebuggingMaterials()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "gummianden"));

        var appearances = page.FindAll(".character-appearance-picker button");
        Assert.Equal(6, appearances.Count);
        Assert.EndsWith("/lyttemakkeren.webp", page.Find(".character-main-art img").GetAttribute("src"));
        appearances.Single(button => button.TextContent.Contains("Logvagten", StringComparison.Ordinal)).Click();

        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        page.Find("#character-illustration-search").Input("breakpoint");
        Assert.InRange(page.FindAll(".character-gallery figure").Count, 1, 15);
        Assert.Contains(page.FindAll(".character-gallery .character-illustration-explanation"), card =>
            card.QuerySelector("code") is not null);

        var animation = page.FindAll(".character-animation-card")[0];
        var poster = animation.QuerySelector("img")!.GetAttribute("src");
        var download = animation.QuerySelector("a[download]")!.GetAttribute("href");
        Assert.EndsWith(".gif", download);
        Assert.NotEqual(download, poster);
        animation.QuerySelector("button")!.Click();
        Assert.Equal(download, page.Find(".character-animation-card img").GetAttribute("src"));
        page.Find(".character-animation-card button").Click();
        Assert.Equal(poster, page.Find(".character-animation-card img").GetAttribute("src"));
        Assert.EndsWith("/logvagten.webp", page.Find(".character-main-art img").GetAttribute("src"));
    }

    [Fact]
    public async Task UnknownProfileShowsTheReturnLinkAndRemovesPreviousProfileContent()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));

        page.Render(parameters => parameters.Add(p => p.Slug, "unknown"));

        Assert.Equal("Karakteren blev ikke fundet", page.Find("h1").TextContent);
        Assert.Equal("/characters", page.Find("a.action").GetAttribute("href"));
        Assert.Empty(page.FindAll(".character-detail, .character-appearance-picker, .character-main-art img"));
    }

    [Fact]
    public async Task Raymonds48EmojisCanBeSearchedAndClearedWithoutChangingTheSelectedPortrait()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.FindAll(".character-appearance-picker button")[2].Click();

        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(48, page.FindAll(".character-reactions .character-artwork-download").Select(link => link.GetAttribute("href")).Distinct().Count());
        Assert.Single(page.FindAll(".character-reactions .character-artwork-download[href='/images/characters/raymond/reactions/laughing.png?v=097b5eeb']"));
        page.Find(".character-reactions .character-artwork-search input").Input("Tommel op");

        Assert.Single(page.FindAll(".character-reactions figure"));
        Assert.Contains("1 af 48", page.Find(".character-reactions .character-artwork-count").TextContent);
        Assert.Equal("/images/characters/raymond/reactions/thumbs-up.png", page.Find(".character-reactions .character-artwork-download").GetAttribute("href"));
        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));

        page.Find(".character-reactions .character-artwork-search input").Input("ingen-match-her-987");
        Assert.Empty(page.FindAll(".character-reactions figure"));
        Assert.Contains("Ingen emojis", page.Find(".character-reactions .character-artwork-empty").TextContent);
        page.Find(".character-reactions .character-artwork-search button").Click();

        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Contains("48 af 48", page.Find(".character-reactions .character-artwork-count").TextContent);
        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));
    }

    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Character profiles must not request private account data.");
    }
}
