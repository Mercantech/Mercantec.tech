using Bunit;
using Mercantec.Tech.Web.Characters;
using Mercantec.Tech.Web.Public;
using CharacterDetail = Mercantec.Tech.Web.Pages.Characters.Detail;

namespace Mercantec.Tech.Web.Tests;

public sealed class ChameleonCharacterTests
{
    [Fact]
    public async Task MlExpertHasTheCompleteMaterialCollectionAndCraftsmanPersonality()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, "chameleon"));
        var character = CharacterCatalog.Find("chameleon")!;

        Assert.Equal("Kamæleonen", page.Find("h1").TextContent);
        Assert.Contains("Machine Learning", page.Find(".character-subtitle").TextContent);
        Assert.Contains("Italien", page.Find(".character-description").TextContent);
        Assert.Contains("banjo", page.Find(".character-description").TextContent);
        Assert.Contains("ikke automatisk", page.Find(".character-features").TextContent);
        Assert.Contains("En god model tilpasser sig", page.Find(".character-features").TextContent);
        Assert.Equal(4, character.Appearances.Count);
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        Assert.Equal(6, page.FindAll(".character-scenes figure").Count);
        Assert.Equal(3, page.FindAll(".character-animation-card").Count);
        Assert.Contains("Machine Learning", page.Find(".character-animations h2").TextContent);
        Assert.DoesNotContain("databaser", page.Find(".character-animations header").TextContent);
        Assert.DoesNotContain("professoren", page.Find(".character-scenes header").TextContent);
        Assert.Empty(page.FindAll(".character-status"));

        foreach (var (figure, item) in page.FindAll(".character-gallery figure").Zip(character.Illustrations))
        {
            Assert.Equal(item.Preview, figure.QuerySelector("img")!.GetAttribute("src"));
            Assert.EndsWith(".webp", item.Preview);
            Assert.Equal(item.Image, figure.QuerySelector(".character-illustration-download")!.GetAttribute("href"));
            Assert.True(item.Explanation.Count >= 3);
            Assert.NotEmpty(item.Sources);
            Assert.Empty(item.Objectives);
            Assert.DoesNotContain("Målpind", figure.TextContent);
        }
    }

    [Fact]
    public async Task SearchPlaybackAndProfileSwitchKeepUserChoicesWithinTheirProfile()
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, "chameleon"));
        page.FindAll(".character-appearance-picker button")[2].Click();
        page.Find(".character-reactions input[type='search']").Input("banjo");
        Assert.Single(page.FindAll(".character-reactions figure"));
        Assert.EndsWith("/banjo.webp", page.Find(".character-main-art img").GetAttribute("src"));
        page.Find("#character-illustration-search").Input("overfitting");
        Assert.NotEmpty(page.FindAll(".character-gallery figure"));
        Assert.True(page.FindAll(".character-gallery figure").Count < 16);
        var play = page.Find(".character-animation-actions button");
        play.Click();
        Assert.EndsWith(".gif", page.Find(".character-animation-screen img").GetAttribute("src"));
        play.Click();
        Assert.EndsWith(".png", page.Find(".character-animation-screen img").GetAttribute("src"));

        page.Render(p => p.Add(x => x.Slug, "raymond"));
        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        Assert.EndsWith("/neutral.webp", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.All(page.FindAll(".character-animation-screen img"), image => Assert.DoesNotContain("chameleon", image.GetAttribute("src")));
        page.Render(p => p.Add(x => x.Slug, "chameleon"));
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        Assert.All(page.FindAll(".character-animation-screen img"), image => Assert.EndsWith(".png", image.GetAttribute("src")));
    }

    [Theory]
    [InlineData("Machine Learning")]
    [InlineData("machinelearning")]
    [InlineData("maskinlæring")]
    [InlineData("kameleon")]
    [InlineData("Kamæleonen overfitting")]
    [InlineData("Kamæleonen banjo")]
    public void LibrarySearchFindsTheExpertAndHisTeachingMaterials(string query) =>
        Assert.Contains(PublicCatalog.Search(query), hit => hit.Href == "/characters/chameleon");

    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Public character materials must not request private account data.");
    }
}
