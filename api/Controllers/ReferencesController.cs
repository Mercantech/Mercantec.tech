using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.References;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/references")]
public sealed class ReferencesController(AppDbContext db) : ControllerBase
{
    private ReferenceService Service => new(db);
    [HttpGet("search")]
    public async Task<IActionResult> Search(CancellationToken ct, string? q = null, string? sourceKind = null,
        string? sourceId = null, Guid? holdId = null, int limit = 30, int offset = 0, bool publishedOnly = false, bool contentOnly = false)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (q?.Length > 200 || limit is < 1 or > 100 || offset is < 0 or > 100000) return BadRequest(new { error = "invalid_search" });
        NoCache();
        return Ok(await Service.SearchAsync(User, q, Source(sourceKind, sourceId), holdId, offset, limit, publishedOnly, ct, contentOnly));
    }

    [HttpPost("resolve")]
    public async Task<IActionResult> Resolve(ResolveReferences request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (request?.References is null || request.References.Length > 1000) return BadRequest(new { error = "invalid_references" });
        NoCache();
        return Ok(await Service.ResolveAsync(User, request.References, Source(request.SourceKind, request.SourceId), request.PublishedOnly, ct));
    }

    [HttpPost("graph")]
    public async Task<IActionResult> Graph(ReferenceGraphRequest request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (request is null || request.Offset < 0 || request.Limit is < 1 or > 400
            || request.Focus is not null && ReferenceService.Normalize(request.Focus) is null
            || request.ScopeKind is "collection" or "notebook" or "hold" && (!Guid.TryParseExact(request.ScopeId, "D", out var scope) || scope == Guid.Empty)
            || request.ScopeKind is not (null or "all" or "collection" or "notebook" or "hold"))
            return BadRequest(new { error = "invalid_graph" });
        NoCache();
        return Ok(await Service.GraphAsync(User, request, ct));
    }

    [HttpGet("backlinks")]
    public async Task<IActionResult> Backlinks(string kind, string id, CancellationToken ct, bool publishedOnly = false)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (ReferenceService.Normalize(new(kind, id)) is null) return BadRequest(new { error = "invalid_reference" });
        NoCache();
        return Ok(await Service.BacklinksAsync(User, new(kind, id), publishedOnly, ct));
    }

    [HttpGet("people/{sub:guid}")]
    public async Task<IActionResult> Person(Guid sub, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        var person = await Service.PersonAsync(User, sub, ct);
        NoCache(); return person is null ? NotFound() : Ok(person);
    }

    private static EntityReference? Source(string? kind, string? id) => kind is not null && id is not null ? ReferenceService.Normalize(new(kind, id)) : null;
    private void NoCache() => Response.Headers.CacheControl = "private, no-store";
}
