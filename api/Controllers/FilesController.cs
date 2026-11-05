using FilePreview = Mercantec.Tech.Contracts.FilePreview;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/files")]
public sealed class FilesController(
    IObjectStorage storage,
    AppDbContext db,
    IDataProtectionProvider protection) : ControllerBase
{
    public const long MaxUploadBytes = 100 * 1024 * 1024;
    private const int MaxTextBytes = 1024 * 1024;
    private const int MaxArchiveBytes = 20 * 1024 * 1024;
    private readonly FileViewerTickets viewerTickets = new(protection);

    [HttpGet("status")]
    public IActionResult Status() => Ok(new FileStorageStatus(storage.IsConfigured,
        storage.IsConfigured ? storage.Bucket : null));

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct, [FromForm] Guid? holdId = null)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!storage.IsConfigured) return StatusCode(503, new { error = "object_storage_disabled" });
        if (holdId is Guid hold && !await new CourseAccess(db).CanAccessAsync(hold, User, ct)) return NotFound();
        if (holdId is null && !UserClaims.IsTeacher(User)) return Forbid();
        if (file is null || file.Length == 0) return BadRequest(new { error = "missing_file" });
        if (file.Length > MaxUploadBytes) return BadRequest(new { error = "file_too_large", maxBytes = MaxUploadBytes });

        var originalName = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        if (string.IsNullOrWhiteSpace(originalName) || originalName.Length > 240 || originalName.Any(char.IsControl))
            return BadRequest(new { error = "invalid_filename" });
        var type = FileTypePolicy.Classify(originalName);
        if (type is null) return BadRequest(new { error = "unsupported_file_type" });
        var id = Guid.NewGuid();
        var key = $"files/{id:N}/{SanitizeFileName(originalName)}";
        var tempPath = Path.Combine(Path.GetTempPath(), $"mercantec-upload-{Guid.NewGuid():N}");
        await using var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await using (var input = file.OpenReadStream())
        {
            var buffer = new byte[81920];
            int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (temp.Length + count > MaxUploadBytes)
                    return BadRequest(new { error = "file_too_large", maxBytes = MaxUploadBytes });
                await temp.WriteAsync(buffer.AsMemory(0, count), ct);
            }
        }
        if (temp.Length != file.Length) return BadRequest(new { error = "incomplete_upload" });
        temp.Position = 0;
        await storage.PutAsync(key, temp, type.Value.Mime, ct);
        var asset = new FileAsset
        {
            Id = id, Key = key, OriginalName = originalName, ContentType = type.Value.Mime,
            SizeBytes = temp.Length, OwnerSub = sub, HoldId = holdId, CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.FileAssets.Add(asset);
        try { await db.SaveChangesAsync(ct); }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }
        return Ok(new UploadedFile(id, key, storage.Bucket, asset.ContentType, asset.SizeBytes,
            null, originalName, type.Value.Kind));
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
    {
        var asset = await FindAuthorized(id, User, ct);
        if (asset is null) return NotFound();
        var expires = DateTimeOffset.UtcNow.AddMinutes(5);
        var token = viewerTickets.Create(asset.Id, User, expires);
        var url = $"/api/files/{id}/content?ticket={Uri.EscapeDataString(token)}";
        var kind = FileTypePolicy.Classify(asset.OriginalName)?.Kind ?? "download";
        var derivative = await db.FilePreviews.AsNoTracking().SingleOrDefaultAsync(p => p.FileId == id, ct);
        string? message = null;
        if (derivative?.State == "ready" && derivative.StorageKey is not null)
        {
            kind = derivative.ContentType == "application/pdf" ? "pdf" : "video";
            url = $"/api/files/{id}/preview/content?ticket={Uri.EscapeDataString(token)}";
        }
        else if (kind == "download")
        {
            message = derivative?.State switch
            {
                "pending" or "processing" => "Forhåndsvisningen behandles. Originalen kan hentes imens.",
                "failed" => "Forhåndsvisningen kunne ikke genereres. Originalen kan stadig hentes.",
                _ => "Denne filtype kan hentes som original. Forhåndsvisning er endnu ikke tilgængelig."
            };
        }
        var downloadUrl = $"/api/files/{id}/content?ticket={Uri.EscapeDataString(token)}&download=true";
        Response.Headers.CacheControl = "no-store";
        return Ok(new FilePreview(id, asset.OriginalName, asset.ContentType, asset.SizeBytes, kind,
            url, downloadUrl, message, expires));
    }

    [HttpGet("{id:guid}/text")]
    public async Task<IActionResult> Text(Guid id, CancellationToken ct)
    {
        var asset = await FindAuthorized(id, User, ct);
        if (asset is null) return NotFound();
        if (FileTypePolicy.Classify(asset.OriginalName)?.Kind != "text")
            return BadRequest(new { error = "not_text" });
        await using var obj = await storage.GetAsync(asset.Key, ct);
        if (obj is null) return NotFound();
        var bytes = new byte[MaxTextBytes + 1];
        var length = await obj.Content.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, ct);
        var truncated = length > MaxTextBytes;
        Response.Headers.CacheControl = "no-store";
        return Ok(new FileTextPreview(Encoding.UTF8.GetString(bytes, 0, Math.Min(length, MaxTextBytes)), truncated));
    }

    [HttpGet("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var asset = await FindAuthorized(id, User, ct);
        if (asset is null) return NotFound();
        if (!asset.OriginalName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "not_zip" });
        if (asset.SizeBytes > MaxArchiveBytes)
            return BadRequest(new { error = "archive_preview_too_large", maxBytes = MaxArchiveBytes });
        try
        {
            using var archive = await OpenArchive(asset, ct);
            if (archive is null) return NotFound();
            var entries = archive.Entries.Take(501).ToArray();
            Response.Headers.CacheControl = "no-store";
            return Ok(new FileArchivePreview(entries.Take(500).Select(e =>
                new FileArchiveEntry(e.FullName, e.Length, e.FullName.EndsWith('/'))).ToArray(),
                entries.Length > 500));
        }
        catch (InvalidDataException) { return BadRequest(new { error = "invalid_archive" }); }
    }

    [HttpGet("{id:guid}/archive/text")]
    public async Task<IActionResult> ArchiveText(Guid id, [FromQuery] string path, CancellationToken ct)
    {
        var asset = await FindAuthorized(id, User, ct);
        if (asset is null) return NotFound();
        if (!asset.OriginalName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            || asset.SizeBytes > MaxArchiveBytes || !WebBuildService.IsSafePath(path)
            || FileTypePolicy.Classify(path)?.Kind != "text")
            return BadRequest(new { error = "unsupported_archive_entry" });
        try
        {
            using var archive = await OpenArchive(asset, ct);
            var entry = archive?.GetEntry(path);
            if (entry is null) return NotFound();
            await using var input = entry.Open();
            var bytes = new byte[MaxTextBytes + 1];
            var count = await input.ReadAtLeastAsync(bytes, bytes.Length, false, ct);
            Response.Headers.CacheControl = "no-store";
            return Ok(new FileTextPreview(Encoding.UTF8.GetString(bytes, 0, Math.Min(count, MaxTextBytes)),
                count > MaxTextBytes));
        }
        catch (InvalidDataException) { return BadRequest(new { error = "invalid_archive" }); }
    }

    private async Task<ZipArchive?> OpenArchive(FileAsset asset, CancellationToken ct)
    {
        await using var obj = await storage.GetAsync(asset.Key, ct);
        if (obj is null) return null;
        var buffer = new MemoryStream();
        try
        {
            var chunk = new byte[81920];
            int count;
            while ((count = await obj.Content.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + count > MaxArchiveBytes) throw new InvalidDataException("archive_preview_too_large");
                await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
            }
            buffer.Position = 0;
            return new ZipArchive(buffer, ZipArchiveMode.Read);
        }
        catch { buffer.Dispose(); throw; }
    }

    [AllowAnonymous, HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct, string? ticket = null, bool download = false)
    {
        var viewer = ResolveViewer(id, ticket);
        if (viewer is null) return Unauthorized();
        var asset = await FindAuthorized(id, viewer, ct);
        if (asset is null) return NotFound();
        return await Serve(asset, download, ct);
    }

    [AllowAnonymous, HttpGet("{id:guid}/preview/content")]
    public async Task<IActionResult> PreviewContent(Guid id, CancellationToken ct, string? ticket = null)
    {
        var viewer = ResolveViewer(id, ticket);
        if (viewer is null) return Unauthorized();
        var asset = await FindAuthorized(id, viewer, ct);
        if (asset is null) return NotFound();
        var preview = await db.FilePreviews.AsNoTracking().SingleOrDefaultAsync(p => p.FileId == id, ct);
        if (preview?.State != "ready" || preview.StorageKey is null
            || preview.ContentType is not ("application/pdf" or "video/mp4")) return NotFound();
        var derived = new FileAsset
        {
            Id = asset.Id, Key = preview.StorageKey, OriginalName = Path.GetFileNameWithoutExtension(asset.OriginalName)
                + (preview.ContentType == "application/pdf" ? ".pdf" : ".mp4"),
            ContentType = preview.ContentType, SizeBytes = preview.SizeBytes, OwnerSub = asset.OwnerSub,
            HoldId = asset.HoldId, CreatedAtUtc = asset.CreatedAtUtc
        };
        return await Serve(derived, false, ct);
    }

    [HttpPost("{id:guid}/web-preview")]
    public async Task<IActionResult> PrepareWebPreview(Guid id, CancellationToken ct, [FromQuery] string? entryPoint = null)
    {
        var asset = await FindAuthorized(id, User, ct);
        if (asset is null) return NotFound();
        var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        if (!WebPreviewOrigins.TryGet(configuration, out var origin, out _))
            return StatusCode(503, new { error = "web_preview_not_configured" });
        if (asset.SizeBytes > MaxUploadBytes) return BadRequest(new { error = "web_build_too_large" });
        try
        {
            var manifest = await new WebBuildService(storage).PrepareAsync(asset, entryPoint, ct);
            var ticket = viewerTickets.Create(asset.Id, User, DateTimeOffset.UtcNow.AddHours(1));
            var entryPath = string.Join("/", manifest.EntryPoint.Split('/').Select(Uri.EscapeDataString));
            var url = $"{origin.GetLeftPart(UriPartial.Authority)}/api/web-files/{id}/{Uri.EscapeDataString(ticket)}/{entryPath}";
            Response.Headers.CacheControl = "no-store";
            return Ok(new FileWebPreview(url, manifest.EntryPoint));
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Retain the former key URL for registered objects while failing closed for unregistered legacy keys.
    [HttpGet("{*key}")]
    public async Task<IActionResult> Download(string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains("..", StringComparison.Ordinal)) return BadRequest();
        var asset = await db.FileAssets.AsNoTracking().SingleOrDefaultAsync(f => f.Key == key, ct);
        if (asset is null || !await new FileAuthorization(db).CanReadAsync(asset, User, ct)) return NotFound();
        return await Serve(asset, true, ct);
    }

    private async Task<FileAsset?> FindAuthorized(Guid id, ClaimsPrincipal viewer, CancellationToken ct)
    {
        var asset = await db.FileAssets.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id, ct);
        return asset is not null && await new FileAuthorization(db).CanReadAsync(asset, viewer, ct) ? asset : null;
    }

    private ClaimsPrincipal? ResolveViewer(Guid fileId, string? ticket) =>
        User.Identity?.IsAuthenticated == true && UserClaims.GetSub(User) is not null
            ? User : viewerTickets.Resolve(fileId, ticket);

    private async Task<IActionResult> Serve(FileAsset asset, bool download, CancellationToken ct)
    {
        if (!storage.IsConfigured) return StatusCode(503, new { error = "object_storage_disabled" });
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; frame-ancestors 'self'";
        var kind = FileTypePolicy.Classify(asset.OriginalName)?.Kind ?? "download";
        var disposition = new ContentDispositionHeaderValue(download || kind is "download" or "archive" ? "attachment" : "inline");
        disposition.SetHttpFileName(asset.OriginalName);
        Response.Headers.ContentDisposition = disposition.ToString();

        ObjectGetResult? obj;
        if (storage.SupportsRanges)
        {
            Response.Headers.AcceptRanges = "bytes";
            var rawRange = Request.Headers.Range.ToString();
            if (!string.IsNullOrEmpty(rawRange))
            {
                if (!TryRange(rawRange, asset.SizeBytes, out var start, out var end))
                {
                    Response.Headers.ContentRange = $"bytes */{asset.SizeBytes}";
                    return StatusCode(416);
                }
                obj = await storage.GetRangeAsync(asset.Key, start, end, ct);
                if (obj is null) return NotFound();
                Response.StatusCode = 206;
                Response.ContentLength = end - start + 1;
                Response.Headers.ContentRange = $"bytes {start}-{end}/{asset.SizeBytes}";
            }
            else obj = await storage.GetAsync(asset.Key, ct);
        }
        else obj = await storage.GetAsync(asset.Key, ct);
        if (obj is null) return NotFound();
        Response.RegisterForDisposeAsync(obj);
        if (Response.StatusCode != 206) Response.ContentLength = obj.ContentLength;
        return File(obj.Content, asset.ContentType);
    }

    internal static bool TryRange(string header, long size, out long start, out long end)
    {
        start = 0; end = size - 1;
        if (size <= 0 || !RangeHeaderValue.TryParse(header, out var parsed)
            || !string.Equals(parsed.Unit.Value, "bytes", StringComparison.OrdinalIgnoreCase)
            || parsed.Ranges.Count != 1) return false;
        var range = parsed.Ranges.Single();
        if (range.From is long from)
        {
            if (from >= size || from < 0 || range.To < from) return false;
            start = from; end = Math.Min(range.To ?? end, end);
        }
        else
        {
            if (range.To is not long suffix || suffix <= 0) return false;
            start = Math.Max(0, size - suffix);
        }
        return true;
    }

    private static string SanitizeFileName(string name)
    {
        var cleaned = new string(name.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray());
        return cleaned[..Math.Min(cleaned.Length, 120)];
    }

    public sealed record FileStorageStatus(bool Enabled, string? Bucket);
}
