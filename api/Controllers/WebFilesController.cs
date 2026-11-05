using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

/// <summary>Serves untrusted executable web assets only from the configured isolated HTTPS origin.</summary>
[ApiController, AllowAnonymous, Route("api/web-files")]
public sealed class WebFilesController(
    IObjectStorage storage, AppDbContext db, IDataProtectionProvider protection, IConfiguration configuration) : ControllerBase
{
    [HttpGet("{id:guid}/{ticket}/{*path}")]
    public async Task<IActionResult> Get(Guid id, string ticket, string? path, CancellationToken ct)
    {
        if (!WebPreviewOrigins.TryGet(configuration, out var origin, out var appOrigin)
            || !Request.Scheme.Equals(origin.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Request.Host.Value, origin.Authority, StringComparison.OrdinalIgnoreCase))
            return NotFound();
        var viewer = new FileViewerTickets(protection).Resolve(id, ticket);
        if (viewer is null) return Unauthorized();
        var asset = await db.FileAssets.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id, ct);
        if (asset is null || !await new FileAuthorization(db).CanReadAsync(asset, viewer, ct)) return NotFound();
        if (path is null || !WebBuildService.IsSafePath(path)) return NotFound();
        var manifest = await new WebBuildService(storage).GetManifestAsync(id, ct);
        var entry = manifest?.Entries.SingleOrDefault(e => e.Path == path);
        if (entry is null) return NotFound();
        var obj = await storage.GetAsync(WebBuildService.Key(id, path), ct);
        if (obj is null) return NotFound();
        Response.RegisterForDisposeAsync(obj);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
        Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        Response.Headers["Content-Security-Policy"] =
            $"sandbox allow-scripts allow-same-origin; default-src 'self' blob: data:; "
            + "script-src 'self' 'unsafe-inline' 'unsafe-eval' blob:; style-src 'self' 'unsafe-inline'; "
            + "connect-src 'self' blob:; worker-src 'self' blob:; object-src 'none'; "
            + $"frame-ancestors {appOrigin.GetLeftPart(UriPartial.Authority)}; base-uri 'self'; form-action 'none'";
        if (entry.ContentEncoding is not null) Response.Headers.ContentEncoding = entry.ContentEncoding;
        Response.ContentLength = entry.SizeBytes;
        return File(obj.Content, entry.ContentType);
    }
}
