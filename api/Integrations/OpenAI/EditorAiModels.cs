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
    IReadOnlyList<EditorAiMemberRef>? Members = null,
    IReadOnlyList<EditorAiTurn>? History = null)
{
    // Only the authenticated server may populate the authoritative emoji catalogue.
    [JsonIgnore]
    public IReadOnlyList<EditorAiEmojiRef> Emojis { get; init; } = [];
}

public sealed record EditorAiTurn(string Role, string Text);

public sealed record EditorAiPageRef(string Id, string Title);

public sealed record EditorAiMemberRef(string Id, string DisplayName);

public sealed record EditorAiEmojiRef(string Id, string Name, string? AltText = null,
    string? Category = null, IReadOnlyList<string>? Keywords = null);

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

/// <summary>Ids the model may use for references and custom emojis. Anything else is dropped.</summary>
public sealed class EditorAiCatalog
{
    public static EditorAiCatalog Empty { get; } = new();

    public string? HoldIdText { get; private set; }
    public Dictionary<Guid, string> Pages { get; } = new();
    public Dictionary<Guid, string> Members { get; } = new();
    public Dictionary<Guid, EditorAiEmojiRef> Emojis { get; } = new();

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

        foreach (var emoji in (request.Emojis ?? []).Take(400))
        {
            if (emoji is null || !TryGuid(emoji.Id, out var id)
                || Mercantec.Tech.Api.Emojis.EmojiName.Normalize(emoji.Name) is not { } name) continue;
            catalog.Emojis.TryAdd(id, emoji with { Id = id.ToString(), Name = name });
        }

        return catalog;
    }

    public bool TryPage(string? value, out string id) => TryMapped(Pages, value, out id);

    public bool TryEmoji(string? value, out EditorAiEmojiRef emoji)
    {
        emoji = null!;
        return TryGuid(value, out var id) && Emojis.TryGetValue(id, out emoji!);
    }

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
