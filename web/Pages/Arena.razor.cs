using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Api.Arena;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Demo;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages;

public partial class Arena
{
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private DemoWorld World { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [SupplyParameterFromQuery(Name = "view")] public string? RequestedView { get; set; }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private ElementReference _canvas;
    private IJSObjectReference? _module;
    private DotNetObjectReference<Arena>? _reference;
    private ArenaSimulation? _simulation;
    private CancellationTokenSource? _practiceCancellation;
    private Task? _practiceTask;
    private RoomView? _room;
    private readonly List<RoomView> _rooms = [];
    private ArenaState? _result;
    private string _playerId = "local";
    private string _selectedAvatarId = "starter";
    private string _stageId = "rooftops";
    private string _joinCode = "";
    private string _phase = "lobby";
    private string? _error;
    private int _admissionGeneration;
    private ArenaAppearance CurrentAppearance => ArenaAppearance.Sanitize(new(World.Skin, World.Hair, World.HairStyle, World.OutfitColor));
    private int _stocks = 3;
    private int _minutes = 5;
    private bool _busy, _practice, _playing, _ready, _isPublic, _copied, _roomsLoaded, _reconnecting, _disposed;
    private bool _sound = true;
    private double _soundVolume = 65;
    private bool _sandbox = true;
    private bool _refillPractice;
    private string _practiceOpponent = "raymond", _practiceMode = "idle", _practiceDifficulty = "normal";
    private string _practiceMove = "pulse", _practicePosition = "center";
    private bool _loading = true, _practicePaused, _artReady;
    private string _screen = "main", _settingsReturn = "main", _rosterReturn = "main";
    private bool _linkedRoster;
    private bool IsBattle => _screen == "battle";
    private string FighterName(string id) => id == "starter" ? "Din avatar" : AvatarCatalog.Find(id)?.Name ?? "Avatar";
    private static string Portrait(string id) => AvatarCatalog.Find(id)?.FaceImage ?? "/assets/arena/v2/portrait-starter.webp";
    private static string StageArt(string id) => "/assets/arena/v2/stage-" + id + ".webp";
    private string AccountName => Auth.IsAuthenticated ? Auth.DisplayName ?? "Spiller" : "Gæst";
    private bool IsHost => _room?.HostId == _playerId;
    private bool SettingsDisabled => _practice || _playing || (_room is not null && (!IsHost || _phase != "lobby"));
    private ArenaStage SelectedStage => ArenaCatalog.FindStage(_stageId);
    private IEnumerable<PlayerView> RemovablePlayers => IsHost && _room is not null
        ? _room.Players.Where(player => player.Id != _playerId && player.CanRemoveAfk) : [];
    private string FighterSpecial => _selectedAvatarId switch { "bip01" => "Pulsbolt & raketryk", "raymond" => "Snabelpuls & elefantstorm", "santa" => "Stjerneskud & kanesus", "lupp" => "Idépuls & iterationsryk", _ => "Energipuls & himmelspring" };
    private static string FighterRole(string id) => ArenaMoveSets.Find(id)?.Role ?? "Alsidig";
    private bool Owns(string id) => id == "starter" || World.OwnsAvatar(id);

    protected override void OnInitialized() { Auth.AuthStateChanged += AuthChanged; }

    protected override void OnParametersSet()
    {
        var showRoster = string.Equals(RequestedView, "fighters", StringComparison.OrdinalIgnoreCase);
        if (_linkedRoster == showRoster) return;

        _linkedRoster = showRoster;
        if (_practice || _room is not null || _busy) return;

        if (showRoster)
            OpenFighters();
        else if (_screen == "fighters")
            _screen = _rosterReturn;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            await World.EnsureAsync();
            if (_disposed) return;
            await World.BindAvatarProfileAsync(Auth.IsAuthenticated ? Auth.Subject : null);
            if (_disposed) return;
            _selectedAvatarId = World.SelectedAvatarId;
            _module = await Js.InvokeAsync<IJSObjectReference>("import", "./js/arena-client.js?v=8");
            if (_disposed) { await _module.DisposeAsync(); _module = null; return; }
            _sound = await _module.InvokeAsync<bool>("soundPreference");
            _soundVolume = Math.Clamp(await _module.InvokeAsync<double>("soundVolume"), 0, 100);
            if (_disposed) return;
            _reference = DotNetObjectReference.Create(this);
            _artReady = await _module.InvokeAsync<bool>("mount", _canvas, _reference, new { stages = ArenaCatalog.Stages, selectedAvatarId = _selectedAvatarId, soundEnabled = _sound, soundVolume = _soundVolume,
                phrases = AvatarCatalog.All.Where(avatar => avatar.AudioFiles.Count > 0).ToDictionary(avatar => avatar.Id,
                    avatar => avatar.Catchphrases.Zip(avatar.AudioFiles,
                        (phrase, file) => new { context = phrase.Context, text = phrase.Text, speaker = avatar.Name, file })),
                appearance = new { skin = World.Skin, hair = World.Hair, hairStyle = World.HairStyle, outfit = World.OutfitColor } });
            if (_disposed) return;
            var query = new Uri(Nav.Uri).Query;
            var invitation = query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).FirstOrDefault(p => p.Length == 2 && p[0] == "room");
            if (invitation is not null) _joinCode = Uri.UnescapeDataString(invitation[1]).ToUpperInvariant();
            _loading = false;
            if (!_artReady) _error = "Grafikken kunne ikke indlæses. Genindlæs siden.";
            if (_joinCode.Length == 6) _screen = "online";
            StateHasChanged();
            if (_joinCode.Length == 6 && Auth.IsAuthenticated && _artReady) await JoinRoom();
        }
        catch (Exception ex) when (ex is JSException or HttpRequestException or TaskCanceledException) { if (_disposed) return; _loading = false; _error = "Arenaen kunne ikke indlæses. Genindlæs siden."; StateHasChanged(); }
    }
    private void AuthChanged() => _ = InvokeAsync(async () =>
    {
        if (_disposed) return;
        if (!Auth.IsAuthenticated) { if (_room is not null) await LeaveRoom(); await World.BindAvatarProfileAsync(null); if (!_practice) _selectedAvatarId = World.SelectedAvatarId; }
        else { await World.BindAvatarProfileAsync(Auth.Subject); _selectedAvatarId = World.SelectedAvatarId; }
        StateHasChanged();
    });
    private void OpenSetup() => _screen = "setup";
    private void OpenFighters() { _rosterReturn = "main"; _screen = "fighters"; }
    private void OpenOnlineFighters() { _rosterReturn = "online"; _screen = "fighters"; }
    private void OpenOnline() => _screen = "online";
    private void ShowSettings() { _settingsReturn = _screen; _practicePaused = _practice; _screen = "settings"; }
    private void Back()
    {
        _practicePaused = false;
        _error = null;
        var closingLinkedRoster = _screen == "fighters" && _linkedRoster;
        _screen = _screen == "settings" ? _settingsReturn : _screen == "fighters" ? _rosterReturn : _room is not null ? "online" : _practice ? "battle" : "main";
        if (closingLinkedRoster)
            Nav.NavigateTo(Nav.GetUriWithQueryParameter("view", (string?)null), replace: true);
    }
    private async Task ReturnMain() { if (_practice) await StopPractice(); if (_room is not null) await LeaveRoom(); _practicePaused = false; _screen = "main"; }
    private async Task MenuKey(KeyboardEventArgs args)
    {
        if (args.Key != "Escape") return;
        if (IsBattle) ShowSettings();
        else if (_screen == "result") await ReturnMain();
        else Back();
    }
    private async Task StartTraining() { _practicePaused = false; await StartPractice(); }
    private async Task SelectFighter(string id) { if (!Owns(id) || _practice || _busy) return; _selectedAvatarId = id; if (_module is not null) await _module.InvokeVoidAsync("preview", _stageId, id); }
    private async Task SelectStage(string id) { if (SettingsDisabled || _busy) return; _stageId = id; if (_module is not null) await _module.InvokeVoidAsync("preview", id, _selectedAvatarId); await ConfigureRoom(); }
    private async Task ToggleSound() { _sound = !_sound; if (_module is not null) await _module.InvokeVoidAsync("sound", _sound); }
    private async Task ChangeVolume(ChangeEventArgs args)
    {
        if (!double.TryParse(args.Value?.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var value) ||
            !double.IsFinite(value)) return;
        _soundVolume = Math.Clamp(value, 0, 100);
        if (_module is not null) await _module.InvokeVoidAsync("setVolume", _soundVolume);
    }

    private async Task PlayFighterPhrase()
    {
        if (_module is not null)
            await _module.InvokeVoidAsync("previewPhrase", _selectedAvatarId, FighterName(_selectedAvatarId));
    }

    private void ClearError() => _error = null;
    private async Task Fullscreen() { if (_module is not null) await _module.InvokeVoidAsync("fullscreen"); }
    private async Task ConfigureRoom() { if (_room is not null && IsHost && _phase == "lobby") await Send(new { type = "configure", stageId = _stageId, stocks = _stocks, minutes = _minutes, isPublic = _isPublic }); }
    private async Task Send(object message) { if (_module is not null) await _module.InvokeVoidAsync("send", message); }

    private async Task StartPractice()
    {
        if (_module is null || !_artReady || _practice || _room is not null || _busy) return;
        var entry = _screen;
        var stage = _stageId;
        var fighter = _selectedAvatarId;
        var opponent = _practiceOpponent;
        var appearance = CurrentAppearance;
        _busy = true;
        _error = null;
        try
        {
            var ready = await _module.InvokeAsync<bool>("prepareBattle", stage, new[]
            {
                new { avatarId = fighter, appearance },
                new { avatarId = opponent, appearance = ArenaAppearance.Sanitize(null) }
            });
            if (_disposed || _screen != entry) return;
            if (!ready) { _error = "Kampbanen kunne ikke åbnes. Prøv igen."; return; }

            var simulation = new ArenaSimulation(stage, _stocks, _minutes, training: true);
            simulation.AddFighter("local", Auth.DisplayName ?? "Dig", fighter, appearance: appearance);
            simulation.AddFighter("cpu", "Computer", opponent, true);
            if (_sandbox)
                simulation.ConfigurePractice("local", "cpu", new(_practiceMode, _practiceDifficulty,
                    _practiceMove, _practicePosition, InfiniteResources: _refillPractice));
            else
            {
                simulation.SetPracticeDifficulty(_practiceDifficulty);
                simulation.Start();
            }

            await _module.InvokeVoidAsync("beginPractice", "local");
            if (_disposed) return;
            _simulation = simulation;
            _result = null;
            _screen = "battle";
            _practicePaused = false;
            _practice = _playing = true;
            _phase = simulation.Phase;
            _playerId = "local";
            await _module.InvokeVoidAsync("pushState", simulation.Snapshot());
            if (_disposed) return;
            _practiceCancellation?.Dispose();
            _practiceCancellation = new CancellationTokenSource();
            _practiceTask = RunPractice(simulation, _module, _practiceCancellation.Token);
        }
        catch (JSException)
        {
            _practice = _playing = false;
            _simulation = null;
            _screen = "setup";
            _error = "Træningen kunne ikke åbnes. Prøv igen.";
        }
        finally { _busy = false; }
    }

    private async Task RunPractice(ArenaSimulation simulation, IJSObjectReference module, CancellationToken ct)
    {
        const double step = 1d / ArenaCatalog.TickRate;
        var watch = Stopwatch.StartNew();
        var previous = 0d;
        var accumulator = 0d;
        var ticksSinceSnapshot = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(8, ct);
                var now = watch.Elapsed.TotalSeconds;
                if (_practicePaused) { previous = now; accumulator = 0; continue; }
                accumulator += Math.Min(.1, now - previous);
                previous = now;
                while (accumulator >= step)
                {
                    simulation.Tick();
                    accumulator -= step;
                    ticksSinceSnapshot++;
                    if (simulation.Phase == "finished") break;
                }

                if (ticksSinceSnapshot < 2 && simulation.Phase != "finished") continue;
                ticksSinceSnapshot = 0;
                var state = simulation.Snapshot();
                await module.InvokeVoidAsync("pushState", ct, state);
                if (state.Phase != _phase)
                {
                    _phase = state.Phase;
                    if (_phase == "finished")
                    {
                        _playing = false;
                        _result = state;
                        _screen = "result";
                    }
                    await InvokeAsync(StateHasChanged);
                }
                if (state.Phase == "finished") break;
            }
        }
        catch (OperationCanceledException) { }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
        catch (JSException)
        {
            if (_disposed) return;
            _practice = _playing = false;
            _simulation = null;
            _screen = "setup";
            _error = "Træningen blev afbrudt. Start en ny øvelse.";
            await InvokeAsync(StateHasChanged);
        }
    }

    [JSInvokable] public void PracticeInput(ArenaInput input) => _simulation?.SetInput("local", input);
    [JSInvokable] public Task ResetPracticeFromKeyboard() => InvokeAsync(ResetPractice);

    private async Task ResetPractice()
    {
        if (_screen != "battle" || _simulation is not { } simulation || _module is null || !simulation.ResetPractice()) return;
        await _module.InvokeVoidAsync("resetPracticePresentation");
        await _module.InvokeVoidAsync("pushState", simulation.Snapshot());
    }

    private void RefillPractice() => _simulation?.RefillPracticeResources();
    private async Task EditPractice()
    {
        await StopPractice();
        _screen = "setup";
    }

    private async Task StopPractice()
    {
        _practiceCancellation?.Cancel();
        if (_practiceTask is not null) await _practiceTask;
        _practiceCancellation?.Dispose(); _practiceCancellation = null; _practiceTask = null;
        _practice = _playing = false; _practicePaused = false; _screen = "main"; _simulation = null; _phase = "lobby"; _result = null;
        if (_module is not null) await _module.InvokeVoidAsync("stopPractice", _stageId, _selectedAvatarId);
    }
    private async Task CreateRoom()
    {
        if (_busy || !_artReady || !Auth.IsAuthenticated || _practice) return;
        _busy = true; _error = null;
        try { await Admit("arena/rooms", new { stageId = _stageId, stocks = _stocks, minutes = _minutes, isPublic = _isPublic, avatarId = _selectedAvatarId, appearance = CurrentAppearance }); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or JSException or TaskCanceledException) { _error = ex.Message; }
        finally { _busy = false; }
    }
    private async Task JoinRoom()
    {
        if (_busy || !_artReady || !Auth.IsAuthenticated || _practice) return;
        _joinCode = _joinCode.Trim().ToUpperInvariant();
        if (_joinCode.Length != 6 || !_joinCode.All(char.IsAsciiLetterOrDigit)) { _error = "Skriv rummets kode på seks tegn."; return; }
        _busy = true; _error = null;
        try { await Admit("arena/rooms/" + _joinCode + "/join", new { avatarId = _selectedAvatarId, appearance = CurrentAppearance }); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or JSException or TaskCanceledException) { _error = ex.Message; }
        finally { _busy = false; }
    }
    private async Task Admit(string route, object body)
    {
        var generation = _admissionGeneration;
        using var response = await Http.PostAsJsonAsync(route, body);
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode == System.Net.HttpStatusCode.Unauthorized ? "Dit login er udløbet. Log ind igen." : "Rummet kunne ikke åbnes. Prøv igen.";
            try { var error = await response.Content.ReadFromJsonAsync<JsonElement>(); if (error.TryGetProperty("detail", out var value) || error.TryGetProperty("message", out value) || error.TryGetProperty("title", out value)) message = value.GetString() ?? message; } catch (JsonException) { }
            throw new HttpRequestException(message);
        }
        var admission = await response.Content.ReadFromJsonAsync<AdmissionView>(Json) ?? throw new JsonException("Serveren returnerede et tomt rum.");
        if (_disposed || generation != _admissionGeneration) return;
        _room = admission.Room; _screen = _room.Phase switch { "finished" when _result is not null => "result", "countdown" or "playing" or "finished" => "battle", _ => "online" }; _playerId = admission.PlayerId; _stageId = _room.StageId; _stocks = _room.Stocks; _minutes = _room.Minutes; _phase = _room.Phase; if (_phase != "finished") _result = null;
        var socket = new Uri(Http.BaseAddress!, admission.WebSocketPath);
        if (_module is not null) await _module.InvokeVoidAsync("connect", socket.AbsoluteUri, _playerId);
    }
    [JSInvokable] public Task RoomChanged(JsonElement room) => InvokeAsync(() =>
    {
        _room = room.Deserialize<RoomView>(Json); if (_room is null) return;
        _stageId = _room.StageId; _stocks = _room.Stocks; _minutes = _room.Minutes; _isPublic = _room.IsPublic;
        _ready = _room.Players.FirstOrDefault(p => p.Id == _playerId)?.Ready ?? false;
        _phase = _room.Phase; _playing = _phase is "countdown" or "playing";
        if (_phase == "lobby") { _result = null; _screen = "online"; }
        else if (_phase == "finished" && _result is not null) _screen = "result";
        else if (_screen != "settings") _screen = "battle";
        StateHasChanged();
    });
    [JSInvokable] public Task PhaseChanged(JsonElement state) => InvokeAsync(() =>
    {
        var value = state.Deserialize<ArenaState>(Json); if (value is null) return;
        _phase = value.Phase; _playing = _phase is "countdown" or "playing";
        _result = _phase == "finished" ? value : null;
        _screen = _phase switch { "finished" => "result", "lobby" => "online", _ when _screen == "settings" => "settings", _ => "battle" };
        StateHasChanged();
    });
    [JSInvokable] public Task ArenaRemoved(string message) => InvokeAsync(() =>
    {
        _admissionGeneration++;
        _room = null;
        _playing = _ready = _reconnecting = false;
        _result = null;
        _phase = "lobby";
        _screen = "online";
        _error = message;
        StateHasChanged();
    });

    [JSInvokable] public Task ArenaError(string message) => InvokeAsync(() => { _error = message; StateHasChanged(); });
    [JSInvokable] public Task ConnectionChanged(bool reconnecting) => InvokeAsync(() => { _reconnecting = reconnecting; StateHasChanged(); });
    [JSInvokable] public async Task<bool> Reconnect()
    {
        if (_disposed || _room is null || !Auth.IsAuthenticated || !_artReady) return false;
        try { await Admit("arena/rooms/" + _room.Code + "/ticket", new { }); return true; }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or JSException or TaskCanceledException) { return false; }
    }
    private async Task ToggleReady()
    {
        if (_busy || _room is not { Phase: "lobby" } room || _module is null) return;
        var ready = !_ready;
        _busy = true;
        try
        {
            if (ready && !await _module.InvokeAsync<bool>("prepareBattle", room.StageId, room.Players))
            {
                _error = "Kampbanen kunne ikke åbnes. Prøv igen.";
                return;
            }
            if (_disposed || _room is not { Phase: "lobby" } current || current.Code != room.Code) return;
            await Send(new { type = "ready", ready });
        }
        catch (JSException) { _error = "Kampbanen kunne ikke åbnes. Prøv igen."; }
        finally { _busy = false; }
    }

    private Task RemoveAfk(string id) => RemovablePlayers.Any(player => player.Id == id)
        ? Send(new { type = "removeAfk", playerId = id }) : Task.CompletedTask;
    private Task AddBot() => Send(new { type = "bot", add = true });
    private Task StartOnline() => Send(new { type = "start" });
    private async Task Rematch() { if (_practice) { await StopPractice(); await StartPractice(); } else if (IsHost) await Send(new { type = "rematch" }); }
    private async Task LeaveRoom()
    {
        _admissionGeneration++; var code = _room?.Code;
        if (_module is not null) await _module.InvokeVoidAsync("disconnect");
        await LeaveMembership(code);
        _room = null; _screen = "main"; _playing = _ready = _reconnecting = false; _result = null; _phase = "lobby";
        if (_module is not null) await _module.InvokeVoidAsync("preview", _stageId, _selectedAvatarId);
    }
    private async Task LeaveMembership(string? code)
    {
        if (code is null) return;
        try { using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(3)); using var response = await Http.DeleteAsync("arena/rooms/" + code + "/players/me", ct.Token); }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException) { }
    }
    private async Task CopyInvite()
    {
        if (_room is null || _module is null) return;
        _copied = await _module.InvokeAsync<bool>("copyInvite", new Uri(new Uri(Nav.BaseUri), "arena?room=" + _room.Code).AbsoluteUri);
        if (!_copied) _error = "Invitationen kunne ikke kopieres. Del rumkoden " + _room.Code + " med dine venner.";
    }
    private async Task JoinPublicRoom(string code) { _joinCode = code; await JoinRoom(); }
    private async Task JoinKey(KeyboardEventArgs e) { if (e.Key == "Enter") await JoinRoom(); }
    private async Task LoadRooms()
    {
        try { var result = await Http.GetFromJsonAsync<List<RoomView>>("arena/rooms", Json); _rooms.Clear(); if(result is not null) _rooms.AddRange(result); _roomsLoaded = true; }
        catch (HttpRequestException) { _error = "Listen over rum kunne ikke hentes. Prøv igen."; }
    }
    public async ValueTask DisposeAsync()
    {
        _disposed = true; _admissionGeneration++; Auth.AuthStateChanged -= AuthChanged;
        _practiceCancellation?.Cancel(); if (_practiceTask is not null) await _practiceTask;
        _practiceCancellation?.Dispose();
        if (_module is not null) { try { await _module.InvokeVoidAsync("dispose"); await _module.DisposeAsync(); } catch (JSDisconnectedException) { } }
        await LeaveMembership(_room?.Code);
        _reference?.Dispose();
    }
    public sealed record PlayerView(string Id, string Name, string AvatarId, bool Ready, bool Connected, bool IsBot, string Color, ArenaAppearance? Appearance = null, bool CanRemoveAfk = false);
    public sealed record RoomView(string Code, string HostId, string Phase, string StageId, int Stocks, int Minutes, bool IsPublic, List<PlayerView> Players);
    public sealed record AdmissionView(RoomView Room, string PlayerId, string Ticket, string WebSocketPath, DateTimeOffset ExpiresAt);
}
