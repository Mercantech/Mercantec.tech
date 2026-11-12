using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Arena;

/// <summary>One authoritative simulation per room. Only input crosses the client boundary.</summary>
public sealed class ArenaRoomService(IServiceScopeFactory scopes, ILogger<ArenaRoomService> logger, TimeProvider? timeProvider = null) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] Colors = ["#f77168", "#64c9f1", "#f2cd60", "#b9a0ef"];
    private static readonly HashSet<string> Stages = ["rooftops", "workshop", "skyline"];
    private readonly object _gate = new();
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Ticket> _tickets = new(StringComparer.Ordinal);
    public const int MaximumRooms = 128;
    public const int ReconnectSeconds = 30;
    public static readonly TimeSpan LobbyAfkDelay = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan MatchAfkDelay = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan DisconnectAfkDelay = TimeSpan.FromSeconds(10);

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
            var now = _time.GetUtcNow();
            RemoveExpired(now);
            if (_rooms.Count >= MaximumRooms) throw Error(503, "arena_busy", "Arenaen er fuld lige nu. Prøv igen om lidt.");
            EnsureNotInOtherRoom(identity.Subject, null);
            string code;
            do { code = NewCode(); } while (_rooms.ContainsKey(code));
            var room = new Room(code, request.StageId, request.Stocks, request.Minutes, request.IsPublic, now);
            var member = NewPlayer(identity, room, now);
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
            var now = _time.GetUtcNow();
            RemoveExpired(now);
            var room = FindRoom(code);
            EnsureNotInOtherRoom(identity.Subject, room.Code);
            var existing = room.Players.FirstOrDefault(p => p.Subject == identity.Subject);
            if (existing is not null) return Admission(room, existing);
            if (room.Phase != "lobby") throw Error(409, "match_started", "Kampen er allerede startet. Vent på næste runde.");
            if (room.Players.Count >= 4) throw Error(409, "room_full", "Rummet har allerede fire spillere.");
            var player = NewPlayer(identity, room, now);
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
            if (player.Connection is null && _time.GetUtcNow() - player.DisconnectedAt > TimeSpan.FromSeconds(ReconnectSeconds))
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
            if (ticket is null || !_tickets.Remove(ticket, out var grant) || grant.ExpiresAt <= _time.GetUtcNow())
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
            player.Connection?.RequestClose(WebSocketCloseStatus.NormalClosure, "En ny forbindelse blev åbnet.");
            player.Connection = connection;
            player.LastSequence = -1;
            player.HeldButtons = ArenaPress.None;
            player.DisconnectedAt = DateTimeOffset.MaxValue;
            player.LastActivityAt = _time.GetUtcNow();
            room.Simulation?.SetConnected(player.Id, true);
            BroadcastRoom(room);
            if (room.Simulation is not null) connection.PublishState(SerializeState(room.Simulation));
        }
        var sending = SendLoop(connection);
        try { await ReceiveLoop(grant, connection); }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException) { }
        finally
        {
            connection.ReceiverFinished.TrySetResult();
            lock (_gate)
            {
                if (_rooms.TryGetValue(grant.Code, out var room))
                {
                    var player = room.Players.FirstOrDefault(p => p.Id == grant.PlayerId);
                    if (player?.Connection == connection)
                    {
                        player.Connection = null;
                        player.DisconnectedAt = _time.GetUtcNow();
                        player.Ready = false;
                        room.Simulation?.SetConnected(player.Id, false);
                        MigrateHost(room);
                        BroadcastRoom(room);
                    }
                }
            }
            connection.RequestClose(WebSocketCloseStatus.NormalClosure, "Forbindelsen er lukket.");
            try { await sending; }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException) { }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1d / ArenaCatalog.TickRate));
        var clock = Stopwatch.StartNew();
        long accountedTicks = 0;
        long simulatedTicks = 0;
        var nextMaintenance = TimeSpan.Zero;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var elapsed = clock.Elapsed;
            var due = (long)(elapsed.TotalSeconds * ArenaCatalog.TickRate) - accountedTicks;
            if (due <= 0) continue;
            accountedTicks += due;
            // Limit catch-up after a stalled process so commands can run between ticks.
            var steps = (int)Math.Min(due, 5);
            var publish = (simulatedTicks + steps) / 3 != simulatedTicks / 3;
            simulatedTicks += steps;
            Room[] rooms;
            lock (_gate)
            {
                if (elapsed >= nextMaintenance)
                {
                    var now = _time.GetUtcNow();
                    RemoveExpired(now);
                    foreach (var room in _rooms.Values)
                        if (room.Players.Any(player => player.CanRemoveAfk != IsAfkEligible(room, player, now)))
                            BroadcastRoom(room, now);
                    nextMaintenance = elapsed + TimeSpan.FromSeconds(1);
                }
                rooms = _rooms.Values.ToArray();
            }

            foreach (var room in rooms)
            {
                lock (_gate)
                {
                    if (!_rooms.TryGetValue(room.Code, out var current) || current != room ||
                        room.Simulation is not { } simulation || room.Phase == "finished") continue;
                    try
                    {
                        for (var i = 0; i < steps; i++) simulation.Tick();
                        var phaseChanged = room.Phase != simulation.Phase;
                        if (phaseChanged)
                        {
                            room.Phase = simulation.Phase;
                            BroadcastRoom(room);
                        }
                        if (publish || phaseChanged) BroadcastState(room, simulation);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Arena simulation failed in room {Room}", room.Code);
                        ResetLobby(room);
                        Broadcast(room, new { type = "error", message = "Kampen blev afbrudt. Rummet er klar til en ny runde." });
                        BroadcastRoom(room);
                    }
                }
            }
        }
    }

    private async Task ReceiveLoop(TicketGrant grant, Connection connection)
    {
        var buffer = new byte[4096];
        var count = 0;
        var windowStarted = Stopwatch.GetTimestamp();
        var messages = 0;
        var commands = 0;
        while (!connection.Token.IsCancellationRequested &&
            connection.Socket.State is WebSocketState.Open or WebSocketState.CloseSent)
        {
            var result = await connection.Socket.ReceiveAsync(buffer.AsMemory(count), connection.Token);
            if (result.MessageType == WebSocketMessageType.Close) return;
            if (connection.IsClosing) { count = 0; continue; }
            if (result.MessageType != WebSocketMessageType.Text) { connection.RequestClose(WebSocketCloseStatus.PolicyViolation, "Kun tekstbeskeder er tilladt."); return; }
            count += result.Count;
            if (count == buffer.Length && !result.EndOfMessage) { connection.RequestClose(WebSocketCloseStatus.MessageTooBig, "Beskeden er for stor."); return; }
            if (!result.EndOfMessage) continue;
            if (Stopwatch.GetElapsedTime(windowStarted) > TimeSpan.FromSeconds(1))
            {
                windowStarted = Stopwatch.GetTimestamp();
                messages = commands = 0;
            }
            if (++messages > 120) { connection.RequestClose(WebSocketCloseStatus.PolicyViolation, "Der blev sendt for mange beskeder."); return; }
            try
            {
                using var document = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 8 });
                var message = document.RootElement;
                if (message.ValueKind != JsonValueKind.Object) throw new JsonException();
                var type = GetString(message, "type");
                if (type != "input" && ++commands > 24)
                {
                    connection.RequestClose(WebSocketCloseStatus.PolicyViolation, "Der blev sendt for mange kommandoer.");
                    return;
                }
                var input = type == "input" ? message.Deserialize<ArenaInput>(Json) : null;
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
                    if (player?.Connection != connection || connection.IsClosing) return;
                    switch (type)
                    {
                        case "input":
                            if (input is null || !double.IsFinite(input.Horizontal) || !double.IsFinite(input.Vertical) || input.Sequence < 0) break;
                            if (input.Sequence <= player.LastSequence) break;
                            player.LastSequence = input.Sequence;
                            room.Simulation?.SetInput(player.Id, input with { Horizontal = Math.Clamp(input.Horizontal, -1, 1), Vertical = Math.Clamp(input.Vertical, -1, 1) });
                            var held = HeldButtons(input);
                            if (room.Phase is "countdown" or "playing" &&
                                (Math.Abs(input.Horizontal) >= .2 || Math.Abs(input.Vertical) >= .2 ||
                                 (input.Pressed & 127) != 0 || held != player.HeldButtons))
                                MarkActive(room, player);
                            player.HeldButtons = held;
                            break;
                        case "ping":
                            Send(connection, new { type = "pong", sentAt = message.TryGetProperty("sentAt", out var sentAt) && sentAt.TryGetDouble(out var time) && double.IsFinite(time) ? time : 0 });
                            break;
                        case "ready":
                            RequireLobby(room);
                            var ready = GetBool(message, "ready");
                            if (player.Ready == ready) break;
                            player.Ready = ready;
                            player.LastActivityAt = _time.GetUtcNow();
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
                            var started = _time.GetUtcNow();
                            foreach (var fighter in room.Players)
                            {
                                fighter.LastActivityAt = started;
                                fighter.HeldButtons = ArenaPress.None;
                            }
                            BroadcastRoom(room);
                            BroadcastState(room, simulation);
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
                            player.LastActivityAt = _time.GetUtcNow();
                            ResetReady(room);
                            BroadcastRoom(room);
                            break;
                        case "avatar":
                            RequireLobby(room);
                            if (player.AvatarId == avatar) break;
                            player.AvatarId = avatar!;
                            player.Ready = false;
                            player.LastActivityAt = _time.GetUtcNow();
                            BroadcastRoom(room);
                            break;
                        case "bot":
                            RequireHost(room, player);
                            RequireLobby(room);
                            if (GetBool(message, "add"))
                            {
                                if (room.Players.Count == 4) throw Error(409, "room_full", "Der er allerede fire spillere.");
                                var ids = new[] { "starter", "bip01", "raymond", "santa", "lupp" };
                                var hostIndex = Math.Max(0, Array.IndexOf(ids, room.Players[0].AvatarId));
                                room.Players.Add(new Player(Guid.NewGuid().ToString("N"), null, "Træningsbot", ids[(hostIndex + room.Players.Count) % ids.Length], AvailableColor(room), true, _time.GetUtcNow()));
                            }
                            else
                            {
                                var bot = room.Players.LastOrDefault(p => p.IsBot);
                                if (bot is not null) room.Players.Remove(bot);
                            }
                            player.LastActivityAt = _time.GetUtcNow();
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
                        case "removeAfk":
                            RequireHost(room, player);
                            var inactive = room.Players.FirstOrDefault(p => p.Id == GetString(message, "playerId"));
                            if (inactive is null || !IsAfkEligible(room, inactive, _time.GetUtcNow()))
                                throw Error(409, "player_active", "Spilleren kan ikke fjernes som inaktiv lige nu.");
                            RemovePlayer(room, inactive, afk: true);
                            break;
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
            while (await connection.Wake.Reader.WaitToReadAsync(connection.Token))
            {
                connection.Wake.Reader.TryRead(out _);
                while (!connection.IsClosing && connection.Control.Reader.TryRead(out var control))
                    await WriteSocket(connection, control);

                if (connection.Close is { } close)
                {
                    if (close.FinalMessage is not null && connection.Socket.State == WebSocketState.Open)
                        await WriteSocket(connection, close.FinalMessage);
                    if (connection.Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(connection.Token);
                        timeout.CancelAfter(TimeSpan.FromSeconds(2));
                        await connection.Socket.CloseOutputAsync(close.Status, close.Reason, timeout.Token);
                    }
                    // Cancelling an outstanding ReceiveAsync aborts the socket. Give the peer
                    // time to receive the removal notice and acknowledge the close first.
                    if (connection.Socket.State == WebSocketState.CloseSent)
                    {
                        try { await connection.ReceiverFinished.Task.WaitAsync(TimeSpan.FromSeconds(2), connection.Token); }
                        catch (TimeoutException) { }
                    }
                    return;
                }

                if (connection.TakeState() is { } state) await WriteSocket(connection, state);
            }
        }
        finally { connection.Stop(); }
    }

    private static async Task WriteSocket(Connection connection, byte[] message)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(connection.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await connection.Socket.SendAsync(message.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
    }

    private static byte[] Serialize(object message) => JsonSerializer.SerializeToUtf8Bytes(message, Json);
    private static byte[] SerializeState(ArenaSimulation simulation) => Serialize(new { type = "state", state = simulation.Snapshot() });
    private static void Send(Connection connection, object message) => connection.PublishControl(Serialize(message));
    private static void Broadcast(Room room, object message)
    {
        var bytes = Serialize(message);
        foreach (var player in room.Players) player.Connection?.PublishControl(bytes);
    }
    private static void BroadcastState(Room room, ArenaSimulation simulation)
    {
        var bytes = SerializeState(simulation);
        foreach (var player in room.Players) player.Connection?.PublishState(bytes);
    }
    private void BroadcastRoom(Room room, DateTimeOffset? at = null)
    {
        var now = at ?? _time.GetUtcNow();
        foreach (var player in room.Players) player.CanRemoveAfk = IsAfkEligible(room, player, now);
        Broadcast(room, new { type = "room", room = View(room) });
    }

    private ArenaAdmission Admission(Room room, Player player)
    {
        var now = _time.GetUtcNow();
        foreach (var key in _tickets.Where(t => t.Value.PlayerId == player.Id || t.Value.ExpiresAt <= now).Select(t => t.Key).ToArray()) _tickets.Remove(key);
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expires = now.AddSeconds(30);
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

    private void RemovePlayer(Room room, Player player, bool afk = false)
    {
        if (afk)
        {
            player.Connection?.RequestClose(WebSocketCloseStatus.PolicyViolation, "afk_removed",
                Serialize(new { type = "removed", code = "afk_removed", message = "Værten fjernede dig som inaktiv." }));
        }
        else
        {
            player.Connection?.RequestClose(WebSocketCloseStatus.NormalClosure, "Du har forladt rummet.");
        }
        room.Players.Remove(player);
        room.Simulation?.RemoveFighter(player.Id);
        foreach (var key in _tickets.Where(t => t.Value.PlayerId == player.Id).Select(t => t.Key).ToArray()) _tickets.Remove(key);
        if (!room.Players.Any(p => !p.IsBot)) { _rooms.Remove(room.Code); return; }
        MigrateHost(room);
        BroadcastRoom(room);
    }

    private static bool IsAfkEligible(Room room, Player player, DateTimeOffset now)
    {
        if (player.IsBot || player.Id == room.HostId || room.Phase == "finished") return false;
        if (player.Connection is null) return now - player.DisconnectedAt >= DisconnectAfkDelay;
        var delay = room.Phase == "lobby" ? LobbyAfkDelay : MatchAfkDelay;
        return now - player.LastActivityAt >= delay;
    }

    private static ArenaPress HeldButtons(ArenaInput input) =>
        (input.Jump ? ArenaPress.Jump : ArenaPress.None) |
        (input.Attack ? ArenaPress.Attack : ArenaPress.None) |
        (input.Special ? ArenaPress.Special : ArenaPress.None) |
        (input.Shield ? ArenaPress.Shield : ArenaPress.None) |
        (input.Grab ? ArenaPress.Grab : ArenaPress.None) |
        (input.Smash ? ArenaPress.Smash : ArenaPress.None) |
        (input.Dodge ? ArenaPress.Dodge : ArenaPress.None);

    private void MarkActive(Room room, Player player)
    {
        player.LastActivityAt = _time.GetUtcNow();
        if (player.CanRemoveAfk) BroadcastRoom(room, player.LastActivityAt);
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
                foreach (var player in room.Players)
                    player.Connection?.RequestClose(WebSocketCloseStatus.NormalClosure, "Rummet er udløbet.");
                _rooms.Remove(room.Code);
            }
        }
    }

    private Room FindRoom(string code) => code.Length == 6 && _rooms.TryGetValue(code, out var room) ? room : throw Error(404, "room_not_found", "Rummet findes ikke. Kontrollér de seks tegn i koden.");
    private static ArenaRoomDto View(Room room) => new(room.Code, room.HostId, room.Phase, room.StageId, room.Stocks, room.Minutes, room.IsPublic,
        room.Players.Select(p => new ArenaPlayerDto(p.Id, p.Name, p.AvatarId, p.Ready, p.Connection is not null || p.IsBot, p.IsBot, p.Color, p.Appearance, p.CanRemoveAfk)).ToArray());
    private static void ValidateSettings(string stage, int stocks, int minutes)
    {
        if (!Stages.Contains(stage) || stocks is < 1 or > 5 || minutes is < 1 or > 10)
            throw Error(400, "invalid_settings", "Vælg en gyldig bane, 1–5 liv og 1–10 minutter.");
    }
    private static void ValidateAvatar(string id, IReadOnlySet<string> owned)
    {
        if (AvatarEconomy.FindAvatar(id) is null || !owned.Contains(id)) throw Error(403, "avatar_not_owned", "Lås avataren op på din profil først.");
    }
    private static Player NewPlayer(ArenaIdentity identity, Room room, DateTimeOffset now) => new(Guid.NewGuid().ToString("N"), identity.Subject,
        string.IsNullOrWhiteSpace(identity.Name) ? "Spiller" : new string(identity.Name.Where(c => !char.IsControl(c)).Take(40).ToArray()), identity.AvatarId, AvailableColor(room), false, now, identity.Appearance);
    private static string AvailableColor(Room room) => Colors.FirstOrDefault(c => room.Players.All(p => p.Color != c)) ?? Colors[0];
    private static string NewCode() { const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; return new(Enumerable.Range(0, 6).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray()); }
    private static void RequireHost(Room room, Player player) { if (room.HostId != player.Id) throw Error(403, "host_required", "Kun værten kan ændre dette."); }
    private static void RequireLobby(Room room) { if (room.Phase != "lobby") throw Error(409, "match_started", "Vent, til kampen er færdig."); }
    private static void ResetReady(Room room) { foreach (var p in room.Players) p.Ready = p.IsBot; }
    private void ResetLobby(Room room)
    {
        room.Simulation = null;
        room.Phase = "lobby";
        ResetReady(room);
        var now = _time.GetUtcNow();
        foreach (var player in room.Players)
        {
            if (player.Connection is not { } connection) continue;
            player.LastActivityAt = now;
            connection.TakeState();
        }
    }
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

    private sealed class Room(string code, string stage, int stocks, int minutes, bool isPublic, DateTimeOffset now)
    {
        public string Code { get; } = code;
        public string HostId { get; set; } = "";
        public string Phase { get; set; } = "lobby";
        public string StageId { get; set; } = stage;
        public int Stocks { get; set; } = stocks;
        public int Minutes { get; set; } = minutes;
        public bool IsPublic { get; set; } = isPublic;
        public DateTimeOffset CreatedAt { get; } = now;
        public List<Player> Players { get; } = [];
        public ArenaSimulation? Simulation { get; set; }
    }
    private sealed class Player(string id, Guid? subject, string name, string avatarId, string color, bool bot, DateTimeOffset now, ArenaAppearance? appearance = null)
    {
        public string Id { get; } = id;
        public Guid? Subject { get; } = subject;
        public string Name { get; } = name;
        public string AvatarId { get; set; } = avatarId;
        public string Color { get; } = color;
        public ArenaAppearance Appearance { get; } = ArenaAppearance.Sanitize(appearance);
        public bool IsBot { get; } = bot;
        public bool Ready { get; set; } = bot;
        public DateTimeOffset DisconnectedAt { get; set; } = now;
        public DateTimeOffset LastActivityAt { get; set; } = now;
        public bool CanRemoveAfk { get; set; }
        public ArenaPress HeldButtons { get; set; }
        public long LastSequence { get; set; } = -1;
        public Connection? Connection { get; set; }
    }
    private sealed record Ticket(string Code, string PlayerId, Guid Subject, DateTimeOffset ExpiresAt);
    private sealed record SocketClose(WebSocketCloseStatus Status, string Reason, byte[]? FinalMessage);
    private sealed class Connection(WebSocket socket, CancellationToken cancellationToken) : IDisposable
    {
        private readonly CancellationTokenSource _cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        private SocketClose? _close;
        private byte[]? _latestState;
        public WebSocket Socket { get; } = socket;
        public CancellationToken Token => _cancel.Token;
        public SocketClose? Close => Volatile.Read(ref _close);
        public bool IsClosing => Close is not null;
        public Channel<byte[]> Control { get; } = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(32) { SingleReader = true });
        public Channel<bool> Wake { get; } = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
        public TaskCompletionSource ReceiverFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void PublishControl(byte[] message)
        {
            if (IsClosing) return;
            if (!Control.Writer.TryWrite(message))
                RequestClose(WebSocketCloseStatus.PolicyViolation, "Forbindelsen kan ikke følge med.");
            Wake.Writer.TryWrite(true);
        }

        // Physics snapshots replace one another while a socket is sending, so a slow peer
        // catches up immediately and cannot evict lobby updates, errors, or close messages.
        public void PublishState(byte[] state)
        {
            if (IsClosing) return;
            Interlocked.Exchange(ref _latestState, state);
            Wake.Writer.TryWrite(true);
        }

        public byte[]? TakeState() => Interlocked.Exchange(ref _latestState, null);

        public void RequestClose(WebSocketCloseStatus status, string reason, byte[]? finalMessage = null)
        {
            Interlocked.CompareExchange(ref _close, new SocketClose(status, reason, finalMessage), null);
            Wake.Writer.TryWrite(true);
        }

        public void Stop()
        {
            _cancel.Cancel();
            Control.Writer.TryComplete();
            Wake.Writer.TryComplete();
        }
        public void Dispose() { Stop(); _cancel.Dispose(); }
    }
}
