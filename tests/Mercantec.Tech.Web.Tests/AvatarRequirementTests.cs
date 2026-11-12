using System.Net;
using System.Net.Http.Json;
using Bunit;
using Bunit.TestDoubles;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Avatars;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Demo;
using Mercantec.Tech.Web.Gifts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

public sealed class AvatarRequirementTests
{
    private const string Requirement = "christmas-gift-recipients";

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task Santa_requires_confirmed_server_progress_even_with_enough_level_and_gold(int recipients, bool canUnlock)
    {
        using var api = new RequirementApi();
        api.Profiles[api.Subject] = Profile(recipients);
        var world = new DemoWorld(new EmptyRuntime(), api);
        await world.BindAvatarProfileAsync(api.Subject);

        Assert.Equal(320, world.Gold);
        Assert.Equal(7, world.CurrentLevel);
        Assert.Equal(canUnlock, world.CanUnlockAvatar(AvatarEconomy.SantaId));
        var progress = Assert.Single(world.GetAvatarUnlockProgress(AvatarEconomy.SantaId));
        Assert.Equal(recipients, progress.Current);
        Assert.Equal(3, progress.Required);
        Assert.Equal(canUnlock, progress.IsMet);
        Assert.True(world.CanUnlockAvatar(AvatarEconomy.Bip01Id));
        Assert.Equal(canUnlock, await world.TryUnlockAvatarAsync(AvatarEconomy.SantaId));
        Assert.Equal(canUnlock ? 1 : 0, api.UnlockCalls);
        Assert.Equal(canUnlock ? 170 : 320, world.Gold);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-requirement")]
    [InlineData("wrong-avatar")]
    [InlineData("wrong-target")]
    [InlineData("not-confirmed")]
    [InlineData("below-target")]
    public async Task Missing_or_mismatched_progress_fails_closed(string scenario)
    {
        using var api = new RequirementApi();
        api.Profiles[api.Subject] = Profile(3) with
        {
            UnlockProgress = scenario switch
            {
                "missing" => null,
                "wrong-requirement" => [new(AvatarEconomy.SantaId, "another-requirement", 3, 3, true)],
                "wrong-avatar" => [new(AvatarEconomy.RaymondId, Requirement, 3, 3, true)],
                "wrong-target" => [new(AvatarEconomy.SantaId, Requirement, 3, 2, true)],
                "not-confirmed" => [new(AvatarEconomy.SantaId, Requirement, 3, 3, false)],
                _ => [new(AvatarEconomy.SantaId, Requirement, 2, 3, true)]
            }
        };
        var world = new DemoWorld(new EmptyRuntime(), api);
        await world.BindAvatarProfileAsync(api.Subject);

        Assert.False(world.CanUnlockAvatar(AvatarEconomy.SantaId));
        Assert.False(await world.TryUnlockAvatarAsync(AvatarEconomy.SantaId));
        Assert.Equal(0, api.UnlockCalls);
        Assert.Equal(320, world.Gold);
        Assert.False(world.OwnsAvatar(AvatarEconomy.SantaId));
    }

    [Fact]
    public async Task Switching_accounts_clears_the_previous_accounts_completed_requirement_before_the_next_response()
    {
        using var api = new RequirementApi();
        api.Profiles["student-a"] = Profile(3);
        var world = new DemoWorld(new EmptyRuntime(), api);
        await world.BindAvatarProfileAsync("student-a");
        Assert.True(world.CanUnlockAvatar(AvatarEconomy.SantaId));
        var delayed = new TaskCompletionSource<AvatarProfileDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.PendingGets["student-b"] = delayed;

        var switching = world.BindAvatarProfileAsync("student-b");
        Assert.False(world.AvatarProfileReady);
        Assert.False(world.CanUnlockAvatar(AvatarEconomy.SantaId));
        Assert.Equal(0, Assert.Single(world.GetAvatarUnlockProgress(AvatarEconomy.SantaId)).Current);
        delayed.SetResult(Profile(0));
        await switching;

        Assert.True(world.AvatarProfileReady);
        Assert.False(world.CanUnlockAvatar(AvatarEconomy.SantaId));
        Assert.Equal(0, Assert.Single(world.GetAvatarUnlockProgress(AvatarEconomy.SantaId)).Current);
    }

    [Fact]
    public async Task A_late_completed_requirement_from_an_old_account_is_ignored()
    {
        using var api = new RequirementApi();
        var delayed = new TaskCompletionSource<AvatarProfileDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.PendingGets["student-a"] = delayed;
        var world = new DemoWorld(new EmptyRuntime(), api);
        var oldRequest = world.BindAvatarProfileAsync("student-a");
        await world.BindAvatarProfileAsync("student-b");
        delayed.SetResult(Profile(3));
        await oldRequest;

        Assert.False(world.CanUnlockAvatar(AvatarEconomy.SantaId));
        Assert.Equal(0, Assert.Single(world.GetAvatarUnlockProgress(AvatarEconomy.SantaId)).Current);
        Assert.Equal(320, world.Gold);
    }

    [Fact]
    public async Task An_existing_owner_can_keep_selecting_Santa_without_rebuying_or_repeating_the_new_requirement()
    {
        using var api = new RequirementApi();
        api.Profiles[api.Subject] = Profile(0) with
        {
            OwnedAvatarIds = [AvatarEconomy.StarterId, AvatarEconomy.SantaId], Gold = 170, UnlockProgress = null
        };
        var world = new DemoWorld(new EmptyRuntime(), api);
        await world.BindAvatarProfileAsync(api.Subject);

        Assert.True(world.OwnsAvatar(AvatarEconomy.SantaId));
        Assert.True(await world.SelectAvatarAsync(AvatarEconomy.SantaId));
        Assert.Equal(AvatarEconomy.SantaId, world.SelectedAvatarId);
        Assert.False(await world.TryUnlockAvatarAsync(AvatarEconomy.SantaId));
        Assert.Equal(0, api.UnlockCalls);
        Assert.Equal(170, world.Gold);
    }

    [Fact]
    public async Task Avatar_collection_link_previews_the_requested_fighter_without_selecting_or_unlocking_it()
    {
        using var api = new RequirementApi { Subject = LearningUiTestContext.AccountId.ToString() };
        api.Profiles[api.Subject] = Profile(0);
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            services.AddSingleton<IAvatarClient>(api);
            services.AddSingleton<DemoWorld>();
            services.AddSingleton<GiftMailboxClient>();
        });
        var navigation = context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo("/profile/character?avatar=santa");
        context.ComponentFactories.AddStub<AvatarLayerPreview>();
        var page = context.Render<Mercantec.Tech.Web.Pages.Demo.Universe>();

        page.WaitForAssertion(() => Assert.Equal("Julemanden", page.Find("#avatar-preview-title").TextContent.Trim()));
        Assert.True(page.Find("[data-testid=unlock-santa]").HasAttribute("disabled"));
        Assert.Equal(AvatarEconomy.StarterId, context.Services.GetRequiredService<DemoWorld>().SelectedAvatarId);
        Assert.Equal(0, api.UnlockCalls);

        page.Find("[data-avatar-id=starter] .avatar-inspect").Click();
        Assert.Equal("Din første avatar", page.Find("#avatar-preview-title").TextContent.Trim());
        Assert.Single(page.FindAll(".avatar-preview-canvas .arena-starter-preview"));
        Assert.Equal("/assets/arena/v2/portrait-starter.webp",
            page.Find("[data-avatar-id=starter] .avatar-inspect img").GetAttribute("src"));
    }

    [Fact]
    public async Task Collection_shows_live_server_progress_and_a_confirmed_third_gift_refreshes_the_gate_without_reload()
    {
        using var api = new RequirementApi { Subject = LearningUiTestContext.AccountId.ToString() };
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            services.AddSingleton<IAvatarClient>(api);
            services.AddSingleton<DemoWorld>();
            services.AddSingleton<GiftMailboxClient>();
        });
        var editor = context.Render<AvatarEditor>();
        var world = context.Services.GetRequiredService<DemoWorld>();
        var gifts = context.Services.GetRequiredService<GiftMailboxClient>();
        editor.WaitForAssertion(() => Assert.True(world.AvatarProfileReady));
        Assert.True(editor.Find("[data-testid=unlock-santa]").HasAttribute("disabled"));
        Assert.Matches(@"0\s*/\s*3", editor.Find("[data-avatar-id=santa]").TextContent);

        foreach (var recipient in api.Recipients)
        {
            await gifts.SendAsync(new(Guid.NewGuid(), recipient.UserSub, "thanks", "pine", "Syntetisk testgave", "christmas"), default);
            var current = api.Sent.Count;
            editor.WaitForAssertion(() => Assert.Equal(current, Assert.Single(world.GetAvatarUnlockProgress(AvatarEconomy.SantaId)).Current));
            Assert.Matches($@"{current}\s*/\s*3", editor.Find("[data-avatar-id=santa]").TextContent);
            Assert.Equal(current < 3, editor.Find("[data-testid=unlock-santa]").HasAttribute("disabled"));
        }

        Assert.Equal(3, api.Sent.Count);
        Assert.Equal(320, world.Gold);
        Assert.Equal(0, api.UnlockCalls);
        Assert.True(await world.TryUnlockAvatarAsync(AvatarEconomy.SantaId));
        editor.WaitForAssertion(() => Assert.Equal(AvatarEconomy.SantaId, world.SelectedAvatarId));
        Assert.Equal(170, world.Gold);
        Assert.Equal(1, api.UnlockCalls);
    }

    [Fact]
    public async Task A_third_gift_during_a_purchase_queues_a_fresh_progress_read_after_its_older_response()
    {
        using var api = new RequirementApi { Subject = LearningUiTestContext.AccountId.ToString() };
        api.Profiles[api.Subject] = Profile(2);
        api.Sent.AddRange(Enumerable.Range(0, 2).Select(_ => new GiftMessage(Guid.NewGuid(), "Afsender", "Modtager", "thanks", "pine", "Tidligere testgave", DateTime.UtcNow, null, "christmas")));
        api.PendingUnlock = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            services.AddSingleton<IAvatarClient>(api);
            services.AddSingleton<DemoWorld>();
            services.AddSingleton<GiftMailboxClient>();
        });
        var editor = context.Render<AvatarEditor>();
        var world = context.Services.GetRequiredService<DemoWorld>();
        var gifts = context.Services.GetRequiredService<GiftMailboxClient>();
        editor.WaitForAssertion(() => Assert.True(world.AvatarProfileReady));
        var purchase = world.TryUnlockAvatarAsync(AvatarEconomy.Bip01Id);
        Assert.True(world.AvatarBusy);

        await gifts.SendAsync(new(Guid.NewGuid(), api.Recipients[2].UserSub, "thanks", "pine", "Tredje testgave", "christmas"), default);
        Assert.Equal(3, api.Sent.Count);
        Assert.Equal(1, api.GetCounts[api.Subject]);
        api.PendingUnlock.SetResult(Profile(2) with
        {
            SelectedAvatarId = AvatarEconomy.Bip01Id,
            OwnedAvatarIds = [AvatarEconomy.StarterId, AvatarEconomy.Bip01Id], Gold = 170, Revision = 1
        });
        Assert.True(await purchase);

        editor.WaitForAssertion(() => Assert.Equal(3, Assert.Single(world.GetAvatarUnlockProgress(AvatarEconomy.SantaId)).Current));
        Assert.True(world.CanUnlockAvatar(AvatarEconomy.SantaId));
        Assert.False(editor.Find("[data-testid=unlock-santa]").HasAttribute("disabled"));
        Assert.Equal(170, world.Gold);
        Assert.Equal(1, api.UnlockCalls);
        Assert.True(api.GetCounts[api.Subject] >= 2);
    }

    [Fact]
    public async Task A_queued_refresh_from_an_old_accounts_purchase_cannot_reload_or_unlock_the_next_account()
    {
        using var api = new RequirementApi();
        api.Profiles["student-a"] = Profile(2);
        api.PendingUnlock = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var world = new DemoWorld(new EmptyRuntime(), api);
        await world.BindAvatarProfileAsync("student-a");
        var purchase = world.TryUnlockAvatarAsync(AvatarEconomy.Bip01Id);
        var queuedRefresh = world.ReloadAvatarProfileAsync();
        await world.BindAvatarProfileAsync("student-b");
        api.PendingUnlock.SetResult(Profile(3) with
        {
            SelectedAvatarId = AvatarEconomy.Bip01Id,
            OwnedAvatarIds = [AvatarEconomy.StarterId, AvatarEconomy.Bip01Id], Gold = 170, Revision = 1
        });
        Assert.False(await purchase);
        await queuedRefresh;

        Assert.Equal(1, api.GetCounts["student-b"]);
        Assert.Equal(320, world.Gold);
        Assert.False(world.OwnsAvatar(AvatarEconomy.Bip01Id));
        Assert.False(world.CanUnlockAvatar(AvatarEconomy.SantaId));
        Assert.Equal(0, Assert.Single(world.GetAvatarUnlockProgress(AvatarEconomy.SantaId)).Current);
    }

    private static AvatarProfileDto Profile(int recipients) => new(
        AvatarEconomy.StarterId, [AvatarEconomy.StarterId], 320, 7, [], false, 0,
        [new(AvatarEconomy.SantaId, Requirement, recipients, 3, recipients >= 3)]);

    private sealed class EmptyRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class RequirementApi : HttpMessageHandler, IAvatarClient
    {
        public string Subject { get; init; } = "student-a";
        public Dictionary<string, AvatarProfileDto> Profiles { get; } = [];
        public Dictionary<string, TaskCompletionSource<AvatarProfileDto>> PendingGets { get; } = [];
        public Dictionary<string, int> GetCounts { get; } = [];
        public TaskCompletionSource<AvatarProfileDto>? PendingUnlock { get; set; }
        public List<GiftMessage> Sent { get; } = [];
        public int UnlockCalls { get; private set; }
        public GiftRecipient[] Recipients { get; } =
        [
            new(Guid.Parse("22222222-2222-4222-8222-222222222222"), "Sara Testperson", false),
            new(Guid.Parse("33333333-3333-4333-8333-333333333333"), "Emil Testperson", false),
            new(Guid.Parse("44444444-4444-4444-8444-444444444444"), "Alma Testperson", false)
        ];
        private AvatarProfileDto Get(string? subject) => Profiles.TryGetValue(subject ?? Subject, out var profile) ? profile : Profiles[subject ?? Subject] = Profile(0);
        public Task<AvatarProfileDto> GetAsync(CancellationToken ct = default, string? expectedSubject = null)
        {
            var subject = expectedSubject ?? Subject;
            GetCounts[subject] = GetCounts.GetValueOrDefault(subject) + 1;
            return PendingGets.TryGetValue(subject, out var pending) ? pending.Task : Task.FromResult(Get(subject));
        }
        public async Task<AvatarProfileDto> UnlockAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null)
        {
            UnlockCalls++;
            var p = Get(expectedSubject);
            var updated = PendingUnlock is not null ? await PendingUnlock.Task : p with { SelectedAvatarId = avatarId, OwnedAvatarIds = p.OwnedAvatarIds.Append(avatarId).ToArray(), Gold = p.Gold - 150, Revision = p.Revision + 1 };
            // A purchase response may be stale, but a later GET derives current
            // gift progress independently from the committed gift history.
            Profiles[expectedSubject ?? Subject] = updated with { UnlockProgress = Get(expectedSubject).UnlockProgress };
            return updated;
        }
        public Task<AvatarProfileDto> SelectAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null) =>
            Task.FromResult(Profiles[expectedSubject ?? Subject] = Get(expectedSubject) with { SelectedAvatarId = avatarId });
        public Task<AvatarProfileDto> ImportLegacyAsync(AvatarLegacyImportRequest request, CancellationToken ct = default, string? expectedSubject = null) => throw new InvalidOperationException("No legacy import expected.");
        public Task<AvatarProfileDto> BuyItemAsync(string itemId, CancellationToken ct = default, string? expectedSubject = null) => throw new InvalidOperationException("No furniture purchase expected.");
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var route = request.RequestUri!.AbsolutePath["/api/".Length..];
            if (request.Method == HttpMethod.Post && route == "gifts/profile") return new(HttpStatusCode.NoContent);
            if (request.Method == HttpMethod.Get && route == "gifts") return Json(new { received = Array.Empty<GiftMessage>(), sent = Sent.ToArray(), unread = 0 });
            if (request.Method == HttpMethod.Post && route == "gifts")
            {
                var draft = await request.Content!.ReadFromJsonAsync<GiftDraft>(cancellationToken) ?? throw new InvalidOperationException("Missing draft");
                Sent.Add(new(draft.Id, "Testelev", "Testmodtager", draft.Kind, draft.Wrapping, draft.Message, DateTime.UtcNow, null, draft.Theme));
                var progress = Sent.Count(g => g.Theme == "christmas");
                Profiles[Subject] = Get(Subject) with { UnlockProgress = [new(AvatarEconomy.SantaId, Requirement, progress, 3, progress >= 3)] };
                return Json(Sent[^1]);
            }
            throw new InvalidOperationException($"Unexpected request {request.Method} {route}");
        }
        private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
