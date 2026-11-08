using Mercantec.Tech.Contracts;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/me/actions")]
public sealed class ActionCenterController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get([FromQuery] Guid? asUser, CancellationToken ct, [FromQuery] int version = 1)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty) return Unauthorized();
        var teacher = UserClaims.IsTeacher(User);
        if (asUser.HasValue && !teacher) return Forbid();
        if (asUser == Guid.Empty) return BadRequest(new { error = "invalid_user" });
        if (version is not (1 or 2)) return BadRequest(new { error = "unsupported_actions_version" });
        var preview = asUser.HasValue;
        var target = asUser ?? subject;
        var holds = teacher ? access.AccessibleHolds(User, true) : db.Hold.Where(h => h.Memberships.Any(m => m.UserSub == target));
        if (preview) holds = holds.Where(h => h.Memberships.Any(m => m.UserSub == target));
        var scope = await holds.AsNoTracking().Select(h => new ActionCenterHold(h.Id, h.ForloebId, h.Name)).ToArrayAsync(ct);
        // An empty preview scope cannot establish the selected pupil's personal status.
        if (preview && scope.Length == 0) return NotFound();
        return Ok(await new ActionCenterBuilder(db).BuildAsync(target, scope, teacher && !preview, ct, version: version, includeGifts: !preview, includeNews: !preview));
    }
    [HttpPost("read"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Read(ActionCenterReadRequest request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty) return Unauthorized();
        if (request?.Items is null || request.Items.Length > 200) return BadRequest(new { error = "invalid_read_receipts" });
        var teacher = UserClaims.IsTeacher(User);
        var holds = teacher ? access.AccessibleHolds(User, true) : db.Hold.Where(h => h.Memberships.Any(m => m.UserSub == subject));
        var scope = await holds.AsNoTracking().Select(h => new ActionCenterHold(h.Id, h.ForloebId, h.Name)).ToArrayAsync(ct);
        await new ActionCenterNews(db).AcknowledgeAsync(subject, request.Items, scope, teacher, ct);
        return NoContent();
    }
}
