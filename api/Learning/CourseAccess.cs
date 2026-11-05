using System.Security.Claims;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Learning;

public sealed class CourseAccess(AppDbContext db)
{
    public IQueryable<Hold> AccessibleHolds(ClaimsPrincipal user, bool manageOnly = false)
    {
        if (UserClaims.GetSub(user) is not Guid sub) return db.Hold.Where(h => false);
        var teacher = UserClaims.IsTeacher(user);
        var admin = user.Claims.Any(c => (c.Type is "role" or "roles" || c.Type == ClaimTypes.Role)
            && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase));
        return db.Hold.Where(h => teacher && (admin || h.Forloeb.CreatedBySub == sub
            || h.Memberships.Any(m => m.UserSub == sub))
            || !manageOnly && h.Memberships.Any(m => m.UserSub == sub));
    }

    public Task<bool> CanManageAsync(Guid holdId, ClaimsPrincipal user, CancellationToken ct) =>
        AccessibleHolds(user, true).AnyAsync(h => h.Id == holdId, ct);

    public Task<bool> CanAccessAsync(Guid holdId, ClaimsPrincipal user, CancellationToken ct) =>
        AccessibleHolds(user).AnyAsync(h => h.Id == holdId, ct);
}
