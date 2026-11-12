using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Channels;
using Mercantec.Tech.Api.Arena;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed class ArenaRoomTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _database = Path.Combine(Path.GetTempPath(), "arena-tests-" + Guid.NewGuid().ToString("N") + ".db");
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly ManualClock _clock = new();
    private WebApplication _app = null!;
    private string _origin = "";
    private ArenaRoomService Rooms => _app.Services.GetRequiredService<ArenaRoomService>();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_database};Pooling=False"));
        builder.Services.AddControllers().AddApplicationPart(typeof(ArenaController).Assembly);
        builder.Services.AddAuthentication("ArenaTest").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("ArenaTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<TimeProvider>(_clock);
        builder.Services.AddSingleton<ArenaRoomService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ArenaRoomService>());
        _app = builder.Build();
        _app.UseWebSockets();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        using (var scope = _app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        await _app.StartAsync();
        _origin = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    [Fact]
    public async Task OnlyHostCanRemoveAnInactiveLobbyPlayerAndTicketsAreRevoked()
    {
        var host = Rooms.Create(Identity(_alice), new());
        using var first = await Socket(host);
        var guest = Rooms.Join(host.Room.Code, Identity(_bob));
        using var second = await Socket(guest);
        await Receive(first, j => Type(j) == "room" && Player(j, guest.PlayerId)?.GetProperty("connected").GetBoolean() == true);

        await Send(second, new { type = "removeAfk", playerId = host.PlayerId });
        Assert.Equal("host_required", (await Receive(second, j => Type(j) == "error")).GetProperty("code").GetString());
        await Send(first, new { type = "removeAfk", playerId = guest.PlayerId });
        Assert.Equal("player_active", (await Receive(first, j => Type(j) == "error")).GetProperty("code").GetString());

        _clock.Advance(ArenaRoomService.LobbyAfkDelay);
        await Send(second, new { type = "ping", sentAt = 1 });
        await Receive(second, j => Type(j) == "pong");
        await Receive(first, j => Type(j) == "room" && CanRemove(j, guest.PlayerId));
        var pending = Rooms.Reconnect(host.Room.Code, _bob);

        await Send(first, new { type = "removeAfk", playerId = guest.PlayerId });
        var removed = await Receive(second, j => Type(j) == "removed");
        Assert.Equal("afk_removed", removed.GetProperty("code").GetString());
        await Receive(first, j => Type(j) == "room" && Player(j, guest.PlayerId) is null);
        Assert.Equal("not_in_room", Assert.Throws<ArenaException>(() => Rooms.Reconnect(host.Room.Code, _bob)).Code);
        Assert.Equal("ticket_expired", Assert.Throws<ArenaException>(() => Rooms.ConsumeTicket(pending.Ticket)).Code);
    }

    [Fact]
    public async Task MatchInputProtectsAnActiveGuestWhileNeutralKeepaliveCannotProtectAnAfkGuest()
    {
        var host = Rooms.Create(Identity(_alice), new());
        using var first = await Socket(host);
        var guest = Rooms.Join(host.Room.Code, Identity(_bob));
        using var second = await Socket(guest);
        var idle = Rooms.Join(host.Room.Code, Identity(Guid.NewGuid()));
        using var third = await Socket(idle);
        await Send(first, new { type = "ready", ready = true });
        await Send(second, new { type = "ready", ready = true });
        await Send(third, new { type = "ready", ready = true });
        await Receive(first, EveryoneReady);
        await Send(first, new { type = "start" });
        await Receive(first, IsPlaying, 7);

        _clock.Advance(ArenaRoomService.MatchAfkDelay - TimeSpan.FromSeconds(1));
        await Send(second, new { type = "input", sequence = 1, pressed = (int)ArenaPress.Jump });
        await Send(third, new { type = "input", sequence = 1, horizontal = 0, vertical = 0 });
        await Receive(first, j => Type(j) == "state" && Fighter(j, guest.PlayerId).GetProperty("lastInputSequence").GetInt64() == 1);
        _clock.Advance(TimeSpan.FromSeconds(1));

        var afk = await Receive(first, j => Type(j) == "room" && CanRemove(j, idle.PlayerId));
        Assert.False(CanRemove(afk, guest.PlayerId));
        await Send(first, new { type = "removeAfk", playerId = guest.PlayerId });
        Assert.Equal("player_active", (await Receive(first, j => Type(j) == "error")).GetProperty("code").GetString());
        await Send(first, new { type = "removeAfk", playerId = idle.PlayerId });
        await Receive(third, j => Type(j) == "removed");
        await Receive(first, j => Type(j) == "room" && Player(j, idle.PlayerId) is null);
        await Receive(second, j => IsPlaying(j) && Fighter(j, idle.PlayerId).GetProperty("out").GetBoolean());
    }

    [Fact]
    public async Task MigratedHostCanRemoveTheDisconnectedFormerHostAfterGrace()
    {
        var host = Rooms.Create(Identity(_alice), new());
        using var first = await Socket(host);
        var guest = Rooms.Join(host.Room.Code, Identity(_bob));
        using var second = await Socket(guest);
        await Receive(second, j => Type(j) == "room" && Player(j, guest.PlayerId)?.GetProperty("connected").GetBoolean() == true);
        first.Abort();
        await Receive(second, j => Type(j) == "room" && j.GetProperty("room").GetProperty("hostId").GetString() == guest.PlayerId);
        await Send(second, new { type = "removeAfk", playerId = host.PlayerId });
        Assert.Equal("player_active", (await Receive(second, j => Type(j) == "error")).GetProperty("code").GetString());

        _clock.Advance(ArenaRoomService.DisconnectAfkDelay);
        await Receive(second, j => Type(j) == "room" && CanRemove(j, host.PlayerId));
        var pending = Rooms.Reconnect(host.Room.Code, _alice);
        await Send(second, new { type = "removeAfk", playerId = host.PlayerId });
        await Receive(second, j => Type(j) == "room" && Player(j, host.PlayerId) is null);
        Assert.Equal("ticket_expired", Assert.Throws<ArenaException>(() => Rooms.ConsumeTicket(pending.Ticket)).Code);
    }

    [Fact]
    public async Task ABlockedPeerDoesNotDelayOtherClientsAndCatchesUpToTheLatestSnapshot()
    {
        var host = Rooms.Create(Identity(_alice), new());
        using var first = await Socket(host);
        var guest = Rooms.Join(host.Room.Code, Identity(_bob));
        using var second = await Socket(guest);
        var slow = Rooms.Join(host.Room.Code, Identity(Guid.NewGuid()));
        using var stalled = new StalledWebSocket();
        using var cancellation = new CancellationTokenSource();
        var connection = Rooms.RunSocketAsync(Rooms.ConsumeTicket(slow.Ticket), stalled, cancellation.Token);
        try
        {
            await Send(first, new { type = "ready", ready = true });
            await Send(second, new { type = "ready", ready = true });
            stalled.Queue(new { type = "ready", ready = true });
            await Receive(first, EveryoneReady);
            await Send(first, new { type = "start" });
            var blockedTick = await stalled.BlockedTick.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stalled.Queue(new { type = "ping", sentAt = 2 });
            var playing = await Receive(second, IsPlaying, 7);
            var initialX = Fighter(playing, guest.PlayerId).GetProperty("x").GetDouble();
            await Send(second, new { type = "input", sequence = 1, horizontal = 1 });
            var advanced = await Receive(first, j => IsPlaying(j) && Fighter(j, guest.PlayerId).GetProperty("x").GetDouble() > initialX + 3);
            var latestHealthyTick = advanced.GetProperty("state").GetProperty("tick").GetInt32();

            stalled.Release.TrySetResult();
            await stalled.Pong.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var catchupTick = await stalled.CatchupTick.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(catchupTick > blockedTick + 100);
            Assert.True(catchupTick >= latestHealthyTick - 3);
        }
        finally
        {
            cancellation.Cancel();
            await connection.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(_database + suffix);
    }

    private HttpClient Client(Guid? subject = null)
    {
        var client = new HttpClient { BaseAddress = new Uri(_origin), Timeout = TimeSpan.FromSeconds(8) };
        if (subject.HasValue) client.DefaultRequestHeaders.Add("X-Arena-Test-Subject", subject.Value.ToString());
        return client;
    }
    private static ArenaIdentity Identity(Guid id, string avatar = "starter") => new(id, "Spiller", avatar, new HashSet<string> { "starter", avatar });

    [Fact]
    public async Task HttpRequiresAnAuthenticatedIdentityAndCannotGrantUnownedAvatars()
    {
        using var anonymous = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/arena/rooms")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/arena/rooms", new ArenaCreateRequest())).StatusCode);
        using var alice = Client(_alice);
        var denied = await alice.PostAsJsonAsync("/api/arena/rooms", new ArenaCreateRequest(AvatarId: "raymond"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("avatar_not_owned", await denied.Content.ReadAsStringAsync());
        Assert.Empty(Rooms.ListRooms());
        var response = await alice.PostAsJsonAsync("/api/arena/rooms", new ArenaCreateRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var admission = (await response.Content.ReadFromJsonAsync<ArenaAdmission>())!;
        Assert.Equal("starter", Assert.Single(admission.Room.Players).AvatarId);
        Assert.Equal(6, admission.Room.Code.Length);
        using var bob = Client(_bob);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PostAsJsonAsync($"/api/arena/rooms/{admission.Room.Code}/ticket", new { })).StatusCode);
    }

    [Fact]
    public async Task AdmissionUsesTheServerSavedProfileAvatarAndOwnership()
    {
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AvatarProfiles.Add(new AvatarProfile { UserSub = _alice, SelectedAvatarId = "bip01", Avatars = new List<AvatarOwnership> { new() { UserSub = _alice, AvatarId = "starter" }, new() { UserSub = _alice, AvatarId = "bip01" } } });
            await db.SaveChangesAsync();
        }
        using var alice = Client(_alice);
        var result = await alice.PostAsJsonAsync("/api/arena/rooms", new ArenaCreateRequest());
        var admission = (await result.Content.ReadFromJsonAsync<ArenaAdmission>())!;
        Assert.Equal("bip01", Assert.Single(admission.Room.Players).AvatarId);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsJsonAsync("/api/arena/rooms", new ArenaCreateRequest(AvatarId: "raymond"))).StatusCode);
    }

    [Fact]
    public void RoomCapacityReconnectAndSingleUseTicketsAreEnforced()
    {
        var first = Rooms.Create(Identity(_alice), new());
        var again = Rooms.Join(first.Room.Code, Identity(_alice));
        Assert.Equal(first.PlayerId, again.PlayerId);
        Assert.Single(again.Room.Players);
        Assert.Throws<ArenaException>(() => Rooms.ConsumeTicket(first.Ticket));
        Assert.Equal(first.PlayerId, Rooms.ConsumeTicket(again.Ticket).PlayerId);
        Assert.Throws<ArenaException>(() => Rooms.ConsumeTicket(again.Ticket));
        Assert.InRange(again.ExpiresAt, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(31));
        for (var i = 0; i < 3; i++) Rooms.Join(first.Room.Code, Identity(Guid.NewGuid()));
        var full = Assert.Throws<ArenaException>(() => Rooms.Join(first.Room.Code, Identity(Guid.NewGuid())));
        Assert.Equal("room_full", full.Code);
        Assert.Equal("room_not_found", Assert.Throws<ArenaException>(() => Rooms.Join("AAAAAA", Identity(_bob))).Code);
        Assert.Equal("invalid_settings", Assert.Throws<ArenaException>(() => Rooms.Create(Identity(_bob), new("unknown"))).Code);
        Assert.Equal("avatar_not_owned", Assert.Throws<ArenaException>(() => Rooms.Create(new(_bob, "B", "raymond", new HashSet<string> { "starter" }), new())).Code);
    }

    [Fact]
    public async Task TwoIndependentWebSocketClientsShareAuthoritativeStateAndHostMigrates()
    {
        using var alice = Client(_alice);
        using var bob = Client(_bob);
        var appearance = new ArenaAppearance("#abcdef", "#123456", "short", "#fedcba");
        var admission = (await (await alice.PostAsJsonAsync("/api/arena/rooms", new ArenaCreateRequest(IsPublic: true, Appearance: appearance))).Content.ReadFromJsonAsync<ArenaAdmission>())!;
        Assert.Equal(appearance, Assert.Single(admission.Room.Players).Appearance);
        using var first = await Socket(admission);
        await Receive(first, j => Type(j) == "room");
        var guest = (await (await bob.PostAsJsonAsync($"/api/arena/rooms/{admission.Room.Code}/join", new ArenaJoinRequest())).Content.ReadFromJsonAsync<ArenaAdmission>())!;
        using var second = await Socket(guest);
        var room = await Receive(first, j => Type(j) == "room" && j.GetProperty("room").GetProperty("players").GetArrayLength() == 2);
        Assert.Equal(admission.PlayerId, room.GetProperty("room").GetProperty("hostId").GetString());
        Assert.Single(Rooms.ListRooms());
        await Send(second, new { type = "start" });
        Assert.Equal("host_required", (await Receive(second, j => Type(j) == "error")).GetProperty("code").GetString());
        await Send(first, new { type = "start" });
        Assert.Equal("players_not_ready", (await Receive(first, j => Type(j) == "error")).GetProperty("code").GetString());
        await Send(first, new { type = "ready", ready = true });
        await Send(second, new { type = "ready", ready = true });
        await Receive(first, j => Type(j) == "room" && j.GetProperty("room").GetProperty("players").EnumerateArray().All(p => p.GetProperty("ready").GetBoolean()));
        await Send(first, new { type = "start" });
        var firstState = await Receive(first, j => Type(j) == "state");
        var secondState = await Receive(second, j => Type(j) == "state");
        Assert.Equal(2, firstState.GetProperty("state").GetProperty("fighters").GetArrayLength());
        var remoteAppearance = secondState.GetProperty("state").GetProperty("fighters").EnumerateArray()
            .Single(f => f.GetProperty("id").GetString() == admission.PlayerId).GetProperty("appearance");
        Assert.Equal("#abcdef", remoteAppearance.GetProperty("skin").GetString());
        Assert.Equal("#123456", remoteAppearance.GetProperty("hair").GetString());
        Assert.Equal("short", remoteAppearance.GetProperty("hairStyle").GetString());
        Assert.Equal("#fedcba", remoteAppearance.GetProperty("outfit").GetString());
        Assert.Equal(firstState.GetProperty("state").GetProperty("stageId").GetString(), secondState.GetProperty("state").GetProperty("stageId").GetString());
        Assert.Equal("already_playing", Assert.Throws<ArenaException>(() => Rooms.Create(Identity(_alice), new())).Code);
        Assert.Empty(Rooms.ListRooms());
        var playing = await Receive(first, j => Type(j) == "state" && j.GetProperty("state").GetProperty("phase").GetString() == "playing", 7);
        var x = playing.GetProperty("state").GetProperty("fighters").EnumerateArray().Single(f => f.GetProperty("id").GetString() == admission.PlayerId).GetProperty("x").GetDouble();
        await Send(first, new { type = "input", sequence = 1, horizontal = 1 });
        var moved = await Receive(second, j => Type(j) == "state" && j.GetProperty("state").GetProperty("fighters").EnumerateArray().Any(f => f.GetProperty("id").GetString() == admission.PlayerId && f.GetProperty("x").GetDouble() > x + 3));
        Assert.NotEqual(0, moved.GetProperty("state").GetProperty("tick").GetInt32());
        await Send(first, new { type = "state", state = new { winnerId = admission.PlayerId } });
        Assert.Equal("unknown_action", (await Receive(first, j => Type(j) == "error")).GetProperty("code").GetString());
        await Send(first, new { type = "leave" });
        var migrated = await Receive(second, j => Type(j) == "room" && j.GetProperty("room").GetProperty("hostId").GetString() == guest.PlayerId);
        Assert.Single(migrated.GetProperty("room").GetProperty("players").EnumerateArray());
    }

    [Fact]
    public async Task AppearanceIsCosmeticAndInvalidValuesAreSanitizedBeforeAdmission()
    {
        using var alice = Client(_alice);
        var untrusted = new ArenaAppearance("url(https://example.test)", "<script>", "../unsafe", "#00ffcc");
        var response = await alice.PostAsJsonAsync("/api/arena/rooms", new ArenaCreateRequest(Appearance: untrusted));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var admission = (await response.Content.ReadFromJsonAsync<ArenaAdmission>())!;
        var player = Assert.Single(admission.Room.Players);
        Assert.Equal("starter", player.AvatarId);
        Assert.Equal(ArenaAppearance.Sanitize(untrusted), player.Appearance);
        Assert.Equal("#00ffcc", player.Appearance!.Outfit);
        Assert.NotEqual(untrusted.Skin, player.Appearance.Skin);
        Assert.NotEqual(untrusted.Hair, player.Appearance.Hair);
        Assert.NotEqual(untrusted.HairStyle, player.Appearance.HairStyle);
    }

    [Fact]
    public async Task ReconnectingReusesThePlayerAndRejectsForeignWebOrigins()
    {
        var admission = Rooms.Create(Identity(_alice), new());
        using var first = await Socket(admission);
        await Receive(first, j => Type(j) == "room");
        using var alice = Client(_alice);
        var renewed = (await (await alice.PostAsJsonAsync($"/api/arena/rooms/{admission.Room.Code}/ticket", new { })).Content.ReadFromJsonAsync<ArenaAdmission>())!;
        using var replacement = await Socket(renewed);
        Assert.Equal(admission.PlayerId, renewed.PlayerId);
        var room = await Receive(replacement, j => Type(j) == "room");
        Assert.Single(room.GetProperty("room").GetProperty("players").EnumerateArray());
        var fresh = Rooms.Reconnect(admission.Room.Code, _alice);
        using var foreign = new ClientWebSocket();
        foreign.Options.SetRequestHeader("Origin", "https://unrelated.example");
        await Assert.ThrowsAsync<WebSocketException>(() => foreign.ConnectAsync(new Uri(_origin.Replace("http:", "ws:") + fresh.WebSocketPath), CancellationToken.None));
        // A rejected origin never consumes the legitimate reconnect ticket.
        Assert.Equal(admission.PlayerId, Rooms.ConsumeTicket(fresh.Ticket).PlayerId);
    }

    private async Task<ClientWebSocket> Socket(ArenaAdmission admission)
    {
        var socket = new ClientWebSocket();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(new Uri(_origin.Replace("http:", "ws:") + admission.WebSocketPath), deadline.Token);
        return socket;
    }
    private static async Task Send(ClientWebSocket socket, object message) =>
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message, Json).AsMemory(), WebSocketMessageType.Text, true, CancellationToken.None);
    private static string? Type(JsonElement element) => element.GetProperty("type").GetString();
    private static JsonElement? Player(JsonElement element, string playerId) => element.GetProperty("room").GetProperty("players").EnumerateArray()
        .Where(p => p.GetProperty("id").GetString() == playerId).Cast<JsonElement?>().SingleOrDefault();
    private static bool CanRemove(JsonElement element, string playerId) => Player(element, playerId)?.GetProperty("canRemoveAfk").GetBoolean() == true;
    private static JsonElement Fighter(JsonElement element, string playerId) => element.GetProperty("state").GetProperty("fighters").EnumerateArray()
        .Single(f => f.GetProperty("id").GetString() == playerId);
    private static bool EveryoneReady(JsonElement element) => Type(element) == "room" && element.GetProperty("room").GetProperty("players").EnumerateArray()
        .All(p => p.GetProperty("ready").GetBoolean());
    private static bool IsPlaying(JsonElement element) => Type(element) == "state" && element.GetProperty("state").GetProperty("phase").GetString() == "playing";
    private static async Task<JsonElement> Receive(ClientWebSocket socket, Func<JsonElement, bool> predicate, int seconds = 5)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        var buffer = new byte[65536];
        while (true)
        {
            var length = 0;
            ValueWebSocketReceiveResult packet;
            do
            {
                packet = await socket.ReceiveAsync(buffer.AsMemory(length), deadline.Token);
                if (packet.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("Socket closed before expected message.");
                length += packet.Count;
            } while (!packet.EndOfMessage);
            using var document = JsonDocument.Parse(buffer.AsMemory(0, length));
            if (predicate(document.RootElement)) return document.RootElement.Clone();
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _utcTicks = DateTimeOffset.UtcNow.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);
        public void Advance(TimeSpan duration) => Interlocked.Add(ref _utcTicks, duration.Ticks);
    }

    // One controlled stalled send models a network socket that cannot accept more bytes.
    // The other peers use real ClientWebSocket connections against Kestrel.
    private sealed class StalledWebSocket : WebSocket
    {
        private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
        private int _state = (int)WebSocketState.Open;
        public TaskCompletionSource<int> BlockedTick { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> CatchupTick { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Pong { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => (WebSocketState)Volatile.Read(ref _state);
        public override string? SubProtocol => null;
        public override void Abort() => Volatile.Write(ref _state, (int)WebSocketState.Aborted);
        public override void Dispose() => Abort();
        public void Queue(object command) => _input.Writer.TryWrite(JsonSerializer.SerializeToUtf8Bytes(command, Json));

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
            CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            Volatile.Write(ref _state, (int)WebSocketState.CloseSent);
            return Task.CompletedTask;
        }

        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            var bytes = await _input.Reader.ReadAsync(cancellationToken);
            bytes.AsMemory().CopyTo(buffer.AsMemory());
            return new(bytes.Length, WebSocketMessageType.Text, true);
        }

        public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            using var packet = JsonDocument.Parse(buffer.AsMemory());
            var root = packet.RootElement;
            if (Type(root) == "pong") Pong.TrySetResult();
            if (Type(root) != "state") return;
            var tick = root.GetProperty("state").GetProperty("tick").GetInt32();
            if (BlockedTick.TrySetResult(tick)) await Release.Task.WaitAsync(cancellationToken);
            else CatchupTick.TrySetResult(tick);
        }
    }

    public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Arena-Test-Subject"], out var subject)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new("sub", subject.ToString()), new("name", "Testspiller")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
