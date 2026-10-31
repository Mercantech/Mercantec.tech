using System.Net.Http.Headers;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.Lectio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Route("api/forloeb")]
[Authorize]
public class ForloebController(AppDbContext db, LectioCollectorClient lectio) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> List(CancellationToken ct)
    {
        var items = await db.Forloeb
            .AsNoTracking()
            .Include(f => f.Hold)!
            .ThenInclude(h => h!.Memberships)
            .OrderBy(f => f.Name)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.Description,
                f.CreatedBySub,
                f.CreatedAtUtc,
                holdId = f.Hold != null ? f.Hold.Id : (Guid?)null,
                holdName = f.Hold != null ? f.Hold.Name : null,
                lectioHoldKey = f.Hold != null ? f.Hold.LectioHoldKey : null,
                memberCount = f.Hold != null ? f.Hold.Memberships.Count : 0,
                shadowCount = f.Hold != null ? f.Hold.Memberships.Count(m => m.UserSub == null) : 0
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

        return Ok(ToDto(f));
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

        var created = await db.Forloeb
            .AsNoTracking()
            .Include(x => x.Hold)!
            .ThenInclude(h => h!.Memberships)
            .FirstAsync(x => x.Id == forloeb.Id, ct);

        return CreatedAtAction(nameof(Get), new { id = forloeb.Id }, ToDto(created));
    }

    public sealed record UpdateForloebRequest(string Name, string? Description);

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<object>> Update(Guid id, [FromBody] UpdateForloebRequest body, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        var forloeb = await db.Forloeb.Include(f => f.Hold).FirstOrDefaultAsync(f => f.Id == id, ct);
        if (forloeb is null)
            return NotFound();

        var name = (body.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return BadRequest(new { error = "invalid_name" });

        forloeb.Name = name;
        forloeb.Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim();
        if (forloeb.Hold is not null && string.IsNullOrWhiteSpace(forloeb.Hold.LectioHoldKey))
            forloeb.Hold.Name = name;

        await db.SaveChangesAsync(ct);

        var updated = await db.Forloeb
            .AsNoTracking()
            .Include(x => x.Hold)!
            .ThenInclude(h => h!.Memberships)
            .FirstAsync(x => x.Id == id, ct);

        return Ok(ToDto(updated));
    }

    public sealed record FromLectioRequest(string LectioHoldKey, string? Name, string? Description);

    /// <summary>
    /// Opret forløb + hold fra Lectio-Collector-hold.
    /// Elever importeres; claimed Auth→Lectio bliver rigtige medlemmer, resten shadow.
    /// </summary>
    [HttpPost("from-lectio")]
    public async Task<ActionResult<object>> CreateFromLectio([FromBody] FromLectioRequest body, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        var sub = UserClaims.GetSub(User);
        if (sub is null)
            return Unauthorized(new { error = "missing_sub" });

        var key = (body.LectioHoldKey ?? "").Trim();
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { error = "missing_lectio_hold_key" });

        var existing = await db.Hold.AsNoTracking()
            .FirstOrDefaultAsync(h => h.LectioHoldKey == key, ct);
        if (existing is not null)
        {
            return Conflict(new
            {
                error = "already_imported",
                message = "Der findes allerede et forløb for dette Lectio-hold.",
                holdId = existing.Id,
                forloebId = existing.ForloebId
            });
        }

        string bearer;
        try
        {
            bearer = RequireBearer();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "missing_bearer" });
        }

        JsonElement lectioHold;
        try
        {
            lectioHold = await lectio.GetHoldByLectioKeyAsync(key, bearer, ct);
        }
        catch (LectioCollectorException ex)
        {
            return StatusCode(ex.StatusCode, ex.Body);
        }

        var lectioName = GetString(lectioHold, "name") ?? key;
        var name = string.IsNullOrWhiteSpace(body.Name) ? lectioName : body.Name.Trim();
        if (name.Length > 200)
            name = name[..200];

        var members = new List<(string LectioId, string DisplayName)>();
        if (lectioHold.TryGetProperty("members", out var memArr) && memArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in memArr.EnumerateArray())
            {
                var elevId = GetString(m, "studentLectioId");
                if (string.IsNullOrWhiteSpace(elevId))
                    continue;
                var display = GetString(m, "studentName") ?? elevId;
                members.Add((elevId, display));
            }
        }

        IReadOnlyDictionary<string, Guid> links;
        try
        {
            links = await lectio.ResolveAuthLinksAsync(members.Select(m => m.LectioId), bearer, ct);
        }
        catch (LectioCollectorException)
        {
            links = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        }

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
            Name = lectioName.Length > 200 ? lectioName[..200] : lectioName,
            LectioHoldKey = key,
            CreatedAtUtc = now
        };

        var memberships = new List<Membership>();
        var seenLectio = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var claimed = 0;
        var shadows = 0;

        foreach (var (elevId, display) in members)
        {
            if (!seenLectio.Add(elevId))
                continue;

            Guid? userSub = links.TryGetValue(elevId, out var authSub) ? authSub : null;
            if (userSub is not null) claimed++;
            else shadows++;

            memberships.Add(new Membership
            {
                Id = Guid.NewGuid(),
                HoldId = hold.Id,
                UserSub = userSub,
                LectioId = elevId,
                DisplayName = display.Length > 200 ? display[..200] : display,
                Source = "lectio",
                CreatedAtUtc = now
            });
        }

        db.Forloeb.Add(forloeb);
        db.Hold.Add(hold);
        db.Memberships.AddRange(memberships);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = forloeb.Id }, new
        {
            forloeb.Id,
            forloeb.Name,
            forloeb.Description,
            forloeb.CreatedBySub,
            forloeb.CreatedAtUtc,
            hold = new
            {
                hold.Id,
                hold.Name,
                hold.LectioHoldKey,
                hold.CreatedAtUtc,
                memberCount = memberships.Count,
                claimedCount = claimed,
                shadowCount = shadows
            },
            imported = new { total = memberships.Count, claimed, shadows }
        });
    }

    private static object ToDto(Forloeb f) => new
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
            f.Hold.LectioHoldKey,
            f.Hold.CreatedAtUtc,
            memberCount = f.Hold.Memberships.Count,
            shadowCount = f.Hold.Memberships.Count(m => m.UserSub == null),
            members = f.Hold.Memberships
                .OrderBy(m => m.DisplayName ?? m.LectioId ?? m.UserSub.ToString())
                .Select(m => new
                {
                    m.Id,
                    m.UserSub,
                    m.LectioId,
                    m.DisplayName,
                    m.Source,
                    isShadow = m.UserSub == null,
                    m.CreatedAtUtc
                })
        }
    };

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private string RequireBearer()
    {
        var header = Request.Headers.Authorization.ToString();
        if (AuthenticationHeaderValue.TryParse(header, out var value)
            && value.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(value.Parameter))
        {
            return value.Parameter;
        }

        throw new UnauthorizedAccessException("missing_bearer");
    }
}
