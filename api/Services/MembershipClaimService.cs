using Mercantec.Tech.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Services;

/// <summary>Kobler shadow-medlemskaber til Auth-sub når Lectio-id claims.</summary>
public class MembershipClaimService(AppDbContext db)
{
    public async Task<int> ClaimByLectioIdAsync(Guid userSub, string lectioId, CancellationToken ct = default)
    {
        lectioId = lectioId.Trim();
        if (string.IsNullOrWhiteSpace(lectioId))
            return 0;

        var shadows = await db.Memberships
            .Where(m => m.LectioId == lectioId && m.UserSub == null)
            .ToListAsync(ct);

        var claimed = 0;
        foreach (var m in shadows)
        {
            // Undgå unikheds-konflikt hvis brugeren allerede er claimed på samme hold
            var already = await db.Memberships.AnyAsync(
                x => x.HoldId == m.HoldId && x.UserSub == userSub && x.Id != m.Id, ct);
            if (already)
            {
                db.Memberships.Remove(m);
                continue;
            }

            m.UserSub = userSub;
            claimed++;
        }

        if (shadows.Count > 0)
            await db.SaveChangesAsync(ct);

        return claimed;
    }
}
