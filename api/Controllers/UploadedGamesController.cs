using System.Security.Cryptography;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/me/games")]
public sealed class UploadedGamesController(AppDbContext db, IObjectStorage storage) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid subject) return Unauthorized();
        return Ok(await db.UploadedGames.AsNoTracking().Where(game => game.OwnerSub == subject && game.FileAsset.OwnerSub == subject)
            .OrderByDescending(game => game.CreatedAtUtc).Take(50)
            .Select(game => new UploadedGameView(game.Id, game.FileAssetId, game.Title,
                game.FileAsset.OriginalName, game.EntryPoint, new DateTimeOffset(DateTime.SpecifyKind(game.CreatedAtUtc, DateTimeKind.Utc)))).ToArrayAsync(ct));
    }

    [HttpPost]
    [RequestSizeLimit(FilesController.MaxUploadBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = FilesController.MaxUploadBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, [FromForm] string? title,
        [FromForm] Guid requestId, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid subject) return Unauthorized();
        if (Request.Headers.TryGetValue("X-LUP-Game-Subject", out var expected)
            && (!Guid.TryParse(expected.ToString(), out var expectedSubject) || expectedSubject != subject))
            return Conflict(new { error = "account_changed" });
        if (!storage.IsConfigured) return StatusCode(503, new { error = "object_storage_disabled" });
        title = title?.Trim();
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(title) || title.Length > 120 || title.Any(char.IsControl))
            return BadRequest(new { error = "invalid_game" });
        if (file is null || file.Length <= 0) return BadRequest(new { error = "missing_file" });
        if (file.Length > FilesController.MaxUploadBytes) return BadRequest(new { error = "file_too_large" });
        var name = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        if (name.Length is 0 or > 240 || name.Any(char.IsControl)
            || Path.GetExtension(name).ToLowerInvariant() is not (".zip" or ".html" or ".htm"))
            return BadRequest(new { error = "unsupported_game_format" });

        var temporary = Path.Combine(Path.GetTempPath(), $"lup-game-{Guid.NewGuid():N}");
        await using var content = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await using (var source = file.OpenReadStream())
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                if (content.Length + read > FilesController.MaxUploadBytes) return BadRequest(new { error = "file_too_large" });
                await content.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        if (content.Length != file.Length) return BadRequest(new { error = "incomplete_upload" });
        content.Position = 0;
        var digest = Convert.ToHexStringLower(await SHA256.HashDataAsync(content, ct));
        content.Position = 0;
        var previous = await Existing(subject, requestId, ct);
        if (previous is not null) return Repeat(previous, title, name, digest);

        var id = Guid.NewGuid();
        var mime = Path.GetExtension(name).Equals(".zip", StringComparison.OrdinalIgnoreCase) ? "application/zip" : "text/html";
        var asset = new FileAsset
        {
            Id = id, OwnerSub = subject, OriginalName = name, ContentType = mime, SizeBytes = content.Length,
            Key = $"files/{id:N}/game{Path.GetExtension(name).ToLowerInvariant()}", CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var written = new List<string> { asset.Key };
        var saved = false;
        try
        {
            await storage.PutAsync(asset.Key, content, mime, ct);
            var build = await new WebBuildService(storage).PrepareAsync(asset, null, ct, written);
            var game = new UploadedGame
            {
                Id = Guid.NewGuid(), RequestId = requestId, OwnerSub = subject, FileAssetId = asset.Id, FileAsset = asset,
                Title = title, ContentSha256 = digest, EntryPoint = build.EntryPoint, CreatedAtUtc = asset.CreatedAtUtc.UtcDateTime
            };
            db.UploadedGames.Add(game);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                previous = await Existing(subject, requestId, ct);
                if (previous is not null) return Repeat(previous, title, name, digest);
                throw;
            }
            saved = true;
            return Created($"/api/me/games/{game.Id:D}", View(game));
        }
        catch (InvalidDataException error)
        {
            var code = error.Message is "too_many_entries" or "unsafe_archive_path" or "duplicate_archive_path"
                or "expanded_build_too_large" or "no_web_entrypoint" or "invalid_archive_length" or "invalid_file_length"
                ? error.Message : "invalid_game_archive";
            return BadRequest(new { error = code });
        }
        finally
        {
            if (!saved)
                foreach (var key in written.Distinct())
                    try { await storage.DeleteAsync(key, CancellationToken.None); }
                    catch { /* Preserve the original failure; unregistered objects cannot be served. */ }
        }
    }

    private Task<UploadedGame?> Existing(Guid subject, Guid request, CancellationToken ct) => db.UploadedGames
        .AsNoTracking().Include(game => game.FileAsset).SingleOrDefaultAsync(game => game.OwnerSub == subject && game.RequestId == request, ct);

    private IActionResult Repeat(UploadedGame game, string title, string name, string digest) =>
        game.Title == title && game.FileAsset.OriginalName == name && game.ContentSha256 == digest
            ? Ok(View(game)) : Conflict(new { error = "request_conflict" });

    private static UploadedGameView View(UploadedGame game) => new(game.Id, game.FileAssetId, game.Title,
        game.FileAsset.OriginalName, game.EntryPoint, new DateTimeOffset(DateTime.SpecifyKind(game.CreatedAtUtc, DateTimeKind.Utc)));
}
