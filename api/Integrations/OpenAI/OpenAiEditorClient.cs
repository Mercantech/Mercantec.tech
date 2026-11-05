using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Integrations.OpenAI;

public sealed class OpenAiEditorClient(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiEditorClient> logger)
{
    public const int MaxPromptChars = 4000;
    public const int MaxSelectionChars = 8000;

    private static readonly HashSet<string> AllowedTypes = new(StringComparer.Ordinal)
    {
        "paragraph", "heading", "bulletListItem", "numberedListItem", "checkListItem",
        "quote", "codeBlock", "learningGoal", "callout", "mermaidDiagram"
    };

    private static readonly HashSet<string> AllowedTones = new(StringComparer.OrdinalIgnoreCase)
    {
        "info", "tip", "warning", "important"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly OpenAiOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public async Task<EditorAiResponse> CompleteAsync(EditorAiRequest request, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("OpenAI er ikke konfigureret");

        var action = NormalizeAction(request.Action);
        var prompt = (request.Prompt ?? "").Trim();
        if (prompt.Length == 0)
            throw new ArgumentException("prompt_required");
        if (prompt.Length > MaxPromptChars)
            throw new ArgumentException("prompt_too_long");

        var selection = Truncate(request.SelectionText?.Trim() ?? "", MaxSelectionChars);
        if (action is "rewrite" && selection.Length == 0)
            throw new ArgumentException("selection_required");

        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-4o-mini" : _options.Model.Trim();
        var payload = new
        {
            model,
            temperature = 0.4,
            max_tokens = 2500,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt(action) },
                new { role = "user", content = UserPrompt(action, prompt, selection, request.PageTitle) }
            }
        };

        var http = httpClientFactory.CreateClient("openai");
        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());

        using var response = await http.SendAsync(message, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("OpenAI chat fejlede: {Status} {Body}", (int)response.StatusCode, Truncate(body, 400));
            throw new HttpRequestException(
                $"OpenAI fejlede ({(int)response.StatusCode})", null, response.StatusCode);
        }

        var chat = JsonSerializer.Deserialize<OpenAiChatResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("empty_openai_response");
        var content = chat.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("empty_openai_content");

        var parsed = ParseModelJson(content);
        var blocks = SanitizeBlocks(parsed.Blocks ?? [], action);
        if (blocks.Count == 0)
            throw new InvalidOperationException("no_valid_blocks");

        return new EditorAiResponse(blocks, Truncate(parsed.AssistantNote, 500));
    }

    public static string NormalizeAction(string? action) =>
        (action ?? "").Trim().ToLowerInvariant() switch
        {
            "rewrite" => "rewrite",
            "mermaid" or "diagram" => "mermaid",
            _ => "generate"
        };

    public static ModelEditorPayload ParseModelJson(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
                trimmed = trimmed[start..(end + 1)];
        }

        return JsonSerializer.Deserialize<ModelEditorPayload>(trimmed, JsonOptions)
            ?? new ModelEditorPayload();
    }

    public static IReadOnlyList<EditorAiBlock> SanitizeBlocks(IEnumerable<EditorAiBlock> raw, string action)
    {
        var result = new List<EditorAiBlock>();
        foreach (var block in raw)
        {
            if (block is null || string.IsNullOrWhiteSpace(block.Type))
                continue;
            var type = block.Type.Trim();
            if (!AllowedTypes.Contains(type))
                continue;

            if (action == "mermaid" && type != "mermaidDiagram")
                continue;
            if (action == "rewrite" && type == "mermaidDiagram")
                continue;

            var cleaned = new EditorAiBlock { Type = type };

            if (type == "heading")
            {
                var level = ReadIntProp(block.Props, "level", 2);
                level = Math.Clamp(level, 1, 3);
                cleaned.Props = Dict(("level", JsonSerializer.SerializeToElement(level)));
            }
            else if (type == "callout")
            {
                var tone = ReadStringProp(block.Props, "tone", "info");
                if (!AllowedTones.Contains(tone))
                    tone = "info";
                cleaned.Props = Dict(("tone", JsonSerializer.SerializeToElement(tone.ToLowerInvariant())));
            }
            else if (type == "mermaidDiagram")
            {
                var source = ReadStringProp(block.Props, "source", "");
                if (string.IsNullOrWhiteSpace(source))
                    continue;
                cleaned.Props = Dict(("source", JsonSerializer.SerializeToElement(source.Trim())));
            }
            else if (type == "codeBlock")
            {
                var lang = ReadStringProp(block.Props, "language", "");
                if (!string.IsNullOrWhiteSpace(lang))
                    cleaned.Props = Dict(("language", JsonSerializer.SerializeToElement(lang.Trim())));
            }

            if (type != "mermaidDiagram" && block.Content is { } content
                && content.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
                cleaned.Content = content;

            if (type == "mermaidDiagram")
            {
                result.Add(cleaned);
            }
            else
            {
                cleaned.Content ??= JsonSerializer.SerializeToElement("");
                result.Add(cleaned);
            }

            if (result.Count >= 12)
                break;
        }

        return result;
    }

    private static string SystemPrompt(string action) => action switch
    {
        "mermaid" => """
            Du er en dansk undervisningsassistent. Returnér KUN JSON: {"blocks":[{"type":"mermaidDiagram","props":{"source":"<mermaid>"}}],"assistantNote":"kort note"}.
            source skal være gyldig Mermaid (fx flowchart TD). Ingen markdown fences. Ingen HTML.
            """,
        "rewrite" => """
            Du er en dansk undervisningsassistent. Omskriv den markerede tekst efter brugerens instruktion.
            Returnér KUN JSON: {"blocks":[...],"assistantNote":"kort note"}.
            Tilladte type-værdier: paragraph, heading (props.level 1-3), bulletListItem, numberedListItem, checkListItem, quote, codeBlock, learningGoal, callout (props.tone: info|tip|warning|important).
            content er almindelig tekststreng (dansk). Ingen HTML, ingen scripts, ingen mermaidDiagram ved omskrivning.
            """,
        _ => """
            Du er en dansk undervisningsassistent til forløbssider. Generér indhold som BlockNote-blokke.
            Returnér KUN JSON: {"blocks":[...],"assistantNote":"kort note"}.
            Tilladte type-værdier: paragraph, heading (props.level 1-3), bulletListItem, numberedListItem, checkListItem, quote, codeBlock, learningGoal, callout (props.tone: info|tip|warning|important), mermaidDiagram (props.source).
            content er almindelig tekststreng (dansk). Maks 12 blokke. Ingen HTML eller scripts.
            """
    };

    private static string UserPrompt(string action, string prompt, string selection, string? pageTitle)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(pageTitle))
            sb.AppendLine($"Sidetitel: {pageTitle.Trim()}");
        sb.AppendLine($"Handling: {action}");
        sb.AppendLine($"Instruktion: {prompt}");
        if (selection.Length > 0)
        {
            sb.AppendLine("Markering:");
            sb.AppendLine(selection);
        }

        return sb.ToString();
    }

    private static Dictionary<string, JsonElement> Dict(params (string Key, JsonElement Value)[] items)
    {
        var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, value) in items)
            dict[key] = value;
        return dict;
    }

    private static int ReadIntProp(Dictionary<string, JsonElement>? props, string key, int fallback)
    {
        if (props is null || !props.TryGetValue(key, out var el))
            return fallback;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
            return n;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out n))
            return n;
        return fallback;
    }

    private static string ReadStringProp(Dictionary<string, JsonElement>? props, string key, string fallback)
    {
        if (props is null || !props.TryGetValue(key, out var el))
            return fallback;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? fallback,
            JsonValueKind.Number => el.ToString(),
            _ => fallback
        };
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value ?? "";
        return value[..max];
    }
}
