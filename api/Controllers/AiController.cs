using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/ai")]
public sealed class AiController(OpenAiEditorClient openAi) : ControllerBase
{
    /// <summary>Underviser-AI til forløbseditoren: generér, omskriv eller Mermaid via OpenAI-proxy.</summary>
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
            var result = await openAi.CompleteAsync(body, ct);
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
}
