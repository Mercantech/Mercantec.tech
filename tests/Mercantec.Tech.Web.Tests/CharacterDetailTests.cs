using Bunit;
using CharacterDetail = Mercantec.Tech.Web.Pages.Characters.Detail;

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
    public async Task MovingBetweenProfilesResetsTheAppearanceWithoutShowingThePreviousPortrait()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.FindAll(".character-appearance-picker button")[3].Click();

        page.Render(parameters => parameters.Add(p => p.Slug, "lupp"));

        Assert.Equal("LUPP", page.Find("h1").TextContent);
        Assert.EndsWith("/lup-fireworks-player.png", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Empty(page.FindAll(".character-appearance-picker"));

        page.Render(parameters => parameters.Add(p => p.Slug, "raymond"));

        Assert.EndsWith("/neutral.webp", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Contains("Portræt", page.Find(".character-appearance-picker button[aria-pressed='true']").TextContent);
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
