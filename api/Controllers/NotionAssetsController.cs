using System.Security.Claims;
using System.Globalization;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/notion")]
public sealed class NotionAssetsController(AppDbContext db, CourseAccess access, IObjectStorage storage,
    IDataProtectionProvider protection) : ControllerBase
{
    private readonly NotionAccess pagesAccess = new(db, access);
    // Compatibility with already-open older editors. New editors use /assets/uploads, which accepts
    // whole files above this bounded single-request size through authenticated multipart requests.
    public const long MaxLegacyRequestBytes = 100 * 1024 * 1024;
    private readonly IDataProtector tickets = protection.CreateProtector("LUP.NotionMedia.v1");

    [HttpPost("pages/{pageId:guid}/assets")]
    [RequestSizeLimit(MaxLegacyRequestBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxLegacyRequestBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid pageId, IFormFile? file, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await pagesAccess.CanManageAsync(pageId, User, ct)) return NotFound();
        if (!storage.IsConfigured) return StatusCode(503, new { error = "object_storage_disabled" });
        if (file is null || file.Length == 0) return BadRequest(new { error = "missing_file" });
        if (file.Length > MaxLegacyRequestBytes) return StatusCode(413, new { error = "use_multipart_upload" });
        var name = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 240 || name.Any(char.IsControl))
            return BadRequest(new { error = "invalid_filename" });
        var type = NotionMediaPolicy.Classify(name);
        if (type is null) return BadRequest(new { error = "unsupported_file_type" });

        // Spool to disk instead of retaining large videos in memory. The spool is deleted on every exit path.
        var tempPath = Path.Combine(Path.GetTempPath(), $"notion-upload-{Guid.NewGuid():N}");
        await using var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await using (var input = file.OpenReadStream())
        {
            var buffer = new byte[81920]; int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (temp.Length + read > MaxLegacyRequestBytes) return StatusCode(413, new { error = "use_multipart_upload" });
                await temp.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        if (temp.Length != file.Length) return BadRequest(new { error = "incomplete_upload" });
        temp.Position = 0;
        var header = new byte[32]; var headerLength = await temp.ReadAsync(header, ct);
        if (!NotionMediaPolicy.MatchesSignature(name, header.AsSpan(0, headerLength)))
            return BadRequest(new { error = "invalid_file_signature" });
        temp.Position = 0;
        var id = Guid.NewGuid();
        // This namespace is explicitly denied by the old /api/files catch-all.
        var key = $"notion-private/{pageId:N}/{id:N}";
        await storage.PutAsync(key, temp, type.Value.ContentType, ct);
        var asset = new NotionAsset
        {
            Id = id, PageId = pageId, Key = key, OriginalName = name, ContentType = type.Value.ContentType,
            Kind = type.Value.Kind, SizeBytes = temp.Length, UploadedBySub = sub, CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.NotionAssets.Add(asset);
        try { await db.SaveChangesAsync(ct); }
        catch { await storage.DeleteAsync(key, CancellationToken.None); throw; }
        return Ok(new NotionAssetView(id, name, asset.ContentType, asset.Kind, asset.SizeBytes));
    }

    [HttpGet("assets/{id:guid}/access")]
    public async Task<IActionResult> Access(Guid id, CancellationToken ct)
    {
        var asset = await AuthorizedAsset(id, User, ct);
        if (asset is null) return NotFound();
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var expires = DateTimeOffset.UtcNow.AddMinutes(5);
        var admin = User.Claims.Any(c => (c.Type is "role" or "roles" || c.Type == ClaimTypes.Role)
            && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase));
        var manager = await pagesAccess.CanManageAsync(asset.Page, User, ct);
        var ticket = tickets.Protect(JsonSerializer.Serialize(new MediaTicket(id, sub, UserClaims.IsTeacher(User), admin,
            expires, manager ? null : asset.Page.PublicationVersion)));
        var url = $"/api/notion/assets/{id}/content?ticket={Uri.EscapeDataString(ticket)}";
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new NotionAssetAccess(url, url + "&download=true", expires, asset.OriginalName,
            asset.ContentType, asset.Kind, asset.SizeBytes));
    }

    [AllowAnonymous, HttpGet("assets/{id:guid}/content"), HttpHead("assets/{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct, string? ticket = null, bool download = false)
    {
        var viewer = ResolveViewer(id, ticket);
        if (viewer is null) return Unauthorized();
        // A ticket is a short-lived bearer URL, not a publication grant: every seek rechecks membership and publication.
        var asset = await AuthorizedAsset(id, viewer, ct);
        if (asset is null) return NotFound();
        if (!storage.IsConfigured) return StatusCode(503, new { error = "object_storage_disabled" });
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
        Response.Headers.AcceptRanges = "bytes";
        var disposition = new ContentDispositionHeaderValue(download || asset.Kind == "download" ? "attachment" : "inline")
            { FileNameStar = asset.OriginalName };
        Response.Headers.ContentDisposition = disposition.ToString();
        var etag = $"\"notion-{asset.Id:N}\"";
        Response.Headers.ETag = etag;
        Response.Headers.LastModified = asset.CreatedAtUtc.ToUniversalTime().ToString("R", CultureInfo.InvariantCulture);

        long? start = null, end = null;
        var isHead = HttpMethods.IsHead(Request.Method);
        var mayUseRange = !isHead && (!Request.Headers.TryGetValue("If-Range", out var ifRange)
            || IfRangeMatches(ifRange.ToString(), etag, asset.CreatedAtUtc));
        if (mayUseRange && Request.Headers.TryGetValue("Range", out var range))
        {
            if (!TryRange(range.ToString(), asset.SizeBytes, out var from, out var to))
            {
                Response.Headers.ContentRange = $"bytes */{asset.SizeBytes}";
                return StatusCode(416);
            }
            start = from; end = to;
        }
        var length = start is long offset && end is long last ? last - offset + 1 : asset.SizeBytes;
        void ApplyStreamHeaders()
        {
            Response.ContentType = asset.ContentType; Response.ContentLength = length;
            if (start is long first && end is long final)
            {
                Response.StatusCode = 206;
                Response.Headers.ContentRange = $"bytes {first}-{final}/{asset.SizeBytes}";
            }
        }
        if (isHead) { ApplyStreamHeaders(); return new EmptyResult(); }
        ObjectGetResult? obj;
        if (start is long rangeStart && end is long rangeEnd)
        {
            if (!storage.SupportsRanges) return StatusCode(503, new { error = "media_ranges_unavailable" });
            obj = await storage.GetRangeAsync(asset.Key, rangeStart, rangeEnd, ct);
        }
        else obj = await storage.GetAsync(asset.Key, ct);
        if (obj is null) return NotFound();
        ApplyStreamHeaders();
        Response.RegisterForDisposeAsync(obj);
        return File(obj.Content, asset.ContentType, enableRangeProcessing: false);
    }

    private async Task<NotionAsset?> AuthorizedAsset(Guid id, ClaimsPrincipal viewer, CancellationToken ct)
    {
        var asset = await db.NotionAssets.AsNoTracking().Include(a => a.Page).SingleOrDefaultAsync(a => a.Id == id, ct);
        if (asset is null) return null;
        if (await pagesAccess.CanManageAsync(asset.Page, viewer, ct)) return asset;
        if (asset.Page.PublishedAtUtc is null || !await pagesAccess.CanAccessScopeAsync(asset.Page.HoldId, viewer, ct)) return null;
        var issuedVersion = viewer.FindFirstValue("notion_publication_version");
        if (issuedVersion is not null && (!long.TryParse(issuedVersion, out var version) || version != asset.Page.PublicationVersion)) return null;
        try
        {
            return (JsonSerializer.Deserialize<Guid[]>(asset.Page.PublishedAssetIdsJson ?? "[]") ?? []).Contains(id) ? asset : null;
        }
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
            if (data.Admin) claims.Add(new(ClaimTypes.Role, "Admin"));
            if (data.PublicationVersion is long version) claims.Add(new("notion_publication_version", version.ToString(CultureInfo.InvariantCulture)));
            return new(new ClaimsIdentity(claims, "notion-media-ticket"));
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or FormatException) { return null; }
    }

    public static bool TryRange(string header, long size, out long start, out long end)
    {
        start = end = 0;
        if (size <= 0 || !RangeHeaderValue.TryParse(header, out var range) || range.Unit != "bytes" || range.Ranges.Count != 1) return false;
        var item = range.Ranges.Single();
        if (item.From is long from)
        {
            start = from; end = Math.Min(item.To ?? size - 1, size - 1);
            return start >= 0 && start < size && end >= start;
        }
        if (item.To is not long suffix || suffix <= 0) return false;
        start = Math.Max(0, size - suffix); end = size - 1; return true;
    }

    private static bool IfRangeMatches(string value, string etag, DateTimeOffset modified)
    {
        if (value.StartsWith('"') || value.StartsWith("W/", StringComparison.Ordinal)) return value == etag;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            && modified.ToUnixTimeSeconds() <= date.ToUnixTimeSeconds();
    }

    private sealed record MediaTicket(Guid AssetId, Guid UserSub, bool Teacher, bool Admin,
        DateTimeOffset ExpiresAtUtc, long? PublicationVersion);
}
