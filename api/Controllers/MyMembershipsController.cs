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
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> List(CancellationToken ct)
    {
        var sub = UserClaims.GetSub(User);
        if (sub is null)
            return Unauthorized(new { error = "missing_sub" });

        var items = await db.Memberships
            .AsNoTracking()
            .Where(m => m.UserSub == sub.Value)
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
