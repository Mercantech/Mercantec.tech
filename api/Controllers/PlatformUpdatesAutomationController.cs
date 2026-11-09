using Mercantec.Tech.Api.Updates.Automation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/platform-updates")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlatformUpdatesAutomationController(PlatformUpdatesAutomationKey key,
    PlatformUpdatesAutomationStore store, PlatformUpdatesAutomationAnalyzer analyzer, IDeploymentRevision deployment) : ControllerBase
{
    [HttpGet("deployment")]
    public IActionResult Deployment()
    {
        NoStore();
        var state = store.ReadState();
        var revision = deployment.Read();
        var heartbeat = store.ReadObserver();
        return Ok(new { revision, automationReady = revision is not null && state.BaselineRecorded && state.Revision == revision,
            captureSuccessCount = state.CaptureSuccessCount, scenarioCount = state.ScenarioCount,
            observer = new { phase = heartbeat.Phase, errorCode = heartbeat.ErrorCode, lastSeenUtc = heartbeat.LastSeenUtc } });
    }

    [HttpGet("automation/state")]
    public IActionResult State()
    {
        NoStore();
        return Authorized() ? Ok(store.ReadState()) : Unauthorized();
    }

    [HttpPost("automation/state"), RequestSizeLimit(32_768)]
    public IActionResult State([FromBody] PlatformUpdatesAutomationState state)
    {
        NoStore();
        if (!Authorized()) return Unauthorized();
        try
        {
            PlatformUpdatesAutomationValidation.State(state);
            if (deployment.Read() is not { } revision) return StatusCode(503, new { error = "deployment_revision_unavailable" });
            if (revision != state.Revision) return Conflict(new { error = "deployment_revision_changed" });
            return Ok(store.RecordState(state));
        }
        catch (PlatformUpdatesAutomationException error) { return StatusCode(error.Status, new { error = error.Code }); }
    }

    [HttpPost("automation/analyze"), RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Analyze([FromBody] PlatformUpdatesAnalyzeRequest request, CancellationToken ct)
    {
        NoStore();
        if (!Authorized()) return Unauthorized();
        try { return Ok(await analyzer.AnalyzeAsync(request, ct)); }
        catch (PlatformUpdatesAutomationException error) { return StatusCode(error.Status, new { error = error.Code }); }
        catch (HttpRequestException) { return StatusCode(502, new { error = "openai_unavailable" }); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return StatusCode(504, new { error = "openai_timeout" }); }
    }

    [HttpPost("automation/publish"), RequestSizeLimit(32 * 1024 * 1024)]
    public async Task<IActionResult> Publish([FromBody] PlatformUpdatesPublishRequest request, CancellationToken ct)
    {
        NoStore();
        if (!Authorized()) return Unauthorized();
        if (deployment.Read() is not { } revision) return StatusCode(503, new { error = "deployment_revision_unavailable" });
        if (request is null || revision != request.Revision) return Conflict(new { error = "deployment_revision_changed" });
        try { return Ok(await store.PublishAsync(request, ct)); }
        catch (PlatformUpdatesAutomationException error) { return StatusCode(error.Status, new { error = error.Code }); }
    }

    private bool Authorized() => Request.Headers.TryGetValue("X-LUP-Updates-Key", out var values)
        && values.Count == 1 && key.IsAuthorized(values[0]);
    private void NoStore() => Response.Headers.CacheControl = "no-store";
}
