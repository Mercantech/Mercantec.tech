using System.Security.Cryptography;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Updates.Automation;

public sealed class PlatformUpdatesAutomationStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _root;
    private readonly ILogger<PlatformUpdatesAutomationStore> _logger;

    public PlatformUpdatesAutomationStore(IOptions<PlatformUpdatesAutomationOptions> options,
        ILogger<PlatformUpdatesAutomationStore> logger)
    {
        _root = Path.GetFullPath(options.Value.StoragePath);
        _logger = logger;
    }

    public IReadOnlyList<PlatformUpdate> Entries => ReadPublications()
        .OrderByDescending(publication => publication.CapturedAtUtc).ThenByDescending(publication => publication.Commit, StringComparer.Ordinal)
        .SelectMany(publication => publication.Entries).GroupBy(entry => entry.Id, StringComparer.Ordinal)
        .Select(group => group.First() with { Authors = group.SelectMany(entry => entry.Authors)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() })
        .OrderByDescending(entry => entry.Date).ToArray();

    public PlatformUpdatesStoredImage? OpenImage(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 160 || !id.StartsWith("auto-", StringComparison.Ordinal)) return null;
        // Only a manifest reference authorizes access. An id is never interpreted as an arbitrary path.
        foreach (var publication in ReadPublications())
        {
            if (!publication.Entries.SelectMany(entry => new[] { entry.After, entry.Before })
                    .OfType<UpdateImage>().Any(image => image.Id == id)) continue;
            try
            {
                return new(new FileStream(Path.Combine(_root, "releases", publication.Commit, id + ".png"),
                    FileMode.Open, FileAccess.Read, FileShare.Read), "image/png");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
        }
        return null;
    }

    public PlatformUpdatesAutomationState ReadState()
    {
        var filename = Path.Combine(_root, "state.json");
        try
        {
            if (!File.Exists(filename) || new FileInfo(filename).Length > 32_768) return new();
            var state = JsonSerializer.Deserialize<PlatformUpdatesAutomationState>(File.ReadAllText(filename), Json);
            PlatformUpdatesAutomationValidation.State(state);
            return state!;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or PlatformUpdatesAutomationException)
        { return new(); }
    }

    public PlatformUpdatesAutomationState RecordState(PlatformUpdatesAutomationState state)
    {
        PlatformUpdatesAutomationValidation.State(state);
        Directory.CreateDirectory(_root);
        using var lease = AcquireLock("state", CancellationToken.None).GetAwaiter().GetResult();
        var previous = ReadState();
        if (previous.CapturedAtUtc > state.CapturedAtUtc)
            throw new PlatformUpdatesAutomationException("stale_automation_state", 409);
        if (previous.BaselineRecorded && !state.BaselineRecorded)
            throw new PlatformUpdatesAutomationException("baseline_cannot_be_removed", 409);
        state = state with { CapturedAtUtc = state.CapturedAtUtc!.Value.ToUniversalTime() };
        WriteJsonAtomic(Path.Combine(_root, "state.json"), state);
        return state;
    }

    public async Task<PlatformUpdatesAnalysis> GetOrCreateAnalysisAsync(string digest,
        Func<CancellationToken, Task<PlatformUpdatesAnalysis>> create, CancellationToken ct)
    {
        if (!PlatformUpdatesAutomationValidation.IsRevision(digest)) throw new ArgumentException("Invalid cache digest.", nameof(digest));
        var folder = Path.Combine(_root, "analysis");
        Directory.CreateDirectory(folder);
        using var lease = await AcquireLock("analysis-" + digest, ct);
        var filename = Path.Combine(folder, digest + ".json");
        if (File.Exists(filename))
        {
            try
            {
                if (new FileInfo(filename).Length <= 32_768)
                {
                    var cached = JsonSerializer.Deserialize<PlatformUpdatesAnalysis>(await File.ReadAllTextAsync(filename, ct), Json);
                    if (cached?.Entries is { Length: <= 5 }) return cached;
                }
                // A damaged result must not silently cause another paid request.
                throw new PlatformUpdatesAutomationException("analysis_cache_invalid", 503);
            }
            catch (JsonException) { throw new PlatformUpdatesAutomationException("analysis_cache_invalid", 503); }
        }
        var result = await create(ct);
        ct.ThrowIfCancellationRequested();
        WriteJsonAtomic(filename, result);
        return result;
    }

    public async Task<PlatformUpdatesPublishResult> PublishAsync(PlatformUpdatesPublishRequest request, CancellationToken ct)
    {
        if (!PlatformUpdatesAutomationValidation.IsCommit(request.BeforeCommit)
            || !PlatformUpdatesAutomationValidation.IsCommit(request.Commit)
            || !PlatformUpdatesAutomationValidation.IsRevision(request.Revision)
            || request.CapturedAtUtc == default || request.Entries is null || request.Entries.Length is < 1 or > 5)
            throw new PlatformUpdatesAutomationException("invalid_publication");
        var normalized = new List<PlatformUpdate>();
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var imageHashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var time = request.CapturedAtUtc.ToUniversalTime();
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Europe/Copenhagen").DateTime);
        foreach (var entry in request.Entries)
        {
            if (entry is null || !PlatformUpdatesAutomationValidation.IsId(entry.Id) || !ids.Add(entry.Id)
                || !PlatformUpdatesAutomationValidation.IsText(entry.Title, 100)
                || !PlatformUpdatesAutomationValidation.IsText(entry.Description, 500)
                || entry.Before is not null && entry.After is null)
                throw new PlatformUpdatesAutomationException("invalid_publication_entry");
            var authors = PlatformUpdatesAutomationValidation.Authors(entry.Authors);
            var title = entry.Title.Trim();
            var description = entry.Description.Trim();
            UpdateImage? AddImage(PlatformUpdatesPublishImage? image, string side)
            {
                if (image is null) return null;
                var bytes = PlatformUpdatesPng.Decode(image);
                var id = $"auto-{request.Commit}-{entry.Id}-{side}";
                images.Add(id, bytes);
                imageHashes.Add(id, Convert.ToHexStringLower(SHA256.HashData(bytes)));
                return new(id, image.Alt.Trim());
            }
            normalized.Add(new(entry.Id, date, title, description, AddImage(entry.After, "after"), AddImage(entry.Before, "before"))
                { Authors = authors });
        }
        var digest = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            request.BeforeCommit, request.Commit, request.Revision, CapturedAtUtc = time,
            Entries = normalized, Images = imageHashes
        }, Json)));
        var releases = Path.Combine(_root, "releases");
        Directory.CreateDirectory(releases);
        using var lease = await AcquireLock("publish-" + request.Commit, ct);
        var destination = Path.Combine(releases, request.Commit);
        var existing = ReadPublication(destination);
        if (existing is not null)
        {
            if (existing.RequestDigest != digest) throw new PlatformUpdatesAutomationException("publication_already_exists", 409);
            return new(request.Commit, true, existing.Entries.Length);
        }
        if (Directory.Exists(destination)) throw new PlatformUpdatesAutomationException("publication_manifest_invalid", 503);
        var staging = Path.Combine(releases, ".pending-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var (id, bytes) in images)
                await File.WriteAllBytesAsync(Path.Combine(staging, id + ".png"), bytes, ct);
            var publication = new StoredPublication(request.BeforeCommit, request.Commit, request.Revision,
                time, digest, normalized.ToArray());
            await File.WriteAllBytesAsync(Path.Combine(staging, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(publication, Json), ct);
            ct.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
            return new(request.Commit, false, normalized.Count);
        }
        finally
        {
            // This path is generated within the configured private release directory, never from a request.
            if (Directory.Exists(staging))
            {
                var allowed = Path.GetFullPath(releases) + Path.DirectorySeparatorChar;
                if (!Path.GetFullPath(staging).StartsWith(allowed, StringComparison.Ordinal))
                    throw new IOException("Invalid private staging directory.");
                Directory.Delete(staging, true);
            }
        }
    }

    private IEnumerable<StoredPublication> ReadPublications()
    {
        var folder = Path.Combine(_root, "releases");
        if (!Directory.Exists(folder)) yield break;
        foreach (var directory in Directory.EnumerateDirectories(folder))
        {
            if (!PlatformUpdatesAutomationValidation.IsCommit(Path.GetFileName(directory))) continue;
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
            var publication = ReadPublication(directory);
            if (publication is not null) yield return publication;
        }
    }

    private StoredPublication? ReadPublication(string directory)
    {
        try
        {
            var filename = Path.Combine(directory, "manifest.json");
            if (!File.Exists(filename) || new FileInfo(filename).Length > 32_768) return null;
            var publication = JsonSerializer.Deserialize<StoredPublication>(File.ReadAllText(filename), Json);
            if (publication is null || publication.Commit != Path.GetFileName(directory)
                || !PlatformUpdatesAutomationValidation.IsCommit(publication.Commit)
                || !PlatformUpdatesAutomationValidation.IsCommit(publication.BeforeCommit)
                || !PlatformUpdatesAutomationValidation.IsRevision(publication.Revision)
                || !PlatformUpdatesAutomationValidation.IsRevision(publication.RequestDigest)
                || publication.Entries is not { Length: > 0 and <= 5 } || publication.Entries.Any(entry => entry is null)
                || publication.Entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() != publication.Entries.Length) return null;
            foreach (var entry in publication.Entries)
            {
                if (entry is null || !PlatformUpdatesAutomationValidation.IsId(entry.Id)
                    || !PlatformUpdatesAutomationValidation.IsText(entry.Title, 100)
                    || !PlatformUpdatesAutomationValidation.IsText(entry.Description, 500)
                    || entry.Authors is null || entry.Authors.Count > 2
                    || entry.Authors.Any(author => author is not ("Kasper" or "Mathias"))
                    || entry.Before is not null && entry.After is null) return null;
                foreach (var image in new[] { entry.After, entry.Before }.OfType<UpdateImage>())
                {
                    if (image.Id != $"auto-{publication.Commit}-{entry.Id}-after"
                        && image.Id != $"auto-{publication.Commit}-{entry.Id}-before") return null;
                }
            }
            return publication;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning("Skipping an unreadable automated LUP publication");
            return null;
        }
    }

    private async Task<FileStream> AcquireLock(string name, CancellationToken ct)
    {
        var folder = Path.Combine(_root, "locks");
        Directory.CreateDirectory(folder);
        var deadline = DateTimeOffset.UtcNow.AddMinutes(3);
        for (;;)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(Path.Combine(folder, name + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline) { await Task.Delay(100, ct); }
        }
    }

    private static void WriteJsonAtomic<T>(string destination, T value)
    {
        var temporary = destination + ".pending-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(value, Json));
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed record StoredPublication(string BeforeCommit, string Commit, string Revision,
        DateTimeOffset CapturedAtUtc, string RequestDigest, PlatformUpdate[] Entries);
}
