using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Route("api/me/memberships")]
[Authorize]
public class MyMembershipsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Egne hold-medlemskaber. Undervisere kan angive <paramref name="asUser"/>
    /// for at se en elevs medlemskaber (fx under «se som elev»).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> List([FromQuery] Guid? asUser, CancellationToken ct)
    {
        var sub = UserClaims.GetSub(User);
        if (sub is null)
            return Unauthorized(new { error = "missing_sub" });

        var target = sub.Value;
        if (asUser is Guid requested && requested != Guid.Empty && requested != sub.Value)
        {
            if (!UserClaims.IsTeacher(User))
                return Forbid();
            target = requested;
        }

        var items = await db.Memberships
            .AsNoTracking()
            .Where(m => m.UserSub == target)
            .Include(m => m.Hold)
            .ThenInclude(h => h.Forloeb)
            .OrderBy(m => m.Hold.Name)
            .Select(m => new
            {
                m.Id,
                m.HoldId,
                holdName = m.Hold.Name,
                forloebId = m.Hold.ForloebId,
                forloebName = m.Hold.Forloeb.Name,
                m.Source,
                m.CreatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(items);
    }
}
