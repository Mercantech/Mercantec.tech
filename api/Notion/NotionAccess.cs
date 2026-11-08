using System.Security.Claims;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Notion;

/// <summary>Shared access policy for teaching metadata, bodies, graphs, collaboration and private media.</summary>
public sealed class NotionAccess(AppDbContext db, CourseAccess courses)
{
    public IQueryable<NotionPage> ReadablePages(ClaimsPrincipal user, bool publishedOnly = false)
    {
        if (UserClaims.GetSub(user) is not Guid) return db.NotionPages.Where(p => false);
        var readable = courses.AccessibleHolds(user).Select(h => h.Id);
        var manageable = courses.AccessibleHolds(user, true).Select(h => h.Id);
        var teacher = UserClaims.IsTeacher(user);
        return db.NotionPages.Where(p => (p.HoldId == null || readable.Contains(p.HoldId.Value))
            && (!publishedOnly && (p.HoldId == null && teacher || p.HoldId != null && manageable.Contains(p.HoldId.Value))
                || p.PublishedAtUtc != null && p.PublishedState != null && p.PublishedTitle != null));
    }

    public Task<bool> CanReadAsync(Guid pageId, ClaimsPrincipal user, CancellationToken ct, bool publishedOnly = false) =>
        ReadablePages(user, publishedOnly).AnyAsync(p => p.Id == pageId, ct);

    public async Task<bool> CanManageAsync(Guid pageId, ClaimsPrincipal user, CancellationToken ct)
    {
        var scope = await db.NotionPages.AsNoTracking().Where(p => p.Id == pageId)
            .Select(p => new { p.HoldId }).SingleOrDefaultAsync(ct);
        return scope is not null && await CanManageScopeAsync(scope.HoldId, user, ct);
    }

    public Task<bool> CanManageAsync(NotionPage page, ClaimsPrincipal user, CancellationToken ct) =>
        CanManageScopeAsync(page.HoldId, user, ct);

    public Task<bool> CanManageScopeAsync(Guid? holdId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (UserClaims.GetSub(user) is not Guid) return Task.FromResult(false);
        return holdId is Guid hold ? courses.CanManageAsync(hold, user, ct) : Task.FromResult(UserClaims.IsTeacher(user));
    }

    public Task<bool> CanAccessScopeAsync(Guid? holdId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (UserClaims.GetSub(user) is not Guid) return Task.FromResult(false);
        return holdId is Guid hold ? courses.CanAccessAsync(hold, user, ct) : Task.FromResult(true);
    }
}
