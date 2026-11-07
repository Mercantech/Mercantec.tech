namespace Mercantec.Tech.Api.Storage;

public readonly record struct NotionMediaType(string ContentType, string Kind);

public static class NotionMediaPolicy
{
    public static bool IsPrivateKey(string? key) => !string.IsNullOrEmpty(key)
        && key.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals("notion-private", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("notebook-private", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("drawing-private", StringComparison.OrdinalIgnoreCase));

    public static NotionMediaType? Classify(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".mp4" or ".m4v" => new("video/mp4", "video"),
        ".webm" => new("video/webm", "video"),
        ".mp3" => new("audio/mpeg", "audio"),
        ".wav" => new("audio/wav", "audio"),
        ".ogg" or ".oga" => new("audio/ogg", "audio"),
        ".png" => new("image/png", "image"),
        ".jpg" or ".jpeg" => new("image/jpeg", "image"),
        ".gif" => new("image/gif", "image"),
        ".webp" => new("image/webp", "image"),
        ".pdf" => new("application/pdf", "pdf"),
        ".zip" => new("application/zip", "download"),
        ".docx" => new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "download"),
        ".xlsx" => new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "download"),
        ".pptx" => new("application/vnd.openxmlformats-officedocument.presentationml.presentation", "download"),
        ".txt" or ".md" or ".csv" => new("text/plain", "download"),
        _ => null
    };

    public static bool MatchesSignature(string fileName, ReadOnlySpan<byte> bytes)
    {
        static bool Starts(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix) => value.StartsWith(prefix);
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".mp4" or ".m4v" => bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8),
            ".webm" => Starts(bytes, [0x1a, 0x45, 0xdf, 0xa3]),
            ".mp3" => Starts(bytes, "ID3"u8) || bytes.Length >= 2 && bytes[0] == 0xff && (bytes[1] & 0xe0) == 0xe0,
            ".wav" => bytes.Length >= 12 && Starts(bytes, "RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WAVE"u8),
            ".ogg" or ".oga" => Starts(bytes, "OggS"u8),
            ".png" => Starts(bytes, [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
            ".jpg" or ".jpeg" => Starts(bytes, [0xff, 0xd8, 0xff]),
            ".gif" => Starts(bytes, "GIF87a"u8) || Starts(bytes, "GIF89a"u8),
            ".webp" => bytes.Length >= 12 && Starts(bytes, "RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
            ".pdf" => Starts(bytes, "%PDF-"u8),
            ".zip" or ".docx" or ".xlsx" or ".pptx" => Starts(bytes, [0x50, 0x4b, 0x03, 0x04]) || Starts(bytes, [0x50, 0x4b, 0x05, 0x06]),
            ".txt" or ".md" or ".csv" => true,
            _ => false
        };
    }
}
