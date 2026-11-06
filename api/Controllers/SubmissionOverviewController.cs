using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/holds/{holdId:guid}/submission-overview")]
public sealed class SubmissionOverviewController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid holdId, [FromQuery] Guid? courseId, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        if (courseId is Guid id && !await db.TeachingCourses.AnyAsync(c => c.Id == id && c.HoldId == holdId, ct))
            return NotFound();
        return Ok(await new SubmissionOverviewBuilder(db).BuildAsync(holdId, courseId, ct));
    }
}
