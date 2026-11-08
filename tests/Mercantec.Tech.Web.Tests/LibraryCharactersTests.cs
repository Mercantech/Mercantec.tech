using Bunit;
using Mercantec.Tech.Web.Characters;
using Mercantec.Tech.Web.Demo;
using Mercantec.Tech.Web.Public;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using CharacterDetail = Mercantec.Tech.Web.Pages.Characters.Detail;
using CharacterIndex = Mercantec.Tech.Web.Pages.Characters.Index;
using LibraryPage = Mercantec.Tech.Web.Pages.Library;

namespace Mercantec.Tech.Web.Tests;

public sealed class LibraryCharactersTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Character_category_uses_public_profile_portraits_and_stable_links(bool authenticated)
    {
        using var api = new NoNetwork();
        using var context = await CreateContext(api, authenticated);
        var page = RenderCharacters(context);

        Assert.Contains(page.FindAll(".library-categories button[aria-pressed='true']"),
            button => button.TextContent.Contains("Karakterer", StringComparison.Ordinal));
        Assert.Equal(5, page.FindAll("article.character-card").Count);
        foreach (var character in CharacterCatalog.All)
        {
            var card = page.Find($".character-card.character-{character.Slug}");
            Assert.Equal(character.Path, card.QuerySelector("a.character-card-profile")!.GetAttribute("href"));
            Assert.Equal(character.Name, card.QuerySelector("h3")!.TextContent);
            Assert.Contains(character.Role, card.TextContent);
            Assert.Equal(character.Appearances[0].Image, card.QuerySelector(".character-art img")!.GetAttribute("src"));
            Assert.False(string.IsNullOrWhiteSpace(card.QuerySelector(".character-art img")!.GetAttribute("alt")));
        }
        Assert.Empty(page.FindAll(".character-card a a"));
        Assert.Equal(authenticated, page.FindAll(".library-examples").Count == 1);
    }

    [Fact]
    public async Task Material_shortcuts_show_available_media_and_land_on_the_profile_galleries()
    {
        using var api = new NoNetwork();
        using var context = await CreateContext(api);
        var page = RenderCharacters(context);
        var expected = new[]
        {
            ("vatson", "character-reactions-title", "63 emojis"),
            ("raymond", "character-reactions-title", "48 emojis"),
            ("beaver", "character-reactions-title", "12 emojis"),
            ("fox", "character-reactions-title", "12 emojis"),
            ("lupp", "character-reactions-title", "48 emojis"),
            ("raymond", "character-scenes-title", "4 miljøer"),
            ("raymond", "character-animations-title", "3 GIF-animationer"),
            ("raymond", "character-gallery-title", "16 infografikker"),
            ("lupp", "character-gallery-title", "9 illustrationer")
        };
        foreach (var (slug, section, label) in expected)
        {
            var link = page.Find($".character-card-materials a[href='/characters/{slug}#{section}']");
            Assert.Contains(label, link.TextContent);
            Assert.StartsWith($"/images/characters/{slug}/", link.QuerySelector("img")!.GetAttribute("src"));
            var profile = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, slug));
            Assert.Single(profile.FindAll("#" + section));
        }
        Assert.Equal(2, page.FindAll(".character-beaver .character-card-materials, .character-fox .character-card-materials").Count);
        Assert.DoesNotContain(page.FindAll(".character-card-materials img"),
            image => image.GetAttribute("src")!.EndsWith(".gif", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_keeps_character_portraits_and_clear_returns_to_the_selected_category()
    {
        using var api = new NoNetwork();
        using var context = await CreateContext(api);
        var page = RenderCharacters(context);

        page.Find("#library-search").Input("emojis");
        var results = page.FindAll(".library-search-character");
        Assert.Equal(5, results.Count);
        Assert.All(results, result =>
        {
            var path = result.GetAttribute("href");
            var character = Assert.Single(CharacterCatalog.All, character => character.Path == path);
            Assert.Equal(character.Appearances[0].Image, result.QuerySelector("img")!.GetAttribute("src"));
        });
        page.FindAll(".library-categories button").Single(button => button.TextContent.Contains("Karakterer", StringComparison.Ordinal)).Click();
        Assert.Equal(5, page.FindAll(".library-search-character").Count);
        Assert.Contains("i Karakterer", page.Find(".library-section-heading [role='status']").TextContent);

        page.Find(".library-search button").Click();

        Assert.Equal("", page.Find("#library-search").GetAttribute("value"));
        Assert.Empty(page.FindAll(".library-search-results"));
        Assert.Equal(5, page.FindAll(".character-card").Count);
        Assert.Contains(page.FindAll(".library-categories button[aria-pressed='true']"),
            button => button.TextContent.Contains("Karakterer", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("emojis", "/characters/vatson")]
    [InlineData("emojis", "/characters/raymond")]
    [InlineData("emojis", "/characters/beaver")]
    [InlineData("emojis", "/characters/fox")]
    [InlineData("emojis", "/characters/lupp")]
    [InlineData("LUPP playtest", "/characters/lupp")]
    [InlineData("GIF", "/characters/raymond")]
    [InlineData("illustrationer", "/characters/lupp")]
    [InlineData("infografikker", "/characters/raymond")]
    [InlineData("Raymond normalisering", "/characters/raymond")]
    [InlineData("Raymond ORM", "/characters/raymond")]
    [InlineData("Raymond SQL injection", "/characters/raymond")]
    [InlineData("Raymond kaffepause", "/characters/raymond")]
    [InlineData("Vatson PLC", "/characters/vatson")]
    public void Public_search_finds_media_types_and_named_motifs(string query, string expectedPath)
    {
        var hit = Assert.Single(PublicCatalog.Search(query), hit => hit.Href == expectedPath);
        Assert.Equal("Karakter", hit.Kind);
        Assert.True(Mercantec.Tech.Web.Auth.PublicPageAccess.Allows(hit.Href));
    }

    [Fact]
    public async Task Character_overview_and_each_profile_link_back_to_the_library_category()
    {
        using var api = new NoNetwork();
        using var context = await CreateContext(api);
        var overview = context.Render<CharacterIndex>();
        Assert.Contains(overview.FindAll("a"), link => link.GetAttribute("href") == "/library?category=characters");
        foreach (var character in CharacterCatalog.All)
        {
            var profile = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, character.Slug));
            Assert.Contains(profile.FindAll("a"), link => link.GetAttribute("href") == "/library?category=characters");
        }
    }

    [Theory]
    [InlineData("raymond", "character-animations-title")]
    [InlineData("raymond", "character-gallery-title")]
    [InlineData("vatson", "character-reactions-title")]
    [InlineData("beaver", "character-reactions-title")]
    [InlineData("fox", "character-reactions-title")]
    [InlineData("lupp", "character-reactions-title")]
    [InlineData("lupp", "character-gallery-title")]
    public async Task Gallery_deep_links_scroll_after_render_without_repeating_on_profile_interactions(string slug, string section)
    {
        using var api = new NoNetwork();
        var javascript = new ScrollJavascript();
        using var context = await LearningUiTestContext.CreateAsync(api,
            configure: services => services.AddSingleton<IJSRuntime>(javascript));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/characters/{slug}#{section}");

        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, slug));

        Assert.Equal(new[] { section }, javascript.Sections);
        Assert.Single(page.FindAll("#" + section));
        if (page.FindAll(".character-appearance-picker button") is { Count: > 1 } appearances)
            appearances[1].Click();
        page.Render(p => p.Add(x => x.Slug, slug));
        Assert.Equal(new[] { section }, javascript.Sections);
    }

    [Fact]
    public async Task A_new_gallery_location_scrolls_once_to_the_new_target()
    {
        using var api = new NoNetwork();
        var javascript = new ScrollJavascript();
        using var context = await LearningUiTestContext.CreateAsync(api,
            configure: services => services.AddSingleton<IJSRuntime>(javascript));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/characters/raymond#character-animations-title");
        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, "raymond"));

        navigation.NavigateTo("/characters/raymond#character-reactions-title");
        page.Render(p => p.Add(x => x.Slug, "raymond"));
        page.Render(p => p.Add(x => x.Slug, "raymond"));

        Assert.Equal(new[] { "character-animations-title", "character-reactions-title" }, javascript.Sections);
    }

    [Theory]
    [InlineData("raymond", "#not-a-gallery")]
    [InlineData("beaver", "#character-animations-title")]
    [InlineData("lupp", "#character-animations-title")]
    [InlineData("missing", "#character-animations-title")]
    [InlineData("raymond", "")]
    public async Task Unknown_or_unavailable_gallery_targets_do_not_scroll(string slug, string fragment)
    {
        using var api = new NoNetwork();
        var javascript = new ScrollJavascript();
        using var context = await LearningUiTestContext.CreateAsync(api,
            configure: services => services.AddSingleton<IJSRuntime>(javascript));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/characters/{slug}{fragment}");

        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, slug));
        page.Render(p => p.Add(x => x.Slug, slug));

        Assert.Empty(javascript.Sections);
    }

    private sealed class ScrollJavascript : IJSRuntime
    {
        public List<string> Sections { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (identifier == "lupCharacterUi.scrollToSection")
                Sections.Add(Assert.IsType<string>(Assert.Single(args!)));
            return ValueTask.FromResult(default(TValue)!);
        }
    }

    private static async Task<LearningUiTestContext> CreateContext(NoNetwork api, bool authenticated = false)
    {
        var context = await LearningUiTestContext.CreateAsync(api,
            configure: services => services.AddSingleton<LearningContext>());
        if (!authenticated) await context.Auth.LogoutLocalAsync();
        return context;
    }

    private static IRenderedComponent<LibraryPage> RenderCharacters(LearningUiTestContext context)
    {
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/library?category=characters");
        return context.Render<LibraryPage>();
    }

    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException($"Public character library must not request private data: {request.Method} {request.RequestUri}");
    }
}
