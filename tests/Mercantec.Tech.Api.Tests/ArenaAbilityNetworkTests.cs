using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

// These use the actual HTTP admission, Kestrel WebSocket transport, hosted room
// clock and two independent clients. No combat state is injected into the engine.
public sealed class ArenaAbilityNetworkTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _database = Path.Combine(Path.GetTempPath(), "arena-ability-network-" + Guid.NewGuid().ToString("N") + ".db");
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private WebApplication _app = null!;
    private string _origin = "";

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_database};Pooling=False"));
        builder.Services.AddControllers().AddApplicationPart(typeof(ArenaController).Assembly);
        builder.Services.AddAuthentication("ArenaAbilityTest").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("ArenaAbilityTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<ArenaRoomService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ArenaRoomService>());
        _app = builder.Build();
        _app.UseWebSockets();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.AvatarProfiles.AddRange(Profile(_alice, "bip01"), Profile(_bob, "raymond"));
            for (var i = 0; i < 3; i++)
                db.ChristmasGifts.Add(new ChristmasGift
                {
                    Id = Guid.NewGuid(), SenderSub = _alice, RecipientSub = Guid.NewGuid(),
                    Theme = "christmas", CreatedAtUtc = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }
        await _app.StartAsync();
        _origin = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(_database + suffix);
    }

    [Fact]
    public async Task ClientSuppliedRequirementProgressCannotUnlockSantaWithoutStoredChristmasGifts()
    {
        using var bob = Client(_bob);
        var response = await bob.PostAsJsonAsync("/api/me/avatars/santa/unlock", new
        {
            ownedAvatarIds = new[] { "santa" },
            unlockProgress = new[] { new { avatarId = "santa", requirementId = "christmas-gift-recipients", current = 3, required = 3, isMet = true } }
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("avatar_requirement_not_met", problem.RootElement.GetProperty("code").GetString());
        var profile = await bob.GetFromJsonAsync<AvatarProfileDto>("/api/me/avatars");
        Assert.DoesNotContain("santa", profile!.OwnedAvatarIds);
        Assert.Equal(320, profile.Gold);
        Assert.Equal(0, Assert.Single(profile.UnlockProgress!).Current);
    }

    [Fact]
    public async Task PurchasedSantaEntersOnlineArenaAndBothClientsReceiveTheActualArcingStar()
    {
        using var alice = Client(_alice);
        var purchase = await alice.PostAsJsonAsync("/api/me/avatars/santa/unlock", new { });
        purchase.EnsureSuccessStatusCode();
        await using var match = await StartMatch(firstAvatar: AvatarEconomy.SantaId);
        var initial = await match.First.WaitState(state => state.Phase == "playing" && state.Tick >= 216);
        Assert.Equal(AvatarEconomy.SantaId, Fighter(initial, match.FirstId).AvatarId);
        await match.First.Input(special: true);
        var fired = await match.First.WaitState(state => state.Tick > initial.Tick && state.Projectiles.Any(projectile => projectile.SourceAvatarId == "santa"));
        var star = Assert.Single(fired.Projectiles);
        Assert.Equal("star", star.Kind);
        Assert.Equal(match.FirstId, star.OwnerId);
        Assert.Equal(16, star.Radius);
        Assert.True(star.Vy < 0);
        Assert.InRange(Fighter(fired, match.FirstId).ActionFrame, 4, 5);
        Assert.InRange(Fighter(fired, match.FirstId).Combat!.Energy, 85, 90);
        Assert.True(Fighter(fired, match.FirstId).Combat!.Cooldowns.Pulse > 0);
        await AssertSharedState(match.Second, fired);
    }

    [Fact]
    public async Task BothClientsSeeDifferentProjectilePhysicsAndActualCombatDamage()
    {
        await using var match = await StartMatch();
        var before = await match.First.WaitState(s => s.Phase == "playing" && s.Tick >= 216);
        Assert.All(before.Fighters, f => Assert.Equal(0, f.Combat!.Ultimate));

        await match.First.Input(special: true);
        await match.Second.Input(special: true);
        var fired = await match.First.WaitState(s => s.Tick > before.Tick &&
            s.Projectiles.Any(p => p.OwnerId == match.FirstId) && s.Projectiles.Any(p => p.OwnerId == match.SecondId));
        var bip = Assert.Single(fired.Projectiles, p => p.OwnerId == match.FirstId);
        var raymond = Assert.Single(fired.Projectiles, p => p.OwnerId == match.SecondId);
        Assert.Equal("bip01", bip.SourceAvatarId);
        Assert.Equal("raymond", raymond.SourceAvatarId);
        Assert.Equal("pulse", bip.Kind);
        Assert.Equal("pulse", raymond.Kind);
        Assert.True(Math.Abs(bip.Vx) > Math.Abs(raymond.Vx));
        Assert.True(raymond.Radius > bip.Radius);
        Assert.InRange(Fighter(fired, match.FirstId).Combat!.Energy, 80, 95);
        Assert.True(Fighter(fired, match.FirstId).Combat!.Cooldowns.Pulse > 0);
        Assert.True(Fighter(fired, match.SecondId).Combat!.Cooldowns.Pulse > 0);
        await AssertSharedState(match.Second, fired);

        var hit = await match.Second.WaitState(s => s.Tick > fired.Tick && s.Fighters.All(f => f.Damage > 0));
        Assert.Equal(7, Fighter(hit, match.SecondId).Damage);
        Assert.Equal(13, Fighter(hit, match.FirstId).Damage);
        Assert.All(hit.Fighters, f => Assert.InRange(f.Combat!.Ultimate, .01, 99.99));
        Assert.All(hit.Fighters, f => Assert.Equal(3, f.Stocks));
        await AssertSharedState(match.First, hit);
    }

    [Fact]
    public async Task ClientCannotGrantTrainingMeterHealthEnergyCaptureOrReplayAnAbility()
    {
        await using var match = await StartMatch(requestTraining: true);
        var before = await match.First.WaitState(s => s.Phase == "playing" && s.Tick >= 216);
        await match.First.Send(new
        {
            type = "input", sequence = 1, smash = true, training = true,
            damage = -500, stocks = 999, energy = 999, ultimate = 100,
            combat = new { energy = 999, ultimate = 100, capturedBy = match.SecondId, armored = true },
            projectiles = new[] { new { kind = "join", ownerId = match.FirstId } }
        });
        await match.First.Send(new { type = "state", state = new { winnerId = match.FirstId, phase = "finished" } });
        var error = await match.First.WaitMessage(m => MessageType(m) == "error");
        Assert.Equal("unknown_action", error.GetProperty("code").GetString());
        var rejected = await match.Second.WaitState(s => s.Tick >= before.Tick + 9 && Fighter(s, match.FirstId).LastInputSequence == 1);
        Assert.Equal("playing", rejected.Phase);
        Assert.Null(rejected.WinnerId);
        Assert.Empty(rejected.Projectiles);
        Assert.All(rejected.Fighters, f =>
        {
            Assert.Equal(0, f.Damage);
            Assert.Equal(3, f.Stocks);
            Assert.Equal(100, f.Combat!.Energy);
            Assert.Equal(0, f.Combat.Ultimate);
            Assert.Null(f.Combat.CapturedBy);
            Assert.False(f.Combat.Armored);
            Assert.Null(f.Combat.ActiveAbility);
        });
        await AssertSharedState(match.First, rejected);

        await match.First.Send(new { type = "input", sequence = 50, special = true });
        var cast = await match.Second.WaitState(s => s.Tick > rejected.Tick && s.Projectiles.Any(p => p.OwnerId == match.FirstId));
        var castEnergy = Fighter(cast, match.FirstId).Combat!.Energy;
        var castX = Fighter(cast, match.FirstId).X;
        await match.First.Send(new { type = "input", sequence = 100 });
        for (var i = 0; i < 8; i++)
        {
            await match.First.Send(new { type = "input", sequence = 99, horizontal = 1 });
            await match.First.Send(new { type = "input", sequence = 50, special = true });
        }
        var settled = await match.Second.WaitState(s => s.Tick >= cast.Tick + 48);
        Assert.Equal(100, Fighter(settled, match.FirstId).LastInputSequence);
        Assert.Equal(castX, Fighter(settled, match.FirstId).X);
        Assert.True(Fighter(settled, match.FirstId).Combat!.Energy > castEnergy);
        Assert.Equal(7, Fighter(settled, match.SecondId).Damage);
        var projectileEvents = match.Second.States()
            .SelectMany(s => s.Events).Where(e => e.Type == "projectile" && e.ActorId == match.FirstId)
            .Select(e => e.Id).Distinct();
        Assert.Single(projectileEvents);
        await AssertSharedState(match.First, settled);
    }

    [Fact]
    public async Task BipMagnetPhysicallyPullsAStationaryOpponentWithoutCapturingThem()
    {
        await using var match = await StartMatch();
        var positioned = await MoveBipNearRaymond(match, 175);
        var rayX = Fighter(positioned, match.SecondId).X;
        await match.First.Input(grab: true);
        var pulled = await match.Second.WaitState(s => s.Tick > positioned.Tick &&
            Fighter(s, match.SecondId).X < rayX - 3 &&
            s.Events.Any(e => e.Type == "pull" && e.ActorId == match.FirstId && e.TargetId == match.SecondId));
        Assert.Null(Fighter(pulled, match.SecondId).Combat!.CapturedBy);
        Assert.Equal(0, Fighter(pulled, match.SecondId).Damage);
        Assert.True(Fighter(pulled, match.FirstId).Combat!.Energy < 90);
        Assert.True(Fighter(pulled, match.FirstId).Combat!.Cooldowns.Grab > 0);
        await AssertSharedState(match.First, pulled);
    }

    [Fact]
    public async Task RaymondCaptureRestrainsTheOtherClientAndDirectionallyThrowsThem()
    {
        await using var match = await StartMatch();
        var positioned = await MoveBipNearRaymond(match, 85);
        await match.Second.Input(grab: true);
        var captured = await match.First.WaitState(s => s.Tick > positioned.Tick &&
            Fighter(s, match.FirstId).Combat!.CapturedBy == match.SecondId);
        Assert.Equal(0, Fighter(captured, match.FirstId).Damage);
        Assert.InRange(Math.Abs(Fighter(captured, match.FirstId).X - Fighter(captured, match.SecondId).X), 50, 65);
        await AssertSharedState(match.Second, captured);

        // The captured client's ordinary movement cannot undo a server capture.
        await match.First.Input(horizontal: -1);
        await match.Second.Input();
        var restrained = await match.First.WaitState(s => s.Tick >= captured.Tick + 6 &&
            Fighter(s, match.FirstId).Combat!.CapturedBy == match.SecondId);
        Assert.InRange(Math.Abs(Fighter(restrained, match.FirstId).X - Fighter(restrained, match.SecondId).X), 50, 65);
        await match.Second.Input(horizontal: 1, vertical: -1, attack: true);
        var thrown = await match.First.WaitState(s => s.Tick > restrained.Tick &&
            s.Events.Any(e => e.Type == "throw" && e.ActorId == match.SecondId && e.TargetId == match.FirstId));
        var victim = Fighter(thrown, match.FirstId);
        Assert.Null(victim.Combat!.CapturedBy);
        Assert.Equal(13, victim.Damage);
        Assert.True(victim.Vx > 100);
        Assert.True(victim.Vy < -100);
        Assert.True(Fighter(thrown, match.SecondId).Combat!.Ultimate > 0);
        await AssertSharedState(match.Second, thrown);
    }

    [Fact]
    public async Task ReconnectPreservesSpentEnergyCooldownsAndStocksButStartsANewInputEpoch()
    {
        await using var match = await StartMatch();
        var before = await match.First.WaitState(s => s.Phase == "playing" && s.Tick >= 216);
        await match.First.Input(grab: true);
        var spent = await match.Second.WaitState(s => s.Tick > before.Tick &&
            Fighter(s, match.FirstId).Combat!.Cooldowns.Grab > 1.2);
        Assert.InRange(Fighter(spent, match.FirstId).Combat!.Energy, 80, 85);

        using var alice = Client(_alice);
        var renewal = await alice.PostAsJsonAsync($"/api/arena/rooms/{match.Code}/ticket", new { });
        renewal.EnsureSuccessStatusCode();
        var admission = (await renewal.Content.ReadFromJsonAsync<ArenaAdmission>())!;
        Assert.Equal(match.FirstId, admission.PlayerId);
        await using var replacement = await Connect(admission);
        var initialReconnect = await replacement.WaitState(s => s.Tick >= spent.Tick && Fighter(s, match.FirstId).Connected);
        // The immediate reconnect snapshot can fall between broadcast ticks.
        var reconnected = await replacement.WaitState(s => s.Tick >= initialReconnect.Tick + 3);
        var fighter = Fighter(reconnected, match.FirstId);
        Assert.Equal(3, fighter.Stocks);
        Assert.Equal(0, fighter.Damage);
        Assert.InRange(fighter.Combat!.Energy, Fighter(spent, match.FirstId).Combat!.Energy, 98);
        Assert.InRange(fighter.Combat.Cooldowns.Grab, .01, Fighter(spent, match.FirstId).Combat!.Cooldowns.Grab);
        Assert.Equal(0, fighter.Combat.Ultimate);
        Assert.Null(fighter.Combat.CapturedBy);
        await AssertSharedState(match.Second, reconnected);

        await replacement.Input(horizontal: 1);
        var moved = await match.Second.WaitState(s => s.Tick > reconnected.Tick &&
            Fighter(s, match.FirstId).LastInputSequence == 1 && Fighter(s, match.FirstId).X > fighter.X + 3);
        Assert.Equal(3, Fighter(moved, match.FirstId).Stocks);
        Assert.True(Fighter(moved, match.FirstId).Combat!.Energy < 100);
        await AssertSharedState(replacement, moved);
    }

    private async Task<Match> StartMatch(bool requestTraining = false, string firstAvatar = "bip01")
    {
        using var alice = Client(_alice);
        using var bob = Client(_bob);
        var created = await alice.PostAsJsonAsync("/api/arena/rooms", new
        {
            avatarId = firstAvatar, stageId = "rooftops", stocks = 3, minutes = 1, training = requestTraining
        });
        created.EnsureSuccessStatusCode();
        var host = (await created.Content.ReadFromJsonAsync<ArenaAdmission>())!;
        var joined = await bob.PostAsJsonAsync($"/api/arena/rooms/{host.Room.Code}/join", new ArenaJoinRequest(AvatarId: "raymond"));
        joined.EnsureSuccessStatusCode();
        var guest = (await joined.Content.ReadFromJsonAsync<ArenaAdmission>())!;
        var first = await Connect(host);
        var second = await Connect(guest);
        var match = new Match(host.Room.Code, host.PlayerId, guest.PlayerId, first, second);
        try
        {
            await first.Send(new { type = "ready", ready = true });
            await second.Send(new { type = "ready", ready = true });
            await first.WaitMessage(m => MessageType(m) == "room" &&
                m.GetProperty("room").GetProperty("players").EnumerateArray().All(p => p.GetProperty("ready").GetBoolean()));
            await first.Send(new { type = "start" });
            await first.WaitState(s => s.Phase == "playing" && s.Tick >= 216);
            return match;
        }
        catch
        {
            await match.DisposeAsync();
            throw;
        }
    }

    private static async Task<ArenaState> MoveBipNearRaymond(Match match, double distance)
    {
        var state = await match.First.WaitState(s => s.Phase == "playing" && s.Tick >= 216);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (Fighter(state, match.SecondId).X - Fighter(state, match.FirstId).X > distance)
        {
            deadline.Token.ThrowIfCancellationRequested();
            await match.First.Input(horizontal: 1);
            var tick = state.Tick;
            state = await match.First.WaitState(s => s.Tick >= tick + 3);
        }
        await match.First.Input();
        var stopTick = state.Tick;
        return await match.First.WaitState(s => s.Tick >= stopTick + 12 && Math.Abs(Fighter(s, match.FirstId).Vx) < 1);
    }

    private HttpClient Client(Guid subject)
    {
        var client = new HttpClient { BaseAddress = new Uri(_origin), Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.Add("X-Arena-Test-Subject", subject.ToString());
        return client;
    }

    private static AvatarProfile Profile(Guid subject, string avatar) => new()
    {
        UserSub = subject, SelectedAvatarId = avatar,
        Avatars = new List<AvatarOwnership>
        {
            new() { UserSub = subject, AvatarId = "starter" }, new() { UserSub = subject, AvatarId = avatar }
        }
    };

    private async Task<Peer> Connect(ArenaAdmission admission)
    {
        var socket = new ClientWebSocket();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(new Uri(_origin.Replace("http:", "ws:") + admission.WebSocketPath), deadline.Token);
        return new Peer(socket);
    }

    private static ArenaFighterSnapshot Fighter(ArenaState state, string id) => state.Fighters.Single(f => f.Id == id);
    private static string? MessageType(JsonElement message) => message.GetProperty("type").GetString();
    private static async Task AssertSharedState(Peer other, ArenaState expected)
    {
        var actual = await other.WaitState(s => s.Tick == expected.Tick);
        Assert.Equal(JsonSerializer.Serialize(expected, Json), JsonSerializer.Serialize(actual, Json));
    }

    private sealed record Match(string Code, string FirstId, string SecondId, Peer First, Peer Second) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await First.DisposeAsync();
            await Second.DisposeAsync();
        }
    }

    // Drain both sockets concurrently so predicates and assertions never compare
    // an old receive backlog with a live state on the other machine.
    private sealed class Peer : IAsyncDisposable
    {
        private readonly ClientWebSocket _socket;
        private readonly CancellationTokenSource _stop = new();
        private readonly List<(JsonElement Message, ArenaState? State)> _received = [];
        private readonly Task _reader;
        private Exception? _readFailure;
        private long _sequence;

        public Peer(ClientWebSocket socket)
        {
            _socket = socket;
            _reader = Read();
        }

        public Task Input(double horizontal = 0, double vertical = 0, bool special = false,
            bool grab = false, bool attack = false) => Send(new
        {
            type = "input", sequence = ++_sequence, horizontal, vertical, special, grab, attack
        });

        public async Task Send(object message)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message, Json).AsMemory(),
                WebSocketMessageType.Text, true, deadline.Token);
        }

        public ArenaState[] States()
        {
            lock (_received) return _received.Where(m => m.State is not null).Select(m => m.State!).ToArray();
        }

        public async Task<ArenaState> WaitState(Func<ArenaState, bool> predicate, int seconds = 8)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(seconds))
            {
                lock (_received)
                {
                    foreach (var packet in _received)
                        if (packet.State is { } state && predicate(state)) return state;
                    if (_readFailure is not null) throw new InvalidOperationException("WebSocket reader failed.", _readFailure);
                }
                await Task.Delay(10);
            }
            var last = States().LastOrDefault();
            throw new TimeoutException("Expected authoritative arena state was not received. Last state: " + JsonSerializer.Serialize(last, Json));
        }

        public async Task<JsonElement> WaitMessage(Func<JsonElement, bool> predicate)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(8))
            {
                lock (_received)
                {
                    foreach (var packet in _received)
                        if (predicate(packet.Message)) return packet.Message;
                    if (_readFailure is not null) throw new InvalidOperationException("WebSocket reader failed.", _readFailure);
                }
                await Task.Delay(10);
            }
            throw new TimeoutException("Expected arena WebSocket message was not received.");
        }

        private async Task Read()
        {
            var buffer = new byte[65536];
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    var length = 0;
                    ValueWebSocketReceiveResult packet;
                    do
                    {
                        packet = await _socket.ReceiveAsync(buffer.AsMemory(length), _stop.Token);
                        if (packet.MessageType == WebSocketMessageType.Close) return;
                        length += packet.Count;
                    } while (!packet.EndOfMessage);
                    using var document = JsonDocument.Parse(buffer.AsMemory(0, length));
                    var message = document.RootElement.Clone();
                    var state = MessageType(message) == "state" ? message.GetProperty("state").Deserialize<ArenaState>(Json) : null;
                    lock (_received) _received.Add((message, state));
                }
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException)
            {
                if (!_stop.IsCancellationRequested) lock (_received) _readFailure = ex;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _socket.Abort();
            await _reader;
            _socket.Dispose();
            _stop.Dispose();
        }
    }

    public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Arena-Test-Subject"], out var subject))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new("sub", subject.ToString()), new("name", "Testspiller")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
