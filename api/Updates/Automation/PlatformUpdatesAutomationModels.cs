using System.Text.RegularExpressions;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Updates.Automation;

public sealed class PlatformUpdatesAutomationOptions
{
    public const string SectionName = "PlatformUpdatesAutomation";
    public string StoragePath { get; set; } = ".data/platform-updates";
    public string ServiceKeyFile { get; set; } = "/run/lup-updates/service-key";
}

public sealed record PlatformUpdatesAutomationState(string? Commit = null, string? Revision = null,
    DateTimeOffset? CapturedAtUtc = null, string Status = "empty", bool BaselineRecorded = false,
    string? LastProcessedCommit = null, string? LastProcessedRevision = null,
    int CaptureSuccessCount = 0, int ScenarioCount = 0);
public sealed record PlatformUpdatesScenario(string Id, string Title);
public sealed record PlatformUpdatesAnalyzeRequest(string BeforeCommit, string Commit, string Diff,
    string[] ChangedFiles, PlatformUpdatesScenario[] Scenarios, string[] Authors);
public sealed record PlatformUpdatesAnalyzedEntry(string Id, string Title, string Description, string? ScenarioId);
public sealed record PlatformUpdatesAnalysis(PlatformUpdatesAnalyzedEntry[] Entries);
public sealed record PlatformUpdatesPublishImage(string Alt, string Base64);
public sealed record PlatformUpdatesPublishEntry(string Id, string Title, string Description, string[] Authors,
    PlatformUpdatesPublishImage? After = null, PlatformUpdatesPublishImage? Before = null);
public sealed record PlatformUpdatesPublishRequest(string BeforeCommit, string Commit, string Revision,
    DateTimeOffset CapturedAtUtc, PlatformUpdatesPublishEntry[] Entries);
public sealed record PlatformUpdatesPublishResult(string Commit, bool AlreadyPublished, int EntryCount);
public sealed record PlatformUpdatesStoredImage(Stream Content, string ContentType);

public sealed class PlatformUpdatesAutomationException(string code, int status = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

internal static class PlatformUpdatesAutomationValidation
{
    private static readonly Regex CommitPattern = new("\\A[0-9a-f]{40}\\z", RegexOptions.CultureInvariant);
    private static readonly Regex RevisionPattern = new("\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant);
    private static readonly Regex SlugPattern = new("\\A[a-z0-9]+(?:-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant);
    public static bool IsCommit(string? value) => value is not null && CommitPattern.IsMatch(value);
    public static bool IsRevision(string? value) => value is not null && RevisionPattern.IsMatch(value);
    public static bool IsId(string? value) => value is { Length: > 0 and <= 80 } && SlugPattern.IsMatch(value);
    public static bool IsText(string? value, int maximum) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= maximum && !value.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t');
    public static string[] Authors(string[]? authors)
    {
        if (authors is null || authors.Length > 2 || authors.Any(author => author is not ("Kasper" or "Mathias")))
            throw new PlatformUpdatesAutomationException("invalid_authors");
        return authors.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }
    public static void Analyze(PlatformUpdatesAnalyzeRequest? request)
    {
        if (request is null || !IsCommit(request.BeforeCommit) || !IsCommit(request.Commit)
            || request.Diff is null || request.Diff.Length > 160_000
            || request.ChangedFiles is null || request.ChangedFiles.Length > 500
            || request.ChangedFiles.Any(file => !IsText(file, 400))
            || request.Scenarios is null || request.Scenarios.Length > 80
            || request.Scenarios.Any(scenario => scenario is null || !IsId(scenario.Id) || !IsText(scenario.Title, 200))
            || request.Scenarios.Select(scenario => scenario.Id).Distinct(StringComparer.Ordinal).Count() != request.Scenarios.Length)
            throw new PlatformUpdatesAutomationException("invalid_analysis_request");
        Authors(request.Authors);
    }
    public static void State(PlatformUpdatesAutomationState? state)
    {
        if (state is null || !IsCommit(state.Commit) || !IsRevision(state.Revision)
            || state.CapturedAtUtc is null || state.CapturedAtUtc == default(DateTimeOffset)
            || state.Status is not ("ready" or "empty")
            || state.CaptureSuccessCount < 0 || state.ScenarioCount < state.CaptureSuccessCount || state.ScenarioCount > 80
            || state.BaselineRecorded && state.CaptureSuccessCount < 1
            || state.LastProcessedCommit is not null && !IsCommit(state.LastProcessedCommit)
            || state.LastProcessedRevision is not null && !IsRevision(state.LastProcessedRevision))
            throw new PlatformUpdatesAutomationException("invalid_automation_state");
    }
}
