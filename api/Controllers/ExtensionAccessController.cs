using System.Security.Claims;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Emojis;
using Mercantec.Tech.Api.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Route("api/extension"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ExtensionAccessController(ExtensionUploadKeyService keys, AppDbContext db,
    IObjectStorage storage, IDataProtectionProvider protection, ExtensionUploadRateLimit rate) : ControllerBase
{
    [HttpGet("access"), Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Access(CancellationToken ct) =>
        Ok(new { enabled = keys.CanManage(User), keys = await keys.List(User, ct) });

    [HttpPost("access"), Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme), RequestSizeLimit(8192)]
    public async Task<IActionResult> Create(CreateExtensionAccess? request, CancellationToken ct)
    {
        if (!keys.CanManage(User)) return Forbid(JwtBearerDefaults.AuthenticationScheme);
        var label = request?.Label?.Trim() ?? "LUP i Chrome";
        if (label.Length is < 1 or > 80 || label.Any(char.IsControl)) return BadRequest(new { error = "invalid_label" });
        var created = await keys.Create(User, label, ct);
        return created is null ? Conflict(new { error = "extension_key_limit" }) : Ok(created);
    }

    [HttpDelete("access/{id:guid}"), Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        if (!keys.CanManage(User)) return Forbid(JwtBearerDefaults.AuthenticationScheme);
        return await keys.Revoke(User, id, ct) ? NoContent() : NotFound();
    }

    [HttpGet("status"), Authorize(AuthenticationSchemes = ExtensionUploadCredentials.Scheme)]
    public IActionResult Status() => Ok(new { connected = true, scope = ExtensionUploadCredentials.UploadScope, email = User.FindFirstValue("email") });

    [HttpPost("emojis"), Authorize(AuthenticationSchemes = ExtensionUploadCredentials.Scheme)]
    [RequestSizeLimit(EmojisController.MaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = EmojisController.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, [FromForm] string? name, CancellationToken ct,
        [FromForm] string? altText = null, [FromForm] string? category = null, [FromForm] string? keywords = null)
    {
        if (!User.HasClaim(ExtensionUploadCredentials.ScopeClaim, ExtensionUploadCredentials.UploadScope)
            || !Guid.TryParse(User.FindFirstValue(ExtensionUploadCredentials.KeyIdClaim), out var keyId))
            return Forbid(ExtensionUploadCredentials.Scheme);
        if (!rate.TryAcquire(keyId))
        {
            Response.Headers.RetryAfter = "60";
            return StatusCode(429, new { error = "extension_rate_limit" });
        }
        // Reuse the established signature/size/name validation and private storage.
        // This principal is scoped to this endpoint: it is never accepted by the normal JWT APIs.
        var upload = new EmojisController(db, storage, protection) { ControllerContext = ControllerContext };
        return await upload.Upload(file, name, ct, altText, category, keywords);
    }
}

public sealed record CreateExtensionAccess(string? Label);
