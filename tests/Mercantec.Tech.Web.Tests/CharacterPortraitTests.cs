using Bunit;
using Mercantec.Tech.Web.Characters;
using Mercantec.Tech.Web.Components;
using CharacterDetail = Mercantec.Tech.Web.Pages.Characters.Detail;

namespace Mercantec.Tech.Web.Tests;

public sealed class CharacterPortraitTests
{
    [Fact]
    public void A_new_character_automatically_uses_its_catalogue_appearance_without_a_separate_image_list()
    {
        var appearance = new CharacterAppearance("Portræt", "/images/new-character.webp", "En ny karakter");
        var profile = new CharacterProfile("new-character", "Ny karakter", "Underviser", "LUP", false,
            "En ny hjælper", "Beskrivelse", [], [appearance], [], []);
        using var context = new BunitContext();
        var card = context.Render<CharacterCard>(p => p.Add(x => x.Character, profile));
        Assert.Same(appearance, profile.DisplayPortrait);
        Assert.Equal(appearance.Image, card.Find(".character-art img").GetAttribute("src"));
        Assert.Equal(appearance.Alt, card.Find(".character-art img").GetAttribute("alt"));
    }

    [Fact]
    public void A_shared_cutout_overrides_the_default_portrait_without_changing_gallery_appearances()
    {
        var original = new CharacterAppearance("Original", "/original.webp", "Originalt portræt");
        var cutout = new CharacterAppearance("Fritlagt", "/transparent.webp", "Fritlagt portræt");
        var profile = new CharacterProfile("new-character", "Ny karakter", "Underviser", "LUP", false,
            "En ny hjælper", "Beskrivelse", [], [original], [], []) { Portrait = cutout };
        using var context = new BunitContext();
        var card = context.Render<CharacterCard>(p => p.Add(x => x.Character, profile));
        Assert.Same(cutout, profile.DisplayPortrait);
        Assert.Same(original, Assert.Single(profile.Appearances));
        Assert.Equal(cutout.Image, card.Find(".character-art img").GetAttribute("src"));
    }

    [Theory]
    [InlineData("gummianden")]
    [InlineData("chameleon")]
    [InlineData("whale")]
    public async Task Profiles_use_the_same_transparent_default_portrait_as_the_catalogue(string slug)
    {
        using var api = new NoNetwork();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var profile = CharacterCatalog.Find(slug)!;
        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, slug));
        Assert.EndsWith("/portrait-transparent.webp", profile.DisplayPortrait!.Image);
        Assert.Equal(profile.DisplayPortrait.Image, page.Find(".character-main-art img").GetAttribute("src"));
        page.FindAll(".character-appearance-picker button")[1].Click();
        Assert.Equal(profile.Appearances[1].Image, page.Find(".character-main-art img").GetAttribute("src"));
        page.FindAll(".character-appearance-picker button")[0].Click();
        Assert.Equal(profile.DisplayPortrait.Image, page.Find(".character-main-art img").GetAttribute("src"));
    }

    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Public character portraits must not load private account data.");
    }
}
