using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/notebooks")]
public sealed class NotebookAssetsController(AppDbContext db, NotebookAccess access, IObjectStorage storage,
    IDataProtectionProvider protection) : ControllerBase
{
    public const long MaxUploadBytes = 100 * 1024 * 1024;
    private readonly IDataProtector tickets = protection.CreateProtector("LUP.NotebookMedia.v1");

    [HttpPost("pages/{pageId:guid}/assets")]
    [RequestSizeLimit(MaxUploadBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid pageId, IFormFile? file, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await access.CanEditAsync(pageId, User, ct)) return NotFound();
        if (!storage.IsConfigured) return StatusCode(503, new { error = "object_storage_disabled" });
        if (file is null || file.Length == 0) return BadRequest(new { error = "missing_file" });
        if (file.Length > MaxUploadBytes) return StatusCode(413, new { error = "file_too_large", maxBytes = MaxUploadBytes });
        var name = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 240 || name.Any(char.IsControl))
            return BadRequest(new { error = "invalid_filename" });
        var type = NotionMediaPolicy.Classify(name);
        if (type is null) return BadRequest(new { error = "unsupported_file_type" });
        var tempPath = Path.Combine(Path.GetTempPath(), $"notebook-upload-{Guid.NewGuid():N}");
        await using var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await using (var input = file.OpenReadStream())
        {
            var buffer = new byte[81920]; int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (temp.Length + read > MaxUploadBytes) return StatusCode(413, new { error = "file_too_large" });
                await temp.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        if (temp.Length != file.Length) return BadRequest(new { error = "incomplete_upload" });
        temp.Position = 0;
        var header = new byte[32]; var length = await temp.ReadAsync(header, ct);
        if (!NotionMediaPolicy.MatchesSignature(name, header.AsSpan(0, length)))
            return BadRequest(new { error = "invalid_file_signature" });
        temp.Position = 0;
        var id = Guid.NewGuid(); var objectKey = $"notebook-private/{pageId:N}/{id:N}";
        await storage.PutAsync(objectKey, temp, type.Value.ContentType, ct);
        var asset = new NotebookAsset
        {
            Id = id, DocumentId = pageId, Key = objectKey, OriginalName = name,
            ContentType = type.Value.ContentType, Kind = type.Value.Kind, SizeBytes = temp.Length,
            UploadedBySub = sub, CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.NotebookAssets.Add(asset);
        try { await db.SaveChangesAsync(ct); }
        catch { await storage.DeleteAsync(objectKey, CancellationToken.None); throw; }
        return Ok(new NotionAssetView(id, name, asset.ContentType, asset.Kind, asset.SizeBytes));
    }

    [HttpGet("assets/{id:guid}/access")]
    public async Task<IActionResult> Access(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var asset = await AuthorizedAsset(id, User, ct);
        if (asset is null) return NotFound();
        var expires = DateTimeOffset.UtcNow.AddMinutes(5);
        var ticket = tickets.Protect(JsonSerializer.Serialize(new MediaTicket(id, sub, UserClaims.IsTeacher(User), expires)));
        var url = $"/api/notebooks/assets/{id}/content?ticket={Uri.EscapeDataString(ticket)}";
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new NotionAssetAccess(url, url + "&download=true", expires, asset.OriginalName,
            asset.ContentType, asset.Kind, asset.SizeBytes));
    }

    [AllowAnonymous, HttpGet("assets/{id:guid}/content"), HttpHead("assets/{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct, string? ticket = null, bool download = false)
    {
        var viewer = ResolveViewer(id, ticket);
        if (viewer is null) return Unauthorized();
        // Every request, including byte-range seeks, rechecks the current grant and live file reference.
        var asset = await AuthorizedAsset(id, viewer, ct);
        if (asset is null) return NotFound();
        if (!storage.IsConfigured) return StatusCode(503, new { error = "object_storage_disabled" });
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
        Response.Headers.AcceptRanges = "bytes";
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue(download || asset.Kind == "download" ? "attachment" : "inline")
            { FileNameStar = asset.OriginalName }.ToString();
        var etag = $"\"notebook-{asset.Id:N}\"";
        Response.Headers.ETag = etag;
        Response.Headers.LastModified = asset.CreatedAtUtc.ToUniversalTime().ToString("R", CultureInfo.InvariantCulture);
        var isHead = HttpMethods.IsHead(Request.Method);
        long? start = null, end = null;
        var mayUseRange = !isHead && (!Request.Headers.TryGetValue("If-Range", out var ifRange)
            || ifRange.ToString() == etag || DateTimeOffset.TryParse(ifRange, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var date) && date.ToUnixTimeSeconds() >= asset.CreatedAtUtc.ToUnixTimeSeconds());
        if (mayUseRange && Request.Headers.TryGetValue("Range", out var range))
        {
            if (!NotionAssetsController.TryRange(range.ToString(), asset.SizeBytes, out var from, out var to))
            { Response.Headers.ContentRange = $"bytes */{asset.SizeBytes}"; return StatusCode(416); }
            start = from; end = to;
        }
        var contentLength = start is long first && end is long last ? last - first + 1 : asset.SizeBytes;
        if (isHead) { Response.ContentLength = contentLength; Response.ContentType = asset.ContentType; return new EmptyResult(); }
        ObjectGetResult? obj;
        if (start is long rangeStart && end is long rangeEnd)
        {
            if (!storage.SupportsRanges) return StatusCode(503, new { error = "media_ranges_unavailable" });
            obj = await storage.GetRangeAsync(asset.Key, rangeStart, rangeEnd, ct);
        }
        else obj = await storage.GetAsync(asset.Key, ct);
        if (obj is null) return NotFound();
        Response.ContentLength = contentLength;
        if (start is long offset && end is long final)
        { Response.StatusCode = 206; Response.Headers.ContentRange = $"bytes {offset}-{final}/{asset.SizeBytes}"; }
        Response.RegisterForDisposeAsync(obj);
        return File(obj.Content, asset.ContentType, enableRangeProcessing: false);
    }

    private async Task<NotebookAsset?> AuthorizedAsset(Guid id, ClaimsPrincipal viewer, CancellationToken ct)
    {
        var asset = await db.NotebookAssets.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
        if (asset is null || !await access.CanReadAsync(asset.DocumentId, viewer, ct)) return null;
        if (await access.CanEditAsync(asset.DocumentId, viewer, ct)) return asset;
        var manifest = await db.NotebookDocuments.Where(d => d.Id == asset.DocumentId).Select(d => d.AssetIdsJson).SingleAsync(ct);
        try { return (JsonSerializer.Deserialize<Guid[]>(manifest) ?? []).Contains(id) ? asset : null; }
        catch (JsonException) { return null; }
    }

    private ClaimsPrincipal? ResolveViewer(Guid id, string? ticket)
    {
        if (string.IsNullOrEmpty(ticket)) return User.Identity?.IsAuthenticated == true ? User : null;
        if (ticket.Length > 8192) return null;
        try
        {
            var data = JsonSerializer.Deserialize<MediaTicket>(tickets.Unprotect(ticket));
            if (data is null || data.AssetId != id || data.UserSub == Guid.Empty || data.ExpiresAtUtc <= DateTimeOffset.UtcNow
                || data.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(6)) return null;
            var claims = new List<Claim> { new("sub", data.UserSub.ToString()) };
            if (data.Teacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
            return new(new ClaimsIdentity(claims, "notebook-media-ticket"));
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or FormatException) { return null; }
    }

    private sealed record MediaTicket(Guid AssetId, Guid UserSub, bool Teacher, DateTimeOffset ExpiresAtUtc);
}
