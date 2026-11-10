using System.Net.Http.Headers;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.Lectio;
using Mercantec.Tech.Api.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
            .Include(f => f.Hold)!
            .ThenInclude(h => h!.LectioSources)
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
                lectioHoldKeys = f.Hold != null
                    ? f.Hold.LectioSources.Select(s => s.LectioHoldKey).ToList()
                    : new List<string>(),
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
            .Include(x => x.Hold)!
            .ThenInclude(h => h!.LectioSources)
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
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty)
            return Unauthorized();

        var manageableHold = new CourseAccess(db).AccessibleHolds(User, manageOnly: true).Where(h => h.ForloebId == id);
        var forloeb = await db.Forloeb.FirstOrDefaultAsync(f => f.Id == id && manageableHold.Any(), ct);
        if (forloeb is null)
            return NotFound();

        var name = (body.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || name.Any(char.IsControl))
            return BadRequest(new { error = "invalid_name" });

        var previousName = forloeb.Name;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        forloeb.Name = name;
        forloeb.Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim();
        await db.SaveChangesAsync(ct);

        // Preserve a display name set independently on the hold.
        await manageableHold.Where(h => h.Name == previousName && (h.LectioHoldKey == null || h.LectioHoldKey.Trim() == "")
                && !h.LectioSources.Any())
            .ExecuteUpdateAsync(setters => setters.SetProperty(h => h.Name, name), ct);
        await transaction.CommitAsync(ct);

        var updated = await db.Forloeb
            .AsNoTracking()
            .Include(x => x.Hold)!
            .ThenInclude(h => h!.Memberships)
            .FirstAsync(x => x.Id == id, ct);

        return Ok(ToDto(updated));
    }

    public sealed record FromLectioRequest(
        string? LectioHoldKey,
        List<string>? LectioHoldKeys,
        string? Name,
        string? Description,
        string? HoldName = null);

    /// <summary>
    /// Opret forløb + hold fra ét eller flere Lectio-Collector-hold (samles til én elevliste).
    /// Claimed Auth→Lectio bliver rigtige medlemmer, resten shadow. Deduplikerer på Lectio-id.
    /// </summary>
    [HttpPost("from-lectio")]
    public async Task<ActionResult<object>> CreateFromLectio([FromBody] FromLectioRequest body, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        var sub = UserClaims.GetSub(User);
        if (sub is null)
            return Unauthorized(new { error = "missing_sub" });

        var requestedHoldName = body.HoldName?.Trim();
        if (requestedHoldName is not null && (requestedHoldName.Length is 0 or > 200 || requestedHoldName.Any(char.IsControl)))
            return BadRequest(new { error = "invalid_name" });

        var keys = new List<string>();
        if (body.LectioHoldKeys is { Count: > 0 })
            keys.AddRange(body.LectioHoldKeys);
        if (!string.IsNullOrWhiteSpace(body.LectioHoldKey))
            keys.Add(body.LectioHoldKey);

        keys = keys
            .Select(k => k.Trim())
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
            return BadRequest(new { error = "missing_lectio_hold_key" });

        if (await ExistingLectioImportAsync(keys, ct) is { } alreadyImported)
            return alreadyImported;

        string bearer;
        try
        {
            bearer = RequireBearer();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "missing_bearer" });
        }

        var fetched = new List<(string Key, string Name, List<(string LectioId, string DisplayName)> Members)>();
        foreach (var key in keys)
        {
            JsonElement lectioHold;
            try
            {
                lectioHold = await lectio.GetHoldByLectioKeyAsync(key, bearer, ct);
            }
            catch (LectioCollectorException ex)
            {
                return StatusCode(ex.StatusCode, new
                {
                    error = "lectio_hold_fetch_failed",
                    lectioHoldKey = key,
                    body = ex.Body
                });
            }
            catch (JsonException)
            {
                return InvalidLectioRoster(key);
            }

            if (!TryReadRoster(lectioHold, out var details, out var roster))
                return InvalidLectioRoster(key);

            var canonicalKey = GetString(details, "lectioHoldKey") ?? GetString(lectioHold, "lectioHoldKey");
            if (string.IsNullOrWhiteSpace(canonicalKey))
                canonicalKey = key;
            var lectioName = GetString(details, "name") ?? GetString(lectioHold, "name") ?? key;
            var members = new List<(string LectioId, string DisplayName)>();
            foreach (var member in roster.EnumerateArray())
            {
                var elevId = GetString(member, "studentLectioId") ?? GetString(member, "lectioId");
                if (string.IsNullOrWhiteSpace(elevId))
                    return InvalidLectioRoster(key);
                var display = GetString(member, "studentName") ?? GetString(member, "name") ?? elevId;
                members.Add((elevId, display));
            }

            fetched.Add((canonicalKey, lectioName, members));
        }

        fetched = fetched.DistinctBy(source => source.Key, StringComparer.OrdinalIgnoreCase).ToList();
        var canonicalKeys = fetched.Select(source => source.Key).ToList();
        if (!keys.SequenceEqual(canonicalKeys, StringComparer.OrdinalIgnoreCase)
            && await ExistingLectioImportAsync(canonicalKeys, ct) is { } canonicalConflict)
            return canonicalConflict;
        keys = canonicalKeys;

        var allMembers = fetched.SelectMany(f => f.Members).ToList();
        IReadOnlyDictionary<string, Guid> links;
        try
        {
            links = await lectio.ResolveAuthLinksAsync(allMembers.Select(m => m.LectioId), bearer, ct);
        }
        catch (LectioCollectorException)
        {
            links = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        }

        var suggested = SuggestForloebName(fetched.Select(f => f.Name).ToList());
        var name = string.IsNullOrWhiteSpace(body.Name) ? suggested : body.Name.Trim();
        if (name.Length > 200)
            name = name[..200];

        var holdDisplayName = fetched.Count == 1
            ? fetched[0].Name
            : string.Join(" + ", fetched.Select(f => f.Name));
        if (holdDisplayName.Length > 200)
            holdDisplayName = holdDisplayName[..200];

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
            Name = requestedHoldName ?? holdDisplayName,
            LectioHoldKey = keys[0],
            CreatedAtUtc = now
        };

        var sources = fetched.Select(f => new HoldLectioSource
        {
            Id = Guid.NewGuid(),
            HoldId = hold.Id,
            LectioHoldKey = f.Key,
            LectioHoldName = f.Name.Length > 200 ? f.Name[..200] : f.Name,
            ImportedAtUtc = now
        }).ToList();

        var memberships = new List<Membership>();
        var seenLectio = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var claimed = 0;
        var shadows = 0;

        foreach (var (elevId, display) in allMembers)
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
        db.HoldLectioSources.AddRange(sources);
        db.Memberships.AddRange(memberships);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsDuplicateLectioSource(exception))
        {
            // Another import may commit after the preflight check. The unique index rolls back
            // this request's hold and roster, then the same conflict response identifies the winner.
            db.ChangeTracker.Clear();
            if (await ExistingLectioImportAsync(keys, ct) is { } conflict)
                return conflict;
            throw;
        }

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
                lectioHoldKeys = sources.Select(s => s.LectioHoldKey).ToList(),
                lectioSources = sources.Select(s => new { s.LectioHoldKey, s.LectioHoldName }),
                hold.CreatedAtUtc,
                memberCount = memberships.Count,
                claimedCount = claimed,
                shadowCount = shadows
            },
            imported = new
            {
                total = memberships.Count,
                claimed,
                shadows,
                lectioHolds = keys.Count
            }
        });
    }

    private async Task<ConflictObjectResult?> ExistingLectioImportAsync(List<string> keys, CancellationToken ct)
    {
        var lookupKeys = keys.Select(key => key.ToLowerInvariant()).ToArray();
        var sources = await db.HoldLectioSources.AsNoTracking()
            .Where(source => lookupKeys.Contains(source.LectioHoldKey.ToLower()))
            .Select(source => new { source.LectioHoldKey, source.HoldId, source.Hold.ForloebId })
            .ToListAsync(ct);
        var legacy = await db.Hold.AsNoTracking()
            .Where(hold => hold.LectioHoldKey != null && lookupKeys.Contains(hold.LectioHoldKey.ToLower()))
            .Select(hold => new { LectioHoldKey = hold.LectioHoldKey!, HoldId = hold.Id, hold.ForloebId })
            .ToListAsync(ct);
        var conflicts = sources.Concat(legacy)
            .DistinctBy(source => source.LectioHoldKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (conflicts.Length == 0)
            return null;

        var conflictHoldIds = conflicts.Select(source => source.HoldId).ToArray();
        var accessibleHoldIds = await new CourseAccess(db).AccessibleHolds(User)
            .Where(hold => conflictHoldIds.Contains(hold.Id))
            .Select(hold => hold.Id).ToArrayAsync(ct);
        return Conflict(new
        {
            error = "already_imported",
            message = "Ét eller flere Lectio-hold er allerede koblet til et forløb.",
            conflicts = conflicts.Where(source => accessibleHoldIds.Contains(source.HoldId))
        });
    }

    private static bool IsDuplicateLectioSource(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_hold_lectio_sources_LectioHoldKey"
        }
        || exception.InnerException?.Message.Contains(
            "UNIQUE constraint failed: hold_lectio_sources.LectioHoldKey", StringComparison.Ordinal) == true;

    private static bool TryReadRoster(JsonElement hold, out JsonElement details, out JsonElement roster)
    {
        details = hold;
        roster = default;
        if (hold.ValueKind != JsonValueKind.Object)
            return false;
        if (hold.TryGetProperty("members", out roster) && roster.ValueKind == JsonValueKind.Array)
            return true;
        if (!hold.TryGetProperty("hold", out details) || details.ValueKind != JsonValueKind.Object)
            return false;
        return details.TryGetProperty("members", out roster) && roster.ValueKind == JsonValueKind.Array;
    }

    private ObjectResult InvalidLectioRoster(string key) =>
        StatusCode(StatusCodes.Status502BadGateway, new { error = "invalid_lectio_roster", lectioHoldKey = key });

    /// <summary>Fælles præfiks før / (fx dt3H2602/pro + dt3H2602/proEA → dt3H2602).</summary>
    internal static string SuggestForloebName(IReadOnlyList<string> holdNames)
    {
        if (holdNames.Count == 0) return "Forløb";
        if (holdNames.Count == 1) return holdNames[0];

        var prefixes = holdNames
            .Select(n =>
            {
                var slash = n.IndexOf('/');
                return slash > 0 ? n[..slash] : n;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (prefixes.Count == 1)
            return prefixes[0];

        return string.Join(" + ", prefixes);
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
            lectioHoldKeys = f.Hold.LectioSources.Select(s => s.LectioHoldKey).ToList(),
            lectioSources = f.Hold.LectioSources.Select(s => new
            {
                s.LectioHoldKey,
                s.LectioHoldName,
                s.ImportedAtUtc
            }),
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
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

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
