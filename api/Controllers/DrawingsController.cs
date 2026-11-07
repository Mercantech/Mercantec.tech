using Mercantec.Tech.Api.Drawings;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, DrawingErrors, Route("api/drawings")]
public sealed class DrawingsController(DrawingService service, DrawingAccess access) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateDrawing request, CancellationToken ct) => Ok(await service.CreateAsync(request, User, ct));
    [HttpGet("{id:guid}/context")]
    public async Task<IActionResult> Context(Guid id, CancellationToken ct, bool published = false, Guid? snapshotId = null)
    { Response.Headers.CacheControl = "private, no-store"; return Ok(await service.ContextAsync(id, User, published, snapshotId, ct)); }
    [HttpPut("{id:guid}/settings")]
    public async Task<IActionResult> Settings(Guid id, UpdateDrawingSettings request, CancellationToken ct) => Ok(await service.SettingsAsync(id, request, User, ct));
    [HttpGet("{id:guid}/grants")]
    public async Task<IActionResult> Grants(Guid id, CancellationToken ct) => Ok(await service.GrantsAsync(id, User, ct));
    [HttpGet("{id:guid}/candidates")]
    public async Task<IActionResult> Candidates(Guid id, CancellationToken ct, string? search = null) => Ok(await service.CandidatesAsync(id, User, search, ct));
    [HttpPut("{id:guid}/grants")]
    public async Task<IActionResult> Grant(Guid id, SetDrawingGrant request, CancellationToken ct) => Ok(await service.GrantAsync(id, request, User, ct));
    [HttpDelete("{id:guid}/grants/{grantId:guid}")]
    public async Task<IActionResult> Revoke(Guid id, Guid grantId, CancellationToken ct) { await service.RevokeAsync(id, grantId, User, ct); return NoContent(); }

    [HttpPost("collaboration/authorize")]
    public async Task<IActionResult> AuthorizeDocument(AuthorizeDrawingCollaboration request, CancellationToken ct)
    {
        if (request.DocumentName is null || !request.DocumentName.StartsWith("drawing:", StringComparison.Ordinal)
            || !Guid.TryParseExact(request.DocumentName.AsSpan(8), "D", out var id) || id == Guid.Empty) return BadRequest(new { error = "invalid_document" });
        var drawing = await service.FindAsync(id, ct);
        if (drawing is null || !await access.CanReadHeadAsync(drawing, User, ct)) return NotFound();
        return Ok(new DrawingCollaborationAccess(id, true, await access.CanEditAsync(drawing, User, ct), drawing.AccessVersion));
    }

    [HttpPost("{id:guid}/assets"), RequestSizeLimit(4 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 4 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid id, IFormFile? file, CancellationToken ct)
        => file is null ? BadRequest(new { error = "missing_file" }) : Ok(await service.UploadAssetAsync(id, file, User, ct));
    [HttpPost("{id:guid}/previews"), RequestSizeLimit(8 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 8 * 1024 * 1024)]
    public async Task<IActionResult> Previews(Guid id, [FromForm] long expectedRevision, [FromForm] string expectedFingerprint,
        IFormFile? lightPNG, IFormFile? darkPNG, CancellationToken ct)
        => lightPNG is null || darkPNG is null ? BadRequest(new { error = "missing_preview" })
            : Ok(await service.UploadPreviewsAsync(id, expectedRevision, expectedFingerprint, lightPNG, darkPNG, User, ct));

    [AllowAnonymous, HttpGet("{id:guid}/assets/{assetId:guid}/content"), HttpHead("{id:guid}/assets/{assetId:guid}/content")]
    public async Task<IActionResult> Content(Guid id, Guid assetId, CancellationToken ct, string? ticket = null)
    {
        var viewer = service.TicketViewer(id, assetId, ticket, User); if (viewer is null) return Unauthorized();
        var (asset, content) = await service.ReadAssetAsync(id, assetId, viewer, ct);
        Response.Headers.CacheControl = "private, no-store"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Referrer-Policy"] = "no-referrer"; Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
        if (HttpMethods.IsHead(Request.Method)) { await content.DisposeAsync(); Response.ContentType = asset.ContentType; Response.ContentLength = asset.SizeBytes; return new EmptyResult(); }
        Response.RegisterForDisposeAsync(content);
        return File(content.Content, asset.ContentType, enableRangeProcessing: false);
    }
}
