using System.Security.Cryptography;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Emojis;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/emojis")]
public sealed class EmojisController(AppDbContext db, IObjectStorage storage, IDataProtectionProvider protection,
    OpenAiEmojiAnalysisClient? analysis = null) : ControllerBase
{
    public const int MaxBytes = EmojiName.MaxBytes;
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromHours(12);
    private static readonly DateTimeOffset CharacterEmojiUpdatedAt = new(2026, 11, 8, 0, 0, 0, TimeSpan.Zero);
    private readonly IDataProtector tickets = protection.CreateProtector("Mercantec.CustomEmoji.v1");

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        var rows = await db.CustomEmojis.AsNoTracking().OrderBy(e => e.Name).ToListAsync(ct);
        var characters = CharacterEmojiPolicy.Available(rows);
        return Ok(rows.Select(ToView).Concat(characters.Select(ToView))
            .OrderBy(emoji => emoji.Name, StringComparer.Ordinal).ToList());
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
    public async Task<IActionResult> Upload(IFormFile? file, [FromForm] string? name, CancellationToken ct,
        [FromForm] string? altText = null, [FromForm] string? category = null, [FromForm] string? keywords = null)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!storage.IsConfigured) return StatusCode(503, new { error = "object_storage_disabled" });
        var normalized = EmojiName.Normalize(name);
        if (normalized is null) return BadRequest(new { error = "invalid_name" });
        if (!TryKeywords(keywords, out var keywordValues)) return BadRequest(new { error = "invalid_metadata" });
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

        if (IsCharacterName(normalized) || await db.CustomEmojis.AnyAsync(e => e.Name == normalized, ct))
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
            AltText = EmojiMetadataPolicy.CleanText(altText, EmojiMetadataPolicy.MaxAltText),
            Category = EmojiMetadataPolicy.Category(category), KeywordsJson = JsonSerializer.Serialize(keywordValues),
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

    [HttpPost("analyze")]
    [RequestSizeLimit(MaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Analyze(IFormFile? file, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
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
        if (buffer.Length != file.Length) return BadRequest(new { error = "incomplete_upload" });
        var bytes = buffer.ToArray();
        if (!NotionMediaPolicy.MatchesSignature(original, bytes)) return BadRequest(new { error = "invalid_file_signature" });
        if (analysis?.IsConfigured != true)
            return StatusCode(503, new { error = "openai_not_configured", message = "Billedanalysen er ikke tilgængelig. Du kan udfylde felterne og gemme manuelt." });
        try
        {
            var result = await analysis.AnalyzeAsync(bytes, type.Value.ContentType, original, ct);
            var names = await db.CustomEmojis.AsNoTracking().Select(item => item.Name).ToListAsync(ct);
            names.AddRange(CharacterEmojiCatalog.All.Select(item => item.Name));
            var name = result.Name;
            for (var number = 2; names.Contains(name, StringComparer.OrdinalIgnoreCase); number++)
            {
                var suffix = "_" + number;
                name = result.Name[..Math.Min(result.Name.Length, 32 - suffix.Length)].TrimEnd('_') + suffix;
            }
            return Ok(result with { Name = name });
        }
        catch (ArgumentException)
        { return BadRequest(new { error = "invalid_file_signature", message = "Billedet kunne ikke læses. Vælg et andet billede." }); }
        catch (HttpRequestException)
        { return StatusCode(502, new { error = "analysis_failed", message = "Billedanalysen svarede ikke. Du kan udfylde felterne og gemme manuelt." }); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return StatusCode(504, new { error = "analysis_failed", message = "Billedanalysen tog for lang tid. Du kan udfylde felterne og gemme manuelt." }); }
        catch (Exception error) when (error is InvalidOperationException or JsonException or KeyNotFoundException or IndexOutOfRangeException)
        { return UnprocessableEntity(new { error = "invalid_analysis", message = "Analysen kunne ikke læses. Du kan udfylde felterne og gemme manuelt." }); }
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, RenameEmoji? request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var normalized = EmojiName.Normalize(request?.Name);
        if (normalized is null) return BadRequest(new { error = "invalid_name" });
        var keywordValues = EmojiMetadataPolicy.Keywords(request?.Keywords);
        var emoji = await db.CustomEmojis.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (emoji is null) return IsCharacterId(id) ? Forbid() : NotFound();
        if (emoji.Name != normalized && (IsCharacterName(normalized)
            || await db.CustomEmojis.AnyAsync(e => e.Name == normalized && e.Id != id, ct)))
            return Conflict(new { error = "name_taken" });
        emoji.Name = normalized;
        if (request!.AltText is not null) emoji.AltText = EmojiMetadataPolicy.CleanText(request.AltText, EmojiMetadataPolicy.MaxAltText);
        if (request.Category is not null) emoji.Category = EmojiMetadataPolicy.Category(request.Category);
        if (request.Keywords is not null) emoji.KeywordsJson = JsonSerializer.Serialize(keywordValues);
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
        if (emoji is null) return IsCharacterId(id) ? Forbid() : NotFound();
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
        var metadata = EmojiMetadataPolicy.Resolve(emoji);
        return new CustomEmojiView(emoji.Id, emoji.Name, emoji.ContentType, emoji.SizeBytes, emoji.UpdatedAtUtc, url,
            standard, metadata.Category, metadata.Keywords, metadata.AltText);
    }

    private static bool IsCharacterId(Guid id) => CharacterEmojiCatalog.All.Any(emoji => emoji.Id == id);
    private static bool IsCharacterName(string name) => CharacterEmojiCatalog.All.Any(emoji =>
        string.Equals(emoji.Name, name, StringComparison.OrdinalIgnoreCase));

    private static CustomEmojiView ToView(CharacterEmojiAsset emoji)
    {
        var metadata = CharacterEmojiPolicy.Resolve(emoji);
        return new(emoji.Id, emoji.Name, "image/webp", emoji.PreviewSizeBytes, CharacterEmojiUpdatedAt, emoji.Preview,
            true, metadata.Category, metadata.Keywords, metadata.AltText, true);
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

    private static bool TryKeywords(string? json, out string[] keywords)
    {
        keywords = [];
        if (json is null) return true;
        if (json.Length > 4096) return false;
        try { keywords = EmojiMetadataPolicy.Keywords(JsonSerializer.Deserialize<string[]>(json)); return true; }
        catch (JsonException) { return false; }
    }

    public sealed record RenameEmoji(string? Name, string? AltText = null, string? Category = null, string[]? Keywords = null);
    public sealed record CustomEmojiView(Guid Id, string Name, string ContentType, long SizeBytes, DateTimeOffset UpdatedAtUtc, string ContentUrl,
        bool IsStandard = false, string Category = "custom", string[]? Keywords = null, string AltText = "", bool IsReadOnly = false);
    private sealed record EmojiTicket(Guid EmojiId, DateTimeOffset ExpiresAtUtc);
}
