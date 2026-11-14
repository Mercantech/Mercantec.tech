using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Api.Storage.CharacterOriginals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/internal/character-originals")]
public sealed class CharacterOriginalsController(IObjectStorage storage, CharacterOriginalCatalog catalog) : ControllerBase
{
    [HttpGet("{**path}"), HttpHead("{**path}")]
    public async Task<IActionResult> Get(string path, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (catalog.Find(path) is not { } file) return NotFound();
        if (!storage.IsConfigured) return StatusCode(StatusCodes.Status503ServiceUnavailable);

        var content = await storage.GetAsync(file.Key, ct);
        if (content is null) return NotFound();
        if (content.ContentLength is { } bytes && bytes != file.Bytes)
        {
            await content.DisposeAsync();
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        Response.RegisterForDisposeAsync(content);
        Response.Headers.CacheControl = "public, max-age=86400, no-transform";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.ContentLength = file.Bytes;
        var etag = new EntityTagHeaderValue($"\"sha256-{file.Sha256}\"");
        return File(content.Content, "image/png", lastModified: null, entityTag: etag);
    }
}
