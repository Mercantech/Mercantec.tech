using System.Text.Json;

namespace Mercantec.Tech.Api.Updates.Automation;

public sealed record PlatformUpdatesObserverSnapshot(string? Phase, string? ErrorCode, DateTimeOffset? LastSeenUtc);

internal static class PlatformUpdatesObserver
{
    private static readonly HashSet<string> Phases = new(StringComparer.Ordinal)
    { "idle", "polling", "state", "resolving-source", "capturing", "analyzing", "publishing", "recording" };
    private static readonly HashSet<string> Errors = new(StringComparer.Ordinal)
    { "git_index_locked", "git_network", "browser_cache_not_writable", "browser_launch_failed",
      "service_auth_failed", "source_not_found", "capture_failed", "endpoint_unreachable",
      "web_metadata_unavailable", "api_metadata_unavailable", "request_bad_request", "request_forbidden",
      "request_not_found", "request_server_error", "response_not_json", "request_redirected", "worker_error" };
    private static readonly PlatformUpdatesObserverSnapshot Empty = new(null, null, null);

    public static PlatformUpdatesObserverSnapshot Read(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Empty;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > 4096) return Empty;
            // Recheck the bound while reading in case the file changed after opening.
            var bytes = new byte[4097];
            var count = 0;
            while (count < bytes.Length)
            {
                var read = stream.Read(bytes, count, bytes.Length - count);
                if (read == 0) break;
                count += read;
            }
            if (count > 4096) return Empty;
            using var document = JsonDocument.Parse(bytes.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 4 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Empty;
            string? phase = null, error = null;
            DateTimeOffset? seen = null;
            if (root.TryGetProperty("phase", out var phaseValue) && phaseValue.ValueKind == JsonValueKind.String
                && Phases.Contains(phaseValue.GetString()!)) phase = phaseValue.GetString();
            if (root.TryGetProperty("errorCode", out var errorValue) && errorValue.ValueKind == JsonValueKind.String
                && Errors.Contains(errorValue.GetString()!)) error = errorValue.GetString();
            if (root.TryGetProperty("updatedAtUtc", out var timeValue) && timeValue.ValueKind == JsonValueKind.String
                && timeValue.TryGetDateTimeOffset(out var time)) seen = time.ToUniversalTime();
            return new(phase, error, seen);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
            or ArgumentException or NotSupportedException)
        { return Empty; }
    }
}
