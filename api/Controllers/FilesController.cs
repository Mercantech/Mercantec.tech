using System.Text;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/files")]
public sealed class FilesController(IObjectStorage storage) : ControllerBase
{
    private const long MaxUploadBytes = 100 * 1024 * 1024; // 100 MiB

    [HttpGet("status")]
    public IActionResult Status() =>
        Ok(new FileStorageStatus(storage.IsConfigured, storage.IsConfigured ? storage.Bucket : null));

    /// <summary>Upload en fil til MinIO (underviser/admin). Platform-assets og kommende afleveringer.</summary>
    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!storage.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "object_storage_disabled" });
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "missing_file" });
        if (file.Length > MaxUploadBytes)
            return BadRequest(new { error = "file_too_large", maxBytes = MaxUploadBytes });

        var safeName = SanitizeFileName(file.FileName);
        var key = $"{DateTime.UtcNow:yyyy}/{DateTime.UtcNow:MM}/{Guid.NewGuid():N}-{safeName}";
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        await using var stream = file.OpenReadStream();
        var put = await storage.PutAsync(key, stream, contentType, ct);
        var download = await storage.GetPresignedGetUrlAsync(key, TimeSpan.FromHours(1), ct);

        return Ok(new FileUploadResult(put.Key, put.Bucket, put.ContentType, file.Length, download?.ToString()));
    }

    /// <summary>Hent fil via API (auth). Streamer fra MinIO — MinIO behøver ikke være offentligt eksponeret.</summary>
    [HttpGet("{*key}")]
    public async Task<IActionResult> Download(string key, CancellationToken ct)
    {
        if (!storage.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "object_storage_disabled" });
        if (string.IsNullOrWhiteSpace(key) || key.Contains("..", StringComparison.Ordinal))
            return BadRequest(new { error = "invalid_key" });

        var obj = await storage.GetAsync(key, ct);
        if (obj is null) return NotFound();

        Response.RegisterForDisposeAsync(obj);
        if (obj.ContentLength is long len)
            Response.ContentLength = len;

        return File(obj.Content, obj.ContentType, enableRangeProcessing: true);
    }

    private static string SanitizeFileName(string? name)
    {
        var raw = Path.GetFileName(name ?? "file");
        if (string.IsNullOrWhiteSpace(raw)) raw = "file";
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')
                sb.Append(c);
            else
                sb.Append('_');
        }

        var cleaned = sb.ToString().Trim('.', '_', '-');
        return string.IsNullOrWhiteSpace(cleaned) ? "file" : cleaned[..Math.Min(cleaned.Length, 120)];
    }

    public sealed record FileStorageStatus(bool Enabled, string? Bucket);
    public sealed record FileUploadResult(string Key, string Bucket, string ContentType, long SizeBytes, string? DownloadUrl);
}
