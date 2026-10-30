using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Route("api/privacy")]
[Authorize]
public class PrivacyController(AppDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Ret til indsigt (GDPR art. 15): eksporter egne platform-data.
    /// Auth-konto (password, MFA m.m.) ligger hos Mercantec Auth — ikke her.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var package = await BuildDataPackageAsync(ct);
        var json = JsonSerializer.Serialize(package, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = $"mercantec-tech-data-{package.Sub ?? "unknown"}-{DateTime.UtcNow:yyyyMMdd}.json";
        return File(bytes, "application/json", fileName);
    }

    /// <summary>
    /// Sletteanmodning (GDPR art. 17): bekræfter anmodning. Fuld sletning i Auth/Lectio er out of scope.
    /// </summary>
    [HttpPost("deletion-request")]
    public IActionResult RequestDeletion([FromBody] DeletionRequestBody? body)
    {
        var subGuid = UserClaims.GetSub(User);
        if (subGuid is null)
            return Unauthorized(new { error = "missing_sub" });
        var sub = subGuid.Value.ToString();

        var reference = $"del-{DateTime.UtcNow:yyyyMMddHHmmss}-{sub[..8]}";

        // I v1 logger vi kun — ingen persistens før domæne-DB (#4).
        Console.WriteLine(
            $"[privacy] deletion-request ref={reference} sub={sub} reason={body?.Reason ?? "(none)"} utc={DateTime.UtcNow:O}");

        return Ok(new
        {
            received = true,
            reference,
            requestedAtUtc = DateTime.UtcNow,
            sub,
            nextSteps = new[]
            {
                "Platform-data i Mercantec.tech slettes manuelt af drift, når anmodningen er verificeret (indtil automatik findes).",
                "Identitetskonto (login, MFA, roller) administreres i Mercantec Auth — kontakt Auth/drift for sletning dér.",
                "Lectio-skoleforløb ligger uden for denne app (Lectio-Collector / Lectio)."
            },
            contact = "mags@mercantec.dk"
        });
    }

    private async Task<PrivacyExportPackage> BuildDataPackageAsync(CancellationToken ct)
    {
        var subGuid = UserClaims.GetSub(User);
        var sub = subGuid?.ToString();
        var name = User.FindFirstValue("name");
        var email = User.FindFirstValue("email");
        var loginMethod = User.FindFirstValue("login_method");
        var roles = User.Claims
            .Where(c =>
                c.Type is "role" or "roles"
                || c.Type.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r)
            .ToArray();

        var safeClaims = User.Claims
            .Where(c => !IsSensitiveClaim(c.Type))
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Value).Distinct().ToArray());

        object[] memberships = [];
        if (subGuid is not null)
        {
            memberships = await db.Memberships
                .AsNoTracking()
                .Where(m => m.UserSub == subGuid.Value)
                .Include(m => m.Hold)
                .ThenInclude(h => h.Forloeb)
                .Select(m => (object)new
                {
                    m.Id,
                    m.HoldId,
                    holdName = m.Hold.Name,
                    forloebId = m.Hold.ForloebId,
                    forloebName = m.Hold.Forloeb.Name,
                    m.Source,
                    m.CreatedAtUtc
                })
                .ToArrayAsync(ct);
        }

        return new PrivacyExportPackage(
            ExportedAtUtc: DateTime.UtcNow,
            Source: "mercantec.tech",
            Sub: sub,
            Profile: new
            {
                name,
                email,
                loginMethod,
                roles
            },
            PlatformRecords: new
            {
                memberships,
                submissions = Array.Empty<object>(),
                deletionRequests = Array.Empty<object>()
            },
            Claims: safeClaims,
            Notes: new[]
            {
                "Eksporten indeholder data kendt af Mercantec.tech API ud fra din JWT og platform-DB — ikke Auth-backendens fulde profil.",
                "Tokens, refresh-tokens og secrets eksporteres aldrig."
            }
        );
    }

    private static bool IsSensitiveClaim(string type)
    {
        var t = type.ToLowerInvariant();
        return t.Contains("password")
               || t.Contains("secret")
               || t.Contains("token")
               || t.Contains("refresh")
               || t.Contains("credential")
               || t.Contains("otp")
               || t.Contains("mfa");
    }

    public sealed record DeletionRequestBody(string? Reason);

    private sealed record PrivacyExportPackage(
        DateTime ExportedAtUtc,
        string Source,
        string? Sub,
        object Profile,
        object PlatformRecords,
        Dictionary<string, string[]> Claims,
        string[] Notes);
}
