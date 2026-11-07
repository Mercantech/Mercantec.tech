using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Emojis;
using Mercantec.Tech.Web.Learning;
using Mercantec.Tech.Web.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.Learning;

public partial class Assignments
{
    [Inject] private LearningClient Client { get; set; } = null!;
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private AuthService Auth { get; set; } = null!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Parameter] public Guid QuestId { get; set; }
    [SupplyParameterFromQuery(Name = "hold")] public Guid? RequestedHoldId { get; set; }
    [SupplyParameterFromQuery(Name = "submission")] public Guid? RequestedSubmissionId { get; set; }
    [SupplyParameterFromQuery(Name = "workspace")] public Guid? WorkspaceQuery { get; set; }
    [SupplyParameterFromQuery(Name = "course")] public Guid? CourseQuery { get; set; }
    private (Guid Quest, Guid? Hold, Guid? Submission, Guid? Workspace, Guid? Course) _loadedRoute;
    private (Guid Quest, Guid? Hold, Guid? Submission, Guid? Workspace, Guid? Course) CurrentRoute => (QuestId, RequestedHoldId, RequestedSubmissionId, WorkspaceQuery, CourseQuery);
    private WorkspaceRouteContext? _returnContext;
    private string WorkspaceHref(string view = "overview", bool create = false) => _returnContext is { } context
        ? WorkspaceNavigation.View(context.WorkspaceId, view, context.CourseId, create)
        : WorkspaceNavigation.Hold(_holdId, view, create: create);
    private string QuestHref(Guid id) => WorkspaceNavigation.Quest(id, _returnContext?.WorkspaceId, _returnContext?.CourseId, _holdId);

    private LearningHold[] _holds = [];
    private CourseMaterialView[] _materials = [];
    private QuestView[] _quests = [];
    private LearningGroupView[] _groups = [];
    private SubmissionView[] _submissions = [];
    private Guid _holdId;
    private QuestView? _quest;
    private SubmissionView? _submission;
    private Guid? _viewer;
    private bool _busy, _loading, _disposed, _accountLoaded, _previewActive;
    private string? _error, _notice, _subject;
    private CancellationTokenSource _account = new();

    private string _materialTitle = "", _materialDescription = "";
    private bool _materialPublished;
    private IReadOnlyList<IBrowserFile> _materialFiles = [];
    private string _submissionGroup = "";
    private string _draftText = "";
    private List<SubmissionFileView> _draftFiles = [];
    private string _reviewRevision = "", _reviewFile = "", _reviewText = "", _reviewDecision = "";
    private readonly List<SubmissionFileView> _feedbackFiles = [];

    private bool CanManage => !ViewAs.IsActive && _holds.FirstOrDefault(h => h.Id == _holdId)?.CanManage == true;
    private string HoldName => _holds.FirstOrDefault(h => h.Id == _holdId)?.Name ?? "Holdet";
    private bool DraftDirty => !ViewAs.IsActive && _submission?.CanEdit == true
        && (_draftText != _submission.DraftText || !_draftFiles.Select(f => f.Id).Order().SequenceEqual(_submission.DraftFiles.Select(f => f.Id).Order()));
    private bool HasDraftContent => !string.IsNullOrWhiteSpace(_draftText) || _draftFiles.Count > 0;
    private string CurrentGroupMembers => _groups.FirstOrDefault(g => g.Id.ToString() == _submissionGroup) is { } group
        ? string.Join(", ", group.Members.Select(m => m.DisplayName)) : "";
    private IEnumerable<LearningGroupView> SubmissionGroups => _groups.Where(g => (_quest?.GroupSetId is null || g.GroupSetId == _quest.GroupSetId)
        && (CanManage || _quest?.AllowedGroupIds is not Guid[] allowed || allowed.Contains(g.Id)));
    private SubmissionFileView[] ReviewFiles => _submission?.Revisions
        .FirstOrDefault(r => r.Id.ToString() == _reviewRevision)?.Files ?? [];
    private bool ReviewIsLatest => _submission?.Revisions.OrderByDescending(r => r.Number)
        .FirstOrDefault()?.Id.ToString() == _reviewRevision;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        // Blazor reuses this component between the quest routes and /afleveringer.
        // Missing parameters must not retain the previous route's quest or hold.
        QuestId = Guid.Empty;
        RequestedHoldId = null;
        RequestedSubmissionId = null;
        WorkspaceQuery = CourseQuery = null;
        return base.SetParametersAsync(parameters);
    }

    protected override void OnInitialized()
    {
        Auth.AuthStateChanged += AccountChanged;
        ViewAs.Changed += AccountChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await SyncAccount();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_accountLoaded && Auth.IsAuthenticated && _loadedRoute != CurrentRoute)
            await Reload();
    }

    private void AccountChanged() => _ = InvokeAsync(SyncAccount);

    private async Task SyncAccount()
    {
        if (!Auth.IsInitialized) return;
        // Token refreshes notify subscribers too. Keep the user's current draft during a refresh.
        if (_accountLoaded && _subject == Auth.Subject && _previewActive == ViewAs.IsActive) return;
        _accountLoaded = true;
        _previewActive = ViewAs.IsActive;
        _account.Cancel();
        _account.Dispose();
        _account = new();
        _subject = Auth.Subject;
        _busy = false;
        _holds = [];
        _holdId = Guid.Empty;
        ClearHold();
        _error = null;
        _notice = null;
        if (Auth.IsAuthenticated) await Reload();
        if (!_disposed) StateHasChanged();
    }

    private Task Reload() => Run(async ct =>
    {
        var route = CurrentRoute;
        _loadedRoute = route;
        _loading = true;
        try
        {
            ClearHold();
            var holds = await Client.Holds(ct);
            ct.ThrowIfCancellationRequested();
            _holds = holds;
            QuestView? routeQuest = null;
            if (route.Quest != Guid.Empty)
            {
                routeQuest = await Client.Quest(route.Quest, ct);
                if (ViewAs.IsActive && !routeQuest.Published) throw new LearningException("Questen er endnu ikke udgivet til eleverne.");
            }
            if (route != CurrentRoute) return;
            var requested = routeQuest?.HoldId ?? route.Hold;
            if (requested is Guid requestedId && _holds.Any(h => h.Id == requestedId)) _holdId = requestedId;
            if (!_holds.Any(h => h.Id == _holdId)) _holdId = _holds.FirstOrDefault()?.Id ?? Guid.Empty;
            if (_holdId != Guid.Empty)
            {
                await LoadHold(ct);
                if ((WorkspaceNavigation.NonEmpty(route.Workspace) is not null || WorkspaceNavigation.NonEmpty(route.Course) is not null)
                    && (route.Hold is null || route.Hold == _holdId))
                    _returnContext = await WorkspaceNavigation.Resolve(Http, _holdId, route.Workspace, route.Course, ViewAs.IsActive, ct);
                ct.ThrowIfCancellationRequested();
                if (route != CurrentRoute) return;
            }
            if (routeQuest is not null)
            {
                _quest = _quests.FirstOrDefault(q => q.Id == routeQuest.Id);
                if (_quest is not null && !ViewAs.IsActive)
                {
                    _submissions = await Client.Submissions(_quest.Id, ct);
                    var selected = route.Submission is Guid submissionId ? _submissions.FirstOrDefault(s => s.Id == submissionId)
                        : CanManage ? null : _submissions.OrderByDescending(s => s.CanEdit)
                            .ThenByDescending(s => s.Revisions.Select(r => (DateTimeOffset?)r.SubmittedAtUtc).Max()).ThenBy(s => s.Id).FirstOrDefault();
                    if (route.Submission.HasValue && selected is null) throw new LearningException("Afleveringen er ikke tilgængelig for din konto.");
                    if (selected is not null) SetSubmission(await Client.Submission(selected.Id, ct));
                }
            }
            ct.ThrowIfCancellationRequested();
        }
        finally { _loading = false; }
    });

    private async Task ChangeHold(ChangeEventArgs e)
    {
        if (!Guid.TryParse(e.Value?.ToString(), out var id) || id == _holdId || !await DiscardDraft()) return;
        Nav.NavigateTo($"/afleveringer?hold={id}");
    }

    private async Task LoadHold(CancellationToken ct)
    {
        var materials = await Client.Materials(_holdId, ct);
        var quests = await Client.Quests(_holdId, ct);
        var groups = ViewAs.IsActive ? [] : await Client.Groups(_holdId, ct);
        ct.ThrowIfCancellationRequested();
        _materials = ViewAs.IsActive ? materials.Where(m => m.Published).ToArray() : materials;
        _quests = ViewAs.IsActive ? quests.Where(q => q.Published).ToArray() : quests;
        _groups = groups;
    }

    private void ClearHold()
    {
        _returnContext = null;
        _materials = [];
        _quests = [];
        _groups = [];
        _submissions = [];
        _quest = null;
        _submission = null;
        _viewer = null;
        _materialFiles = [];
        _materialTitle = _materialDescription = "";
        SetSubmission(null);
    }

    private void SelectMaterials(InputFileChangeEventArgs e)
    {
        try { _materialFiles = e.GetMultipleFiles(20); }
        catch (InvalidOperationException) { _error = "Vælg højst 20 filer ad gangen."; }
    }

    private Task UploadMaterials() => Run(async ct =>
    {
        // Remove completed files from this local queue so a retry cannot republish them.
        while (_materialFiles.Count > 0)
        {
            var file = _materialFiles[0];
            var uploaded = await Client.Upload(file, _holdId, ct);
            var title = string.IsNullOrWhiteSpace(_materialTitle) ? file.Name : _materialTitle.Trim();
            var material = await Client.AddMaterial(new(_holdId, uploaded.Id, title,
                _materialDescription, _materialPublished), ct);
            ct.ThrowIfCancellationRequested();
            _materials = [.. _materials, material];
            _materialFiles = _materialFiles.Skip(1).ToArray();
        }
        _notice = "Materialerne er gemt.";
    });

    private Task ToggleMaterial(CourseMaterialView material) => Run(async ct =>
    {
        var updated = await Client.UpdateMaterial(material.Id,
            new(material.Title, material.Description, !material.Published), ct);
        ct.ThrowIfCancellationRequested();
        _materials = _materials.Select(m => m.Id == updated.Id ? updated : m).ToArray();
    });

    private async Task StartSubmission()
    {
        if (ViewAs.IsActive || CanManage || _quest is null || !await DiscardDraft()) return;
        await Run(async ct =>
        {
            Guid? group = Guid.TryParse(_submissionGroup, out var id) ? id : null;
            if (_quest.SubmissionMode == "group" && group is null) return;
            var submission = await Client.Start(new(_quest.Id, group), ct);
            ct.ThrowIfCancellationRequested();
            SetSubmission(submission);
        });
    }

    private async Task OpenSubmission(Guid id)
    {
        if (ViewAs.IsActive || _submission?.Id == id || !await DiscardDraft()) return;
        await Run(async ct => { var submission = await Client.Submission(id, ct); ct.ThrowIfCancellationRequested(); SetSubmission(submission); });
    }

    private void SetSubmission(SubmissionView? submission)
    {
        _submission = submission;
        _submissionGroup = submission?.GroupId?.ToString() ?? "";
        _draftText = submission?.DraftText ?? "";
        _draftFiles = submission?.DraftFiles.ToList() ?? [];
        _reviewRevision = submission?.Revisions.OrderByDescending(r => r.Number).FirstOrDefault()?.Id.ToString() ?? "";
        _reviewFile = _reviewText = _reviewDecision = "";
        _feedbackFiles.Clear();
        if (submission is not null)
            _submissions = [.. _submissions.Where(s => s.Id != submission.Id), submission];
    }

    private void RemoveDraftFile(Guid id) => _draftFiles.RemoveAll(f => f.Id == id);
    private void RemoveFeedbackFile(Guid id) => _feedbackFiles.RemoveAll(f => f.Id == id);

    private void ChangeReviewRevision(ChangeEventArgs e)
    {
        _reviewRevision = e.Value?.ToString() ?? "";
        _reviewFile = _reviewDecision = "";
    }

    private Task UploadDraftFiles(InputFileChangeEventArgs e) => Run(async ct =>
    {
        if (ViewAs.IsActive || _submission?.CanEdit != true) return;
        foreach (var file in SelectedFiles(e))
        {
            var uploaded = await Client.Upload(file, _holdId, ct);
            ct.ThrowIfCancellationRequested();
            _draftFiles.Add(new(uploaded.Id, uploaded.OriginalName, uploaded.ContentType, uploaded.SizeBytes));
        }
        _notice = "Filerne er uploadet. Gem kladden for at dele ændringerne med gruppen.";
    });

    private Task SaveDraft() => Run(async ct =>
    {
        if (ViewAs.IsActive || _submission?.CanEdit != true) return;
        await PersistDraft(ct);
        _notice = "Kladden er gemt.";
    });

    private async Task PersistDraft(CancellationToken ct)
    {
        if (ViewAs.IsActive || _submission?.CanEdit != true) return;
        var updated = await Client.SaveDraft(_submission.Id,
            new(_submission.Version, _draftText, _draftFiles.Select(f => f.Id).ToArray()), ct);
        ct.ThrowIfCancellationRequested();
        SetSubmission(updated);
    }

    private Task Submit() => Run(async ct =>
    {
        if (ViewAs.IsActive || _submission?.CanEdit != true || !HasDraftContent) return;
        await PersistDraft(ct);
        var updated = await Client.Submit(_submission!.Id, new(_submission.Version), ct);
        ct.ThrowIfCancellationRequested();
        SetSubmission(updated);
        _notice = "Afleveringen er indsendt. Alle registrerede deltagere kan se den.";
    });

    private Task UploadFeedbackFiles(InputFileChangeEventArgs e) => Run(async ct =>
    {
        foreach (var file in SelectedFiles(e))
        {
            var uploaded = await Client.Upload(file, _holdId, ct);
            ct.ThrowIfCancellationRequested();
            _feedbackFiles.Add(new(uploaded.Id, uploaded.OriginalName, uploaded.ContentType, uploaded.SizeBytes));
        }
    });

    private static IReadOnlyList<IBrowserFile> SelectedFiles(InputFileChangeEventArgs e)
    {
        if (e.FileCount > 20) throw new LearningException("Vælg højst 20 filer ad gangen.");
        return e.GetMultipleFiles(20);
    }

    private Task Review() => Run(async ct =>
    {
        if (_submission is null || !Guid.TryParse(_reviewRevision, out var revisionId)) return;
        Guid? fileId = Guid.TryParse(_reviewFile, out var id) ? id : null;
        var updated = await Client.Review(_submission.Id, new(revisionId, _submission.Version,
            _reviewText, fileId, string.IsNullOrEmpty(_reviewDecision) ? null : _reviewDecision,
            _feedbackFiles.Select(f => f.Id).ToArray()), ct);
        ct.ThrowIfCancellationRequested();
        SetSubmission(updated);
        _notice = "Feedback og vurdering er gemt.";
    });

    private void OpenFile(Guid id) => _viewer = id;
    private void CloseFile() => _viewer = null;
    private static string FileSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024):0.#} MiB" : $"{Math.Max(1, bytes / 1024d):0.#} KiB";

    private async Task<bool> DiscardDraft() => !DraftDirty || await Js.InvokeAsync<bool>("confirm", "Du har ugemte ændringer i afleveringen. Kassér dem, og fortsæt?");
    private async Task Refresh()
    {
        if (await DiscardDraft()) await Reload();
    }
    private async Task BeforeNavigation(LocationChangingContext context)
    {
        if (!await DiscardDraft()) context.PreventNavigation();
    }

    private string SubmissionLabel(SubmissionView submission)
    {
        var participants = submission.Revisions.OrderByDescending(r => r.Number).FirstOrDefault()?.Participants;
        if (participants?.Length > 0) return string.Join(", ", participants.Select(p => p.DisplayName));
        return submission.GroupId is Guid groupId
            ? _groups.FirstOrDefault(g => g.Id == groupId)?.Name ?? "Gruppeaflevering"
            : "Individuel aflevering";
    }

    private static string ModeLabel(string mode) => mode switch
    {
        "group" => "Gruppeaflevering", "either" => "Individuel eller gruppe", _ => "Individuel aflevering"
    };

    private static string StateLabel(string state) => state switch
    {
        "submitted" => "Afleveret", "changes-requested" => "Kræver rettelser", "approved" => "Godkendt", _ => "Kladde"
    };

    private async Task Run(Func<CancellationToken, Task> action)
    {
        if (_busy || _disposed) return;
        var ct = _account.Token;
        var subject = _subject;
        _busy = true;
        _error = null;
        _notice = null;
        try { await action(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is LearningException or HttpRequestException or OperationCanceledException or IOException)
        {
            if (!ct.IsCancellationRequested)
                _error = ex is LearningException ? ex.Message : "Forbindelsen blev afbrudt. Prøv igen.";
        }
        finally
        {
            if (!_disposed && subject == _subject && !ct.IsCancellationRequested)
            {
                _busy = false;
                StateHasChanged();
                if (Auth.IsAuthenticated && _loadedRoute != CurrentRoute) await Reload();
            }
        }
    }

    private void InsertDraftEmoji(CustomEmojiItem emoji) => _draftText = EmojiInsert.Append(_draftText, emoji.Name, 20000);
    private void InsertReviewEmoji(CustomEmojiItem emoji) => _reviewText = EmojiInsert.Append(_reviewText, emoji.Name, 10000);

    public void Dispose()
    {
        _disposed = true;
        Auth.AuthStateChanged -= AccountChanged;
        ViewAs.Changed -= AccountChanged;
        _account.Cancel();
        _account.Dispose();
    }
}
