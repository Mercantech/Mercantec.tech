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

        Assert.Equal(12, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(4, page.FindAll(".character-scenes figure").Count);
        Assert.All(page.FindAll(".character-artwork-gallery figure"), figure =>
        {
            var image = figure.QuerySelector("img")!;
            var preview = figure.QuerySelector(".character-artwork-preview")!;
            var download = figure.QuerySelector(".character-artwork-download")!;
            var source = image.GetAttribute("src")!;
            var filePath = source.Split('?')[0];
            Assert.StartsWith("/images/characters/raymond/", source);
            Assert.EndsWith(".png", filePath);
            Assert.False(string.IsNullOrWhiteSpace(image.GetAttribute("alt")));
            Assert.Equal(source, preview.GetAttribute("href"));
            Assert.Equal("_blank", preview.GetAttribute("target"));
            Assert.Contains("noopener", preview.GetAttribute("rel"));
            Assert.Equal(source, download.GetAttribute("href"));
            Assert.Equal($"raymond-{Path.GetFileName(filePath)}", download.GetAttribute("download"));
        });
        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Equal(4, page.FindAll(".character-appearance-picker button").Count);
        Assert.Equal(16, page.FindAll(".character-artwork-preview").Select(link => link.GetAttribute("href")).Distinct().Count());
    }

    [Fact]
    public async Task RaymondArtworkDoesNotFollowTheUserToOtherProfiles()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        Assert.Equal(2, page.FindAll(".character-artwork-gallery").Count);

        page.Render(parameters => parameters.Add(p => p.Slug, "lupp"));
        Assert.Empty(page.FindAll(".character-artwork-gallery"));
        Assert.Equal(9, page.FindAll(".character-gallery figure").Count);

        page.Render(parameters => parameters.Add(p => p.Slug, "vatson"));
        Assert.Empty(page.FindAll(".character-scenes, .character-gallery"));
        Assert.Equal(63, page.FindAll(".character-reactions figure").Count);
        Assert.All(page.FindAll(".character-reactions img"), image => Assert.StartsWith("/images/characters/vatson/", image.GetAttribute("src")));

        page.Render(parameters => parameters.Add(p => p.Slug, "raymond"));
        Assert.Equal(12, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(4, page.FindAll(".character-scenes figure").Count);
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
        Assert.Empty(page.FindAll(".character-artwork-search, .character-reactions-large"));
        Assert.Equal(12, page.FindAll(".character-reactions figure").Count);

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
        Assert.Equal(9, page.FindAll(".character-gallery figure").Count);

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
        Assert.Empty(page.FindAll(".character-gallery"));
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
    }

    [Fact]
    public async Task GameDesignIllustrationsAreSeparateFromPortraitsAndRemainLocallyHosted()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "lupp"));

        Assert.Contains("Underviser i Game Design", page.Find(".character-subtitle").TextContent);
        Assert.Equal(2, page.FindAll(".character-appearance-picker button").Count);
        Assert.Equal(9, page.FindAll(".character-gallery figure").Count);
        Assert.All(page.FindAll(".character-gallery figure"), figure =>
        {
            var img = figure.QuerySelector("img")!;
            var link = figure.QuerySelector("a")!;
            Assert.StartsWith("/images/characters/lupp/", img.GetAttribute("src"));
            Assert.False(string.IsNullOrWhiteSpace(img.GetAttribute("alt")));
            Assert.Equal(img.GetAttribute("src"), link.GetAttribute("href"));
            Assert.Equal("_blank", link.GetAttribute("target"));
            Assert.Contains("noopener", link.GetAttribute("rel"));
        });
        Assert.Contains(page.FindAll(".character-links a"), link => link.GetAttribute("href") == "/courses/template/game-design");

        page.FindAll(".character-appearance-picker button")[1].Click();
        Assert.EndsWith("/lup-fireworks-player.png", page.Find(".character-main-art img").GetAttribute("src"));

        page.Render(parameters => parameters.Add(p => p.Slug, "fox"));
        Assert.Empty(page.FindAll(".character-gallery, .character-appearance-picker"));
        Assert.Equal("/images/characters/fox/portrait.png", page.Find(".character-main-art img").GetAttribute("src"));
    }

    [Fact]
    public async Task OverviewIncludesAllFiveCharactersAndUsesStableAnimalRoutes()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterIndex>();

        Assert.Equal(5, page.FindAll(".character-card").Count);
        Assert.Equal(5, CharacterCatalog.All.Select(character => character.Path).Distinct().Count());
        Assert.Equal("Bæver", page.Find("a[href='/characters/beaver'] h2").TextContent);
        Assert.Equal("Ræv", page.Find("a[href='/characters/fox'] h2").TextContent);
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

    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Character profiles must not request private account data.");
    }
}
