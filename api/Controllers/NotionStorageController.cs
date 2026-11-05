using System.Security.Cryptography;
using System.Text;
using Mercantec.Tech.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

/// <summary>Internal Hocuspocus persistence only. Never accepts a browser user's JWT as storage access.</summary>
[ApiController, AllowAnonymous, Route("api/internal/notion/documents")]
public sealed class NotionStorageController(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    public const int MaxStateBytes = 16 * 1024 * 1024;

    [HttpGet("{id:guid}/state")]
    public async Task<IActionResult> ReadState(Guid id, CancellationToken ct)
    {
        var denied = CheckServiceKey();
        if (denied is not null) return denied;
        Response.Headers.CacheControl = "no-store";
        var document = await db.NotionPages.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.YjsState }).SingleOrDefaultAsync(ct);
        if (document is null) return NotFound();
        return document.YjsState is { Length: > 0 } bytes ? File(bytes, "application/octet-stream") : NoContent();
    }

    [HttpPut("{id:guid}/state"), RequestSizeLimit(MaxStateBytes)]
    public async Task<IActionResult> WriteState(Guid id, CancellationToken ct)
    {
        var denied = CheckServiceKey();
        if (denied is not null) return denied;
        if (!string.Equals(Request.ContentType?.Split(';')[0].Trim(), "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (Request.ContentLength > MaxStateBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge);
        if (!await db.NotionPages.AnyAsync(p => p.Id == id, ct)) return NotFound();
        using var state = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await Request.Body.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            if (state.Length + count > MaxStateBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge);
            await state.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        if (state.Length == 0) return BadRequest(new { error = "empty_state" });
        var updated = DateTimeOffset.UtcNow;
        var bytes = state.ToArray();
        // Atomic column update leaves concurrently renamed page metadata intact.
        var written = await db.NotionPages.Where(p => p.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.YjsState, bytes).SetProperty(p => p.UpdatedAtUtc, updated), ct);
        if (written == 0) return NotFound();
        return Ok(new { savedAtUtc = updated });
    }

    private string? ReadConfiguredSecretFile()
    {
        // This path is deployment configuration only, never supplied by a request.
        var path = configuration["Notion:ServiceSecretFile"];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return null;
        try
        {
            using var file = System.IO.File.OpenRead(path);
            if (file.Length > 1024) return null;
            using var reader = new StreamReader(file, Encoding.UTF8, true);
            return reader.ReadToEnd().Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private IActionResult? CheckServiceKey()
    {
        var expected = configuration["Notion:ServiceSecret"];
        if (string.IsNullOrWhiteSpace(expected)) expected = ReadConfiguredSecretFile();
        // A missing/weak deployment secret cannot accidentally make persistence public.
        if (string.IsNullOrEmpty(expected) || expected.Length is < 32 or > 1024)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "notion_not_configured" });
        if (!Request.Headers.TryGetValue("X-Notion-Service-Key", out var values) || values.Count != 1)
            return Unauthorized();
        var supplied = values[0] ?? "";
        if (supplied.Length > 1024) return Unauthorized();
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(supplied))) ? null : Unauthorized();
    }
}
