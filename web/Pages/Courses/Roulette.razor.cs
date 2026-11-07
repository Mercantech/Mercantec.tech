using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Learning;
using Mercantec.Tech.Web.Notion;
using Mercantec.Tech.Web.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.Courses;

public partial class Roulette
{
    [Inject] private NotionClient Notion { get; set; } = null!;
    [Inject] private GroupSetsClient Sets { get; set; } = null!;
    [Inject] private RouletteSpeechClient Speech { get; set; } = null!;
    [Inject] private AuthService Auth { get; set; } = null!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private HttpClient Http { get; set; } = null!;
    [Parameter] public Guid HoldId { get; set; }
    [SupplyParameterFromQuery(Name = "set")] public Guid? SetId { get; set; }
    [SupplyParameterFromQuery(Name = "workspace")] public Guid? WorkspaceQuery { get; set; }
    [SupplyParameterFromQuery(Name = "course")] public Guid? CourseQuery { get; set; }
    private WorkspaceRouteContext? _returnContext;
    private string? _navigationKey;
    private string NavigationKey => $"{HoldId}:{WorkspaceQuery}:{CourseQuery}:{Auth.Subject}:{ViewAs.IsActive}:{Auth.IsAuthenticated}";
    private ElementReference _root;
    private IJSObjectReference? _module;
    private DotNetObjectReference<Roulette>? _callbacks;
    private CancellationTokenSource _scope = new();
    private CancellationTokenSource? _autoCancellation, _voiceCancellation;
    private Task<PreparedRouletteSpeech?>? _speechPreparation;
    private string? _preparedGroupName;
    private RouletteSpeechStatus _speechStatus = new(false, "Røst", "Kontrollerer de forudindtalte navneled…");
    private string _voiceMode = "browser", _voiceMessage = "Gruppens navn læses op efter hvert spin.";
    private double _spinSeconds = 4, _pauseSeconds = 2;
    private int _volume = 75;
    private bool _speechEnabled = true, _animate = true, _speaking, _testingVoice, _checkingVoice, _voiceSelected, _autoSpin, _autoWaiting, _reducedMotion;
    private (Guid Hold, string? Subject, bool Preview, bool Authenticated)? _context;
    private NotionHoldMember[] _members = [];
    private NotionGroupView[] _groups = [];
    private GroupSetView[] _sets = [];
    private readonly HashSet<Guid> _selected = [], _historySets = [];
    private readonly List<ShowRound> _rounds = [];
    private ShowRound? _current;
    private ShowGroup? _lastWinner;
    private string[] _stageNames = ["Klar?", "Klar?", "Klar?"];
    private string _holdName = "", _distribution = "size", _selectedSet = "", _newSetName = "Makkerpar · Runde 1", _celebration = "", _storageKey = "";
    private string? _error, _notice, _copyFallback;
    private int _amount = 3, _generation;
    private bool _loading = true, _canManage, _spinning, _saving, _sound = true, _showGroups, _overview, _confirmNewRound, _avoidPairs = true, _includeLegacy, _disposed, _restorePending, _leaving;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly RouletteAnnouncementCue[] CelebrationCues = RouletteAnnouncementCatalog.All.Where(cue => cue.Id.StartsWith("celebration-", StringComparison.Ordinal)).ToArray();
    private static readonly string[] Celebrations = CelebrationCues.Select(cue => cue.Text).ToArray();
    private static readonly string[] GroupColors = ["#ffd561", "#ff7bb7", "#89dccb", "#bba4ff", "#ffab78", "#9dcaff"];
    private bool MayManage => _canManage && Auth.IsAuthenticated && !ViewAs.IsActive;
    private bool HasUnsavedRounds => !_leaving && MayManage && _rounds.Any(r => r.Revealed > 0 && !r.Saved);
    private bool CanStartSpin => MayManage && !_spinning && !_speaking && !_cheering && !_saving && !_testingVoice && _selected.Count > 0 && (_current is null || !_current.Complete);
    private bool CanSpin => CanStartSpin && !_autoSpin;
    private double AudioVolume => Math.Clamp(_volume / 100d, 0, 1);
    private int DisplayRound => _current?.Number ?? (_rounds.LastOrDefault()?.Number ?? 0) + 1;
    private int SelectedCount => _current?.ParticipantIds.Length ?? _selected.Count;
    private int CurrentAssignedCount => _current?.Groups.Take(_current.Revealed).Sum(g => g.MemberIds.Length) ?? 0;
    private int RemainingCount => SelectedCount - CurrentAssignedCount;
    private int ProgressPercent => SelectedCount == 0 ? 0 : (int)Math.Round(CurrentAssignedCount * 100d / SelectedCount);
    private string[] SlotNames => _stageNames;
    private string BackHref => _returnContext?.View("groups") ?? WorkspaceNavigation.Hold(HoldId, "groups");
    private string MachineStatus => _reelsRolling ? "Navnene ruller…" : _speaking ? "Arenaspeakeren er på…" : _spinning ? "Gruppen lander…" : _autoWaiting ? "Pause før næste gruppe" : _current?.Complete == true ? "Alle er fordelt!" : SelectedCount == 0 ? "Vælg deltagere" : _current is null ? "Klar til første spin" : $"Gruppe {_current.Revealed + 1} venter";
    private string SpinLabel => _reelsRolling ? "Spinder…" : _speaking ? "Oplæser…" : _spinning ? "Et øjeblik…" : _current?.Complete == true ? "Runden er færdig" : _current is null ? "Spin første gruppe" : "Spin næste gruppe";
    private IEnumerable<NotionHoldMember> RemainingMembers
    {
        get
        {
            if (_current is null) return _members;
            var assigned = _current.Groups.Take(_current.Revealed).SelectMany(g => g.MemberIds).ToHashSet();
            return _members.Where(m => _current.ParticipantIds.Contains(m.Id) && !assigned.Contains(m.Id));
        }
    }
    private string MemberName(Guid id) => _members.FirstOrDefault(m => m.Id == id)?.DisplayName ?? "Elev";
    private static string Initial(string name) => string.IsNullOrWhiteSpace(name) ? "?" : StringInfo.GetNextTextElement(name.Trim());
    private static string GroupColor(int number) => GroupColors[(Math.Max(1, number) - 1) % GroupColors.Length];

    public override Task SetParametersAsync(ParameterView parameters)
    {
        WorkspaceQuery = CourseQuery = null;
        return base.SetParametersAsync(parameters);
    }

    protected override void OnInitialized() { Auth.AuthStateChanged += ContextChanged; ViewAs.Changed += ContextChanged; }
    protected override async Task OnParametersSetAsync() { await SyncContext(); await SyncNavigation(); }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        if (_module is null)
        {
            try
            {
                _module = await Js.InvokeAsync<IJSObjectReference>("import", RouletteNameCatalog.ScriptPath);
                _callbacks = DotNetObjectReference.Create(this);
                await _module.InvokeVoidAsync("initialize", _root, _callbacks, new { sound = _sound, volume = AudioVolume });
                await InitializeAppearance();
                var capabilities = await _module.InvokeAsync<EffectCapabilities?>("capabilities", _root);
                _reducedMotion = capabilities?.ReducedMotion == true;
                _animate = !_reducedMotion;
                if (!_disposed) await InvokeAsync(StateHasChanged);
            }
            catch (JSException) { _error = "Effekterne kunne ikke indlæses. Grupperne kan stadig trækkes uden animation."; }
        }
        if (_restorePending && MayManage && _module is not null)
        {
            _restorePending = false;
            var generation = _generation;
            try
            {
                var stored = await _module.InvokeAsync<string?>("loadSession", _storageKey);
                if (CurrentContext(generation) && stored is not null) Restore(stored);
            }
            catch (Exception ex) when (ex is JSException or JsonException) { }
            if (CurrentContext(generation)) await InvokeAsync(StateHasChanged);
        }
    }
    private void ContextChanged() => _ = InvokeAsync(async () => { await SyncContext(); await SyncNavigation(); if (!_disposed) StateHasChanged(); });
    private async Task SyncNavigation()
    {
        if (_disposed || !Auth.IsInitialized || _navigationKey == NavigationKey) return;
        var key = _navigationKey = NavigationKey;
        _returnContext = null;
        if (!Auth.IsAuthenticated || WorkspaceNavigation.NonEmpty(WorkspaceQuery) is null && WorkspaceNavigation.NonEmpty(CourseQuery) is null) return;
        try
        {
            var context = await WorkspaceNavigation.Resolve(Http, HoldId, WorkspaceQuery, CourseQuery, ViewAs.IsActive, _scope.Token);
            if (!_disposed && key == NavigationKey) _returnContext = context;
        }
        catch (OperationCanceledException) { }
    }
    private async Task SyncContext()
    {
        if (_disposed || !Auth.IsInitialized) return;
        var next = (HoldId, Auth.Subject, ViewAs.IsActive, Auth.IsAuthenticated);
        if (_context == next) return;
        StopAuto(); CancelVoicePreparation();
        _context = next; _scope.Cancel(); _scope.Dispose(); _scope = new(); var generation = ++_generation;
        _speaking = _testingVoice = _checkingVoice = _reelsRolling = _cheering = _announcementsAvailable = false;
        _loading = true; _canManage = _spinning = _saving = _overview = false; _members = []; _groups = []; _sets = [];
        _selected.Clear(); _historySets.Clear(); _rounds.Clear(); _current = null; _lastWinner = null; _stageNames = ["Klar?", "Klar?", "Klar?"]; _holdName = _storageKey = ""; _error = _notice = null;
        if (_module is not null) { try { await _module.InvokeVoidAsync("cancel", _root); } catch (JSException) { } }
        if (!Auth.IsAuthenticated || ViewAs.IsActive) { _loading = false; return; }
        var ct = _scope.Token;
        try
        {
            var holds = await Notion.Holds(ct);
            var hold = holds.FirstOrDefault(h => h.Id == HoldId && h.CanManage);
            if (!CurrentContext(generation)) return;
            if (hold is null) { _error = "Din konto kan ikke administrere dette hold."; return; }
            _holdName = hold.Name;
            var membersTask = Notion.Members(HoldId, ct); var groupsTask = Notion.Groups(HoldId, ct); var setsTask = Sets.List(HoldId, ct, true);
            await Task.WhenAll(membersTask, groupsTask, setsTask);
            if (!CurrentContext(generation)) return;
            _members = (await membersTask).OrderBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
            _groups = await groupsTask; _sets = await setsTask; _canManage = true;
            await CheckVoice();
            foreach (var member in _members) _selected.Add(member.Id);
            _selectedSet = SetId is Guid selected && _sets.Any(s => s.Id == selected && !s.Archived) ? selected.ToString() : "";
            if (Guid.TryParse(_selectedSet, out var historySet)) _historySets.Add(historySet);
            _storageKey = $"lup:roulette:v2:{Auth.Subject}:{HoldId}"; _restorePending = true;
        }
        catch (Exception ex) when (ex is NotionException or LearningException or HttpRequestException or OperationCanceledException)
        { if (CurrentContext(generation)) _error = ex is NotionException or LearningException ? ex.Message : "Eleverne kunne ikke hentes. Kontrollér forbindelsen, og prøv fra holdet igen."; }
        finally { if (CurrentContext(generation)) _loading = false; }
    }
    private bool CurrentContext(int generation) => !_disposed && generation == _generation && _context == (HoldId, Auth.Subject, ViewAs.IsActive, Auth.IsAuthenticated);
    private void SelectAll() { if (_current is not null || _spinning) return; _selected.Clear(); foreach (var member in _members) _selected.Add(member.Id); }
    private void ToggleMember(Guid id, ChangeEventArgs e) { if (_current is not null || _spinning) return; if (e.Value is true) _selected.Add(id); else _selected.Remove(id); }
    private void ToggleHistorySet(Guid id, ChangeEventArgs e) { if (e.Value is true) _historySets.Add(id); else _historySets.Remove(id); }
    private string NewName()
    {
        var used = _groups.Select(g => g.Name).Concat(_rounds.SelectMany(r => r.Groups).Select(g => g.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < 700; i++) { var candidate = RouletteNameCatalog.Starts[Random.Shared.Next(RouletteNameCatalog.Starts.Count)] + RouletteNameCatalog.Ends[Random.Shared.Next(RouletteNameCatalog.Ends.Count)]; if (!used.Contains(candidate)) return candidate; }
        return $"Stjernehold {Guid.NewGuid().ToString("N")[..5]}";
    }
    private bool PrepareRound()
    {
        if (_amount < 1 || _distribution == "size" && _amount > 12 || _distribution == "count" && _amount > 100) { _error = "Vælg 1–12 elever pr. gruppe eller 1–100 grupper."; return false; }
        if (_selectedSet.Length > 0 && (!Guid.TryParse(_selectedSet, out var id) || !_sets.Any(s => s.Id == id && !s.Archived))) { _error = "Vælg et aktivt gruppesæt, eller opret et nyt til runden."; return false; }
        if (_selectedSet.Length == 0 && (string.IsNullOrWhiteSpace(_newSetName) || _newSetName.Length > 120)) { _error = "Giv det nye gruppesæt et navn på højst 120 tegn."; return false; }
        var participants = _selected.Where(id => _members.Any(m => m.Id == id)).ToArray();
        var previous = _avoidPairs ? _groups.Where(g => g.GroupSetId is Guid set ? _historySets.Contains(set) : _includeLegacy).Select(g => g.Members.Select(m => m.Id).ToArray())
            .Concat(_rounds.SelectMany(r => r.Groups.Take(r.Revealed)).Select(g => g.MemberIds)).ToArray() : [];
        try
        {
            var plan = GroupRoulette.Draw(participants, _amount, _distribution == "size", previous);
            _current = new() { Number = (_rounds.LastOrDefault()?.Number ?? 0) + 1, ParticipantIds = participants, TargetSetId = Guid.TryParse(_selectedSet, out var setId) ? setId : null, SetName = Guid.TryParse(_selectedSet, out setId) ? _sets.First(s => s.Id == setId).Name : _newSetName.Trim() };
            _rounds.Add(_current);
            _current.Groups = plan.Select((ids, index) => new ShowGroup { Number = index + 1, MemberIds = ids, Name = "" }).ToArray();
            foreach (var group in _current.Groups) group.Name = NewName();
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { _error = ex.Message; return false; }
    }
    [JSInvokable] public Task SpinFromLever() => InvokeAsync(Spin);
    private async Task Spin() { if (CanSpin) await SpinOne(); }
    private async Task<bool> SpinOne()
    {
        if (!CanStartSpin) return false;
        _error = _notice = null;
        if (_current is null && !PrepareRound()) return false;
        var round = _current!; var group = round.Groups[round.Revealed]; var generation = _generation;
        var celebrationIndex = Random.Shared.Next(Celebrations.Length);
        var visualCelebration = Random.Shared.Next(12);
        QueueSpeech(group, generation);
        var pool = RemainingMembers.Select(m => m.DisplayName).ToArray();
        var targets = group.MemberIds.Take(6).Select(MemberName).ToArray();
        _stageNames = Enumerable.Repeat("…", targets.Length).ToArray(); _spinning = _reelsRolling = true; _lastWinner = null;
        await InvokeAsync(StateHasChanged);
        var roundIntro = round.Revealed == 0 && FullAnnouncements ? PlayCue("round-start", generation) : Task.FromResult(false);
        try
        {
            var animated = _module is null || await _module.InvokeAsync<bool>("spin", _scope.Token, _root, pool, targets, _sound,
                new { durationMs = (int)(Math.Clamp(_spinSeconds, 1.5, 12) * 1000), volume = AudioVolume, motion = _animate, celebrationIndex = visualCelebration, groupName = group.Name, celebrationText = Celebrations[celebrationIndex], preserveAnnouncement = round.Revealed == 0 && FullAnnouncements });
            _reelsRolling = false;
            if (!animated)
            {
                if (_module is not null) await _module.InvokeVoidAsync("cancelSpeech", _root);
                await roundIntro;
                if (CurrentContext(generation)) _error = "Spinnet blev afbrudt. Prøv igen; gruppen er endnu ikke afsløret.";
                return false;
            }
            await roundIntro;
            if (!CurrentContext(generation) || !MayManage) return false;
            if (_module is not null && _sound)
            {
                var audioState = await _module.InvokeAsync<EffectCapabilities?>("capabilities", _root);
                if (audioState?.AudioReady == false || audioState?.Sound == false)
                {
                    _sound = false;
                    _notice = "Browseren kunne ikke aktivere lydene. Tryk Lyde til, og kontrollér browserens lydindstillinger.";
                }
            }
            if (!CurrentContext(generation) || !MayManage) return false;
            _stageNames = targets; round.Revealed++; _lastWinner = group; _celebration = Celebrations[celebrationIndex]; _showGroups = true;
            await Persist();
            await SpeakWinner(group, generation, CelebrationCues[celebrationIndex].Id);
            if (round.Complete && CurrentContext(generation)) await PlayCue("round-complete", generation);
            return true;
        }
        catch (OperationCanceledException) when (!CurrentContext(generation) || _scope.IsCancellationRequested) { return false; }
        catch (JSException)
        {
            if (CurrentContext(generation) && _lastWinner != group)
            {
                _stageNames = targets; round.Revealed++; _lastWinner = group; _celebration = "Et nyt hold er landet!"; _showGroups = true;
                _notice = "Gruppen er dannet, men browseren kunne ikke afspille effekterne. Opdatér siden, hvis det gentager sig.";
                await Persist();
            }
            return CurrentContext(generation);
        }
        finally
        {
            if (CurrentContext(generation) && !roundIntro.IsCompleted && _module is not null)
            {
                try { await _module.InvokeVoidAsync("cancelSpeech", _root); }
                catch (JSException) { }
            }
            await roundIntro;
            if (CurrentContext(generation))
            {
                _spinning = _speaking = _reelsRolling = false;
                // The draggable lever enters through JSInvokable, which does not
                // get Blazor's automatic render after an EventCallback completes.
                await InvokeAsync(StateHasChanged);
            }
        }
    }
    private void StopAuto() { _autoSpin = _autoWaiting = false; _autoCancellation?.Cancel(); }
    private void EnableAnimations() { if (MayManage && !_spinning && !_autoSpin) _animate = true; }
    private async Task ToggleAutoSpin()
    {
        if (_autoSpin) { StopAuto(); return; }
        if (!CanStartSpin) return;
        var generation = _generation;
        using var auto = CancellationTokenSource.CreateLinkedTokenSource(_scope.Token);
        _autoCancellation = auto; _autoSpin = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            while (_autoSpin && CurrentContext(generation) && CanStartSpin && !auto.IsCancellationRequested)
            {
                if (!await SpinOne() || !CanStartSpin || !_autoSpin) break;
                _autoWaiting = true;
                await InvokeAsync(StateHasChanged);
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_pauseSeconds, .5, 15)), auto.Token);
                _autoWaiting = false;
            }
        }
        catch (OperationCanceledException) when (auto.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_autoCancellation, auto)) { _autoCancellation = null; _autoSpin = _autoWaiting = false; }
            if (CurrentContext(generation)) await InvokeAsync(StateHasChanged);
        }
    }
    private async Task NewRound()
    {
        if (_spinning || _speaking || _cheering || _saving || _autoSpin || _testingVoice) return;
        if (_current is { Complete: false, Revealed: > 0 }) { _confirmNewRound = true; return; }
        await ConfirmNewRound();
    }
    private async Task ConfirmNewRound()
    {
        _confirmNewRound = false; _current = null; _lastWinner = null; _stageNames = ["Klar?", "Klar?", "Klar?"]; _selectedSet = ""; _newSetName = $"Makkerpar · Runde {DisplayRound}"; _showGroups = false; _notice = "En ny runde er klar. Dine tidligere grupper bliver i overblikket."; _error = null;
        CancelVoicePreparation();
        if (_module is not null) await _module.InvokeVoidAsync("cancelSpeech", _root);
        await Persist();
    }
    private async Task Reroll(ShowRound round, ShowGroup group) { if (round.Saved || round.SaveAttempted || _saving || _spinning) return; group.Name = NewName(); if (_lastWinner == group) _celebration = "Nyt navn. Samme stjernehold!"; await Persist(); }
    private async Task ChangeDestination(ShowRound round, ChangeEventArgs e)
    {
        if (round.Saved || round.SaveAttempted || _saving) return;
        if (Guid.TryParse(e.Value?.ToString(), out var id) && _sets.FirstOrDefault(s => s.Id == id && !s.Archived) is { } set) { round.TargetSetId = id; round.SetName = set.Name; }
        else { round.TargetSetId = null; round.SetName = $"Makkerpar · Runde {round.Number}"; }
        await Persist();
    }
    private async Task SaveRound(ShowRound? round)
    {
        if (round is null || !MayManage || _saving || _spinning || _speaking || _cheering || !round.Complete || round.Saved) return;
        if (string.IsNullOrWhiteSpace(round.SetName) || round.SetName.Length > 120) { _error = "Giv gruppesættet et navn på højst 120 tegn."; return; }
        var generation = _generation; var ct = _scope.Token; _saving = true; _error = null;
        try
        {
            if (round.TargetSetId is null)
            {
                var created = await Sets.Create(HoldId, round.SetName, ct);
                if (!CurrentContext(generation)) return;
                round.TargetSetId = created.Id; _sets = [.. _sets, created];
            }
            round.SaveAttempted = true;
            await Persist();
            var saved = await Notion.CreateGroupBatch(HoldId, round.Groups.Select(g => new SaveNotionGroup(g.Name, g.MemberIds)).ToArray(), ct, round.TargetSetId, round.RequestId);
            if (!CurrentContext(generation)) return;
            round.Saved = true; round.SavedGroupIds = saved.Select(g => g.Id).ToArray(); _groups = [.. _groups, .. saved];
            _notice = $"Runde {round.Number} er gemt som {round.Groups.Length} grupper i {round.SetName}."; await Persist();
            await PlayCue("round-saved", generation);
        }
        catch (Exception ex) when (ex is NotionException or LearningException or HttpRequestException or OperationCanceledException)
        {
            if (CurrentContext(generation))
            {
                if (ex is NotionException { ErrorCode: "invalid_group_set" or "invalid_group_batch" })
                {
                    round.SaveAttempted = false;
                    if (ex is NotionException { ErrorCode: "invalid_group_set" }) { round.TargetSetId = null; round.SetName = $"Makkerpar · Runde {round.Number}"; }
                    await Persist();
                }
                _error = ex is NotionException or LearningException ? ex.Message : "Runden kunne ikke gemmes. Forslaget er bevaret. Prøv Gem igen; samme runde oprettes ikke to gange.";
            }
        }
        finally { if (CurrentContext(generation)) _saving = false; }
    }
    private async Task CheckVoice()
    {
        if (!MayManage || _checkingVoice) return;
        var generation = _generation; _checkingVoice = true;
        try
        {
            var status = await Speech.Status(_scope.Token);
            if (!CurrentContext(generation)) return;
            _speechStatus = status;
            _announcementsAvailable = status.Available && await Speech.AnnouncementsAvailable(_scope.Token);
            if (!CurrentContext(generation)) return;
            if (!_voiceSelected) _voiceMode = status.Available ? "rost" : "browser";
            _voiceMessage = status.Available ? _announcementsAvailable ? "Arenaspeakeren er klar til hele showet." : "Gruppenavnene er klar. De ekstra opråb er ikke klar endnu." : status.Message ?? "De forudindtalte navneled er ikke klar. Du kan bruge browserstemmen.";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            if (CurrentContext(generation)) { _speechStatus = new(false, "Røst", "Lydbiblioteket kunne ikke indlæses. Du kan bruge browserstemmen."); _voiceMessage = _speechStatus.Message!; }
        }
        finally { if (CurrentContext(generation)) _checkingVoice = false; }
    }
    private static string SpokenName(string name) => Regex.Replace(name, "(?<=[a-zæøå])(?=[A-ZÆØÅ])", " ");
    private void CancelVoicePreparation()
    {
        _voiceCancellation?.Cancel(); _voiceCancellation?.Dispose(); _voiceCancellation = null;
        _speechPreparation = null; _preparedGroupName = null;
    }
    private void QueueSpeech(ShowGroup group, int generation)
    {
        if (!_speechEnabled || _voiceMode != "rost" || !_speechStatus.Available || !CurrentContext(generation)) return;
        if (_preparedGroupName == group.Name && _speechPreparation is not null) return;
        CancelVoicePreparation();
        _voiceCancellation = CancellationTokenSource.CreateLinkedTokenSource(_scope.Token);
        _preparedGroupName = group.Name;
        _speechPreparation = PrepareSpeechAudio(group.Name, generation, _voiceCancellation.Token);
    }
    private async Task<PreparedRouletteSpeech?> PrepareSpeechAudio(string name, int generation, CancellationToken ct)
    {
        try
        {
            var audio = Speech.Prepare(name);
            if (audio is null || !CurrentContext(generation) || ct.IsCancellationRequested) return null;
            if (_module is null || !await _module.InvokeAsync<bool>("prepareSpeechSegments", ct, _root, audio.ClipUrls)) return null;
            return audio;
        }
        catch (Exception ex) when (ex is LearningException or HttpRequestException or OperationCanceledException or JsonException or JSException)
        {
            if (CurrentContext(generation) && !ct.IsCancellationRequested)
                _voiceMessage = "Navneklippene kunne ikke indlæses. Browserstemmen bliver brugt i stedet.";
            return null;
        }
    }
    private async Task SpeakWinner(ShowGroup group, int generation, string? celebrationCue = null)
    {
        if (!_speechEnabled || _module is null || !CurrentContext(generation)) return;
        _speaking = true;
        await InvokeAsync(StateHasChanged);
        var voiceMode = _voiceMode;
        try
        {
            if (await TryPresentWinner(group.Name, celebrationCue ?? "celebration-00", generation)) return;
            QueueSpeech(group, generation);
            var audio = voiceMode == "rost" && _preparedGroupName == group.Name && _speechPreparation is not null
                ? await _speechPreparation : null;
            if (!CurrentContext(generation) || !_speechEnabled || voiceMode != _voiceMode) return;
            var usedRost = audio is not null && await _module.InvokeAsync<bool>("playSpeechSegments", _scope.Token, _root, audio.ClipUrls, AudioVolume);
            if (!CurrentContext(generation) || !_speechEnabled || voiceMode != _voiceMode) return;
            var spoken = usedRost || await _module.InvokeAsync<bool>("playBrowserSpeech", _scope.Token, _root, SpokenName(group.Name), AudioVolume);
            if (CurrentContext(generation) && _speechEnabled)
                _voiceMessage = spoken ? usedRost ? "Gruppens navn er læst op med de forudindtalte Røst-klip." : "Gruppens navn er læst op med browserstemmen."
                    : "Browseren kunne ikke afspille oplæsningen. Tryk Prøv stemme, og kontrollér lyden.";
        }
        catch (Exception ex) when (ex is JSException or OperationCanceledException)
        {
            if (CurrentContext(generation) && _speechEnabled) _voiceMessage = "Oplæsningen blev afbrudt. Grupperne er stadig gemt i runden.";
        }
        finally { if (CurrentContext(generation)) { _speaking = false; await InvokeAsync(StateHasChanged); } }
    }
    private async Task ToggleSpeech()
    {
        _speechEnabled = !_speechEnabled;
        if (!_speechEnabled)
        {
            CancelVoicePreparation();
            if (_module is not null) await _module.InvokeVoidAsync("cancelSpeech", _root);
            _voiceMessage = "Oplæsning er slået fra.";
        }
        else _voiceMessage = "Gruppens navn læses op efter næste spin.";
    }
    private async Task VoiceModeChanged()
    {
        _voiceSelected = true; CancelVoicePreparation();
        if (_module is not null) await _module.InvokeVoidAsync("cancelSpeech", _root);
        if (_voiceMode != "rost" || !_speechStatus.Available) _voiceMode = "browser";
        _voiceMessage = _voiceMode == "rost" ? "Arenaspeakeren sætter de forudindtalte navneled sammen efter næste spin." : "Browserens lokale stemme læser gruppens navn efter næste spin.";
    }
    private async Task TestVoice()
    {
        if (!MayManage || _module is null || _spinning || _speaking || _cheering || _saving || _autoSpin || _testingVoice) return;
        var generation = _generation;
        _testingVoice = true; _speechEnabled = true;
        try { await SpeakWinner(new ShowGroup { Name = "NeonOpfinderne" }, generation); }
        finally { if (CurrentContext(generation)) { _testingVoice = _speaking = false; await InvokeAsync(StateHasChanged); } }
    }
    private async Task VolumeChanged() { if (_module is not null) await _module.InvokeVoidAsync("setVolume", _root, AudioVolume); }
    private async Task ToggleSound()
    {
        if (_module is null) { _notice = "Lyde er ikke tilgængelige i denne browser."; return; }
        _sound = !_sound;
        try { if (!await _module.InvokeAsync<bool>("setSound", _root, _sound)) { _sound = false; _notice = "Browseren kunne ikke aktivere lyden. Prøv lydknappen igen, og kontrollér browserens lydindstillinger."; } }
        catch (JSException) { _sound = false; _notice = "Lyde er ikke tilgængelige i denne browser."; }
    }
    private async Task Fullscreen() { if (_module is not null) { try { await _module.InvokeVoidAsync("toggleFullscreen", _root); return; } catch (JSException) { } } _notice = "Browseren kunne ikke åbne fuld skærm. Brug evt. browserens fuldskærmsknap."; }
    private async Task CloseWindow() { if (HasUnsavedRounds && !await Js.InvokeAsync<bool>("confirm", "Der er grupper, som endnu ikke er gemt på holdet. Luk alligevel?")) return; _leaving = true; await InvokeAsync(StateHasChanged); if (_module is not null) await _module.InvokeVoidAsync("closeOrReturn", BackHref); else Nav.NavigateTo(BackHref); }
    private async Task OpenOverview() { _overview = true; await InvokeAsync(StateHasChanged); if (_module is not null) await _module.InvokeVoidAsync("setOverview", _root, true); }
    [JSInvokable] public async Task CloseOverview() { _overview = false; if (_module is not null) await _module.InvokeVoidAsync("setOverview", _root, false); await InvokeAsync(StateHasChanged); }
    private Task CopyCurrent() => _current is null ? Task.CompletedTask : CopyRound(_current);
    private Task CopyRound(ShowRound round) => CopyText(RoundText(round));
    private Task CopyAll() => CopyText(string.Join("\n\n", _rounds.Where(r => r.Revealed > 0).Select(RoundText)));
    private string RoundText(ShowRound round) => $"Runde {round.Number} · {round.SetName}\n" + string.Join("\n", round.Groups.Take(round.Revealed).Select(g => $"{g.Name}: {string.Join(", ", g.MemberIds.Select(MemberName))}"));
    private async Task CopyText(string text)
    {
        if (_module is null || !await _module.InvokeAsync<bool>("copy", text)) { _copyFallback = text; _notice = "Markér og kopiér teksten i feltet herunder."; }
        else _notice = "Grupperne er kopieret.";
    }
    private async Task BeforeNavigation(LocationChangingContext context) { if (HasUnsavedRounds && !await Js.InvokeAsync<bool>("confirm", "Du har grupper, som endnu ikke er gemt på holdet. Forlad grupperouletten?")) context.PreventNavigation(); }
    private async Task Persist()
    {
        if (_module is null || !MayManage || _storageKey.Length == 0) return;
        var state = new ShowSession { Rounds = _rounds.TakeLast(12).ToArray(), CurrentRound = _current?.Number, SelectedIds = _selected.ToArray(), Distribution = _distribution, Amount = _amount, AvoidPairs = _avoidPairs, HistorySets = _historySets.ToArray(), IncludeLegacy = _includeLegacy };
        try { await _module.InvokeVoidAsync("saveSession", _storageKey, JsonSerializer.Serialize(state, Json)); } catch (JSException) { }
    }
    private void Restore(string stored)
    {
        if (stored.Length > 500_000) return;
        var state = JsonSerializer.Deserialize<ShowSession>(stored, Json);
        if (state?.Rounds is null || state.Rounds.Length > 12) return;
        var allowed = _members.Select(m => m.Id).ToHashSet();
        foreach (var round in state.Rounds)
        {
            if (round is null || round.ParticipantIds is null || round.Groups is null || round.ParticipantIds.Length is < 1 or > 250 || round.Groups.Length is < 1 or > 100 || round.Revealed < 0 || round.Revealed > round.Groups.Length || round.Number < 1
                || round.ParticipantIds.Any(id => !allowed.Contains(id)) || round.ParticipantIds.Distinct().Count() != round.ParticipantIds.Length
                || round.Groups.Any(g => g is null || string.IsNullOrWhiteSpace(g.Name) || g.Name.Length > 120 || g.MemberIds is null || g.MemberIds.Length == 0)
                || round.Groups.SelectMany(g => g.MemberIds).Order().SequenceEqual(round.ParticipantIds.Order()) == false) continue;
            round.SetName = _sets.FirstOrDefault(s => s.Id == round.TargetSetId)?.Name ?? (string.IsNullOrWhiteSpace(round.SetName) ? $"Makkerpar · Runde {round.Number}" : round.SetName);
            round.Saved = round.Saved && round.SavedGroupIds is { Length: > 0 } && round.SavedGroupIds.Length == round.Groups.Length && round.SavedGroupIds.All(id => _groups.Any(g => g.Id == id));
            if (!round.Saved && !round.SaveAttempted && round.TargetSetId is Guid target && !_sets.Any(s => s.Id == target && !s.Archived)) round.TargetSetId = null;
            if (round.RequestId == Guid.Empty) round.RequestId = Guid.NewGuid();
            _rounds.Add(round);
        }
        if (_rounds.Count == 0) return;
        _current = _rounds.FirstOrDefault(r => r.Number == state.CurrentRound);
        if (SetId is Guid requestedSet && _current?.TargetSetId != requestedSet) _current = null;
        _lastWinner = _current?.Groups.Take(_current.Revealed).LastOrDefault();
        if (_lastWinner is not null) _stageNames = _lastWinner.MemberIds.Take(6).Select(MemberName).ToArray();
        _selected.Clear(); foreach (var id in (state.SelectedIds ?? []).Where(allowed.Contains)) _selected.Add(id);
        _distribution = state.Distribution == "count" ? "count" : "size"; _amount = Math.Clamp(state.Amount, 1, _distribution == "size" ? 12 : 100); _avoidPairs = state.AvoidPairs; _includeLegacy = state.IncludeLegacy;
        _historySets.Clear(); foreach (var id in (state.HistorySets ?? []).Where(id => _sets.Any(s => s.Id == id))) _historySets.Add(id);
        if (SetId is Guid selectedSet && _sets.Any(s => s.Id == selectedSet)) _historySets.Add(selectedSet);
        _newSetName = $"Makkerpar · Runde {DisplayRound}";
        _showGroups = _current?.Revealed > 0; _celebration = "Velkommen tilbage til runden!"; _notice = "De seneste runder er genskabt fra denne browser.";
    }
    public async ValueTask DisposeAsync()
    {
        _disposed = true; StopAuto(); CancelVoicePreparation(); Auth.AuthStateChanged -= ContextChanged; ViewAs.Changed -= ContextChanged; _scope.Cancel(); _scope.Dispose();
        if (_module is not null) { try { await _module.InvokeVoidAsync("dispose", _root); await _module.DisposeAsync(); } catch (JSException) { } }
        _callbacks?.Dispose();
    }
    public sealed record EffectCapabilities(bool ReducedMotion, bool SpeechAvailable, bool? AudioReady = null, bool? Sound = null);
    public sealed class ShowGroup { public int Number { get; set; } public string Name { get; set; } = ""; public Guid[] MemberIds { get; set; } = []; }
    public sealed class ShowRound
    {
        public Guid RequestId { get; set; } = Guid.NewGuid(); public int Number { get; set; } public Guid[] ParticipantIds { get; set; } = []; public ShowGroup[] Groups { get; set; } = []; public int Revealed { get; set; }
        public Guid? TargetSetId { get; set; } public string SetName { get; set; } = ""; public bool Saved { get; set; } public bool SaveAttempted { get; set; } public Guid[] SavedGroupIds { get; set; } = [];
        public bool Complete => Revealed == Groups.Length;
    }
    public sealed class ShowSession
    {
        public ShowRound[] Rounds { get; set; } = []; public int? CurrentRound { get; set; } public Guid[] SelectedIds { get; set; } = []; public string Distribution { get; set; } = "size"; public int Amount { get; set; } = 3;
        public bool AvoidPairs { get; set; } = true; public Guid[] HistorySets { get; set; } = []; public bool IncludeLegacy { get; set; }
    }
}
