using System.IO.Compression;
using System.Text.Json;
using Mercantec.Tech.Api.Data.Entities;

namespace Mercantec.Tech.Api.Storage;

public sealed class WebBuildService(IObjectStorage storage)
{
    private const long MaxExpandedBytes = 512L * 1024 * 1024;
    private const int MaxEntries = 5000;

    private static readonly SemaphoreSlim Preparation = new(1, 1);

    public async Task<WebBuildManifest> PrepareAsync(FileAsset asset, string? requestedEntry, CancellationToken ct, ICollection<string>? writtenKeys = null)
    {
        await Preparation.WaitAsync(ct);
        try { return await PrepareCoreAsync(asset, requestedEntry, ct, writtenKeys); }
        finally { Preparation.Release(); }
    }

    private async Task<WebBuildManifest> PrepareCoreAsync(FileAsset asset, string? requestedEntry, CancellationToken ct, ICollection<string>? writtenKeys)
    {
        var manifestKey = $"web-builds/{asset.Id:N}/manifest.json";
        await using (var existing = await storage.GetAsync(manifestKey, ct))
        {
            if (existing is not null)
            {
                var saved = await JsonSerializer.DeserializeAsync<WebBuildManifest>(existing.Content, cancellationToken: ct);
                if (saved is not null && (requestedEntry is null || requestedEntry == saved.EntryPoint)) return saved;
            }
        }
        await using var original = await storage.GetAsync(asset.Key, ct);
        if (original is null) throw new InvalidDataException("missing_original");
        var entries = new List<WebBuildEntry>();
        string entryPoint;
        if (Path.GetExtension(asset.OriginalName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"mercantec-build-{Guid.NewGuid():N}");
            await using var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            await CopyBounded(original.Content, temp, FilesLimit, ct);
            temp.Position = 0;
            using var archive = new ZipArchive(temp, ZipArchiveMode.Read);
            if (archive.Entries.Count > MaxEntries) throw new InvalidDataException("too_many_entries");
            var fileEntries = archive.Entries.Where(e => !e.FullName.EndsWith('/')).ToArray();
            long expanded = 0;
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.TrimEnd('/');
                if (!IsSafePath(name) || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000)
                    throw new InvalidDataException("unsafe_archive_path");
                if (entry.FullName.EndsWith('/')) continue;
                if (!paths.Add(name)) throw new InvalidDataException("duplicate_archive_path");
                if (entry.Length > MaxExpandedBytes - expanded) throw new InvalidDataException("expanded_build_too_large");
                expanded += entry.Length;
            }
            entryPoint = requestedEntry ?? fileEntries.Where(e => Path.GetFileName(e.FullName)
                    .Equals("index.html", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName.Count(c => c == '/')).ThenBy(e => e.FullName, StringComparer.Ordinal)
                .Select(e => e.FullName).FirstOrDefault()
                ?? (fileEntries.Count(e => IsHtml(e.FullName)) == 1 ? fileEntries.Single(e => IsHtml(e.FullName)).FullName : null)
                ?? throw new InvalidDataException("no_web_entrypoint");
            if (!paths.Contains(entryPoint) || !IsHtml(entryPoint)) throw new InvalidDataException("no_web_entrypoint");
            foreach (var entry in fileEntries)
            {
                await using var input = entry.Open();
                // S3 upload may require a seekable source; one file at a time keeps memory bounded.
                var entryPath = Path.Combine(Path.GetTempPath(), $"mercantec-entry-{Guid.NewGuid():N}");
                await using var output = new FileStream(entryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                    81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                await CopyBounded(input, output, entry.Length, ct);
                if (output.Length != entry.Length) throw new InvalidDataException("invalid_archive_length");
                output.Position = 0;
                var metadata = Metadata(entry.FullName, entry.Length);
                writtenKeys?.Add(Key(asset.Id, entry.FullName));
                await storage.PutAsync(Key(asset.Id, entry.FullName), output, metadata.ContentType, ct);
                entries.Add(metadata);
            }
        }
        else if (IsHtml(asset.OriginalName))
        {
            entryPoint = "index.html";
            var tempPath = Path.Combine(Path.GetTempPath(), $"mercantec-html-{Guid.NewGuid():N}");
            await using var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            await CopyBounded(original.Content, temp, FilesLimit, ct);
            if (temp.Length != asset.SizeBytes) throw new InvalidDataException("invalid_file_length");
            temp.Position = 0;
            writtenKeys?.Add(Key(asset.Id, entryPoint));
            await storage.PutAsync(Key(asset.Id, entryPoint), temp, "text/html; charset=utf-8", ct);
            entries.Add(new(entryPoint, asset.SizeBytes, "text/html; charset=utf-8", null));
        }
        else throw new InvalidDataException("not_web_build");

        var manifest = new WebBuildManifest(entryPoint, entries.ToArray());
        await using var json = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(manifest));
        // Publish the manifest last. Incomplete preparation never exposes a partial build.
        writtenKeys?.Add(manifestKey);
        await storage.PutAsync(manifestKey, json, "application/json", ct);
        return manifest;
    }

    public async Task<WebBuildManifest?> GetManifestAsync(Guid fileId, CancellationToken ct)
    {
        await using var obj = await storage.GetAsync($"web-builds/{fileId:N}/manifest.json", ct);
        return obj is null ? null : await JsonSerializer.DeserializeAsync<WebBuildManifest>(obj.Content, cancellationToken: ct);
    }

    public static string Key(Guid fileId, string entry) => $"web-builds/{fileId:N}/files/{entry}";
    public static bool IsHtml(string name) => Path.GetExtension(name).Equals(".html", StringComparison.OrdinalIgnoreCase)
        || Path.GetExtension(name).Equals(".htm", StringComparison.OrdinalIgnoreCase);

    public static bool IsSafePath(string path) => !string.IsNullOrWhiteSpace(path) && path.Length <= 512
        && !path.StartsWith('/') && !path.Contains('\\') && !path.Contains(':')
        && !path.Contains('%') && !path.Contains('?') && !path.Contains('#') && !path.Any(char.IsControl)
        && path.Split('/').All(p => p.Length > 0 && p is not ("." or ".."));

    private const long FilesLimit = 100L * 1024 * 1024;

    private static async Task CopyBounded(Stream source, Stream target, long maxBytes, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long written = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            if (read > maxBytes - written) throw new InvalidDataException("expanded_build_too_large");
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            written += read;
        }
    }

    private static WebBuildEntry Metadata(string name, long size)
    {
        string? encoding = null;
        var plainName = name;
        if (name.EndsWith(".br", StringComparison.OrdinalIgnoreCase)) { encoding = "br"; plainName = name[..^3]; }
        else if (name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)) { encoding = "gzip"; plainName = name[..^3]; }
        var mime = Path.GetExtension(plainName).ToLowerInvariant() switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".js" or ".mjs" => "application/javascript",
            ".css" => "text/css",
            ".wasm" => "application/wasm",
            ".json" => "application/json",
            ".svg" => "image/svg+xml",
            ".woff" => "font/woff", ".woff2" => "font/woff2", ".ttf" => "font/ttf",
            _ => FileTypePolicy.Classify(plainName)?.Mime ?? "application/octet-stream"
        };
        return new(name, size, mime, encoding);
    }
}

public sealed record WebBuildManifest(string EntryPoint, WebBuildEntry[] Entries);
public sealed record WebBuildEntry(string Path, long SizeBytes, string ContentType, string? ContentEncoding);
