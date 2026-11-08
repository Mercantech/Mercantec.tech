using System.Security.Claims;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Ai.EditorRuns;

public sealed record EditorAiContextBundle(string ContextText, EditorAiSource[] Sources, EditorAiCoverage Coverage, string[]? WritableBlockIds = null,
    string? NextSectionBlockId = null, bool ActiveSectionBlocked = false);
public interface IEditorAiContextService
{
    Task<EditorAiContextBundle> BuildAsync(ClaimsPrincipal user, Guid pageId, EditorAiRunRequest request, CancellationToken ct);
    Task<bool> CanReadSourceAsync(ClaimsPrincipal user, EditorAiSource source, CancellationToken ct);
}
