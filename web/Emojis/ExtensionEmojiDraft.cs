using Microsoft.AspNetCore.Components.Forms;

namespace Mercantec.Tech.Web.Emojis;

/// <summary>Only image bytes cross the extension bridge; LUP keeps authentication and uploading.</summary>
public sealed record ExtensionEmojiDraft(string? Name, string? ContentType, string? Base64)
{
    // Only expose the import login prompt anonymously, never the emoji library.
    public static bool IsImportPath(string relativePath)
    {
        if (relativePath.Length > 2048) return false;
        var parts = relativePath.Split('#', 2)[0].Split('?', 2);
        if (parts.Length != 2 || parts[0] != "emojis") return false;
        var ids = parts[1].Split('&').Select(item => item.Split('=', 2))
            .Where(item => Uri.UnescapeDataString(item[0]) == "lupImport").ToArray();
        return ids.Length == 1 && ids[0].Length == 2
            && Guid.TryParseExact(Uri.UnescapeDataString(ids[0][1]), "D", out _);
    }

    public IBrowserFile? ToBrowserFile()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 240 || Name.Any(char.IsControl)
            || string.IsNullOrEmpty(Base64) || Base64.Length > ((EmojiClient.MaxBytes + 2) / 3) * 4)
            return null;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(Base64); }
        catch (FormatException) { return null; }
        if (bytes.Length is 0 or > EmojiClient.MaxBytes) return null;
        var span = bytes.AsSpan();
        var extension = ContentType switch
        {
            "image/png" when span.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) => ".png",
            "image/jpeg" when span.StartsWith(new byte[] { 255, 216, 255 }) => ".jpg",
            "image/gif" when span.StartsWith("GIF87a"u8) || span.StartsWith("GIF89a"u8) => ".gif",
            "image/webp" when span.Length >= 12 && span[..4].SequenceEqual("RIFF"u8) && span[8..12].SequenceEqual("WEBP"u8) => ".webp",
            _ => null
        };
        if (extension is null) return null;
        var name = EmojiUploadDraft.SuggestName(Name + ".png", []);
        return new ImageFile(name + extension, ContentType!, bytes);
    }

    private sealed class ImageFile(string name, string contentType, byte[] bytes) : IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => bytes.Length;
        public string ContentType => contentType;
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Size > maxAllowedSize) throw new IOException("Billedet overskrider den tilladte størrelse.");
            return new MemoryStream(bytes, writable: false);
        }
    }
}
