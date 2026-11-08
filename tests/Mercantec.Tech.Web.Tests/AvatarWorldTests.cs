using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Avatars;
using Mercantec.Tech.Web.Demo;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

public class AvatarWorldTests
{
    [Fact]
    public async Task Unlock_waits_for_API_confirmation_and_another_computer_reads_the_purchase()
    {
        var api = new AvatarServer();
        var browser = new AvatarStorage();
        var world = new DemoWorld(browser, api);
        await world.BindAvatarProfileAsync("student-a");
        api.PendingUnlock = new(TaskCreationOptions.RunContinuationsAsynchronously);

        var unlock = world.TryUnlockAvatarAsync(AvatarCatalog.Bip01Id);
        Assert.True(world.AvatarBusy);
        Assert.False(world.OwnsAvatar(AvatarCatalog.Bip01Id));
        Assert.Equal(320, world.Gold);
        Assert.False(await world.TryUnlockAvatarAsync(AvatarCatalog.Bip01Id));
        api.PendingUnlock.SetResult(Profile(robot: true, gold: 170, import: false, revision: 1));

        Assert.True(await unlock);
        Assert.Equal(170, world.Gold);
        Assert.Equal(AvatarCatalog.Bip01Id, world.SelectedAvatarId);
        Assert.False(await world.TryUnlockAvatarAsync(AvatarCatalog.Bip01Id));
        Assert.Equal(1, api.UnlockCount);
        Assert.DoesNotContain("lupAvatars.set", browser.Calls);

        var otherComputer = new DemoWorld(new AvatarStorage(), api);
        await otherComputer.BindAvatarProfileAsync("student-a");
        Assert.Equal(AvatarCatalog.Bip01Id, otherComputer.SelectedAvatarId);
        Assert.Equal(170, otherComputer.Gold);
        Assert.True(await otherComputer.SelectAvatarAsync(AvatarCatalog.StarterId));
        await world.ReloadAvatarProfileAsync();
        Assert.Equal(AvatarCatalog.StarterId, world.SelectedAvatarId);
        Assert.Equal(170, world.Gold);
    }

    [Fact]
    public async Task Failed_purchase_does_not_spend_gold_or_claim_ownership_and_retry_fetches_the_server()
    {
        var api = new AvatarServer();
        var world = new DemoWorld(new AvatarStorage(), api);
        await world.BindAvatarProfileAsync("student-a");
        api.MutationFailure = new AvatarApiException("unavailable", "Købet kunne ikke gemmes.");

        Assert.False(await world.TryUnlockAvatarAsync(AvatarCatalog.Bip01Id));
        Assert.Equal(320, world.Gold);
        Assert.False(world.OwnsAvatar(AvatarCatalog.Bip01Id));
        Assert.Equal(AvatarCatalog.StarterId, world.SelectedAvatarId);
        Assert.Equal("Købet kunne ikke gemmes.", world.AvatarProfileError);
        Assert.Null(world.Notice);
        Assert.False(world.AvatarBusy);

        api.MutationFailure = null;
        await world.ReloadAvatarProfileAsync();
        Assert.Null(world.AvatarProfileError);
        Assert.True(await world.TryUnlockAvatarAsync(AvatarCatalog.Bip01Id));
        Assert.Equal(170, world.Gold);
    }

    [Fact]
    public async Task New_computer_without_legacy_data_does_not_close_the_old_computers_import_window()
    {
        var api = new AvatarServer();
        var newBrowser = new AvatarStorage();
        var newComputer = new DemoWorld(newBrowser, api);
        await newComputer.BindAvatarProfileAsync("student-a");
        Assert.Equal(0, api.ImportCount);
        Assert.True(api.Profiles["student-a"].CanImportLegacy);
        Assert.Equal(0, newBrowser.RemoveCount);

        var oldBrowser = new AvatarStorage { Snapshot = JsonSerializer.Serialize(new { gold = 170 }) };
        oldBrowser.Legacy["student-a"] = LegacyRobot();
        var oldComputer = new DemoWorld(oldBrowser, api);
        await oldComputer.BindAvatarProfileAsync("student-a");
        Assert.Equal(1, api.ImportCount);
        Assert.Equal(170, api.LastImport!.Gold);
        Assert.Equal(170, oldComputer.Gold);
        Assert.Equal(AvatarCatalog.Bip01Id, oldComputer.SelectedAvatarId);
        Assert.Equal(1, oldBrowser.RemoveCount);
        Assert.False(oldBrowser.Legacy.ContainsKey("student-a"));

        await newComputer.ReloadAvatarProfileAsync();
        Assert.True(newComputer.OwnsAvatar(AvatarCatalog.Bip01Id));
        Assert.Equal(170, newComputer.Gold);
    }

    [Fact]
    public async Task Failed_import_keeps_the_browser_record_until_the_API_confirms_success()
    {
        var api = new AvatarServer { ImportFailure = new HttpRequestException("offline") };
        var storage = new AvatarStorage { Snapshot = JsonSerializer.Serialize(new { gold = 170 }) };
        storage.Legacy["student-a"] = LegacyRobot();
        var world = new DemoWorld(storage, api);
        await world.BindAvatarProfileAsync("student-a");
        Assert.NotNull(world.AvatarProfileError);
        Assert.False(world.OwnsAvatar(AvatarCatalog.Bip01Id));
        Assert.True(storage.Legacy.ContainsKey("student-a"));
        Assert.Equal(0, storage.RemoveCount);

        api.ImportFailure = null;
        await world.ReloadAvatarProfileAsync();
        Assert.Null(world.AvatarProfileError);
        Assert.True(world.OwnsAvatar(AvatarCatalog.Bip01Id));
        Assert.Equal(170, world.Gold);
        Assert.Equal(1, storage.RemoveCount);
    }

    [Fact]
    public async Task Existing_server_profile_wins_over_local_wallet_and_legacy_avatar_data()
    {
        var api = new AvatarServer();
        api.Profiles["student-a"] = Profile(robot: true, gold: 80, import: false, revision: 4);
        var storage = new AvatarStorage { Snapshot = JsonSerializer.Serialize(new { gold = 0 }) };
        storage.Legacy["student-a"] = JsonSerializer.Serialize(new { selectedAvatarId = "starter", ownedAvatarIds = new[] { "starter" } });
        var world = new DemoWorld(storage, api);
        await world.BindAvatarProfileAsync("student-a");
        Assert.Equal(80, world.Gold);
        Assert.Equal(AvatarCatalog.Bip01Id, world.SelectedAvatarId);
        Assert.Equal(0, api.ImportCount);
        Assert.Equal(1, storage.RemoveCount);

        world.ToggleDone("local-work");
        Assert.Equal(0, JsonDocument.Parse(storage.Snapshot!).RootElement.GetProperty("gold").GetInt32());
        var restored = new DemoWorld(storage, api);
        await restored.BindAvatarProfileAsync("student-a");
        Assert.Equal(80, restored.Gold);
    }

    [Fact]
    public async Task Switching_account_clears_server_ownership_but_preserves_old_local_room_and_items()
    {
        var api = new AvatarServer();
        api.Profiles["student-a"] = Profile(robot: true, gold: 90, import: false, items: ["lamp"]);
        var storage = new AvatarStorage
        {
            Snapshot = JsonSerializer.Serialize(new { gold = 120, ownedWear = new[] { "hoodie" }, ownedFurniture = new[] { "rug" }, room = new Dictionary<string, string> { ["9"] = "rug" } })
        };
        var world = new DemoWorld(storage, api);
        await world.BindAvatarProfileAsync("student-a");
        Assert.Contains("lamp", world.OwnedFurniture);
        Assert.Contains("rug", world.OwnedFurniture);

        api.Subject = "student-b";
        await world.BindAvatarProfileAsync("student-b");
        Assert.False(world.OwnsAvatar(AvatarCatalog.Bip01Id));
        Assert.DoesNotContain("lamp", world.OwnedFurniture);
        Assert.Contains("rug", world.OwnedFurniture);
        Assert.Contains("hoodie", world.OwnedWear);
        Assert.Equal("rug", world.RoomItem(9));
        Assert.Equal(320, world.Gold);
        Assert.False(await world.SelectAvatarAsync(AvatarCatalog.Bip01Id));

        await world.BindAvatarProfileAsync(null);
        Assert.Equal(120, world.Gold);
        Assert.False(await world.TryUnlockAvatarAsync(AvatarCatalog.Bip01Id));
        Assert.False(await world.BuyAsync("lamp"));
    }

    [Fact]
    public async Task Unknown_ids_and_unowned_selection_are_rejected_without_mutating_the_API()
    {
        var api = new AvatarServer();
        api.Profiles["student-a"] = Profile(gold: 149) with { SelectedAvatarId = "bip01", OwnedAvatarIds = ["unknown-avatar"] };
        var world = new DemoWorld(new AvatarStorage(), api);
        await world.BindAvatarProfileAsync("student-a");
        Assert.Equal(AvatarCatalog.StarterId, world.SelectedAvatarId);
        Assert.Single(world.OwnedAvatarIds);
        Assert.False(await world.SelectAvatarAsync("bip01"));
        Assert.False(await world.TryUnlockAvatarAsync("bip01"));
        Assert.False(await world.TryUnlockAvatarAsync("unknown-avatar"));
        Assert.Equal(0, api.UnlockCount);
        Assert.Equal("Mangler 1 guld", world.AvatarRequirementText("bip01"));
    }

    [Fact]
    public async Task Late_account_response_is_ignored_and_same_account_loading_is_shared()
    {
        var api = new AvatarServer();
        var delayed = new TaskCompletionSource<AvatarProfileDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.PendingGets["student-a"] = delayed;
        var world = new DemoWorld(new AvatarStorage(), api);
        var pending = world.BindAvatarProfileAsync("student-a");
        Assert.Same(pending, world.BindAvatarProfileAsync("student-a"));
        Assert.False(world.AvatarProfileReady);

        api.Subject = "student-b";
        await world.BindAvatarProfileAsync("student-b");
        delayed.SetResult(Profile(robot: true, gold: 170, import: false));
        await pending;
        Assert.True(world.AvatarProfileReady);
        Assert.Equal(AvatarCatalog.StarterId, world.SelectedAvatarId);
        Assert.Equal(320, world.Gold);
        Assert.Equal(1, api.GetCounts["student-a"]);
    }

    [Fact]
    public async Task Late_purchase_response_cannot_change_the_next_accounts_wallet_or_selection()
    {
        var api = new AvatarServer();
        var world = new DemoWorld(new AvatarStorage(), api);
        await world.BindAvatarProfileAsync("student-a");
        api.PendingUnlock = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = world.TryUnlockAvatarAsync("bip01");
        api.Subject = "student-b";
        await world.BindAvatarProfileAsync("student-b");
        Assert.True(api.LastMutationToken.IsCancellationRequested);
        api.PendingUnlock.SetResult(Profile(robot: true, gold: 170, import: false, revision: 1));
        Assert.False(await pending);
        Assert.Equal("student-a", api.LastMutationExpectedSubject);
        Assert.Equal(320, world.Gold);
        Assert.Equal(AvatarCatalog.StarterId, world.SelectedAvatarId);
        Assert.False(world.OwnsAvatar("bip01"));
        Assert.Null(world.Notice);
    }

    [Fact]
    public async Task Account_switch_during_state_notification_prevents_starting_the_old_operation()
    {
        var api = new AvatarServer();
        var world = new DemoWorld(new AvatarStorage(), api);
        await world.BindAvatarProfileAsync("student-a");
        Task? switched = null;
        var didSwitch = false;
        world.Changed += () =>
        {
            if (!world.AvatarBusy || didSwitch) return;
            didSwitch = true;
            api.Subject = "student-b";
            switched = world.BindAvatarProfileAsync("student-b");
        };

        Assert.False(await world.TryUnlockAvatarAsync("bip01"));
        await switched!;
        Assert.Equal(0, api.UnlockCount);
        Assert.Equal(AvatarCatalog.StarterId, world.SelectedAvatarId);
        Assert.Equal(320, world.Gold);
    }

    [Fact]
    public async Task Unreachable_API_never_uses_browser_ownership_as_a_logged_in_fallback()
    {
        var api = new AvatarServer { GetFailure = new HttpRequestException("offline") };
        var storage = new AvatarStorage { Snapshot = JsonSerializer.Serialize(new { gold = 170 }) };
        storage.Legacy["student-a"] = LegacyRobot();
        var world = new DemoWorld(storage, api);
        await world.BindAvatarProfileAsync("student-a");
        Assert.False(world.AvatarProfileReady);
        Assert.NotNull(world.AvatarProfileError);
        Assert.False(world.AvatarBusy);
        Assert.False(world.OwnsAvatar("bip01"));
        Assert.Equal(0, world.Gold);
        Assert.True(storage.Legacy.ContainsKey("student-a"));
        Assert.Equal(0, api.ImportCount);
    }

    [Fact]
    public async Task Failed_refresh_preserves_confirmed_server_state_and_exposes_retry()
    {
        var api = new AvatarServer();
        api.Profiles["student-a"] = Profile(robot: true, gold: 170, import: false);
        var world = new DemoWorld(new AvatarStorage(), api);
        await world.BindAvatarProfileAsync("student-a");
        api.GetFailure = new HttpRequestException("offline");
        await world.ReloadAvatarProfileAsync();
        Assert.True(world.AvatarProfileReady);
        Assert.NotNull(world.AvatarProfileError);
        Assert.Equal(170, world.Gold);
        Assert.True(world.OwnsAvatar("bip01"));
        Assert.False(await world.SelectAvatarAsync("starter"));
    }

    [Fact]
    public async Task Furniture_and_avatar_purchases_share_the_server_wallet_across_computers()
    {
        var api = new AvatarServer();
        var storage = new AvatarStorage();
        var world = new DemoWorld(storage, api);
        await world.BindAvatarProfileAsync("student-a");
        Assert.True(await world.TryUnlockAvatarAsync("bip01"));
        Assert.True(await world.BuyAsync("lamp"));
        Assert.Equal(90, world.Gold);
        Assert.Contains("lamp", world.OwnedFurniture);
        Assert.False(await world.BuyAsync("trophy"));
        Assert.Equal(1, api.BuyCount);
        Assert.DoesNotContain("lamp", JsonDocument.Parse(storage.Snapshot!).RootElement.GetProperty("ownedFurniture").EnumerateArray().Select(x => x.GetString()));

        var otherComputer = new DemoWorld(new AvatarStorage(), api);
        await otherComputer.BindAvatarProfileAsync("student-a");
        Assert.Equal(90, otherComputer.Gold);
        Assert.Contains("lamp", otherComputer.OwnedFurniture);
        Assert.True(otherComputer.OwnsAvatar("bip01"));
    }

    [Fact]
    public async Task Http_client_uses_the_account_routes_and_shared_request_contracts()
    {
        var handler = new AvatarHttpHandler();
        var client = new AvatarClient(new HttpClient(handler) { BaseAddress = new Uri("https://lup.example/api/") });
        await client.GetAsync(expectedSubject: "student-a");
        await client.UnlockAsync("bip01", expectedSubject: "student-a");
        await client.SelectAsync("starter", expectedSubject: "student-a");
        await client.ImportLegacyAsync(new(["starter", "bip01"], "bip01", 170), expectedSubject: "student-a");
        await client.BuyItemAsync("lamp", expectedSubject: "student-a");
        Assert.Equal(new[] { "GET /api/me/avatars", "POST /api/me/avatars/bip01/unlock", "PUT /api/me/avatars/selection", "POST /api/me/avatars/import", "POST /api/me/avatars/items/lamp/buy" }, handler.Requests.Select(x => x.Route));
        Assert.All(handler.Requests, request => Assert.Equal("student-a", request.ExpectedSubject));
        Assert.Equal("starter", JsonDocument.Parse(handler.Requests[2].Body!).RootElement.GetProperty("avatarId").GetString());
        Assert.Equal(170, JsonDocument.Parse(handler.Requests[3].Body!).RootElement.GetProperty("gold").GetInt32());
    }

    [Fact]
    public async Task Http_client_surfaces_API_problem_details_without_claiming_success()
    {
        var handler = new AvatarHttpHandler { Failure = new HttpResponseMessage(HttpStatusCode.Conflict) { Content = JsonContent.Create(new { code = "insufficient_gold", detail = "Du har ikke nok guld." }) } };
        var client = new AvatarClient(new HttpClient(handler) { BaseAddress = new Uri("https://lup.example/api/") });
        var error = await Assert.ThrowsAsync<AvatarApiException>(() => client.UnlockAsync("bip01"));
        Assert.Equal("insufficient_gold", error.Code);
        Assert.Equal("Du har ikke nok guld.", error.Message);
    }

    [Fact]
    public async Task Disposed_auth_response_becomes_a_retryable_API_error()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = JsonContent.Create(new { detail = "Session expired" }) };
        response.Dispose();
        var client = new AvatarClient(new HttpClient(new AvatarHttpHandler { Failure = response }) { BaseAddress = new Uri("https://lup.example/api/") });

        var error = await Assert.ThrowsAsync<AvatarApiException>(() => client.GetAsync());
        Assert.Equal("connection_closed", error.Code);
    }

    private static AvatarProfileDto Profile(bool robot = false, int gold = 320, bool import = true, long revision = 0, string[]? items = null) =>
        new(robot ? "bip01" : "starter", robot ? ["starter", "bip01"] : ["starter"], gold, 7, items ?? [], import, revision);

    private static string LegacyRobot() => JsonSerializer.Serialize(new { selectedAvatarId = "bip01", ownedAvatarIds = new[] { "starter", "bip01" } });

    private sealed class AvatarServer : IAvatarClient
    {
        public string Subject { get; set; } = "student-a";
        public Dictionary<string, AvatarProfileDto> Profiles { get; } = [];
        public Dictionary<string, int> GetCounts { get; } = [];
        public Dictionary<string, TaskCompletionSource<AvatarProfileDto>> PendingGets { get; } = [];
        public TaskCompletionSource<AvatarProfileDto>? PendingUnlock { get; set; }
        public Exception? GetFailure { get; set; }
        public Exception? MutationFailure { get; set; }
        public Exception? ImportFailure { get; set; }
        public AvatarLegacyImportRequest? LastImport { get; private set; }
        public CancellationToken LastMutationToken { get; private set; }
        public string? LastMutationExpectedSubject { get; private set; }
        public int ImportCount { get; private set; }
        public int UnlockCount { get; private set; }
        public int BuyCount { get; private set; }
        private AvatarProfileDto Current => Profiles.TryGetValue(Subject, out var profile) ? profile : Profiles[Subject] = Profile();

        public Task<AvatarProfileDto> GetAsync(CancellationToken ct = default, string? expectedSubject = null)
        {
            GetCounts[Subject] = GetCounts.GetValueOrDefault(Subject) + 1;
            if (GetFailure is not null) return Task.FromException<AvatarProfileDto>(GetFailure);
            return PendingGets.TryGetValue(Subject, out var pending) ? pending.Task : Task.FromResult(Current);
        }

        public async Task<AvatarProfileDto> UnlockAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null)
        {
            UnlockCount++;
            LastMutationToken = ct;
            LastMutationExpectedSubject = expectedSubject;
            if (MutationFailure is not null) throw MutationFailure;
            var subject = Subject;
            var current = Current;
            var profile = PendingUnlock is not null ? await PendingUnlock.Task : current with { SelectedAvatarId = avatarId, OwnedAvatarIds = ["starter", avatarId], Gold = current.Gold - 150, CanImportLegacy = false, Revision = current.Revision + 1 };
            return Profiles[subject] = profile;
        }

        public Task<AvatarProfileDto> SelectAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null)
        {
            if (MutationFailure is not null) return Task.FromException<AvatarProfileDto>(MutationFailure);
            var profile = Current with { SelectedAvatarId = avatarId, CanImportLegacy = false, Revision = Current.Revision + 1 };
            return Task.FromResult(Profiles[Subject] = profile);
        }

        public Task<AvatarProfileDto> ImportLegacyAsync(AvatarLegacyImportRequest request, CancellationToken ct = default, string? expectedSubject = null)
        {
            ImportCount++;
            LastImport = request;
            if (ImportFailure is not null) return Task.FromException<AvatarProfileDto>(ImportFailure);
            var robot = request.OwnedAvatarIds.Contains("bip01");
            var profile = Profile(robot, Math.Clamp(request.Gold, 0, robot ? 170 : 320), false, Current.Revision + 1);
            return Task.FromResult(Profiles[Subject] = profile);
        }

        public Task<AvatarProfileDto> BuyItemAsync(string itemId, CancellationToken ct = default, string? expectedSubject = null)
        {
            BuyCount++;
            if (MutationFailure is not null) return Task.FromException<AvatarProfileDto>(MutationFailure);
            var current = Current;
            var item = AvatarEconomy.FindFurniture(itemId)!;
            var profile = current with { OwnedItemIds = current.OwnedItemIds.Append(itemId).ToArray(), Gold = current.Gold - item.Cost, CanImportLegacy = false, Revision = current.Revision + 1 };
            return Task.FromResult(Profiles[Subject] = profile);
        }
    }

    private sealed class AvatarStorage : IJSRuntime
    {
        public string? Snapshot { get; set; }
        public Dictionary<string, string?> Legacy { get; } = [];
        public List<string> Calls { get; } = [];
        public int RemoveCount { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(identifier);
            if (identifier == "lupDemo.get") return ValueTask.FromResult((TValue)(object?)Snapshot!);
            if (identifier == "lupDemo.set") { Snapshot = (string?)args![0]; return ValueTask.FromResult(default(TValue)!); }
            if (identifier == "lupAvatars.get")
            {
                Legacy.TryGetValue((string)args![0]!, out var profile);
                return ValueTask.FromResult((TValue)(object?)profile!);
            }
            if (identifier == "lupAvatars.remove") { Legacy.Remove((string)args![0]!); RemoveCount++; return ValueTask.FromResult(default(TValue)!); }
            throw new InvalidOperationException($"Unexpected JavaScript call: {identifier}");
        }
    }

    private sealed class AvatarHttpHandler : HttpMessageHandler
    {
        public List<(string Route, string? Body, string? ExpectedSubject)> Requests { get; } = [];
        public HttpResponseMessage? Failure { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(($"{request.Method} {request.RequestUri!.AbsolutePath}", request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken), request.Headers.TryGetValues("X-LUP-Avatar-Subject", out var subject) ? subject.Single() : null));
            return Failure ?? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Profile()) };
        }
    }
}
