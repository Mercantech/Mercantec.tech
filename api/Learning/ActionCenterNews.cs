using System.Runtime.CompilerServices;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Learning;

/// <summary>Personal unread events with source access rechecked on every read and acknowledgment.</summary>
public sealed class ActionCenterNews(AppDbContext db)
{
    public static bool CanAcknowledge(string? kind) => kind is "feedback" or "notebook-comment" or "notebook-share";
    public async Task<ActionCenterItem[]> BuildAsync(Guid target, ActionCenterHold[] scope, bool teacher, CancellationToken ct)
    {
        var items = new List<ActionCenterItem>();
        var holds = scope.ToDictionary(h => h.Id); var holdIds = holds.Keys.ToArray();
        if (holdIds.Length > 0)
        {
            var memberIds = db.Memberships.Where(m => m.UserSub == target && holdIds.Contains(m.HoldId)).Select(m => m.Id);
            var readable = new QuestAudienceAccess(db).Readable(db.LearningQuests.Where(q => holdIds.Contains(q.HoldId)), target).Select(q => q.Id);
            var feedback = await db.SubmissionFeedback.AsNoTracking().Where(f => f.TeacherSub != target
                && f.Decision != "changes-requested" && readable.Contains(f.Revision.Submission.QuestId)
                && f.Revision.Participants.Any(p => p.UserSub == target || p.UserSub == null && memberIds.Contains(p.MembershipId))
                && !db.ActionReadReceipts.Any(r => r.UserSub == target && r.Kind == "feedback" && r.ResourceId == f.Id))
                .Select(f => new { f.Id, f.Revision.SubmissionId, f.Revision.Submission.QuestId,
                    f.Revision.Submission.Quest.HoldId, f.Revision.Submission.Quest.Title }).ToArrayAsync(ct);
            foreach (var row in feedback)
            {
                var hold = holds[row.HoldId];
                items.Add(new("feedback", "optional", hold.Id, hold.WorkspaceId, null, row.Id, row.SubmissionId,
                    row.Title, hold.Name, null, State: "unread", TargetId: row.QuestId));
            }
        }
        // Global teacher read/comment privileges are deliberately not an inbox subscription.
        var comments = await (from comment in db.NotebookComments.AsNoTracking()
            join page in db.NotebookDocuments on comment.PageId equals page.Id
            join notebook in db.Notebooks on page.NotebookId equals (Guid?)notebook.Id
            where comment.AuthorSub != target && (notebook.OwnerSub == target || teacher && db.NotebookShares.Any(s =>
                s.NotebookId == notebook.Id && s.RecipientSub == target && (s.PageId == null || s.PageId == page.Id)))
                && !db.ActionReadReceipts.Any(r => r.UserSub == target && r.Kind == "notebook-comment" && r.ResourceId == comment.Id)
            select new { comment.Id, PageId = page.Id, page.Title }).ToArrayAsync(ct);
        foreach (var comment in comments)
            items.Add(new("notebook-comment", "optional", null, null, null, comment.Id, null,
                comment.Title, "Personligt", null, State: "unread", TargetId: comment.PageId));
        var shares = await (from share in db.NotebookShares.AsNoTracking()
            join notebook in db.Notebooks on share.NotebookId equals notebook.Id
            join page in db.NotebookDocuments on share.PageId equals (Guid?)page.Id into pages
            from page in pages.DefaultIfEmpty()
            where share.RecipientSub == target && notebook.OwnerSub != target
                && (share.PageId == null || page != null && page.NotebookId == notebook.Id)
                && !db.ActionReadReceipts.Any(r => r.UserSub == target && r.Kind == "notebook-share" && r.ResourceId == share.Id)
            select new { share.Id, share.NotebookId, share.PageId, Title = page == null ? null : page.Title }).ToArrayAsync(ct);
        foreach (var share in shares)
            items.Add(new("notebook-share", "optional", null, null, null, share.Id, null,
                share.Title ?? "En notesbog er delt med dig", "Personligt", null,
                State: share.PageId.HasValue ? "shared-page" : "shared-notebook", TargetId: share.PageId ?? share.NotebookId));
        return items.ToArray();
    }

    public async Task AcknowledgeAsync(Guid target, ActionCenterReadItem[] requested, ActionCenterHold[] scope, bool teacher, CancellationToken ct)
    {
        var wanted = requested.Where(i => i is not null && CanAcknowledge(i.Kind) && i.ResourceId != Guid.Empty)
            .Select(i => (i.Kind, i.ResourceId)).ToHashSet();
        if (wanted.Count == 0) return;
        var allowed = (await BuildAsync(target, scope, teacher, ct)).Where(i => wanted.Contains((i.Kind, i.ResourceId))).ToArray();
        if (allowed.Length == 0) return;
        var readAt = DateTimeOffset.UtcNow;
        var values = new List<object>(); var rows = new List<string>();
        foreach (var item in allowed)
        {
            var n = values.Count; values.AddRange([target, item.Kind, item.ResourceId, readAt]);
            rows.Add($"({{{n}}}, {{{n + 1}}}, {{{n + 2}}}, {{{n + 3}}})");
        }
        // PostgreSQL and SQLite both support this atomic, idempotent batch insert. No source or
        // user value is interpolated into SQL; EF binds every value as a parameter.
        var command = FormattableStringFactory.Create("INSERT INTO action_read_receipts (\"UserSub\", \"Kind\", \"ResourceId\", \"ReadAtUtc\") VALUES "
            + string.Join(", ", rows) + " ON CONFLICT (\"UserSub\", \"Kind\", \"ResourceId\") DO NOTHING", values.ToArray());
        await db.Database.ExecuteSqlInterpolatedAsync(command, ct);
    }
}
