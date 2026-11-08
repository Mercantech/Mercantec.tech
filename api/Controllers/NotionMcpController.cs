using Mercantec.Tech.Api.Mcp;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/notion/pages/{pageId:guid}/codex-changes")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotionMcpController(NotionMcpService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(Guid pageId, CancellationToken ct) => Execute(async () => Ok(await service.ListAsync(pageId, User, ct)));

    [HttpGet("{operationId:guid}")]
    public Task<IActionResult> Get(Guid pageId, Guid operationId, CancellationToken ct) => Execute(async () =>
    {
        var view = await service.GetAsync(operationId, User, ct);
        return view.PageId == pageId ? Ok(view) : NotFound();
    });

    [HttpPost("{operationId:guid}/undo")]
    public Task<IActionResult> Undo(Guid pageId, Guid operationId, UndoNotionMcpChange request, CancellationToken ct) => Execute(async () =>
    {
        if ((await service.GetAsync(operationId, User, ct)).PageId != pageId) return NotFound();
        var token = NotionMcpEndpoints.Bearer(Request);
        if (token == null) return Unauthorized();
        return Ok(await service.UndoAsync(operationId, request.ReversalId, User, token, ct));
    });

    private static async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (NotionMcpException error) { return new ObjectResult(new { error = error.Code, detail = error.Detail }) { StatusCode = error.Status }; }
    }
}

[ApiController, AllowAnonymous, Route("api/internal/notion/mcp/changes")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotionMcpStorageController(NotionMcpService service, NotebookServiceKey key) : ControllerBase
{
    [HttpPost("prepare"), RequestSizeLimit(32 * 1024 * 1024)]
    public async Task<IActionResult> Prepare(CancellationToken ct)
    {
        if (!key.Accepts(Request)) return Unauthorized();
        // Page is owned by the previously authorized, durable operation intent, not by this body.
        try
        {
            if (Request.ContentType?.Split(';')[0].Trim() != "application/json") return StatusCode(415);
            var request = await Request.ReadFromJsonAsync<PrepareNotionMcpChange>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 512 }, ct);
            if (request == null) return BadRequest(new { error = "invalid_audit" });
            // The collaboration service passes pageId alongside the audit to bind its live room.
            if (!Guid.TryParse(Request.Query["pageId"], out var pageId)) return BadRequest(new { error = "invalid_page" });
            await service.PrepareAsync(pageId, request, ct);
            return Ok(new { prepared = request.OperationId });
        }
        catch (JsonException) { return BadRequest(new { error = "invalid_audit" }); }
        catch (NotionMcpException error) { return new ObjectResult(new { error = error.Code, detail = error.Detail }) { StatusCode = error.Status }; }
    }
}
