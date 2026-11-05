using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/materials")]
public sealed class MaterialsController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid holdId, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!await access.CanAccessAsync(holdId, User, ct)) return NotFound();
        var manage = await access.CanManageAsync(holdId, User, ct);
        var materials = await db.CourseMaterials.AsNoTracking()
            .Where(m => m.HoldId == holdId && (manage || m.Published))
            .Select(m => new CourseMaterialView(
                m.Id, m.HoldId, m.FileAssetId, m.Title, m.Description, m.Published, m.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(materials.OrderByDescending(m => m.CreatedAtUtc).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCourseMaterial request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await access.CanManageAsync(request.HoldId, User, ct)) return NotFound();
        if (!ValidText(request.Title, request.Description)) return BadRequest(new { error = "invalid_material" });
        // Publishing a file is an explicit owner action, never a way to share somebody else's upload.
        if (!await db.FileAssets.AnyAsync(f => f.Id == request.FileAssetId && f.OwnerSub == sub
            && (f.HoldId == request.HoldId || f.HoldId == null), ct))
            return BadRequest(new { error = "invalid_file" });
        var material = new CourseMaterial
        {
            Id = Guid.NewGuid(), HoldId = request.HoldId, FileAssetId = request.FileAssetId,
            Title = request.Title.Trim(), Description = request.Description?.Trim(),
            Published = request.Published, CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.CourseMaterials.Add(material);
        await db.SaveChangesAsync(ct);
        return Ok(View(material));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCourseMaterial request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        var material = await db.CourseMaterials.FindAsync([id], ct);
        if (material is null || !await access.CanManageAsync(material.HoldId, User, ct)) return NotFound();
        if (!ValidText(request.Title, request.Description)) return BadRequest(new { error = "invalid_material" });
        material.Title = request.Title.Trim();
        material.Description = request.Description?.Trim();
        material.Published = request.Published;
        await db.SaveChangesAsync(ct);
        return Ok(View(material));
    }

    private static bool ValidText(string? title, string? description) =>
        !string.IsNullOrWhiteSpace(title) && title.Trim().Length <= 200 && (description?.Length ?? 0) <= 4000;

    private static CourseMaterialView View(CourseMaterial m) =>
        new(m.Id, m.HoldId, m.FileAssetId, m.Title, m.Description, m.Published, m.CreatedAtUtc);
}
