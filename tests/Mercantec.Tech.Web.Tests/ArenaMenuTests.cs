using System.Net;
using System.Text.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Avatars;
using Mercantec.Tech.Web.Demo;
using Mercantec.Tech.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

public sealed class ArenaMenuTests
{
    [Fact]
    public async Task Guest_can_open_training_choose_a_stage_and_return_without_remounting_the_game()
    {
        using var api = new NoNetworkApi();
        var javascript = new MenuJavascript();
        await using var context = await CreateContext(api, javascript);
        var page = context.Render<Arena>();
        page.WaitForAssertion(() => Assert.Equal(1, javascript.Module.MountCount));

        await Button(page, "Træning").ClickAsync(new MouseEventArgs());
        Assert.Equal("Træning", page.Find(".arena-screen-header h1").TextContent.Trim());
        Assert.Single(page.FindAll("canvas"));
        var skyline = page.FindAll(".arena-stage-card").Single(x => x.TextContent.Contains("Skyøerne"));
        await skyline.ClickAsync(new MouseEventArgs());
        Assert.Equal("true", page.FindAll(".arena-stage-card").Single(x => x.TextContent.Contains("Skyøerne")).GetAttribute("aria-pressed"));
        Assert.False(Button(page, "Start").HasAttribute("disabled"));

        await Button(page, "Tilbage").ClickAsync(new MouseEventArgs());
        Assert.Single(page.FindAll("nav[aria-label='Spilmenu']"));
        Assert.Empty(page.FindAll(".arena-screen"));
        Assert.Equal(1, javascript.Module.MountCount);
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task Guest_roster_keeps_unowned_fighters_locked_without_changing_the_account()
    {
        using var api = new NoNetworkApi();
        await using var context = await CreateContext(api, new MenuJavascript());
        var page = context.Render<Arena>();
        await Button(page, "Kæmpere").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.False(page.FindAll(".arena-fighter").Single(x => x.TextContent.Contains("Din avatar")).HasAttribute("disabled")));
        var fighters = page.FindAll(".arena-fighter");

        Assert.Equal(4, fighters.Count);
        Assert.False(fighters.Single(x => x.TextContent.Contains("Din avatar")).HasAttribute("disabled"));
        Assert.True(fighters.Single(x => x.TextContent.Contains("BIP-01")).HasAttribute("disabled"));
        Assert.True(fighters.Single(x => x.TextContent.Contains("Raymond")).HasAttribute("disabled"));
        var santa = fighters.Single(x => x.TextContent.Contains("Julemanden"));
        Assert.True(santa.HasAttribute("disabled"));
        Assert.Equal("/assets/avatars/santa/face.png", santa.QuerySelector("img")!.GetAttribute("src"));
        Assert.Equal("true", fighters.Single(x => x.TextContent.Contains("Din avatar")).GetAttribute("aria-pressed"));
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task Online_entry_requires_login_and_preserves_the_return_address()
    {
        using var api = new NoNetworkApi();
        await using var context = await CreateContext(api, new MenuJavascript());
        var page = context.Render<Arena>();
        await Button(page, "Spil online").ClickAsync(new MouseEventArgs());

        var login = page.FindAll("a").Single(x => x.TextContent.Trim() == "Log ind");
        Assert.Equal("/login?returnUrl=%2Farena", login.GetAttribute("href"));
        Assert.Empty(page.FindAll(".arena-join"));
        Assert.DoesNotContain(page.FindAll("button"), x => x.TextContent.Trim() == "Opret rum");
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task Settings_are_opt_in_and_sound_and_back_do_not_change_the_game_or_profile()
    {
        using var api = new NoNetworkApi();
        var javascript = new MenuJavascript();
        await using var context = await CreateContext(api, javascript);
        var page = context.Render<Arena>();
        Assert.Empty(page.FindAll(".arena-controls-detail"));
        Assert.DoesNotContain("Din avatar. Dine venner", page.Markup);
        Assert.DoesNotContain("Øvelse gør mester", page.Markup);
        await Button(page, "Indstillinger").ClickAsync(new MouseEventArgs());
        Assert.Single(page.FindAll(".arena-controls-detail"));
        var sound = page.Find(".arena-settings-options [data-arena-sound-toggle]");
        Assert.Equal("true", sound.GetAttribute("aria-pressed"));
        Assert.Equal("Til", sound.QuerySelector("strong")!.TextContent);
        await sound.ClickAsync(new MouseEventArgs());
        Assert.False(javascript.Module.SoundEnabled);
        Assert.Equal("false", page.Find(".arena-settings-options [data-arena-sound-toggle]").GetAttribute("aria-pressed"));
        Assert.Equal("Fra", page.Find(".arena-settings-options [data-arena-sound-toggle] strong").TextContent);
        await Button(page, "Tilbage").ClickAsync(new MouseEventArgs());
        Assert.Single(page.FindAll("nav[aria-label='Spilmenu']"));
        Assert.Empty(page.FindAll(".arena-controls-detail"));
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task Sound_starts_enabled_and_topbar_can_disable_and_reenable_it_without_remounting()
    {
        using var api = new NoNetworkApi();
        var javascript = new MenuJavascript();
        await using var context = await CreateContext(api, javascript);
        var page = context.Render<Arena>();
        page.WaitForAssertion(() => Assert.Equal(1, javascript.Module.MountCount));

        Assert.Equal("./js/arena-client.js?v=6", javascript.ImportPath);
        Assert.Equal(new[] { "soundPreference", "mount" }, javascript.Module.Calls.Take(2));
        Assert.True(javascript.Module.MountedSound);
        var sound = page.Find(".arena-topbar [data-arena-sound-toggle]");
        Assert.Equal("true", sound.GetAttribute("aria-pressed"));
        Assert.Equal("Slå lyd fra", sound.GetAttribute("aria-label"));

        await sound.ClickAsync(new MouseEventArgs());
        Assert.False(javascript.Module.SoundEnabled);
        Assert.Equal("false", page.Find(".arena-topbar [data-arena-sound-toggle]").GetAttribute("aria-pressed"));
        Assert.Equal("Slå lyd til", page.Find(".arena-topbar [data-arena-sound-toggle]").GetAttribute("aria-label"));
        await page.Find(".arena-topbar [data-arena-sound-toggle]").ClickAsync(new MouseEventArgs());
        Assert.True(javascript.Module.SoundEnabled);
        Assert.Equal("true", page.Find(".arena-topbar [data-arena-sound-toggle]").GetAttribute("aria-pressed"));
        Assert.Equal(new[] { false, true }, javascript.Module.SoundChanges);
        Assert.Equal(1, javascript.Module.MountCount);
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task Muted_sound_preference_is_restored_before_the_game_is_remounted()
    {
        var javascript = new MenuJavascript();
        using var firstApi = new NoNetworkApi();
        await using (var firstContext = await CreateContext(firstApi, javascript))
        {
            var firstPage = firstContext.Render<Arena>();
            firstPage.WaitForAssertion(() => Assert.Equal(1, javascript.Module.MountCount));
            await firstPage.Find(".arena-topbar [data-arena-sound-toggle]").ClickAsync(new MouseEventArgs());
            Assert.False(javascript.Module.SoundPreference);
        }

        using var secondApi = new NoNetworkApi();
        await using var secondContext = await CreateContext(secondApi, javascript);
        var secondPage = secondContext.Render<Arena>();
        secondPage.WaitForAssertion(() => Assert.Equal(2, javascript.Module.MountCount));
        Assert.False(javascript.Module.MountedSound);
        Assert.False(javascript.Module.SoundEnabled);
        Assert.Equal("false", secondPage.Find(".arena-topbar [data-arena-sound-toggle]").GetAttribute("aria-pressed"));
        Assert.Equal("Slå lyd til", secondPage.Find(".arena-topbar [data-arena-sound-toggle]").GetAttribute("aria-label"));
        await Button(secondPage, "Indstillinger").ClickAsync(new MouseEventArgs());
        Assert.Equal("Fra", secondPage.Find(".arena-settings-options [data-arena-sound-toggle] strong").TextContent);
        Assert.Empty(firstApi.Requests);
        Assert.Empty(secondApi.Requests);
    }

    [Fact]
    public async Task Missing_artwork_prevents_starting_a_blank_match()
    {
        using var api = new NoNetworkApi();
        await using var context = await CreateContext(api, new MenuJavascript { Module = new MenuModule { Ready = false } });
        var page = context.Render<Arena>();
        page.WaitForAssertion(() => Assert.Contains("Grafikken kunne ikke indlæses", page.Find("[role=alert]").TextContent));
        await Button(page, "Træning").ClickAsync(new MouseEventArgs());
        Assert.True(Button(page, "Start").HasAttribute("disabled"));
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task Training_exposes_real_combat_resources_and_starts_the_CPU_with_a_ready_signature()
    {
        using var api = new NoNetworkApi();
        var javascript = new MenuJavascript();
        await using var context = await CreateContext(api, javascript);
        var page = context.Render<Arena>();
        await Button(page, "Træning").ClickAsync(new MouseEventArgs());
        await Button(page, "Start").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.NotNull(javascript.Module.LastState));
        var state = javascript.Module.LastState!;
        Assert.Equal("countdown", state.Phase);
        var cpu = state.Fighters.Single(x => x.IsBot);
        Assert.Equal(AvatarEconomy.RaymondId, cpu.AvatarId);
        Assert.Equal(100, cpu.Combat!.Ultimate);
        Assert.Equal(100, cpu.Combat.Energy);
        Assert.Null(cpu.Combat.CapturedBy);
        Assert.Empty(api.Requests);
    }

    [Fact]
    public void Fighter_help_changes_controls_and_resources_when_the_selected_fighter_changes()
    {
        using var context = new BunitContext();
        var help = context.Render<ArenaMoveList>(p => p.Add(x => x.AvatarId, AvatarEconomy.Bip01Id).Add(x => x.Expanded, true));
        Assert.True(help.Find("details").HasAttribute("open"));
        Assert.Equal(8, help.FindAll("dl > div").Count);
        Assert.Contains("Kerneoverload", help.Markup);
        Assert.Contains("Magnetgreb", help.Markup);
        Assert.Contains("Energi", help.Find("p").TextContent);
        help.Render(p => p.Add(x => x.AvatarId, AvatarEconomy.RaymondId));
        Assert.Contains("Relationsgreb", help.Markup);
        Assert.Contains("Den store JOIN", help.Markup);
        Assert.DoesNotContain("Kerneoverload", help.Markup);
        Assert.Contains("Skjold", help.Find("p").TextContent);
        help.Render(p => p.Add(x => x.AvatarId, AvatarEconomy.SantaId));
        Assert.Contains("Gavegreb", help.Markup);
        Assert.Contains("Den store gavefest", help.Markup);
        Assert.Contains("Julemagi", help.Find("p").TextContent);
        Assert.DoesNotContain("Den store JOIN", help.Markup);
        help.Render(p => p.Add(x => x.AvatarId, AvatarEconomy.StarterId));
        Assert.Empty(help.FindAll("details"));
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<Arena> page, string label)
        => page.FindAll("button").Single(x => x.TextContent.Trim().TrimStart('‹').Trim() == label);

    private static async Task<BunitContext> CreateContext(NoNetworkApi api, MenuJavascript javascript)
    {
        var context = new BunitContext();
        context.Services.AddOptions();
        context.Services.AddSingleton<IJSRuntime>(javascript);
        context.Services.AddSingleton<TokenStore>();
        context.Services.AddSingleton<AuthService>();
        context.Services.AddSingleton<IAvatarClient>(new StarterAvatars());
        context.Services.AddSingleton<DemoWorld>();
        context.Services.AddSingleton(new HttpClient(api) { BaseAddress = new Uri("https://lup.example/api/") });
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/arena");
        await context.Services.GetRequiredService<AuthService>().InitializeAsync();
        return context;
    }

    private sealed class MenuJavascript : IJSRuntime
    {
        public MenuModule Module { get; init; } = new();
        public string? ImportPath { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier != "import") return ValueTask.FromResult(default(TValue)!);
            ImportPath = (string)args![0]!;
            return ValueTask.FromResult((TValue)(object)Module);
        }
    }
    private sealed class MenuModule : IJSObjectReference
    {
        public bool Ready { get; init; } = true;
        public int MountCount { get; private set; }
        public bool SoundPreference { get; private set; } = true;
        public bool MountedSound { get; private set; }
        public bool SoundEnabled { get; private set; }
        public List<bool> SoundChanges { get; } = [];
        public List<string> Calls { get; } = [];
        public ArenaState? LastState { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(identifier);
            if (identifier == "soundPreference") return ValueTask.FromResult((TValue)(object)SoundPreference);
            if (identifier == "mount")
            {
                MountCount++;
                MountedSound = JsonSerializer.SerializeToElement(args![2]).GetProperty("soundEnabled").GetBoolean();
                SoundEnabled = MountedSound;
                return ValueTask.FromResult((TValue)(object)Ready);
            }
            if (identifier == "pushState") LastState = (ArenaState)args![0]!;
            if (identifier == "sound") { SoundEnabled = SoundPreference = (bool)args![0]!; SoundChanges.Add(SoundEnabled); }
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class StarterAvatars : IAvatarClient
    {
        public Task<AvatarProfileDto> GetAsync(CancellationToken ct = default, string? expectedSubject = null)
            => throw new InvalidOperationException("Guest menus must not request account data.");
        private static Task<AvatarProfileDto> Mutation() => throw new InvalidOperationException("Menu navigation must not mutate profile data.");
        public Task<AvatarProfileDto> UnlockAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null) => Mutation();
        public Task<AvatarProfileDto> SelectAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null) => Mutation();
        public Task<AvatarProfileDto> ImportLegacyAsync(AvatarLegacyImportRequest request, CancellationToken ct = default, string? expectedSubject = null) => Mutation();
        public Task<AvatarProfileDto> BuyItemAsync(string itemId, CancellationToken ct = default, string? expectedSubject = null) => Mutation();
    }
    private sealed class NoNetworkApi : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
