using System.Security.Claims;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Learning;

public sealed class FileAuthorization(AppDbContext db)
{
    public async Task<bool> CanReadAsync(FileAsset file, ClaimsPrincipal user, CancellationToken ct)
    {
        if (UserClaims.GetSub(user) is not Guid sub) return false;
        var access = new CourseAccess(db);
        if (file.HoldId is Guid holdId)
        {
            if (!await access.CanAccessAsync(holdId, user, ct)) return false;
            if (await access.CanManageAsync(holdId, user, ct)) return true;
        }
        if (file.OwnerSub == sub) return true;
        var managedHolds = access.AccessibleHolds(user, true).Select(h => h.Id);
        if (await db.CourseMaterials.AnyAsync(m => m.FileAssetId == file.Id
            && (managedHolds.Contains(m.HoldId) || m.Published
                && db.Memberships.Any(x => x.HoldId == m.HoldId && x.UserSub == sub)), ct)) return true;
        // Current group members can share draft files, but historical files follow the submitted participants.
        var sharedDrafts = await db.QuestSubmissions.AsNoTracking().Where(s => (s.State == "draft" || s.State == "changes-requested") && s.DraftFiles.Any(f => f.FileAssetId == file.Id)
            && (s.Revisions.Count == 0 || s.Revisions.OrderByDescending(r => r.Number).Take(1)
                .Any(r => r.Participants.Any(p => p.UserSub == sub || p.UserSub == null
                    && db.Memberships.Any(m => m.Id == p.MembershipId && m.UserSub == sub))))
            && s.GroupId != null && db.LearningGroupMembers.Any(g => g.GroupId == s.GroupId
                && g.Membership.UserSub == sub && g.Membership.HoldId == s.Quest.HoldId))
            .Select(s => new { s.Quest, s.GroupId }).ToArrayAsync(ct);
        var audience = new QuestAudienceAccess(db);
        foreach (var draft in sharedDrafts)
            // Group membership must not bypass the audience check that protects the draft itself.
            if (await audience.CanParticipateAsync(draft.Quest, sub, draft.GroupId, ct)) return true;
        return await db.SubmissionRevisions.AnyAsync(r =>
            (r.Files.Any(f => f.FileAssetId == file.Id)
             || r.Feedback.Any(f => f.Attachments.Any(a => a.FileAssetId == file.Id)))
            && r.Participants.Any(p => p.UserSub == sub
                || p.UserSub == null && db.Memberships.Any(m => m.Id == p.MembershipId && m.UserSub == sub))
            && db.Memberships.Any(m => m.HoldId == r.Submission.Quest.HoldId && m.UserSub == sub), ct);
    }
}
