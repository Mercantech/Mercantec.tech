using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Learning;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Mercantec.Tech.Web.Pages.Learning;

public partial class Assignments
{
    [Inject] private LearningClient Client { get; set; } = null!;
    [Inject] private AuthService Auth { get; set; } = null!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = null!;

    private LearningHold[] _holds = [];
    private CourseMaterialView[] _materials = [];
    private QuestView[] _quests = [];
    private LearningGroupView[] _groups = [];
    private LearningMember[] _members = [];
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
    private string _questTitle = "", _questDescription = "", _questMode = "individual";
    private bool _questPublished = true;
    private string _editGroupId = "", _groupName = "", _submissionGroup = "";
    private readonly HashSet<Guid> _groupMembers = [];
    private string _draftText = "";
    private List<SubmissionFileView> _draftFiles = [];
    private string _reviewRevision = "", _reviewFile = "", _reviewText = "", _reviewDecision = "";
    private readonly List<SubmissionFileView> _feedbackFiles = [];

    private bool CanManage => _holds.FirstOrDefault(h => h.Id == _holdId)?.CanManage == true;
    private SubmissionFileView[] ReviewFiles => _submission?.Revisions
        .FirstOrDefault(r => r.Id.ToString() == _reviewRevision)?.Files ?? [];
    private bool ReviewIsLatest => _submission?.Revisions.OrderByDescending(r => r.Number)
        .FirstOrDefault()?.Id.ToString() == _reviewRevision;

    protected override void OnInitialized()
    {
        Auth.AuthStateChanged += AccountChanged;
        ViewAs.Changed += AccountChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await SyncAccount();
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
        if (Auth.IsAuthenticated && !ViewAs.IsActive) await Reload();
        if (!_disposed) StateHasChanged();
    }

    private Task Reload() => Run(async ct =>
    {
        _loading = true;
        try
        {
            var holds = await Client.Holds(ct);
            ct.ThrowIfCancellationRequested();
            _holds = holds;
            if (!_holds.Any(h => h.Id == _holdId)) _holdId = _holds.FirstOrDefault()?.Id ?? Guid.Empty;
            ClearHold();
            if (_holdId != Guid.Empty) await LoadHold(ct);
        }
        finally { _loading = false; }
    });

    private Task ChangeHold(ChangeEventArgs e) => Run(async ct =>
    {
        if (!Guid.TryParse(e.Value?.ToString(), out var id)) return;
        _holdId = id;
        ClearHold();
        await LoadHold(ct);
    });

    private async Task LoadHold(CancellationToken ct)
    {
        var materials = await Client.Materials(_holdId, ct);
        var quests = await Client.Quests(_holdId, ct);
        var groups = await Client.Groups(_holdId, ct);
        var members = CanManage ? await Client.Members(_holdId, ct) : [];
        ct.ThrowIfCancellationRequested();
        _materials = materials;
        _quests = quests;
        _groups = groups;
        _members = members;
    }

    private void ClearHold()
    {
        _materials = [];
        _quests = [];
        _groups = [];
        _members = [];
        _submissions = [];
        _quest = null;
        _submission = null;
        _viewer = null;
        _materialFiles = [];
        _materialTitle = _materialDescription = "";
        _questTitle = _questDescription = "";
        _editGroupId = _groupName = _submissionGroup = "";
        _groupMembers.Clear();
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

    private Task CreateQuest() => Run(async ct =>
    {
        var quest = await Client.CreateQuest(new(_holdId, _questTitle, _questDescription,
            _questMode, _questPublished, null), ct);
        ct.ThrowIfCancellationRequested();
        _quests = [.. _quests, quest];
        _questTitle = _questDescription = "";
        _notice = "Questen er oprettet.";
    });

    private Task ToggleQuest(QuestView quest) => Run(async ct =>
    {
        var updated = await Client.UpdateQuest(quest.Id, new(quest.HoldId, quest.Title,
            quest.Description, quest.SubmissionMode, !quest.Published, quest.DeadlineUtc), ct);
        ct.ThrowIfCancellationRequested();
        _quests = _quests.Select(q => q.Id == updated.Id ? updated : q).ToArray();
        if (_quest?.Id == updated.Id) _quest = updated;
    });

    private void ToggleMember(Guid id, ChangeEventArgs e)
    {
        if (e.Value is true) _groupMembers.Add(id);
        else _groupMembers.Remove(id);
    }

    private void EditGroup(ChangeEventArgs e)
    {
        _editGroupId = e.Value?.ToString() ?? "";
        var group = _groups.FirstOrDefault(g => g.Id.ToString() == _editGroupId);
        _groupName = group?.Name ?? "";
        _groupMembers.Clear();
        foreach (var member in group?.Members ?? []) _groupMembers.Add(member.Id);
    }

    private Task SaveGroup() => Run(async ct =>
    {
        var current = _groups.FirstOrDefault(g => g.Id.ToString() == _editGroupId);
        var request = new SaveLearningGroup(_holdId, _groupName, _groupMembers.ToArray(), current?.Version ?? 0);
        var group = current is null
            ? await Client.SaveGroup(request, ct)
            : await Client.UpdateGroup(current.Id, request, ct);
        ct.ThrowIfCancellationRequested();
        _groups = [.. _groups.Where(g => g.Id != group.Id), group];
        _editGroupId = group.Id.ToString();
        _notice = "Gruppen er gemt. Deltagere på tidligere indsendte versioner bevares.";
    });

    private Task SelectQuest(QuestView quest) => Run(async ct =>
    {
        var submissions = await Client.Submissions(quest.Id, ct);
        ct.ThrowIfCancellationRequested();
        _quest = quest;
        _submissionGroup = "";
        _submissions = submissions;
        SetSubmission(null);
        _viewer = null;
    });

    private Task StartSubmission() => Run(async ct =>
    {
        if (_quest is null) return;
        Guid? group = Guid.TryParse(_submissionGroup, out var id) ? id : null;
        var submission = await Client.Start(new(_quest.Id, group), ct);
        ct.ThrowIfCancellationRequested();
        SetSubmission(submission);
    });

    private Task OpenSubmission(Guid id) => Run(async ct =>
    {
        var submission = await Client.Submission(id, ct);
        ct.ThrowIfCancellationRequested();
        SetSubmission(submission);
    });

    private void SetSubmission(SubmissionView? submission)
    {
        _submission = submission;
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
        await PersistDraft(ct);
        _notice = "Kladden er gemt.";
    });

    private async Task PersistDraft(CancellationToken ct)
    {
        if (_submission is null) return;
        var updated = await Client.SaveDraft(_submission.Id,
            new(_submission.Version, _draftText, _draftFiles.Select(f => f.Id).ToArray()), ct);
        ct.ThrowIfCancellationRequested();
        SetSubmission(updated);
    }

    private Task Submit() => Run(async ct =>
    {
        if (_submission is null) return;
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
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        Auth.AuthStateChanged -= AccountChanged;
        ViewAs.Changed -= AccountChanged;
        _account.Cancel();
        _account.Dispose();
    }
}
