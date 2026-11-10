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
    private bool _loading = true, _practicePaused, _artReady;
    private string _screen = "main", _settingsReturn = "main", _rosterReturn = "main";
    private bool IsBattle => _screen == "battle";
    private string FighterName(string id) => id == "starter" ? "Din avatar" : AvatarCatalog.Find(id)?.Name ?? "Avatar";
    private static string Portrait(string id) => AvatarCatalog.Find(id)?.FaceImage ?? "/assets/arena/v2/portrait-starter.webp";
    private static string StageArt(string id) => "/assets/arena/v2/stage-" + id + ".webp";
    private string AccountName => Auth.IsAuthenticated ? Auth.DisplayName ?? "Spiller" : "Gæst";
    private bool IsHost => _room?.HostId == _playerId;
    private bool SettingsDisabled => _practice || _playing || (_room is not null && (!IsHost || _phase != "lobby"));
    private ArenaStage SelectedStage => ArenaCatalog.FindStage(_stageId);
    private string WinnerName => _result?.Fighters.FirstOrDefault(f => f.Id == _result.WinnerId)?.Name ?? "Ingen";
    private string FighterSpecial => _selectedAvatarId switch { "bip01" => "Pulsbolt & raketryk", "raymond" => "Snabelpuls & elefantstorm", "santa" => "Stjerneskud & kanesus", "lupp" => "Idépuls & iterationsryk", _ => "Energipuls & himmelspring" };
    private static string FighterRole(string id) => ArenaMoveSets.Find(id)?.Role ?? "Alsidig";
    private bool Owns(string id) => id == "starter" || World.OwnsAvatar(id);

    protected override void OnInitialized() { Auth.AuthStateChanged += AuthChanged; }
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
            _module = await Js.InvokeAsync<IJSObjectReference>("import", "./js/arena-client.js?v=7");
            if (_disposed) { await _module.DisposeAsync(); _module = null; return; }
            _sound = await _module.InvokeAsync<bool>("soundPreference");
            if (_disposed) return;
            _reference = DotNetObjectReference.Create(this);
            _artReady = await _module.InvokeAsync<bool>("mount", _canvas, _reference, new { stages = ArenaCatalog.Stages, selectedAvatarId = _selectedAvatarId, soundEnabled = _sound, appearance = new { skin = World.Skin, hair = World.Hair, hairStyle = World.HairStyle, outfit = World.OutfitColor } });
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
    private void Back() { _practicePaused = false; _error = null; _screen = _screen == "settings" ? _settingsReturn : _screen == "fighters" ? _rosterReturn : _room is not null ? "online" : _practice ? "battle" : "main"; }
    private async Task ReturnMain() { if (_practice) await StopPractice(); if (_room is not null) await LeaveRoom(); _practicePaused = false; _screen = "main"; }
    private Task MenuKey(KeyboardEventArgs e) { if (e.Key == "Escape") { if (IsBattle) ShowSettings(); else Back(); } return Task.CompletedTask; }
    private async Task StartTraining() { _practicePaused = false; await StartPractice(); }
    private async Task SelectFighter(string id) { if (!Owns(id) || _practice) return; _selectedAvatarId = id; if (_module is not null) await _module.InvokeVoidAsync("preview", _stageId, id); }
    private async Task SelectStage(string id) { if (SettingsDisabled) return; _stageId = id; if (_module is not null) await _module.InvokeVoidAsync("preview", id, _selectedAvatarId); await ConfigureRoom(); }
    private async Task ToggleSound() { _sound = !_sound; if (_module is not null) await _module.InvokeVoidAsync("sound", _sound); }
    private void ClearError() => _error = null;
    private async Task Fullscreen() { if (_module is not null) await _module.InvokeVoidAsync("fullscreen"); }
    private async Task ConfigureRoom() { if (_room is not null && IsHost && _phase == "lobby") await Send(new { type = "configure", stageId = _stageId, stocks = _stocks, minutes = _minutes, isPublic = _isPublic }); }
    private async Task Send(object message) { if (_module is not null) await _module.InvokeVoidAsync("send", message); }

    private async Task StartPractice()
    {
        if (_module is null || !_artReady || _practice || _room is not null) return;
        _error = null; _result = null; _screen = "battle"; _practicePaused = false; _practice = _playing = true; _phase = "countdown"; _playerId = "local";
        _simulation = new ArenaSimulation(_stageId, _stocks, _minutes, training: true);
        _simulation.AddFighter("local", Auth.DisplayName ?? "Dig", _selectedAvatarId, appearance: CurrentAppearance);
        _simulation.AddFighter("cpu", "Computer", _selectedAvatarId == "raymond" ? "bip01" : "raymond", true);
        _simulation.Start();
        await _module.InvokeVoidAsync("beginPractice", "local");
        await _module.InvokeVoidAsync("pushState", _simulation.Snapshot());
        _practiceCancellation = new CancellationTokenSource();
        _practiceTask = RunPractice(_practiceCancellation.Token);
    }
    private async Task RunPractice(CancellationToken ct)
    {
        var watch = Stopwatch.StartNew(); var previous = watch.Elapsed.TotalSeconds; double accumulator = 0; var frames = 0;
        try
        {
            while (!ct.IsCancellationRequested && _simulation is not null && _module is not null)
            {
                await Task.Delay(8, ct);
                var now = watch.Elapsed.TotalSeconds;
                if (_practicePaused) { previous = now; accumulator = 0; continue; }
                accumulator += Math.Min(.1, now - previous); previous = now;
                var changed = false;
                while (accumulator >= 1d / 60) { _simulation.Tick(); accumulator -= 1d / 60; frames++; changed = true; }
                if (!changed || frames % 2 != 0) continue;
                var state = _simulation.Snapshot();
                await _module.InvokeVoidAsync("pushState", ct, state);
                if (state.Phase != _phase) { _phase = state.Phase; if (_phase == "finished") { _playing = false; _result = state; } await InvokeAsync(StateHasChanged); }
                if (state.Phase == "finished") break;
            }
        }
        catch (OperationCanceledException) { }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
    }
    [JSInvokable] public void PracticeInput(ArenaInput input) => _simulation?.SetInput("local", input);
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
        _room = admission.Room; _screen = _room.Phase is "playing" or "countdown" or "finished" ? "battle" : "online"; _playerId = admission.PlayerId; _stageId = _room.StageId; _stocks = _room.Stocks; _minutes = _room.Minutes; _phase = _room.Phase; _result = null;
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
        else _screen = "battle";
        StateHasChanged();
    });
    [JSInvokable] public Task PhaseChanged(JsonElement state) => InvokeAsync(() =>
    {
        var value = state.Deserialize<ArenaState>(Json); if (value is null) return;
        _phase = value.Phase; _playing = _phase is "countdown" or "playing";
        _result = _phase == "finished" ? value : null;
        _screen = _phase == "lobby" ? "online" : "battle"; StateHasChanged();
    });
    [JSInvokable] public Task ArenaError(string message) => InvokeAsync(() => { _error = message; StateHasChanged(); });
    [JSInvokable] public Task ConnectionChanged(bool reconnecting) => InvokeAsync(() => { _reconnecting = reconnecting; StateHasChanged(); });
    [JSInvokable] public async Task<bool> Reconnect()
    {
        if (_disposed || _room is null || !Auth.IsAuthenticated || !_artReady) return false;
        try { await Admit("arena/rooms/" + _room.Code + "/ticket", new { }); return true; }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or JSException or TaskCanceledException) { return false; }
    }
    private async Task ToggleReady() { _ready = !_ready; await Send(new { type = "ready", ready = _ready }); }
    private Task AddBot() => Send(new { type = "bot", add = true });
    private Task StartOnline() => Send(new { type = "start" });
    private async Task Rematch() { if (_practice) { await StopPractice(); await StartPractice(); } else if (IsHost) { _result = null; await Send(new { type = "rematch" }); } }
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
    public sealed record PlayerView(string Id, string Name, string AvatarId, bool Ready, bool Connected, bool IsBot, string Color, ArenaAppearance? Appearance = null);
    public sealed record RoomView(string Code, string HostId, string Phase, string StageId, int Stocks, int Minutes, bool IsPublic, List<PlayerView> Players);
    public sealed record AdmissionView(RoomView Room, string PlayerId, string Ticket, string WebSocketPath, DateTimeOffset ExpiresAt);
}
