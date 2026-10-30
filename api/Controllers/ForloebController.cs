using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Route("api/forloeb")]
[Authorize]
public class ForloebController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> List(CancellationToken ct)
    {
        var items = await db.Forloeb
            .AsNoTracking()
            .Include(f => f.Hold)
            .OrderBy(f => f.Name)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.Description,
                f.CreatedBySub,
                f.CreatedAtUtc,
                holdId = f.Hold != null ? f.Hold.Id : (Guid?)null,
                holdName = f.Hold != null ? f.Hold.Name : null
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id, CancellationToken ct)
    {
        var f = await db.Forloeb
            .AsNoTracking()
            .Include(x => x.Hold)!
            .ThenInclude(h => h!.Memberships)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (f is null)
            return NotFound();

        return Ok(new
        {
            f.Id,
            f.Name,
            f.Description,
            f.CreatedBySub,
            f.CreatedAtUtc,
            hold = f.Hold is null ? null : new
            {
                f.Hold.Id,
                f.Hold.Name,
                f.Hold.CreatedAtUtc,
                memberCount = f.Hold.Memberships.Count
            }
        });
    }

    public sealed record CreateForloebRequest(string Name, string? Description);

    /// <summary>Opret forløb + automatisk 1:1 hold. Underviser/Admin.</summary>
    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateForloebRequest body, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        var sub = UserClaims.GetSub(User);
        if (sub is null)
            return Unauthorized(new { error = "missing_sub" });

        var name = (body.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return BadRequest(new { error = "invalid_name" });

        var now = DateTimeOffset.UtcNow;
        var forloeb = new Forloeb
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim(),
            CreatedBySub = sub.Value,
            CreatedAtUtc = now
        };
        var hold = new Hold
        {
            Id = Guid.NewGuid(),
            ForloebId = forloeb.Id,
            Name = name,
            CreatedAtUtc = now
        };

        db.Forloeb.Add(forloeb);
        db.Hold.Add(hold);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = forloeb.Id }, new
        {
            forloeb.Id,
            forloeb.Name,
            forloeb.Description,
            forloeb.CreatedBySub,
            forloeb.CreatedAtUtc,
            holdId = hold.Id,
            holdName = hold.Name
        });
    }
}
