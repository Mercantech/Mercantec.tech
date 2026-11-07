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
            Assert.StartsWith("/images/characters/raymond/", source);
            Assert.EndsWith(".png", source);
            Assert.False(string.IsNullOrWhiteSpace(image.GetAttribute("alt")));
            Assert.Equal(source, preview.GetAttribute("href"));
            Assert.Equal("_blank", preview.GetAttribute("target"));
            Assert.Contains("noopener", preview.GetAttribute("rel"));
            Assert.Equal(source, download.GetAttribute("href"));
            Assert.Equal($"raymond-{Path.GetFileName(source)}", download.GetAttribute("download"));
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
        Assert.Empty(page.FindAll(".character-artwork-gallery, .character-gallery"));

        page.Render(parameters => parameters.Add(p => p.Slug, "raymond"));
        Assert.Equal(12, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(4, page.FindAll(".character-scenes figure").Count);
        Assert.EndsWith("/neutral.webp", page.Find(".character-main-art img").GetAttribute("src"));
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
