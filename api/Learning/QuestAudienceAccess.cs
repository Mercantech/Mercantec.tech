using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Learning;

/// <summary>One audience rule used by quest reads, assignment starts, and the teacher's expected roster.</summary>
public sealed class QuestAudienceAccess(AppDbContext db)
{
    /// <summary>Current assignments only; historical read access is not a new obligation.</summary>
    public IQueryable<LearningQuest> AssignedTo(IQueryable<LearningQuest> quests, Guid sub) => quests.Where(q => q.Published
        && db.Memberships.Any(m => m.HoldId == q.HoldId && m.UserSub == sub)
        && (q.AudienceMode == "hold"
            || q.AudienceMode == "students" && db.QuestAudienceMemberships.Any(a => a.QuestId == q.Id && a.Membership.UserSub == sub && a.Membership.HoldId == q.HoldId)
            || q.AudienceMode == "groups" && db.QuestAudienceGroups.Any(a => a.QuestId == q.Id && a.Group.HoldId == q.HoldId && a.Group.Members.Any(m => m.Membership.UserSub == sub && m.Membership.HoldId == q.HoldId))));

    public IQueryable<LearningQuest> Readable(IQueryable<LearningQuest> quests, Guid sub) => quests.Where(q => q.Published
        && db.Memberships.Any(m => m.HoldId == q.HoldId && m.UserSub == sub)
        && (q.AudienceMode == "hold"
            || q.AudienceMode == "students" && db.QuestAudienceMemberships.Any(a => a.QuestId == q.Id && a.Membership.UserSub == sub && a.Membership.HoldId == q.HoldId)
            || q.AudienceMode == "groups" && db.QuestAudienceGroups.Any(a => a.QuestId == q.Id && a.Group.HoldId == q.HoldId && a.Group.Members.Any(m => m.Membership.UserSub == sub && m.Membership.HoldId == q.HoldId))
            || db.SubmissionParticipants.Any(p => p.Revision.Submission.QuestId == q.Id
                && (p.UserSub == sub || p.UserSub == null && p.MembershipId != Guid.Empty
                    && db.Memberships.Any(m => m.Id == p.MembershipId && m.HoldId == q.HoldId && m.UserSub == sub)))));

    public Task<bool> CanReadAsync(LearningQuest quest, Guid sub, CancellationToken ct) =>
        Readable(db.LearningQuests.Where(q => q.Id == quest.Id && q.HoldId == quest.HoldId), sub).AnyAsync(ct);

    public Task<Membership[]> ExpectedMembersAsync(LearningQuest quest, CancellationToken ct)
    {
        var members = db.Memberships.AsNoTracking().Where(m => m.HoldId == quest.HoldId);
        if (quest.AudienceMode == "students")
            members = members.Where(m => db.QuestAudienceMemberships.Any(a => a.QuestId == quest.Id && a.MembershipId == m.Id));
        else if (quest.AudienceMode == "groups")
            members = members.Where(m => db.QuestAudienceGroups.Any(a => a.QuestId == quest.Id && a.Group.HoldId == quest.HoldId && a.Group.Members.Any(g => g.MembershipId == m.Id)));
        else if (quest.AudienceMode != "hold") members = members.Where(m => false);
        return members.OrderBy(m => m.DisplayName).ToArrayAsync(ct);
    }

    public async Task<bool> CanParticipateAsync(LearningQuest quest, Guid sub, Guid? groupId, CancellationToken ct)
    {
        if (!quest.Published) return false;
        var targets = await ExpectedMembersAsync(quest, ct);
        if (!targets.Any(m => m.UserSub == sub)) return false;
        if (groupId is not Guid id) return quest.SubmissionMode != "group";
        if (quest.SubmissionMode == "individual") return false;
        var group = await db.LearningGroups.AsNoTracking().Include(g => g.Members).ThenInclude(m => m.Membership)
            .SingleOrDefaultAsync(g => g.Id == id && g.HoldId == quest.HoldId, ct);
        if (group is null || group.Members.Count == 0 || !group.Members.Any(m => m.Membership.UserSub == sub)
            || quest.GroupSetId is Guid set && group.GroupSetId != set) return false;
        if (quest.AudienceMode == "groups" && !await db.QuestAudienceGroups.AnyAsync(a => a.QuestId == quest.Id && a.GroupId == id, ct)) return false;
        var targetIds = targets.Select(m => m.Id).ToHashSet();
        return group.Members.All(m => m.Membership.HoldId == quest.HoldId && targetIds.Contains(m.MembershipId));
    }
}
