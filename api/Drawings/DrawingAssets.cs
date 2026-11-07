using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Drawings;

public sealed partial class DrawingService
{
    private sealed record MediaTicket(Guid DrawingId, Guid AssetId, Guid Sub, bool Teacher, bool Admin, DateTimeOffset Expires);
    private string MediaUrl(Guid drawingId, Guid assetId, ClaimsPrincipal user)
    {
        var sub = UserClaims.GetSub(user) ?? throw DrawingValidation.Denied();
        var ticket = _tickets.Protect(JsonSerializer.Serialize(new MediaTicket(drawingId, assetId, sub, UserClaims.IsTeacher(user),
            user.Claims.Any(c => (c.Type is "role" or "roles" || c.Type == ClaimTypes.Role) && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase)), DateTimeOffset.UtcNow.AddMinutes(5))));
        return $"/api/drawings/{drawingId}/assets/{assetId}/content?ticket={Uri.EscapeDataString(ticket)}";
    }
    private DrawingFileView FileView(DrawingAsset asset, ClaimsPrincipal user) => new(asset.Id, asset.ContentType, asset.SizeBytes,
        asset.CreatedAtUtc, MediaUrl(asset.DrawingId, asset.Id, user));

    public ClaimsPrincipal? TicketViewer(Guid drawingId, Guid assetId, string? ticket, ClaimsPrincipal user)
    {
        if (string.IsNullOrEmpty(ticket)) return user.Identity?.IsAuthenticated == true ? user : null;
        if (ticket.Length > 8192) return null;
        try
        {
            var data = JsonSerializer.Deserialize<MediaTicket>(_tickets.Unprotect(ticket));
            if (data is null || data.DrawingId != drawingId || data.AssetId != assetId || data.Sub == Guid.Empty
                || data.Expires <= DateTimeOffset.UtcNow || data.Expires > DateTimeOffset.UtcNow.AddMinutes(6)) return null;
            var claims = new List<Claim> { new("sub", data.Sub.ToString()) };
            if (data.Teacher) claims.Add(new(ClaimTypes.Role, "Underviser")); if (data.Admin) claims.Add(new(ClaimTypes.Role, "Admin"));
            return new(new ClaimsIdentity(claims, "drawing-media-ticket"));
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or FormatException) { return null; }
    }

    public async Task<(DrawingAsset Asset, ObjectGetResult Content)> ReadAssetAsync(Guid drawingId, Guid assetId, ClaimsPrincipal user, CancellationToken ct)
    {
        var drawing = await FindAsync(drawingId, ct) ?? throw DrawingValidation.Denied();
        var asset = await _db.DrawingAssets.AsNoTracking().SingleOrDefaultAsync(a => a.Id == assetId && a.DrawingId == drawingId, ct);
        if (asset is null || !await _access.CanReadAssetAsync(drawing, asset, user, ct)) throw DrawingValidation.Denied();
        if (!_storage.IsConfigured) throw new DrawingException(503, "object_storage_disabled", "Fillageret er ikke tilgængeligt lige nu.");
        var content = await _storage.GetAsync(asset.Key, ct) ?? throw DrawingValidation.Denied();
        return (asset, content);
    }

    private static async Task<byte[]> ReadFileAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > DrawingValidation.MaxImageBytes) throw new DrawingException(413, "drawing_image_too_large", "Billeder må højst være 3 MiB.");
        await using var stream = file.OpenReadStream(); using var buffer = new MemoryStream();
        var bytes = new byte[81920]; int read;
        while ((read = await stream.ReadAsync(bytes, ct)) > 0)
        { if (buffer.Length + read > DrawingValidation.MaxImageBytes) throw DrawingValidation.Invalid(); await buffer.WriteAsync(bytes.AsMemory(0, read), ct); }
        if (buffer.Length != file.Length) throw DrawingValidation.Invalid();
        return buffer.ToArray();
    }

    private async Task<DrawingAsset> PutImageAsync(Guid drawingId, string name, string contentType, string kind, byte[] bytes, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!_storage.IsConfigured) throw new DrawingException(503, "object_storage_disabled", "Fillageret er ikke tilgængeligt lige nu.");
        var id = Guid.NewGuid(); var key = $"drawing-private/{drawingId:N}/{id:N}";
        using var stream = new MemoryStream(bytes, false); await _storage.PutAsync(key, stream, contentType, ct);
        return new() { Id = id, DrawingId = drawingId, Key = key, OriginalName = name, ContentType = contentType,
            Kind = kind, SizeBytes = bytes.Length, UploadedBySub = UserClaims.GetSub(user)!.Value, CreatedAtUtc = DateTimeOffset.UtcNow };
    }

    public async Task<DrawingFileView> UploadAssetAsync(Guid id, IFormFile file, ClaimsPrincipal user, CancellationToken ct)
    {
        var drawing = await FindAsync(id, ct) ?? throw DrawingValidation.Denied();
        if (!await _access.CanEditAsync(drawing, user, ct)) throw DrawingValidation.Denied();
        var name = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        var type = NotionMediaPolicy.Classify(name);
        if (name.Length is 0 or > 240 || name.Any(char.IsControl) || type?.Kind != "image"
            || type.Value.ContentType is not ("image/png" or "image/jpeg" or "image/webp" or "image/gif")) throw DrawingValidation.Invalid("unsupported_image");
        var bytes = await ReadFileAsync(file, ct);
        if (!NotionMediaPolicy.MatchesSignature(name, bytes.AsSpan(0, Math.Min(32, bytes.Length)))) throw DrawingValidation.Invalid("invalid_file_signature");
        var asset = await PutImageAsync(id, name, type.Value.ContentType, "image", bytes, user, ct);
        _db.DrawingAssets.Add(asset);
        try { await _db.SaveChangesAsync(ct); }
        catch { await _storage.DeleteAsync(asset.Key, CancellationToken.None); throw; }
        return FileView(asset, user);
    }

    public async Task<DrawingPreviews> UploadPreviewsAsync(Guid id, long revision, string fingerprint, IFormFile lightPNG, IFormFile darkPNG,
        ClaimsPrincipal user, CancellationToken ct)
    {
        var drawing = await FindAsync(id, ct) ?? throw DrawingValidation.Denied();
        if (!await _access.CanEditAsync(drawing, user, ct)) throw DrawingValidation.Denied();
        if (drawing.Revision != revision || drawing.Fingerprint != fingerprint || !DrawingValidation.Fingerprint(fingerprint)) throw DrawingValidation.Changed();
        var light = await ReadFileAsync(lightPNG, ct); var dark = await ReadFileAsync(darkPNG, ct);
        if (!DrawingValidation.IsPreviewPng(light) || !DrawingValidation.IsPreviewPng(dark)) throw DrawingValidation.Invalid("invalid_drawing_preview");
        DrawingAsset? lightAsset = null, darkAsset = null; var committed = false;
        try
        {
            lightAsset = await PutImageAsync(id, "preview-light.png", "image/png", "preview-light", light, user, ct);
            darkAsset = await PutImageAsync(id, "preview-dark.png", "image/png", "preview-dark", dark, user, ct);
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            await LockParentAsync(drawing.Domain, drawing.DocumentId, ct);
            if (await _db.Drawings.Where(d => d.Id == id && d.Revision == revision && d.Fingerprint == fingerprint && d.AccessVersion == drawing.AccessVersion)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.PreviewLightAssetId, lightAsset.Id).SetProperty(d => d.PreviewDarkAssetId, darkAsset.Id)
                    .SetProperty(d => d.PreviewRevision, revision).SetProperty(d => d.PreviewFingerprint, fingerprint), ct) != 1) throw DrawingValidation.Changed();
            _db.DrawingAssets.AddRange(lightAsset, darkAsset); await _db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); committed = true;
            return new(revision, fingerprint, MediaUrl(id, lightAsset.Id, user), MediaUrl(id, darkAsset.Id, user));
        }
        finally
        {
            if (!committed)
            {
                if (lightAsset is not null) await _storage.DeleteAsync(lightAsset.Key, CancellationToken.None);
                if (darkAsset is not null) await _storage.DeleteAsync(darkAsset.Key, CancellationToken.None);
            }
        }
    }
}
