using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me/profile")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfileNameController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileNameDto>> Get(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true || UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty)
            return Unauthorized();

        if (!MatchesExpectedSubject(subject, required: false))
            return AccountChanged();

        var displayName = await db.ProfileNames.AsNoTracking()
            .Where(profile => profile.UserSub == subject)
            .Select(profile => profile.DisplayName)
            .SingleOrDefaultAsync(ct);

        return Ok(new ProfileNameDto(displayName));
    }

    [HttpPut]
    public async Task<ActionResult<ProfileNameDto>> Update([FromBody] UpdateProfileNameRequest request, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true || UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty)
            return Unauthorized();

        if (!MatchesExpectedSubject(subject, required: true))
            return AccountChanged();

        if (!ProfileNameRules.TryNormalize(request.DisplayName, out var displayName))
            return Failure(StatusCodes.Status400BadRequest, "invalid_display_name",
                $"Skriv et navn på mellem 1 og {ProfileNameRules.MaxLength} tegn uden kontroltegn eller linjeskift.");

        if (displayName is null)
        {
            await db.ProfileNames.Where(profile => profile.UserSub == subject).ExecuteDeleteAsync(ct);
        }
        else
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO profile_names ("UserSub", "DisplayName") VALUES ({subject}, {displayName})
                ON CONFLICT ("UserSub") DO UPDATE SET "DisplayName" = excluded."DisplayName"
                """, ct);
        }

        return Ok(new ProfileNameDto(displayName));
    }

    // A request started in one browser session must not use a different account's refreshed token.
    private bool MatchesExpectedSubject(Guid subject, bool required) =>
        Request.Headers.TryGetValue(ProfileNameRules.SubjectHeader, out var header)
            ? Guid.TryParse(header.ToString(), out var expected) && expected == subject
            : !required;

    private static ObjectResult AccountChanged() =>
        Failure(StatusCodes.Status409Conflict, "account_changed",
            "Kontoen er skiftet. Åbn profilen igen.");

    private static ObjectResult Failure(int status, string code, string detail)
    {
        var problem = new ProblemDetails { Status = status, Detail = detail };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}
