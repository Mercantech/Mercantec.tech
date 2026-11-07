using System.Security.Cryptography;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Emojis;
using Mercantec.Tech.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/emojis")]
public sealed class EmojisController(AppDbContext db, IObjectStorage storage, IDataProtectionProvider protection) : ControllerBase
{
    public const int MaxBytes = EmojiName.MaxBytes;
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromHours(12);
    private readonly IDataProtector tickets = protection.CreateProtector("Mercantec.CustomEmoji.v1");

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        var rows = await db.CustomEmojis.AsNoTracking().OrderBy(e => e.Name).ToListAsync(ct);
        return Ok(rows.Select(ToView).ToList());
    }

    [AllowAnonymous, HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct, string? ticket = null)
    {
        if (!CanView(id, ticket)) return Unauthorized();
        var emoji = await db.CustomEmojis.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        if (emoji is null) return NotFound();
        Stream content;
        if (StandardEmojiAssets.IsStandard(emoji.Key))
        {
            var embedded = StandardEmojiAssets.Open(emoji.Key);
            if (embedded is null) return NotFound();
            content = embedded;
        }
        else
        {
            if (!storage.IsConfigured) return StatusCode(503, new { error = "object_storage_disabled" });
            var obj = await storage.GetAsync(emoji.Key, ct);
            if (obj is null) return NotFound();
            Response.RegisterForDisposeAsync(obj);
            content = obj.Content;
        }
        Response.Headers.CacheControl = "private, max-age=3600";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
        {
            FileNameStar = emoji.Name + Extension(emoji.ContentType)
        }.ToString();
        return File(content, emoji.ContentType);
    }

    [HttpPost]
    [RequestSizeLimit(MaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, [FromForm] string? name, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!storage.IsConfigured) return StatusCode(503, new { error = "object_storage_disabled" });
        var normalized = EmojiName.Normalize(name);
        if (normalized is null) return BadRequest(new { error = "invalid_name" });
        if (file is null || file.Length == 0) return BadRequest(new { error = "missing_file" });
        if (file.Length > MaxBytes) return BadRequest(new { error = "file_too_large", maxBytes = MaxBytes });
        var original = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        if (string.IsNullOrWhiteSpace(original) || original.Length > 240 || original.Any(char.IsControl))
            return BadRequest(new { error = "invalid_filename" });
        var type = NotionMediaPolicy.Classify(original);
        if (type is not { Kind: "image" }) return BadRequest(new { error = "unsupported_file_type" });

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes) return BadRequest(new { error = "file_too_large", maxBytes = MaxBytes });
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        if (buffer.Length == 0 || buffer.Length != file.Length) return BadRequest(new { error = "incomplete_upload" });
        if (!NotionMediaPolicy.MatchesSignature(original, buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))
            return BadRequest(new { error = "invalid_file_signature" });

        if (await db.CustomEmojis.AnyAsync(e => e.Name == normalized, ct))
            return Conflict(new { error = "name_taken" });
        if (await db.CustomEmojis.CountAsync(ct) >= EmojiName.MaxCount)
            return Conflict(new { error = "emoji_limit" });

        var id = Guid.NewGuid();
        var key = $"emojis/{id:N}";
        buffer.Position = 0;
        await storage.PutAsync(key, buffer, type.Value.ContentType, ct);
        var now = DateTimeOffset.UtcNow;
        var emoji = new CustomEmoji
        {
            Id = id, Name = normalized, Key = key, ContentType = type.Value.ContentType,
            SizeBytes = buffer.Length, CreatedBySub = sub, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.CustomEmojis.Add(emoji);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(emoji).State = EntityState.Detached;
            await storage.DeleteAsync(key, CancellationToken.None);
            if (await db.CustomEmojis.AnyAsync(e => e.Name == normalized, CancellationToken.None))
                return Conflict(new { error = "name_taken" });
            throw;
        }
        return Ok(ToView(emoji));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, RenameEmoji? request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var normalized = EmojiName.Normalize(request?.Name);
        if (normalized is null) return BadRequest(new { error = "invalid_name" });
        var emoji = await db.CustomEmojis.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (emoji is null) return NotFound();
        if (emoji.Name == normalized) return Ok(ToView(emoji));
        if (await db.CustomEmojis.AnyAsync(e => e.Name == normalized && e.Id != id, ct))
            return Conflict(new { error = "name_taken" });
        emoji.Name = normalized;
        emoji.UpdatedAtUtc = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "name_taken" });
        }
        return Ok(ToView(emoji));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var emoji = await db.CustomEmojis.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (emoji is null) return NotFound();
        var key = emoji.Key;
        db.CustomEmojis.Remove(emoji);
        await db.SaveChangesAsync(ct);
        if (!StandardEmojiAssets.IsStandard(key))
            await storage.DeleteAsync(key, CancellationToken.None);
        return NoContent();
    }

    private CustomEmojiView ToView(CustomEmoji emoji)
    {
        var expires = DateTimeOffset.UtcNow.Add(TicketLifetime);
        var token = tickets.Protect(JsonSerializer.Serialize(new EmojiTicket(emoji.Id, expires)));
        var url = $"/api/emojis/{emoji.Id}/content?ticket={Uri.EscapeDataString(token)}";
        var standard = StandardEmojiAssets.IsStandard(emoji.Key);
        var metadata = StandardEmojiMetadata.Find(emoji.Key);
        return new CustomEmojiView(emoji.Id, emoji.Name, emoji.ContentType, emoji.SizeBytes, emoji.UpdatedAtUtc, url,
            standard, metadata?.Category ?? (standard ? "symbols" : "custom"), metadata?.Keywords ?? []);
    }

    private bool CanView(Guid id, string? ticket)
    {
        if (User.Identity?.IsAuthenticated == true && UserClaims.GetSub(User) is not null) return true;
        if (string.IsNullOrEmpty(ticket) || ticket.Length > 4096) return false;
        try
        {
            var data = JsonSerializer.Deserialize<EmojiTicket>(tickets.Unprotect(ticket));
            return data is not null && data.EmojiId == id && data.ExpiresAtUtc > DateTimeOffset.UtcNow
                && data.ExpiresAtUtc <= DateTimeOffset.UtcNow.Add(TicketLifetime).AddMinutes(2);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { return false; }
    }

    private static string Extension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        _ => ""
    };

    public sealed record RenameEmoji(string? Name);
    public sealed record CustomEmojiView(Guid Id, string Name, string ContentType, long SizeBytes, DateTimeOffset UpdatedAtUtc, string ContentUrl,
        bool IsStandard = false, string Category = "custom", string[]? Keywords = null);
    private sealed record EmojiTicket(Guid EmojiId, DateTimeOffset ExpiresAtUtc);
}
