using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/submissions")]
public sealed class SubmissionsController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid questId, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var quest = await db.LearningQuests.FindAsync([questId], ct);
        if (quest is null || !await access.CanAccessAsync(quest.HoldId, User, ct)) return NotFound();
        var manage = await access.CanManageAsync(quest.HoldId, User, ct);
        if (!manage && !quest.Published) return NotFound();
        var submissions = await Query().Where(s => s.QuestId == questId).ToListAsync(ct);
        var memberIds = await db.Memberships.Where(m => m.UserSub == sub).Select(m => m.Id).ToListAsync(ct);
        var result = new List<SubmissionView>();
        foreach (var submission in submissions)
        {
            var edit = await CanEdit(submission, sub, ct);
            if (manage || edit || IsParticipant(submission, sub, memberIds))
                result.Add(View(submission, sub, memberIds, edit, manage));
        }
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var submission = await Query().SingleOrDefaultAsync(s => s.Id == id, ct);
        if (submission is null || !await access.CanAccessAsync(submission.Quest.HoldId, User, ct)) return NotFound();
        var manage = await access.CanManageAsync(submission.Quest.HoldId, User, ct);
        var edit = await CanEdit(submission, sub, ct);
        var memberIds = await db.Memberships.Where(m => m.UserSub == sub).Select(m => m.Id).ToListAsync(ct);
        if (!manage && !submission.Quest.Published) return NotFound();
        if (!manage && !edit && !IsParticipant(submission, sub, memberIds)) return NotFound();
        return Ok(View(submission, sub, memberIds, edit, manage));
    }

    [HttpPost]
    public async Task<IActionResult> Start(StartSubmission request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (UserClaims.IsTeacher(User)) return Forbid();
        var quest = await db.LearningQuests.SingleOrDefaultAsync(q => q.Id == request.QuestId && q.Published, ct);
        if (quest is null || !await IsMember(quest.HoldId, sub, ct)) return NotFound();
        if (request.GroupId.HasValue ? quest.SubmissionMode == "individual" : quest.SubmissionMode == "group")
            return BadRequest(new { error = "invalid_submission_mode" });
        if (request.GroupId is Guid groupId && !await db.LearningGroupMembers.AnyAsync(g =>
            g.GroupId == groupId && g.Group.HoldId == quest.HoldId && g.Membership.UserSub == sub, ct))
            return NotFound();
        var owner = request.GroupId is Guid group ? $"g:{group:N}" : $"u:{sub:N}";
        var existing = await db.QuestSubmissions.FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.OwnerKey == owner, ct);
        if (existing is not null) return await Get(existing.Id, ct);
        var submission = new QuestSubmission
        {
            Id = Guid.NewGuid(), QuestId = quest.Id, CreatedBySub = sub, GroupId = request.GroupId,
            OwnerKey = owner, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        db.QuestSubmissions.Add(submission);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // A concurrent click may have created the same unique quest/owner draft.
            db.ChangeTracker.Clear();
            var winner = await db.QuestSubmissions.FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.OwnerKey == owner, ct);
            if (winner is null) throw;
            return await Get(winner.Id, ct);
        }
        return await Get(submission.Id, ct);
    }

    [HttpPut("{id:guid}/draft")]
    public async Task<IActionResult> SaveDraft(Guid id, SaveSubmissionDraft request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var submission = await Query().SingleOrDefaultAsync(s => s.Id == id, ct);
        if (submission is null) return NotFound();
        if (submission.GroupId is Guid groupId)
            await db.LearningGroups.Where(g => g.Id == groupId)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.Version, g => g.Version + 1), ct);
        if (!await CanEdit(submission, sub, ct)) return NotFound();
        if (request.Text is null || request.Text.Length > 20000 || request.FileIds is null
            || request.FileIds.Length > 100 || request.FileIds.Distinct().Count() != request.FileIds.Length)
            return BadRequest(new { error = "invalid_draft" });
        if (submission.Version != request.Version) return Conflict(new { error = "submission_changed" });
        var assets = await db.FileAssets.Where(f => request.FileIds.Contains(f.Id)).ToListAsync(ct);
        var currentFiles = submission.DraftFiles.Select(f => f.FileAssetId).ToHashSet();
        if (assets.Count != request.FileIds.Length || assets.Any(f => f.HoldId != submission.Quest.HoldId
            || f.OwnerSub != sub && !currentFiles.Contains(f.Id)))
            return BadRequest(new { error = "invalid_attachment" });
        var wanted = request.FileIds.ToHashSet();
        foreach (var old in submission.DraftFiles.Where(f => !wanted.Contains(f.FileAssetId)).ToArray())
            db.Remove(old);
        foreach (var file in assets.Where(f => !currentFiles.Contains(f.Id)))
            submission.DraftFiles.Add(new() { FileAssetId = file.Id });
        submission.DraftText = request.Text;
        submission.Version++;
        submission.UpdatedAtUtc = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "submission_changed" }); }
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return await Get(id, ct);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, SubmitSubmission request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var submission = await Query().SingleOrDefaultAsync(s => s.Id == id, ct);
        if (submission is null || !await CanEdit(submission, sub, ct)) return NotFound();
        if (submission.Version != request.Version) return Conflict(new { error = "submission_changed" });
        if (string.IsNullOrWhiteSpace(submission.DraftText) && submission.DraftFiles.Count == 0)
            return BadRequest(new { error = "empty_submission" });
        List<Membership> members;
        if (submission.GroupId is Guid groupId)
        {
            // Group edits and submission snapshots serialize on the same row.
            await db.LearningGroups.Where(g => g.Id == groupId)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.Version, g => g.Version + 1), ct);
            members = await db.LearningGroupMembers.AsNoTracking().Where(g => g.GroupId == groupId)
                .Select(g => g.Membership).ToListAsync(ct);
            if (!members.Any(m => m.UserSub == sub)) return Conflict(new { error = "group_changed" });
        }
        else members = await db.Memberships.AsNoTracking()
            .Where(m => m.HoldId == submission.Quest.HoldId && m.UserSub == sub).ToListAsync(ct);
        if (members.Count == 0 || members.Any(m => m.HoldId != submission.Quest.HoldId))
            return Conflict(new { error = "group_changed" });
        var revision = new SubmissionRevision
        {
            Id = Guid.NewGuid(), SubmissionId = submission.Id, Submission = submission,
            Number = submission.Revisions.Count + 1, Text = submission.DraftText,
            SubmittedBySub = sub, SubmittedAtUtc = DateTimeOffset.UtcNow
        };
        foreach (var file in submission.DraftFiles)
            revision.Files.Add(new() { FileAssetId = file.FileAssetId });
        foreach (var member in members)
            revision.Participants.Add(new() { MembershipId = member.Id, UserSub = member.UserSub,
                DisplayName = member.DisplayName ?? "Elev" });
        db.SubmissionRevisions.Add(revision);
        submission.State = "submitted";
        submission.Version++;
        submission.UpdatedAtUtc = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "submission_changed" }); }
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return await Get(id, ct);
    }

    [HttpPost("{id:guid}/feedback")]
    public async Task<IActionResult> Feedback(Guid id, ReviewSubmission request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var submission = await Query().SingleOrDefaultAsync(s => s.Id == id, ct);
        if (submission is null || !await access.CanManageAsync(submission.Quest.HoldId, User, ct)) return NotFound();
        var revision = submission.Revisions.FirstOrDefault(r => r.Id == request.RevisionId);
        if (revision is null) return NotFound();
        if (request.Text is null || request.Text.Length > 10000
            || request.Decision is not (null or "approved" or "changes-requested")
            || request.AttachmentIds is null || request.AttachmentIds.Length > 20
            || request.AttachmentIds.Distinct().Count() != request.AttachmentIds.Length
            || string.IsNullOrWhiteSpace(request.Text) && request.Decision is null && request.AttachmentIds.Length == 0)
            return BadRequest(new { error = "invalid_feedback" });
        if (request.FileAssetId is Guid fileId && !revision.Files.Any(f => f.FileAssetId == fileId))
            return BadRequest(new { error = "file_not_in_revision" });
        if (submission.Version != request.Version) return Conflict(new { error = "submission_changed" });
        if (request.Decision is not null && (revision.Number != submission.Revisions.Max(r => r.Number)
            || submission.State is not ("submitted" or "approved" or "changes-requested")))
            return Conflict(new { error = "revision_not_current" });
        var assets = await db.FileAssets.Where(f => request.AttachmentIds.Contains(f.Id)).ToListAsync(ct);
        if (assets.Count != request.AttachmentIds.Length || assets.Any(f => f.OwnerSub != sub
            || f.HoldId != submission.Quest.HoldId)) return BadRequest(new { error = "invalid_attachment" });
        var feedback = new SubmissionFeedback
        {
            Id = Guid.NewGuid(), RevisionId = revision.Id, TeacherSub = sub, FileAssetId = request.FileAssetId,
            Text = request.Text, Decision = request.Decision, CreatedAtUtc = DateTimeOffset.UtcNow
        };
        foreach (var asset in assets) feedback.Attachments.Add(new() { FileAssetId = asset.Id });
        db.SubmissionFeedback.Add(feedback);
        if (request.Decision is not null) submission.State = request.Decision;
        submission.Version++;
        submission.UpdatedAtUtc = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "submission_changed" }); }
        db.ChangeTracker.Clear();
        return await Get(id, ct);
    }

    private IQueryable<QuestSubmission> Query() => db.QuestSubmissions.Include(s => s.Quest)
        .Include(s => s.DraftFiles).ThenInclude(f => f.FileAsset)
        .Include(s => s.Revisions).ThenInclude(r => r.Files).ThenInclude(f => f.FileAsset)
        .Include(s => s.Revisions).ThenInclude(r => r.Participants)
        .Include(s => s.Revisions).ThenInclude(r => r.Feedback).ThenInclude(f => f.Attachments).ThenInclude(a => a.FileAsset)
        .AsSplitQuery();

    private Task<bool> IsMember(Guid holdId, Guid sub, CancellationToken ct) =>
        db.Memberships.AnyAsync(m => m.HoldId == holdId && m.UserSub == sub, ct);

    private async Task<bool> CanEdit(QuestSubmission s, Guid sub, CancellationToken ct)
    {
        if (UserClaims.IsTeacher(User) || !s.Quest.Published || s.State is not ("draft" or "changes-requested")
            || !await IsMember(s.Quest.HoldId, sub, ct)) return false;
        if (s.GroupId is Guid groupId)
        {
            if (!await db.LearningGroupMembers.AnyAsync(m => m.GroupId == groupId && m.Membership.UserSub == sub, ct))
                return false;
            // Adding a member never grants the previous submission as a prefilled draft.
            var latest = s.Revisions.OrderByDescending(r => r.Number).FirstOrDefault();
            if (latest is null) return true;
            var memberIds = await db.Memberships.Where(m => m.HoldId == s.Quest.HoldId && m.UserSub == sub)
                .Select(m => m.Id).ToListAsync(ct);
            return latest.Participants.Any(p => p.UserSub == sub || p.UserSub == null && memberIds.Contains(p.MembershipId));
        }
        return s.CreatedBySub == sub;
    }

    private static bool IsParticipant(QuestSubmission s, Guid sub, List<Guid> memberIds) =>
        s.Revisions.Any(r => r.Participants.Any(p => p.UserSub == sub || p.UserSub == null && memberIds.Contains(p.MembershipId)));

    private static SubmissionFileView FileView(FileAsset f) => new(f.Id, f.OriginalName, f.ContentType, f.SizeBytes);

    private static SubmissionView View(QuestSubmission s, Guid sub, List<Guid> memberIds, bool edit, bool manage) =>
        new(s.Id, s.QuestId, s.GroupId, s.State, s.Version, edit || manage ? s.DraftText : "",
            edit || manage ? s.DraftFiles.Select(f => FileView(f.FileAsset)).ToArray() : [],
            s.Revisions.Where(r => manage || r.Participants.Any(p => p.UserSub == sub || p.UserSub == null && memberIds.Contains(p.MembershipId)))
                .OrderBy(r => r.Number).Select(r => new SubmissionRevisionView(r.Id, r.Number, r.Text, r.SubmittedBySub,
                    r.SubmittedAtUtc, r.Files.Select(f => FileView(f.FileAsset)).ToArray(),
                    r.Participants.Select(p => new SubmissionParticipantView(p.MembershipId, p.UserSub, p.DisplayName)).ToArray(),
                    r.Feedback.OrderBy(f => f.CreatedAtUtc).Select(f => new SubmissionFeedbackView(f.Id, f.TeacherSub,
                        f.FileAssetId, f.Text, f.Decision, f.CreatedAtUtc, f.Attachments.Select(a => FileView(a.FileAsset)).ToArray())).ToArray())).ToArray(),
            edit, manage && s.Revisions.Count != 0);
}
