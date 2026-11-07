using Mercantec.Tech.Api.Drawings;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, AllowAnonymous, DrawingErrors, Route("api/internal/drawings")]
public sealed class DrawingStorageController(DrawingService service, NotebookServiceKey key) : ControllerBase
{
    [HttpGet("{id:guid}/state")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        if (Request.Headers.ContainsKey("Origin") || !key.Accepts(Request)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        var drawing = await service.FindAsync(id, ct); if (drawing is null) return NotFound();
        return drawing.YjsState is { Length: > 0 } state ? File(state, "application/octet-stream") : NoContent();
    }
    [HttpPut("{id:guid}/state"), RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Store(Guid id, StoreDrawingDocument request, CancellationToken ct)
    {
        if (Request.Headers.ContainsKey("Origin") || !key.Accepts(Request)) return Unauthorized();
        return Ok(await service.StoreAsync(id, request, ct));
    }
}
