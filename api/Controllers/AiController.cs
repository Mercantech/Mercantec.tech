using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Emojis;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/ai")]
public sealed class AiController(OpenAiEditorClient openAi, AppDbContext db) : ControllerBase
{
    /// <summary>Skriveassistent til forløbseditoren med afgrænset samtalekontekst: generér, omskriv eller Mermaid.</summary>
    [HttpPost("editor")]
    public async Task<IActionResult> Editor([FromBody] EditorAiRequest? body, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        if (body is null)
            return BadRequest(new { error = "invalid_body" });

        if (!openAi.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new EditorAiResponse(
                [], "OpenAI er ikke konfigureret på serveren.", Configured: false));
        }

        try
        {
            // The client cannot supply or extend this catalogue. It contains metadata, never private image tickets.
            var request = body with { Emojis = await LoadEmojis(ct) };
            var result = await openAi.CompleteAsync(request, ct);
            // Names and availability may change while the model is answering.
            var current = request with { Emojis = await LoadEmojis(ct) };
            result = result with { Blocks = OpenAiEditorClient.SanitizeBlocks(result.Blocks, request.Action, EditorAiCatalog.From(current)) };
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "openai_upstream",
                message = "OpenAI svarede ikke korrekt. Prøv igen om lidt."
            });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { error = ex.Message, message = "AI-svaret kunne ikke omsættes til blokke." });
        }
    }

    private async Task<IReadOnlyList<EditorAiEmojiRef>> LoadEmojis(CancellationToken ct)
    {
        var rows = await db.CustomEmojis.AsNoTracking().OrderBy(emoji => emoji.Name).Take(EmojiName.MaxCount).ToListAsync(ct);
        return rows.Select(emoji =>
        {
            var metadata = EmojiMetadataPolicy.Resolve(emoji);
            return new EditorAiEmojiRef(emoji.Id.ToString(), emoji.Name, metadata.AltText, metadata.Category, metadata.Keywords);
        }).ToList();
    }
}
