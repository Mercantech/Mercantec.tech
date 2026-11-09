using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mercantec.Tech.Api.Updates.Automation;

public interface IDeploymentRevision
{
    string? Read();
}

/// <summary>Content identity of the exact source inputs built into this container.</summary>
public sealed class DeploymentRevision(IConfiguration configuration) : IDeploymentRevision
{
    private readonly string _filename = configuration["PlatformUpdatesAutomation:DeploymentInfoPath"]
        ?? Path.Combine(AppContext.BaseDirectory, "release.json");

    public string? Read()
    {
        try
        {
            using var stream = File.OpenRead(_filename);
            if (stream.Length > 4096) return null;
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("revision", out var property)) return null;
            var revision = property.GetString();
            return revision is not null && Regex.IsMatch(revision, "\\A[a-f0-9]{64}\\z") ? revision : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
