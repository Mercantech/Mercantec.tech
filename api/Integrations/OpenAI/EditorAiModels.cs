using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mercantec.Tech.Api.Integrations.OpenAI;

public sealed record EditorAiRequest(
    string Action,
    string Prompt,
    string? SelectionText = null,
    string? PageTitle = null);

public sealed record EditorAiResponse(
    IReadOnlyList<EditorAiBlock> Blocks,
    string? AssistantNote = null,
    bool Configured = true);

public sealed class EditorAiBlock
{
    public string Type { get; set; } = "paragraph";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonElement>? Props { get; set; }

    /// <summary>Plain string or inline content — forwarded to BlockNote after type whitelist.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Content { get; set; }
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
