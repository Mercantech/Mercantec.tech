using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mercantec.Tech.Api.Integrations.OpenAI;

public sealed record EditorAiRequest(
    string Action,
    string Prompt,
    string? SelectionText = null,
    string? PageTitle = null,
    string? HoldId = null,
    IReadOnlyList<EditorAiPageRef>? Pages = null,
    IReadOnlyList<EditorAiMemberRef>? Members = null);

public sealed record EditorAiPageRef(string Id, string Title);

public sealed record EditorAiMemberRef(string Id, string DisplayName);

public sealed record EditorAiResponse(
    IReadOnlyList<EditorAiBlock> Blocks,
    string? AssistantNote = null,
    bool Configured = true);

public sealed class EditorAiBlock
{
    public string Type { get; set; } = "paragraph";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonElement>? Props { get; set; }

    /// <summary>Plain string, inline content or tableContent — forwarded to BlockNote after the whitelist.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Content { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<EditorAiBlock>? Children { get; set; }
}

/// <summary>Ids the model may use for pageLink, pageMention and studentMention. Anything else is dropped.</summary>
public sealed class EditorAiCatalog
{
    public static EditorAiCatalog Empty { get; } = new();

    public string? HoldIdText { get; private set; }
    public Dictionary<Guid, string> Pages { get; } = new();
    public Dictionary<Guid, string> Members { get; } = new();

    public static EditorAiCatalog From(EditorAiRequest request)
    {
        var catalog = new EditorAiCatalog();
        if (TryGuid(request.HoldId, out _))
            catalog.HoldIdText = request.HoldId!.Trim();

        if (request.Pages is not null)
        {
            foreach (var page in request.Pages.Take(80))
            {
                if (page is null || !TryGuid(page.Id, out var id)) continue;
                catalog.Pages.TryAdd(id, page.Id.Trim());
            }
        }

        if (request.Members is not null)
        {
            foreach (var member in request.Members.Take(120))
            {
                if (member is null || !TryGuid(member.Id, out var id)) continue;
                catalog.Members.TryAdd(id, member.Id.Trim());
            }
        }

        return catalog;
    }

    public bool TryPage(string? value, out string id) => TryMapped(Pages, value, out id);

    public bool TryMember(string? value, out string id)
    {
        if (HoldIdText is null)
        {
            id = "";
            return false;
        }

        return TryMapped(Members, value, out id);
    }

    private static bool TryMapped(Dictionary<Guid, string> map, string? value, out string id)
    {
        id = "";
        if (!TryGuid(value, out var parsed) || !map.TryGetValue(parsed, out var stored))
            return false;
        id = stored;
        return true;
    }

    private static bool TryGuid(string? value, out Guid id) =>
        Guid.TryParse(value, out id) && id != Guid.Empty;
}

internal sealed class OpenAiChatResponse
{
    [JsonPropertyName("choices")]
    public List<OpenAiChoice>? Choices { get; set; }
}

internal sealed class OpenAiChoice
{
    [JsonPropertyName("message")]
    public OpenAiMessage? Message { get; set; }
}

internal sealed class OpenAiMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public sealed class ModelEditorPayload
{
    [JsonPropertyName("blocks")]
    public List<EditorAiBlock>? Blocks { get; set; }

    [JsonPropertyName("assistantNote")]
    public string? AssistantNote { get; set; }
}
