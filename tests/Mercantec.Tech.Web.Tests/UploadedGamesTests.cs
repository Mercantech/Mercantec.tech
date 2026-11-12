using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Avatars;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Demo;
using Mercantec.Tech.Web.Games;
using Mercantec.Tech.Web.Gifts;
using Mercantec.Tech.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class UploadedGamesTests
{
    [Fact]
    public async Task OnlyConfirmedUploadRefreshesTheFreeLuppGateAndTheCollectionHasAClearNextStep()
    {
        using var api = new GameApi();
        await using var context = await Create(api);
        var world = context.Services.GetRequiredService<DemoWorld>();
        await world.BindAvatarProfileAsync(context.Auth.Subject);
        Assert.False(world.CanUnlockAvatar("lupp"));
        var collection = context.Render<AvatarEditor>();
        Assert.True(collection.Find("[data-testid=unlock-lupp]").HasAttribute("disabled"));
        Assert.Contains("Gratis", collection.Find("[data-avatar-id=lupp]").TextContent);
        Assert.Contains(collection.FindAll("a"), link => link.GetAttribute("href") == GamesCatalog.UploadGameProjectPath);

        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(GamesCatalog.GameProjectsPath);
        var page = context.Render<MineProjects>();
        Assert.Equal("Spil", page.Find("nav [aria-current=page]").TextContent);
        await page.InvokeAsync(() => page.FindComponent<InputFile>().Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([new BrowserGame()])));
        await page.Find("button.action").ClickAsync(new());
        page.WaitForAssertion(() => Assert.True(world.CanUnlockAvatar("lupp")));
        Assert.Equal(context.Auth.Subject, api.UploadSubject);
        Assert.Contains("Spillet er uploadet", page.Markup);
        Assert.Contains("spil.html", page.Markup);
        Assert.Equal($"/files/{GameApi.Game.FileId:D}", page.Find(".uploaded-game a").GetAttribute("href"));
        Assert.Contains("Privat", page.Find(".uploaded-game").TextContent);
        Assert.Empty(page.FindAll("iframe"));
        await page.InvokeAsync(() => navigation.NavigateTo(GamesCatalog.ProjectsPath));
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".uploaded-game")));
        Assert.Equal("Alle", page.Find("nav [aria-current=page]").TextContent);
        Assert.Empty(page.FindAll("input[type=file]"));
        collection.WaitForAssertion(() => Assert.False(collection.Find("[data-testid=unlock-lupp]").HasAttribute("disabled")));
        Assert.Contains("Lås op gratis", collection.Find("[data-testid=unlock-lupp]").TextContent);
        Assert.Contains("1 / 1 uploadet spil", collection.Find("[data-avatar-id=lupp]").TextContent);
        var gold = world.Gold;
        Assert.True(await world.TryUnlockAvatarAsync("lupp")); Assert.Equal(gold, world.Gold); Assert.Equal("lupp", world.SelectedAvatarId);
    }

    [Fact]
    public async Task FailedUploadPreservesTitleFileAndRequestIdForSafeRetryWithoutGrantingProgress()
    {
        using var api = new GameApi { Reject = true }; await using var context = await Create(api);
        var page = context.Render<UploadedGames>(); var world = context.Services.GetRequiredService<DemoWorld>();
        await world.BindAvatarProfileAsync(context.Auth.Subject);
        await page.InvokeAsync(() => page.FindComponent<InputFile>().Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([new BrowserGame()])));
        await page.Find("button.action").ClickAsync(new());
        Assert.Contains("startside", page.Find("[role=alert]").TextContent);
        Assert.Equal("spil", page.Find("input[maxlength]").GetAttribute("value"));
        Assert.False(page.Find("button.action").HasAttribute("disabled")); Assert.False(world.CanUnlockAvatar("lupp"));
        var request = api.RequestId; api.Reject = false;
        await page.Find("button.action").ClickAsync(new());
        Assert.Equal(request, api.RequestId); Assert.True(world.CanUnlockAvatar("lupp"));
    }

    [Fact]
    public async Task ACompletedResponseAfterLogoutCannotUpdateOrUnlockTheNextAccount()
    {
        using var api = new GameApi { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var context = await Create(api);
        var client = context.Services.GetRequiredService<UploadedGamesClient>();
        var request = client.UploadAsync(new BrowserGame(), "Spil", Guid.NewGuid(), default);
        await context.Auth.LogoutLocalAsync();
        api.Pending.SetResult(new(HttpStatusCode.Created) { Content = JsonContent.Create(GameApi.Game) });
        await Assert.ThrowsAsync<UploadedGameException>(() => request);
        Assert.False(context.Services.GetRequiredService<DemoWorld>().OwnsAvatar("lupp"));
    }

    [Fact]
    public async Task TeacherPreviewCannotUploadAsAStudentAndMissingProgressStillFailsClosed()
    {
        using var api = new GameApi(); await using var context = await Create(api, preview: true);
        var page = context.Render<UploadedGames>();
        Assert.Contains("Afslut elevvisningen", page.Markup); Assert.Empty(page.FindAll("input[type=file]"));
        var world = context.Services.GetRequiredService<DemoWorld>(); await world.BindAvatarProfileAsync(context.Auth.Subject);
        Assert.False(world.CanUnlockAvatar("lupp")); Assert.Equal(0, api.UploadCalls);
    }

    private static Task<LearningUiTestContext> Create(GameApi api, bool preview = false) => LearningUiTestContext.CreateAsync(api, teacher: preview, preview: preview, configure: services =>
    {
        services.AddSingleton<IAvatarClient>(api); services.AddSingleton<DemoWorld>();
        services.AddSingleton<UploadedGamesClient>(); services.AddSingleton<GiftMailboxClient>();
    });
    private sealed class BrowserGame : IBrowserFile
    {
        private readonly byte[] bytes = Encoding.UTF8.GetBytes("<!doctype html><button>Spil</button>");
        public string Name => "spil.html";
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => bytes.Length;
        public string ContentType => "text/html";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken ct = default) => new MemoryStream(bytes);
    }
    private sealed class GameApi : HttpMessageHandler, IAvatarClient
    {
        public static UploadedGameView Game { get; } = new(Guid.NewGuid(), Guid.NewGuid(), "spil", "spil.html", "index.html", DateTimeOffset.UtcNow);
        private bool uploaded;
        private AvatarProfileDto profile = new("starter", ["starter"], 0, 1, [], false, 0);
        public bool Reject { get; set; }
        public int UploadCalls { get; private set; }
        public Guid RequestId { get; private set; }
        public string? UploadSubject { get; private set; }
        public TaskCompletionSource<HttpResponseMessage>? Pending { get; init; }
        public Task<AvatarProfileDto> GetAsync(CancellationToken ct = default, string? expectedSubject = null) => Task.FromResult(profile with { UnlockProgress = [new("lupp", "uploaded-game", uploaded ? 1 : 0, 1, uploaded)] });
        public async Task<AvatarProfileDto> UnlockAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null)
        { Assert.True(uploaded); Assert.Equal("lupp", avatarId); profile = (await GetAsync(ct)) with { SelectedAvatarId = "lupp", OwnedAvatarIds = ["starter", "lupp"], Revision = 1 }; return profile; }
        public Task<AvatarProfileDto> SelectAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null) => throw new NotImplementedException();
        public Task<AvatarProfileDto> ImportLegacyAsync(AvatarLegacyImportRequest request, CancellationToken ct = default, string? expectedSubject = null) => throw new NotImplementedException();
        public Task<AvatarProfileDto> BuyItemAsync(string itemId, CancellationToken ct = default, string? expectedSubject = null) => throw new NotImplementedException();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.EndsWith("/me/games", request.RequestUri!.AbsolutePath);
            if (request.Method == HttpMethod.Get) return new(HttpStatusCode.OK) { Content = JsonContent.Create(uploaded ? new[] { Game } : []) };
            UploadCalls++; UploadSubject = request.Headers.GetValues("X-LUP-Game-Subject").Single();
            var form = Assert.IsType<MultipartFormDataContent>(request.Content);
            var requestPart = form.Single(part => part.Headers.ContentDisposition!.Name!.Trim('"') == "requestId");
            RequestId = Guid.Parse(await requestPart.ReadAsStringAsync(ct));
            Assert.Contains("Spil", await form.Single(part => part.Headers.ContentDisposition!.Name!.Trim('"') == "file").ReadAsStringAsync(ct));
            if (Pending is not null) return await Pending.Task;
            if (Reject) return new(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { error = "no_web_entrypoint" }) };
            uploaded = true; return new(HttpStatusCode.Created) { Content = JsonContent.Create(Game) };
        }
    }
}
