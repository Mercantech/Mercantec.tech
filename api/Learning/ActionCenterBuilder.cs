using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Learning;

public sealed record ActionCenterHold(Guid Id, Guid WorkspaceId, string Name);

/// <summary>Small batch projections only: no answer text, submission bodies, files, or class rosters.</summary>
public sealed class ActionCenterBuilder(AppDbContext db)
{
    public async Task<ActionCenterSnapshot> BuildAsync(Guid target, ActionCenterHold[] scope, bool teacher,
        CancellationToken ct, DateTimeOffset? at = null, int version = 1, bool includeGifts = true, bool includeNews = true)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        var holds = scope.ToDictionary(h => h.Id);
        var holdIds = holds.Keys.ToArray();
        var items = new List<ActionCenterItem>();
        var extended = version == 2;
        if (extended && includeGifts)
        {
            var gifts = await db.ChristmasGifts.AsNoTracking().Where(g => g.RecipientSub == target && g.OpenedAtUtc == null)
                .Select(g => new { g.Id, g.SenderName }).ToArrayAsync(ct);
            foreach (var gift in gifts)
            {
                var sender = string.Concat(gift.SenderName.Where(c => !char.IsControl(c))).Trim();
                items.Add(new("gift", "optional", null, null, null, gift.Id, null,
                    sender.Length == 0 ? "Du har modtaget en gave" : $"Gave fra {sender}", "Personligt", null, State: "unopened"));
            }
        }
        if (extended && includeNews) items.AddRange(await new ActionCenterNews(db).BuildAsync(target, scope, teacher, ct));
        if (scope.Length == 0) return new(now, Ordered(items));
        if (teacher)
        {
            var pending = await db.QuestSubmissions.AsNoTracking()
                .Where(s => holdIds.Contains(s.Quest.HoldId) && s.State == "submitted" && s.Revisions.Any())
                .Select(s => new { s.QuestId, s.Quest.HoldId, s.Quest.Title, s.Quest.DeadlineUtc }).ToListAsync(ct);
            foreach (var group in pending.GroupBy(s => s.QuestId))
            {
                var quest = group.First(); var hold = holds[quest.HoldId];
                items.Add(new("review", "optional", hold.Id, hold.WorkspaceId, null, quest.QuestId, null,
                    quest.Title, hold.Name, quest.DeadlineUtc, group.Count(), "pending-review"));
            }
        }
        else
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen")).DateTime);
            var membershipIds = await db.Memberships.AsNoTracking().Where(m => holdIds.Contains(m.HoldId) && m.UserSub == target)
                .Select(m => m.Id).ToArrayAsync(ct);
            var assigned = new QuestAudienceAccess(db).AssignedTo(db.LearningQuests.AsNoTracking()
                .Where(q => holdIds.Contains(q.HoldId) && (extended || q.Required)), target);
            var available = await assigned.Select(q => new QuestRow(q.Id, q.HoldId, q.Title, q.DeadlineUtc, q.OccurrenceDate, q.Required, q.Kind)).ToListAsync(ct);
            var quests = available.Where(q => q.OccurrenceDate is null || q.OccurrenceDate <= today).ToArray();
            var questIds = quests.Select(q => q.Id).ToArray();
            var submissions = await db.QuestSubmissions.AsNoTracking().Where(s => questIds.Contains(s.QuestId)
                && s.Revisions.Any(r => r.Participants.Any(p => p.UserSub == target || p.UserSub == null && membershipIds.Contains(p.MembershipId))))
                .Select(s => new SubmissionRow(s.Id, s.QuestId, s.State, s.Revisions.Max(r => r.Number))).ToListAsync(ct);
            var revisions = await db.SubmissionRevisions.AsNoTracking().Where(r => questIds.Contains(r.Submission.QuestId)
                && r.Participants.Any(p => p.UserSub == target || p.UserSub == null && membershipIds.Contains(p.MembershipId)))
                .Select(r => new RevisionRow(r.Id, r.SubmissionId, r.Number, r.SubmittedAtUtc)).ToListAsync(ct);
            var revisionIds = revisions.Select(r => r.Id).ToArray();
            var decisions = await db.SubmissionFeedback.AsNoTracking().Where(f => revisionIds.Contains(f.RevisionId) && f.Decision != null)
                .Select(f => new DecisionRow(f.Id, f.RevisionId, f.Decision!, f.CreatedAtUtc)).ToListAsync(ct);
            var latestDecisions = decisions.GroupBy(f => f.RevisionId).ToDictionary(g => g.Key,
                g => g.OrderByDescending(f => f.CreatedAtUtc).ThenByDescending(f => f.Id).First().Decision);
            var ownRevisions = revisions.GroupBy(r => r.SubmissionId).ToDictionary(g => g.Key,
                g => g.OrderByDescending(r => r.Number).ThenByDescending(r => r.Id).First());
            var receipts = submissions.Where(s => ownRevisions.ContainsKey(s.Id)).GroupBy(s => s.QuestId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => ownRevisions[s.Id].SubmittedAtUtc).ThenByDescending(s => s.Id).First());
            foreach (var quest in quests)
            {
                var hold = holds[quest.HoldId];
                if (receipts.TryGetValue(quest.Id, out var receipt))
                {
                    // Later revisions from a changed group never replace this pupil's receipt or decision.
                    var revision = ownRevisions[receipt.Id];
                    var state = revision.Number == receipt.LatestNumber ? receipt.State : latestDecisions.GetValueOrDefault(revision.Id, "submitted");
                    if (state == "changes-requested")
                        items.Add(new(quest.Required ? "submission" : "quest", quest.Required ? "important" : "optional", hold.Id, hold.WorkspaceId, null, quest.Id, receipt.Id,
                            quest.Title, hold.Name, quest.DeadlineUtc, State: "changes-requested"));
                    continue;
                }
                if (!quest.Required)
                {
                    // An expired voluntary opportunity is not an outstanding obligation.
                    if (quest.DeadlineUtc < now) continue;
                    items.Add(new("quest", "optional", hold.Id, hold.WorkspaceId, null, quest.Id, null,
                        quest.Title, hold.Name, quest.DeadlineUtc, State: quest.Kind is "daily" or "side" ? quest.Kind : "optional"));
                    continue;
                }
                var overdue = quest.DeadlineUtc < now;
                items.Add(new("submission", overdue ? "important" : "optional", hold.Id, hold.WorkspaceId, null, quest.Id, null,
                    quest.Title, hold.Name, quest.DeadlineUtc, State: overdue ? "overdue" : "missing"));
            }
            var polls = await db.PollRounds.AsNoTracking().Where(p => holdIds.Contains(p.HoldId) && p.State == "open"
                && db.Memberships.Any(m => m.HoldId == p.HoldId && m.UserSub == target)
                && !p.Responses.Any(r => r.UserSub == target))
                .Select(p => new { p.Id, p.HoldId, p.Question }).ToListAsync(ct);
            foreach (var poll in polls)
            {
                var hold = holds[poll.HoldId];
                items.Add(new("poll", "optional", hold.Id, hold.WorkspaceId, null, poll.Id, null,
                    poll.Question, hold.Name, null, State: "unanswered"));
            }
        }
        var resources = items.Where(i => i.Kind is "submission" or "review" or "quest" or "feedback").Select(i => i.Kind == "feedback" ? i.TargetId!.Value : i.ResourceId).Distinct().ToArray();
        var courses = await db.TeachingCourseResources.AsNoTracking().Where(r => r.Kind == "quest" && resources.Contains(r.ResourceId)
            && holdIds.Contains(r.Course.HoldId) && !r.Course.Archived && (teacher || r.Course.Published))
            .Select(r => new { r.ResourceId, r.CourseId, r.Course.HoldId }).ToArrayAsync(ct);
        var linked = courses.GroupBy(r => (r.HoldId, r.ResourceId)).ToDictionary(g => g.Key, g => g.OrderBy(r => r.CourseId).First().CourseId);
        return new(now, Ordered(items.Select(item => item.HoldId is Guid holdId && linked.TryGetValue((holdId, item.Kind == "feedback" ? item.TargetId!.Value : item.ResourceId), out var courseId) ? item with { CourseId = courseId } : item)));
    }

    private static ActionCenterItem[] Ordered(IEnumerable<ActionCenterItem> items) => items
        .OrderBy(item => item.Severity == "important" ? 0 : 1).ThenBy(item => item.DeadlineUtc ?? DateTimeOffset.MaxValue)
        .ThenBy(item => item.HoldName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Kind, StringComparer.Ordinal)
        .ThenBy(item => item.ResourceId).ToArray();

    private sealed record QuestRow(Guid Id, Guid HoldId, string Title, DateTimeOffset? DeadlineUtc, DateOnly? OccurrenceDate, bool Required, string Kind);
    private sealed record SubmissionRow(Guid Id, Guid QuestId, string State, int LatestNumber);
    private sealed record RevisionRow(Guid Id, Guid SubmissionId, int Number, DateTimeOffset SubmittedAtUtc);
    private sealed record DecisionRow(Guid Id, Guid RevisionId, string Decision, DateTimeOffset CreatedAtUtc);
}
