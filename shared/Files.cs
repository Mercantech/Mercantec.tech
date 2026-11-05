namespace Mercantec.Tech.Contracts;

public sealed record UploadedFile(Guid Id, string Key, string Bucket, string ContentType,
    long SizeBytes, string? DownloadUrl, string OriginalName, string PreviewKind);

public sealed record FilePreview(Guid Id, string OriginalName, string ContentType, long SizeBytes,
    string Kind, string ContentUrl, string DownloadUrl, string? Message, DateTimeOffset ExpiresAtUtc);

public sealed record FileTextPreview(string Text, bool Truncated);
public sealed record FileArchiveEntry(string Path, long SizeBytes, bool IsDirectory);
public sealed record FileArchivePreview(FileArchiveEntry[] Entries, bool Truncated);

public sealed record FileWebPreview(string LaunchUrl, string EntryPoint);
