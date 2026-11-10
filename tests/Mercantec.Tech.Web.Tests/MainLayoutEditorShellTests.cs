using System.Net;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Avatars;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Demo;
using Mercantec.Tech.Web.Layout;
using Mercantec.Tech.Web.Notebooks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class MainLayoutEditorShellTests
{
    [Theory]
    [InlineData("/notion/11111111-aaaa-4111-8111-111111111111", true, true)]
    [InlineData("/notion/11111111-aaaa-4111-8111-111111111111?published=true", true, true)]
    [InlineData("/NOTION/11111111-AAAA-4111-8111-111111111111", true, true)]
    [InlineData("/notion", true, false)]
    [InlineData("/notion/connections", true, false)]
    [InlineData("/notion/holds/11111111-aaaa-4111-8111-111111111111", true, false)]
    [InlineData("/notebook/11111111-aaaa-4111-8111-111111111111", true, false)]
    [InlineData("/library", true, false)]
    [InlineData("/notion/11111111-aaaa-4111-8111-111111111111", false, false)]
    public async Task Only_authenticated_document_routes_use_the_edge_to_edge_editor_shell(string route, bool authenticated, bool expected)
    {
        using var api = new LayoutApi();
        using var context = await CreateContext(api);
        if (!authenticated) await context.Auth.LogoutLocalAsync();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(route);
        var layout = context.Render<MainLayout>(parameters => parameters.Add(x => x.Body, (RenderFragment)(builder => builder.AddContent(0, "Dokumentets indhold"))));

        Assert.Equal(expected, layout.Find(".lup").ClassList.Contains("notion-ide-shell"));
        Assert.Contains("Dokumentets indhold", layout.Find("#main-content").TextContent);
    }

    [Fact]
    public async Task Leaving_a_document_restores_the_normal_shell_without_replacing_its_page_content()
    {
        using var api = new LayoutApi();
        using var context = await CreateContext(api);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/notion/11111111-aaaa-4111-8111-111111111111");
        var layout = context.Render<MainLayout>(parameters => parameters.Add(x => x.Body, (RenderFragment)(builder => builder.AddContent(0, "Aktuelt indhold"))));
        Assert.Contains("notion-ide-shell", layout.Find(".lup").ClassList);

        navigation.NavigateTo("/notion/connections");
        layout.WaitForAssertion(() => Assert.DoesNotContain("notion-ide-shell", layout.Find(".lup").ClassList));
        Assert.Contains("Aktuelt indhold", layout.Find("#main-content").TextContent);
    }

    [Fact]
    public async Task Public_navigation_uses_the_home_logo_and_overview_on_desktop_and_mobile()
    {
        using var api = new LayoutApi();
        using var context = await CreateContext(api);
        await context.Auth.LogoutLocalAsync();
        var layout = context.Render<MainLayout>();

        Assert.Equal("/", layout.Find("a.brand").GetAttribute("href"));
        Assert.Equal("LUP – Lokal UndervisningsPlatform", layout.Find("a.brand").GetAttribute("aria-label"));
        Assert.Equal(2, layout.FindAll("header nav a[href='/library']").Count);
        Assert.All(layout.FindAll("header nav a[href='/library']"), link => Assert.Equal("Oversigt", link.TextContent));
        Assert.DoesNotContain(layout.FindAll("header nav a"), link => link.TextContent.Trim() == "Undervisning");
    }

    [Theory]
    [InlineData(false, "/student")]
    [InlineData(true, "/teacher")]
    public async Task Renaming_overview_preserves_authenticated_home_destinations(bool teacher, string home)
    {
        using var api = new LayoutApi();
        using var context = await CreateContext(api, teacher);
        var layout = context.Render<MainLayout>();
        Assert.Equal(home, layout.Find("a.brand").GetAttribute("href"));
        Assert.All(layout.FindAll("header nav a[href='/library']"), link => Assert.Equal("Oversigt", link.TextContent));
    }

    [Fact]
    public async Task Footer_keeps_its_visit_name_during_navigation_and_the_logo_keeps_the_official_identity()
    {
        using var api = new LayoutApi();
        using var context = await CreateContext(api);
        await context.Auth.LogoutLocalAsync();
        var layout = context.Render<MainLayout>();
        var first = layout.Find(".footer-brand").TextContent;
        Assert.Contains(Mercantec.Tech.Web.Branding.LupIdentity.Expansions,
            expansion => first == "LUP · " + expansion.Name);
        Assert.Equal("LUP – Lokal UndervisningsPlatform", layout.Find("a.brand").GetAttribute("aria-label"));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/sources");
        layout.WaitForAssertion(() => Assert.Equal(first, layout.Find(".footer-brand").TextContent));
    }

    private static async Task<LearningUiTestContext> CreateContext(LayoutApi api, bool teacher = true)
    {
        var context = await LearningUiTestContext.CreateAsync(api, teacher: teacher, configure: services =>
        {
            services.AddSingleton<IAvatarClient>(new LayoutAvatarClient());
            services.AddSingleton<DemoWorld>();
            services.AddSingleton<LearningContext>();
            services.AddSingleton<NotebookClient>();
        });
        context.ComponentFactories.AddStub<ThemeDecorations>();
        context.ComponentFactories.AddStub<ThemePicker>();
        context.ComponentFactories.AddStub<AvatarIcon>();
        context.ComponentFactories.AddStub<NotificationBell>();
        return context;
    }

    private sealed class LayoutApi : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }

    private sealed class LayoutAvatarClient : IAvatarClient
    {
        public Task<AvatarProfileDto> GetAsync(CancellationToken ct = default, string? expectedSubject = null)
            => Task.FromResult(new AvatarProfileDto(AvatarEconomy.StarterId, [AvatarEconomy.StarterId],
                AvatarEconomy.InitialGold, AvatarEconomy.InitialLevel, AvatarEconomy.InitialItemIds, true, 0));
        private static Task<AvatarProfileDto> UnexpectedMutation() => throw new InvalidOperationException("Changing page layout must not change avatar data.");
        public Task<AvatarProfileDto> UnlockAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null) => UnexpectedMutation();
        public Task<AvatarProfileDto> SelectAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null) => UnexpectedMutation();
        public Task<AvatarProfileDto> ImportLegacyAsync(AvatarLegacyImportRequest request, CancellationToken ct = default, string? expectedSubject = null) => UnexpectedMutation();
        public Task<AvatarProfileDto> BuyItemAsync(string itemId, CancellationToken ct = default, string? expectedSubject = null) => UnexpectedMutation();
    }
}
