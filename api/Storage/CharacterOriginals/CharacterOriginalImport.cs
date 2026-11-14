using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Storage.CharacterOriginals;

internal static class CharacterOriginalImport
{
    public static async Task RunAsync(IConfiguration configuration, string? sourceRoot, CancellationToken cancellationToken = default)
    {
        var options = configuration.GetRequiredSection(ObjectStorageOptions.SectionName).Get<ObjectStorageOptions>()
            ?? throw new InvalidOperationException("Object storage is not configured.");
        using var storage = new S3ObjectStorage(Options.Create(options));
        var files = CharacterOriginalCatalog.Load().Files;
        var verified = 0;

        await Parallel.ForEachAsync(files, new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = cancellationToken
        }, async (file, ct) =>
        {
            if (!await ExistsAndMatches(storage, file, ct))
            {
                if (sourceRoot is null)
                    throw new InvalidDataException($"Missing character original: {file.Path}");
                await using var source = File.OpenRead(Path.Combine(sourceRoot, file.Path));
                await Verify(source, file, ct);
                source.Position = 0;
                await storage.PutAsync(file.Key, source, "image/png", ct);
                if (!await ExistsAndMatches(storage, file, ct))
                    throw new InvalidDataException($"Character original did not persist: {file.Path}");
            }

            var count = Interlocked.Increment(ref verified);
            if (count % 100 == 0) Console.WriteLine($"Verified {count} character originals.");
        });

        Console.WriteLine($"Character original archive verified: {verified} PNGs.");
    }

    private static async Task<bool> ExistsAndMatches(IObjectStorage storage, CharacterOriginalCatalog.Original file, CancellationToken ct)
    {
        await using var result = await storage.GetAsync(file.Key, ct);
        if (result is null) return false;
        if (result.ContentLength != file.Bytes || result.ContentType != "image/png")
            throw new InvalidDataException($"Invalid stored metadata: {file.Path}");
        await Verify(result.Content, file, ct);
        return true;
    }

    private static async Task Verify(Stream stream, CharacterOriginalCatalog.Original file, CancellationToken ct)
    {
        if (stream.CanSeek && stream.Length != file.Bytes)
            throw new InvalidDataException($"Unexpected length: {file.Path}");
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
        if (!hash.Equals(file.Sha256, StringComparison.Ordinal))
            throw new InvalidDataException($"Unexpected SHA-256: {file.Path}");
    }
}
