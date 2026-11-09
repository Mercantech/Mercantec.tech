using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Arena;

/// <summary>One authoritative simulation per room. Only input crosses the client boundary.</summary>
public sealed class ArenaRoomService(IServiceScopeFactory scopes, ILogger<ArenaRoomService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] Colors = ["#f77168", "#64c9f1", "#f2cd60", "#b9a0ef"];
    private static readonly HashSet<string> Stages = ["rooftops", "workshop", "skyline"];
    private readonly object _gate = new();
    private readonly Dictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Ticket> _tickets = new(StringComparer.Ordinal);
    public const int MaximumRooms = 128;
    public const int ReconnectSeconds = 30;

    public IReadOnlyList<ArenaRoomDto> ListRooms()
    {
        lock (_gate)
            return _rooms.Values.Where(r => r.IsPublic && r.Phase == "lobby" && r.Players.Count < 4 && r.Players.Any(p => p.Connection is not null))
                .OrderByDescending(r => r.CreatedAt).Take(40).Select(View).ToArray();
    }

    public ArenaAdmission Create(ArenaIdentity identity, ArenaCreateRequest request)
    {
        ValidateSettings(request.StageId, request.Stocks, request.Minutes);
        ValidateAvatar(identity.AvatarId, identity.OwnedAvatars);
        lock (_gate)
        {
            RemoveExpired(DateTimeOffset.UtcNow);
            if (_rooms.Count >= MaximumRooms) throw Error(503, "arena_busy", "Arenaen er fuld lige nu. Prøv igen om lidt.");
            EnsureNotInOtherRoom(identity.Subject, null);
            string code;
            do { code = NewCode(); } while (_rooms.ContainsKey(code));
            var room = new Room(code, request.StageId, request.Stocks, request.Minutes, request.IsPublic);
            var member = NewPlayer(identity, room);
            room.Players.Add(member);
            room.HostId = member.Id;
            _rooms.Add(code, room);
            return Admission(room, member);
        }
    }

    public ArenaAdmission Join(string code, ArenaIdentity identity)
    {
        ValidateAvatar(identity.AvatarId, identity.OwnedAvatars);
        lock (_gate)
        {
            RemoveExpired(DateTimeOffset.UtcNow);
            var room = FindRoom(code);
            EnsureNotInOtherRoom(identity.Subject, room.Code);
            var existing = room.Players.FirstOrDefault(p => p.Subject == identity.Subject);
            if (existing is not null) return Admission(room, existing);
            if (room.Phase != "lobby") throw Error(409, "match_started", "Kampen er allerede startet. Vent på næste runde.");
            if (room.Players.Count >= 4) throw Error(409, "room_full", "Rummet har allerede fire spillere.");
            var player = NewPlayer(identity, room);
            room.Players.Add(player);
            BroadcastRoom(room);
            return Admission(room, player);
        }
    }

    public ArenaAdmission Reconnect(string code, Guid subject)
    {
        lock (_gate)
        {
            var room = FindRoom(code);
            var player = room.Players.FirstOrDefault(p => p.Subject == subject)
                ?? throw Error(403, "not_in_room", "Du er ikke med i dette rum.");
            if (player.Connection is null && DateTimeOffset.UtcNow - player.DisconnectedAt > TimeSpan.FromSeconds(ReconnectSeconds))
                throw Error(410, "reconnect_expired", "Tiden til at genoprette forbindelsen er udløbet.");
            return Admission(room, player);
        }
    }

    public sealed record TicketGrant(string Code, string PlayerId, Guid Subject);

    // Consumption happens before accepting the websocket: tickets cannot be replayed.
    public TicketGrant ConsumeTicket(string? ticket)
    {
        lock (_gate)
        {
            if (ticket is null || !_tickets.Remove(ticket, out var grant) || grant.ExpiresAt < DateTimeOffset.UtcNow)
                throw Error(401, "ticket_expired", "Forbindelsen er udløbet. Tilslut rummet igen.");
            var room = FindRoom(grant.Code);
            if (!room.Players.Any(p => p.Id == grant.PlayerId)) throw Error(410, "player_left", "Du er ikke længere med i rummet.");
            return new(grant.Code, grant.PlayerId, grant.Subject);
        }
    }

    public void Leave(string code, Guid subject)
    {
        lock (_gate)
        {
            var room = FindRoom(code);
            var member = room.Players.FirstOrDefault(p => p.Subject == subject);
            if (member is not null) RemovePlayer(room, member);
        }
    }

    public async Task RunSocketAsync(TicketGrant grant, WebSocket socket, CancellationToken cancellationToken)
    {
        using var connection = new Connection(socket, cancellationToken);
        lock (_gate)
        {
            var room = FindRoom(grant.Code);
            var player = room.Players.FirstOrDefault(p => p.Id == grant.PlayerId && p.Subject == grant.Subject)
                ?? throw Error(410, "player_left", "Du er ikke længere med i rummet.");
            player.Connection?.Stop();
            player.Connection = connection;
            player.LastSequence = -1;
            player.DisconnectedAt = DateTimeOffset.MaxValue;
            player.LastInputAt = DateTimeOffset.UtcNow;
            room.Simulation?.SetConnected(player.Id, true);
            BroadcastRoom(room);
            if (room.Simulation is not null) Send(connection, new { type = "state", state = room.Simulation.Snapshot() });
        }
        var sending = SendLoop(connection);
        try { await ReceiveLoop(grant, connection); }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException) { }
        finally
        {
            lock (_gate)
            {
                if (_rooms.TryGetValue(grant.Code, out var room))
                {
                    var player = room.Players.FirstOrDefault(p => p.Id == grant.PlayerId);
                    if (player?.Connection == connection)
                    {
                        player.Connection = null;
                        player.DisconnectedAt = DateTimeOffset.UtcNow;
                        player.Ready = false;
                        room.Simulation?.SetConnected(player.Id, false);
                        room.Simulation?.SetInput(player.Id, new ArenaInput());
                        MigrateHost(room);
                        BroadcastRoom(room);
                    }
                }
            }
            connection.Stop();
            try { await sending; } catch (Exception ex) when (ex is WebSocketException or OperationCanceledException) { }
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Forbindelsen er lukket.", CancellationToken.None); }
                catch (WebSocketException) { }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000d / 60));
        var clock = Stopwatch.StartNew();
        var simulated = 0d;
        long ticks = 0;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTimeOffset.UtcNow;
            var steps = Math.Clamp((int)((clock.Elapsed.TotalSeconds - simulated) * 60), 1, 5);
            simulated += steps / 60d;
            // Discard accumulated debt after a long process pause instead of freezing every room.
            if (clock.Elapsed.TotalSeconds - simulated > .25) simulated = clock.Elapsed.TotalSeconds;
            lock (_gate)
            {
                foreach (var room in _rooms.Values.ToArray())
                {
                    if (room.Simulation is null) continue;
                    try
                    {
                        for (var i = 0; i < steps; i++) room.Simulation.Tick();
                        var state = room.Simulation.Snapshot();
                        if (room.Phase != state.Phase) { room.Phase = state.Phase; BroadcastRoom(room); }
                        if (ticks % 3 == 0) Broadcast(room, new { type = "state", state });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Arena simulation failed in room {Room}", room.Code);
                        ResetLobby(room);
                        Broadcast(room, new { type = "error", message = "Kampen blev afbrudt. Rummet er klar til en ny runde." });
                        BroadcastRoom(room);
                    }
                }
                if (++ticks % 60 == 0) RemoveExpired(now);
            }
        }
    }

    private async Task ReceiveLoop(TicketGrant grant, Connection connection)
    {
        var buffer = new byte[4096];
        var count = 0;
        var windowStarted = Stopwatch.GetTimestamp();
        var messages = 0;
        while (!connection.Token.IsCancellationRequested && connection.Socket.State == WebSocketState.Open)
        {
            var result = await connection.Socket.ReceiveAsync(buffer.AsMemory(count), connection.Token);
            if (result.MessageType == WebSocketMessageType.Close) return;
            if (result.MessageType != WebSocketMessageType.Text) { await RejectSocket(connection, "Kun tekstbeskeder er tilladt."); return; }
            count += result.Count;
            if (count == buffer.Length && !result.EndOfMessage) { await RejectSocket(connection, "Beskeden er for stor."); return; }
            if (!result.EndOfMessage) continue;
            if (Stopwatch.GetElapsedTime(windowStarted) > TimeSpan.FromSeconds(1)) { windowStarted = Stopwatch.GetTimestamp(); messages = 0; }
            if (++messages > 120) { await RejectSocket(connection, "Der blev sendt for mange beskeder."); return; }
            try
            {
                using var document = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 8 });
                var message = document.RootElement;
                if (message.ValueKind != JsonValueKind.Object) throw new JsonException();
                var type = GetString(message, "type");
                string? avatar = null;
                if (type == "avatar")
                {
                    avatar = GetString(message, "avatarId");
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    if (!await OwnsAvatarAsync(db, grant.Subject, avatar, connection.Token))
                        throw Error(403, "avatar_not_owned", "Lås avataren op på din profil først.");
                }
                lock (_gate)
                {
                    if (!_rooms.TryGetValue(grant.Code, out var room)) return;
                    var player = room.Players.FirstOrDefault(p => p.Id == grant.PlayerId);
                    if (player?.Connection != connection) return;
                    switch (type)
                    {
                        case "input":
                            var input = message.Deserialize<ArenaInput>(Json) ?? new ArenaInput();
                            if (!double.IsFinite(input.Horizontal) || !double.IsFinite(input.Vertical) || input.Sequence < 0) break;
                            if (input.Sequence <= player.LastSequence) break;
                            player.LastSequence = input.Sequence;
                            player.LastInputAt = DateTimeOffset.UtcNow;
                            room.Simulation?.SetInput(player.Id, input with { Horizontal = Math.Clamp(input.Horizontal, -1, 1), Vertical = Math.Clamp(input.Vertical, -1, 1) });
                            break;
                        case "ping":
                            Send(connection, new { type = "pong", sentAt = message.TryGetProperty("sentAt", out var sentAt) && sentAt.TryGetDouble(out var time) && double.IsFinite(time) ? time : 0 });
                            break;
                        case "ready":
                            RequireLobby(room);
                            player.Ready = GetBool(message, "ready");
                            BroadcastRoom(room);
                            break;
                        case "start":
                            RequireHost(room, player);
                            RequireLobby(room);
                            if (room.Players.Count < 2 || room.Players.Any(p => !p.IsBot && (p.Connection is null || !p.Ready)))
                                throw Error(409, "players_not_ready", "Der skal være mindst to spillere, og alle skal være klar.");
                            var simulation = new ArenaSimulation(room.StageId, room.Stocks, room.Minutes);
                            foreach (var fighter in room.Players) simulation.AddFighter(fighter.Id, fighter.Name, fighter.AvatarId, fighter.IsBot, fighter.Color, fighter.Appearance);
                            room.Simulation = simulation;
                            simulation.Start();
                            room.Phase = "countdown";
                            BroadcastRoom(room);
                            Broadcast(room, new { type = "state", state = simulation.Snapshot() });
                            break;
                        case "configure":
                            RequireHost(room, player);
                            RequireLobby(room);
                            var stage = GetString(message, "stageId");
                            var stocks = GetInt(message, "stocks", room.Stocks);
                            var minutes = GetInt(message, "minutes", room.Minutes);
                            ValidateSettings(stage, stocks, minutes);
                            room.StageId = stage;
                            room.Stocks = stocks;
                            room.Minutes = minutes;
                            room.IsPublic = GetBool(message, "isPublic");
                            ResetReady(room);
                            BroadcastRoom(room);
                            break;
                        case "avatar":
                            RequireLobby(room);
                            player.AvatarId = avatar!;
                            player.Ready = false;
                            BroadcastRoom(room);
                            break;
                        case "bot":
                            RequireHost(room, player);
                            RequireLobby(room);
                            if (GetBool(message, "add"))
                            {
                                if (room.Players.Count == 4) throw Error(409, "room_full", "Der er allerede fire spillere.");
                                var ids = new[] { "starter", "bip01", "raymond", "santa" };
                                room.Players.Add(new Player(Guid.NewGuid().ToString("N"), null, "Træningsbot", ids[room.Players.Count % ids.Length], AvailableColor(room), true));
                            }
                            else
                            {
                                var bot = room.Players.LastOrDefault(p => p.IsBot);
                                if (bot is not null) room.Players.Remove(bot);
                            }
                            BroadcastRoom(room);
                            break;
                        case "rematch":
                            RequireHost(room, player);
                            if (room.Phase != "finished") throw Error(409, "match_running", "Vent, til kampen er færdig.");
                            ResetLobby(room);
                            BroadcastRoom(room);
                            break;
                        case "leave":
                            RemovePlayer(room, player);
                            return;
                        default:
                            throw Error(400, "unknown_action", "Denne handling findes ikke.");
                    }
                }
            }
            catch (ArenaException ex) { Send(connection, new { type = "error", code = ex.Code, message = ex.Message }); }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            { Send(connection, new { type = "error", code = "invalid_message", message = "Beskeden kunne ikke læses." }); }
            count = 0;
        }
    }

    public static async Task<bool> OwnsAvatarAsync(AppDbContext db, Guid subject, string? avatar, CancellationToken ct)
    {
        if (AvatarEconomy.FindAvatar(avatar) is null) return false;
        if (avatar == AvatarEconomy.StarterId) return true;
        return await db.AvatarOwnerships.AsNoTracking().AnyAsync(x => x.UserSub == subject && x.AvatarId == avatar, ct);
    }

    private static async Task SendLoop(Connection connection)
    {
        try
        {
            await foreach (var message in connection.Outbound.Reader.ReadAllAsync(connection.Token))
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(connection.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                await connection.Socket.SendAsync(message, WebSocketMessageType.Text, true, timeout.Token);
            }
        }
        finally { connection.Stop(); }
    }

    private static async Task RejectSocket(Connection connection, string reason)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await connection.Socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, reason, timeout.Token); }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException) { }
    }

    private static void Send(Connection connection, object message) => connection.Outbound.Writer.TryWrite(JsonSerializer.SerializeToUtf8Bytes(message, Json));
    private static void Broadcast(Room room, object message)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        foreach (var player in room.Players) player.Connection?.Outbound.Writer.TryWrite(bytes);
    }
    private static void BroadcastRoom(Room room) => Broadcast(room, new { type = "room", room = View(room) });

    private ArenaAdmission Admission(Room room, Player player)
    {
        foreach (var key in _tickets.Where(t => t.Value.PlayerId == player.Id || t.Value.ExpiresAt <= DateTimeOffset.UtcNow).Select(t => t.Key).ToArray()) _tickets.Remove(key);
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expires = DateTimeOffset.UtcNow.AddSeconds(30);
        _tickets.Add(ticket, new(room.Code, player.Id, player.Subject!.Value, expires));
        return new(View(room), player.Id, ticket, "/api/arena/connect?ticket=" + ticket, expires);
    }

    private void EnsureNotInOtherRoom(Guid subject, string? target)
    {
        foreach (var room in _rooms.Values.ToArray())
        {
            if (room.Code == target) continue;
            var existing = room.Players.FirstOrDefault(p => p.Subject == subject);
            if (existing is null) continue;
            if (room.Phase is "playing" or "countdown") throw Error(409, "already_playing", "Afslut eller forlad din nuværende kamp først.");
            RemovePlayer(room, existing);
        }
    }

    private void RemovePlayer(Room room, Player player)
    {
        player.Connection?.Stop();
        room.Players.Remove(player);
        room.Simulation?.RemoveFighter(player.Id);
        foreach (var key in _tickets.Where(t => t.Value.PlayerId == player.Id).Select(t => t.Key).ToArray()) _tickets.Remove(key);
        if (!room.Players.Any(p => !p.IsBot)) { _rooms.Remove(room.Code); return; }
        MigrateHost(room);
        BroadcastRoom(room);
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var key in _tickets.Where(x => x.Value.ExpiresAt <= now).Select(x => x.Key).ToArray()) _tickets.Remove(key);
        foreach (var room in _rooms.Values.ToArray())
        {
            foreach (var player in room.Players.Where(p => !p.IsBot && p.Connection is null && now - p.DisconnectedAt > TimeSpan.FromSeconds(ReconnectSeconds)).ToArray())
                RemovePlayer(room, player);
            if (now - room.CreatedAt > TimeSpan.FromHours(4))
            {
                foreach (var player in room.Players) player.Connection?.Stop();
                _rooms.Remove(room.Code);
            }
        }
    }

    private Room FindRoom(string code) => code.Length == 6 && _rooms.TryGetValue(code, out var room) ? room : throw Error(404, "room_not_found", "Rummet findes ikke. Kontrollér de seks tegn i koden.");
    private static ArenaRoomDto View(Room room) => new(room.Code, room.HostId, room.Phase, room.StageId, room.Stocks, room.Minutes, room.IsPublic,
        room.Players.Select(p => new ArenaPlayerDto(p.Id, p.Name, p.AvatarId, p.Ready, p.Connection is not null || p.IsBot, p.IsBot, p.Color, p.Appearance)).ToArray());
    private static void ValidateSettings(string stage, int stocks, int minutes)
    {
        if (!Stages.Contains(stage) || stocks is < 1 or > 5 || minutes is < 1 or > 10)
            throw Error(400, "invalid_settings", "Vælg en gyldig bane, 1–5 liv og 1–10 minutter.");
    }
    private static void ValidateAvatar(string id, IReadOnlySet<string> owned)
    {
        if (AvatarEconomy.FindAvatar(id) is null || !owned.Contains(id)) throw Error(403, "avatar_not_owned", "Lås avataren op på din profil først.");
    }
    private static Player NewPlayer(ArenaIdentity identity, Room room) => new(Guid.NewGuid().ToString("N"), identity.Subject,
        string.IsNullOrWhiteSpace(identity.Name) ? "Spiller" : new string(identity.Name.Where(c => !char.IsControl(c)).Take(40).ToArray()), identity.AvatarId, AvailableColor(room), false, ArenaAppearance.Sanitize(identity.Appearance));
    private static string AvailableColor(Room room) => Colors.FirstOrDefault(c => room.Players.All(p => p.Color != c)) ?? Colors[0];
    private static string NewCode() { const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; return new(Enumerable.Range(0, 6).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray()); }
    private static void RequireHost(Room room, Player player) { if (room.HostId != player.Id) throw Error(403, "host_required", "Kun værten kan ændre dette."); }
    private static void RequireLobby(Room room) { if (room.Phase != "lobby") throw Error(409, "match_started", "Vent, til kampen er færdig."); }
    private static void ResetReady(Room room) { foreach (var p in room.Players) p.Ready = p.IsBot; }
    private static void ResetLobby(Room room) { room.Simulation = null; room.Phase = "lobby"; ResetReady(room); }
    private static void MigrateHost(Room room)
    {
        if (room.Players.Any(p => p.Id == room.HostId && p.Connection is not null)) return;
        room.HostId = room.Players.FirstOrDefault(p => !p.IsBot && p.Connection is not null)?.Id
            ?? room.Players.FirstOrDefault(p => !p.IsBot)?.Id ?? "";
    }
    private static string GetString(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() ?? "" : "";
    private static bool GetBool(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.True;
    private static int GetInt(JsonElement value, string name, int fallback) => value.TryGetProperty(name, out var property) && property.TryGetInt32(out var number) ? number : fallback;
    private static ArenaException Error(int status, string code, string message) => new(status, code, message);

    private sealed class Room(string code, string stage, int stocks, int minutes, bool isPublic)
    {
        public string Code { get; } = code;
        public string HostId { get; set; } = "";
        public string Phase { get; set; } = "lobby";
        public string StageId { get; set; } = stage;
        public int Stocks { get; set; } = stocks;
        public int Minutes { get; set; } = minutes;
        public bool IsPublic { get; set; } = isPublic;
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public List<Player> Players { get; } = [];
        public ArenaSimulation? Simulation { get; set; }
    }
    private sealed class Player(string id, Guid? subject, string name, string avatarId, string color, bool bot, ArenaAppearance? appearance = null)
    {
        public string Id { get; } = id;
        public Guid? Subject { get; } = subject;
        public string Name { get; } = name;
        public string AvatarId { get; set; } = avatarId;
        public string Color { get; } = color;
        public ArenaAppearance Appearance { get; } = ArenaAppearance.Sanitize(appearance);
        public bool IsBot { get; } = bot;
        public bool Ready { get; set; } = bot;
        public DateTimeOffset DisconnectedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastInputAt { get; set; } = DateTimeOffset.UtcNow;
        public long LastSequence { get; set; } = -1;
        public Connection? Connection { get; set; }
    }
    private sealed record Ticket(string Code, string PlayerId, Guid Subject, DateTimeOffset ExpiresAt);
    private sealed class Connection(WebSocket socket, CancellationToken cancellationToken) : IDisposable
    {
        private readonly CancellationTokenSource _cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        public WebSocket Socket { get; } = socket;
        public CancellationToken Token => _cancel.Token;
        public Channel<byte[]> Outbound { get; } = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(16) { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });
        public void Stop() { _cancel.Cancel(); Outbound.Writer.TryComplete(); }
        public void Dispose() { Stop(); _cancel.Dispose(); }
    }
}
