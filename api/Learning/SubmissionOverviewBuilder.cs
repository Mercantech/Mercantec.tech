using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Learning;

/// <summary>Counts pupils, not submission records. Submitted participant snapshots remain authoritative.</summary>
public sealed class SubmissionOverviewBuilder(AppDbContext db)
{
    public async Task<HoldSubmissionOverview> BuildAsync(Guid holdId, Guid? courseId, CancellationToken ct,
        DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen")).DateTime);
        var owner = await db.Hold.Where(h => h.Id == holdId).Select(h => h.Forloeb.CreatedBySub).SingleAsync(ct);
        var allMembers = await db.Memberships.AsNoTracking().Where(m => m.HoldId == holdId).ToListAsync(ct);
        var members = allMembers.Where(m => m.UserSub == null || m.UserSub != owner).ToList();
        var holdMemberIds = allMembers.Select(m => m.Id).ToHashSet();
        var query = db.LearningQuests.AsNoTracking().Where(q => q.HoldId == holdId);
        if (courseId is Guid course)
            query = query.Where(q => db.TeachingCourseResources.Any(r => r.CourseId == course
                && r.Kind == "quest" && r.ResourceId == q.Id));
        var quests = await query.ToListAsync(ct);
        var groups = await db.LearningGroups.AsNoTracking().Where(g => g.HoldId == holdId)
            .Include(g => g.Members).ToListAsync(ct);
        var sets = await db.LearningGroupSets.AsNoTracking().Where(s => s.HoldId == holdId).ToListAsync(ct);
        var activeSetIds = sets.Where(s => !s.Archived).Select(s => s.Id).ToHashSet();
        var questIds = quests.Select(q => q.Id).ToArray();
        var audienceMembers = await db.QuestAudienceMemberships.AsNoTracking().Where(a => questIds.Contains(a.QuestId))
            .Select(a => new { a.QuestId, a.MembershipId }).ToArrayAsync(ct);
        var audienceGroups = await db.QuestAudienceGroups.AsNoTracking().Where(a => questIds.Contains(a.QuestId))
            .Select(a => new { a.QuestId, a.GroupId }).ToArrayAsync(ct);
        var submissions = await db.QuestSubmissions.AsNoTracking().Where(s => s.Quest.HoldId == holdId)
            .Include(s => s.Revisions).ThenInclude(r => r.Participants)
            .Include(s => s.Revisions).ThenInclude(r => r.Feedback).AsSplitQuery().ToListAsync(ct);
        var views = new List<QuestSubmissionOverview>();
        foreach (var quest in quests.OrderBy(q => q.DeadlineUtc ?? DateTimeOffset.MaxValue).ThenBy(q => q.Title))
        {
            var targetGroupIds = audienceGroups.Where(a => a.QuestId == quest.Id).Select(a => a.GroupId).ToHashSet();
            var expectedIds = (quest.AudienceMode switch
            {
                "hold" => holdMemberIds,
                "students" => audienceMembers.Where(a => a.QuestId == quest.Id).Select(a => a.MembershipId),
                "groups" => groups.Where(g => targetGroupIds.Contains(g.Id)).SelectMany(g => g.Members.Select(m => m.MembershipId)),
                _ => Enumerable.Empty<Guid>()
            }).Where(holdMemberIds.Contains).ToHashSet();
            // A membership alone does not make a group usable: the complete group must be in the
            // audience, its set must be active, and a groups audience names the exact allowed groups.
            var eligibleGroupIds = groups.Where(g => (quest.GroupSetId == null || g.GroupSetId == quest.GroupSetId)
                && (g.GroupSetId is not Guid setId || activeSetIds.Contains(setId))
                && g.Members.Count > 0 && g.Members.All(m => expectedIds.Contains(m.MembershipId))
                && (quest.AudienceMode != "groups" || targetGroupIds.Contains(g.Id)))
                .Select(g => g.Id).ToHashSet();
            var questSubmissions = submissions.Where(s => s.QuestId == quest.Id).ToArray();
            var rows = quest.Published ? members.Where(m => expectedIds.Contains(m.Id)).OrderBy(m => m.DisplayName).ThenBy(m => m.Id)
                .Select(member => Row(quest, member, groups, eligibleGroupIds, questSubmissions, now)).ToArray() : [];
            var delivered = rows.Count(r => r.SubmittedAtUtc != null);
            var progress = new SubmissionProgress(rows.Length, delivered, rows.Length - delivered,
                rows.Count(r => r.Overdue), rows.Count(r => r.State == "submitted" && r.IsCurrentRevision),
                rows.Count(r => r.State == "approved"), rows.Count(r => r.State == "changes-requested"),
                rows.Count(r => r.State == "draft"), rows.Count(r => r.Ungrouped));
            views.Add(new(quest.Id, quest.Title, quest.Kind, quest.SubmissionMode, quest.Published, quest.Required,
                quest.DeadlineUtc, quest.GroupSetId, sets.FirstOrDefault(s => s.Id == quest.GroupSetId)?.Name, progress, rows,
                questSubmissions.Count(s => s.State == "submitted" && s.Revisions.Count > 0), quest.OccurrenceDate > today, quest.OccurrenceDate));
        }
        return new(holdId, now, SubmissionProgress.Sum(views.Where(q => q.Published && q.Required && !q.Upcoming).Select(q => q.Progress)), views.ToArray());
    }

    private static MemberSubmissionStatus Row(LearningQuest quest, Membership member, List<LearningGroup> groups,
        HashSet<Guid> eligibleGroupIds, IEnumerable<QuestSubmission> submissions, DateTimeOffset now)
    {
        // Existing drafts remain visible to the teacher even if their current group is no longer
        // eligible for a new delivery. Submitted participant snapshots remain independent of both.
        var memberGroups = groups.Where(g => g.Members.Any(m => m.MembershipId == member.Id)).ToArray();
        var candidates = submissions.Select(s => new
        {
            Submission = s,
            Revision = s.Revisions.Where(r => r.Participants.Any(p => p.MembershipId == member.Id))
                .OrderByDescending(r => r.Number).FirstOrDefault()
        }).Where(c => c.Revision != null || c.Submission.Revisions.Count == 0
            && (c.Submission.GroupId is Guid gid ? memberGroups.Any(g => g.Id == gid)
                : member.UserSub != null && c.Submission.CreatedBySub == member.UserSub)).ToArray();
        // A newly opened draft must never replace evidence that the pupil has already submitted.
        var chosen = candidates.OrderByDescending(c => c.Revision != null)
            .ThenByDescending(c => c.Revision?.SubmittedAtUtc ?? c.Submission.UpdatedAtUtc)
            .ThenByDescending(c => c.Submission.Id).FirstOrDefault();
        var submission = chosen?.Submission;
        var revision = chosen?.Revision;
        var firstDelivery = candidates.SelectMany(c => c.Submission.Revisions)
            .Where(r => r.Participants.Any(p => p.MembershipId == member.Id)).Select(r => (DateTimeOffset?)r.SubmittedAtUtc).Min();
        var current = revision != null && revision.Number == submission!.Revisions.Max(r => r.Number);
        var state = submission is null ? "missing" : revision is null ? "draft" : current ? submission.State
            : revision.Feedback.Where(f => f.Decision != null).OrderByDescending(f => f.CreatedAtUtc)
                .FirstOrDefault()?.Decision ?? "submitted";
        var ungrouped = quest.SubmissionMode == "group" && revision is null
            && !memberGroups.Any(g => eligibleGroupIds.Contains(g.Id));
        return new(member.Id, member.DisplayName ?? "Elev", state,
            quest.Required && revision is null && quest.DeadlineUtc < now,
            firstDelivery > quest.DeadlineUtc, ungrouped,
            submission?.Id, submission?.GroupId, groups.FirstOrDefault(g => g.Id == submission?.GroupId)?.Name,
            revision?.SubmittedAtUtc, revision?.Number, current);
    }
}
