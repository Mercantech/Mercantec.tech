using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public const int MaxHistoryTurns = 6;
    public const int MaxHistoryTurnChars = 4000;

    /// <summary>
    /// Komponentkataloget i editor-block-prompt.txt sendes med hvert kald.
    /// Når block specs i editor/src ændres, skal filen opdateres — ellers fejler EditorBlockPrompt_NamesEveryCustomBlockType.
    /// </summary>
    public static string BlockCatalog { get; } = LoadBlockCatalog();

    private static readonly HashSet<string> AllowedTypes = new(StringComparer.Ordinal)
    {
        "paragraph", "heading", "bulletListItem", "numberedListItem", "checkListItem", "toggleListItem",
        "quote", "codeBlock", "learningGoal", "callout", "mermaidDiagram", "pageLink", "table",
        "divider", "slideBreak"
    };

    private static readonly HashSet<string> ChildTypes = new(StringComparer.Ordinal)
    {
        "paragraph", "heading", "bulletListItem", "numberedListItem", "checkListItem", "toggleListItem",
        "quote", "learningGoal", "callout"
    };

    private static readonly HashSet<string> AllowedTones = new(StringComparer.OrdinalIgnoreCase)
    {
        "info", "tip", "warning", "important"
    };

    private static readonly HashSet<string> Colors = new(StringComparer.OrdinalIgnoreCase)
    {
        "default", "gray", "brown", "red", "orange", "yellow", "green", "blue", "purple", "pink"
    };

    private static readonly string[] BoolStyles = ["bold", "italic", "underline", "strike", "code"];
    private static readonly string[] ColorStyles = ["textColor", "backgroundColor"];

    private static readonly Dictionary<string, string> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = "text", ["plaintext"] = "text",
        ["csharp"] = "csharp", ["c#"] = "csharp", ["cs"] = "csharp",
        ["javascript"] = "javascript", ["js"] = "javascript",
        ["typescript"] = "typescript", ["ts"] = "typescript",
        ["jsx"] = "jsx", ["tsx"] = "tsx",
        ["html"] = "html", ["css"] = "css", ["scss"] = "scss", ["json"] = "json",
        ["python"] = "python", ["py"] = "python",
        ["java"] = "java", ["kotlin"] = "kotlin", ["sql"] = "sql",
        ["shellscript"] = "shellscript", ["shell"] = "shellscript", ["bash"] = "shellscript", ["sh"] = "shellscript",
        ["markdown"] = "markdown", ["md"] = "markdown",
        ["xml"] = "xml", ["yaml"] = "yaml", ["yml"] = "yaml",
        ["rust"] = "rust", ["c"] = "c", ["cpp"] = "cpp", ["c++"] = "cpp",
        ["php"] = "php", ["ruby"] = "ruby", ["swift"] = "swift",
        ["lua"] = "lua", ["scala"] = "scala", ["haskell"] = "haskell", ["mermaid"] = "mermaid", ["latex"] = "latex"
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

        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-6-luna" : _options.Model.Trim();
        // gpt-6-luna kræver reasoning_effort=none for temperature; max_completion_tokens (ikke max_tokens).
        var payload = new
        {
            model,
            reasoning_effort = "none",
            temperature = 0.4,
            max_completion_tokens = 3500,
            response_format = new { type = "json_object" },
            messages = BuildMessages(action, prompt, selection, request)
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
        var blocks = SanitizeBlocks(parsed.Blocks ?? [], action, EditorAiCatalog.From(request));
        if (blocks.Count == 0)
            throw new InvalidOperationException("no_valid_blocks");

        return new EditorAiResponse(blocks, Truncate(parsed.AssistantNote, 500));
    }

    public static string BuildSystemPrompt(string? action) =>
        BlockCatalog + "\n\n" + (NormalizeAction(action) switch
        {
            "mermaid" => "Opgaven er præcis ét mermaidDiagram. props.source er gyldig Mermaid uden forklaring og uden kodehegn.",
            "rewrite" => "Opgaven er at omskrive markeringen til blokke. Bevar mening, fakta og relevant formattering, medmindre brugeren beder om andet. Returnér ikke mermaidDiagram.",
            _ => "Opgaven er at foreslå nye blokke ud fra brugerens aktuelle instruktion og den relevante samtalehistorik."
        });

    private static IReadOnlyList<object> BuildMessages(string action, string prompt, string selection, EditorAiRequest request)
    {
        var messages = new List<object> { new { role = "system", content = BuildSystemPrompt(action) } };
        // The caller may supply dialogue context, never another system/developer instruction.
        var history = (request.History ?? [])
            .Where(turn => turn is not null && !string.IsNullOrWhiteSpace(turn.Text)
                && turn.Role?.Trim().ToLowerInvariant() is "user" or "assistant")
            .TakeLast(MaxHistoryTurns);
        foreach (var turn in history)
            messages.Add(new { role = turn.Role.Trim().ToLowerInvariant(), content = Truncate(turn.Text.Trim(), MaxHistoryTurnChars) });
        messages.Add(new { role = "user", content = UserPrompt(action, prompt, selection, request) });
        return messages;
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

    public static IReadOnlyList<EditorAiBlock> SanitizeBlocks(
        IEnumerable<EditorAiBlock> raw,
        string action,
        EditorAiCatalog? catalog = null)
    {
        catalog ??= EditorAiCatalog.Empty;
        action = NormalizeAction(action);
        var result = new List<EditorAiBlock>();
        var budget = 48;
        foreach (var block in raw)
        {
            if (result.Count >= 12 || budget <= 0)
                break;
            var cleaned = CleanBlock(block, action, catalog, depth: 0, ref budget);
            if (cleaned is not null)
                result.Add(cleaned);
        }

        return result;
    }

    private static EditorAiBlock? CleanBlock(
        EditorAiBlock? block,
        string action,
        EditorAiCatalog catalog,
        int depth,
        ref int budget)
    {
        if (block is null || budget <= 0 || depth > 4 || string.IsNullOrWhiteSpace(block.Type))
            return null;

        var type = block.Type.Trim();
        if (!AllowedTypes.Contains(type))
            return null;
        if (action == "mermaid" && type != "mermaidDiagram")
            return null;
        if (action == "rewrite" && type == "mermaidDiagram")
            return null;

        budget--;
        var cleaned = new EditorAiBlock { Type = type };

        switch (type)
        {
            case "heading":
                cleaned.Props = Dict(("level", JsonSerializer.SerializeToElement(
                    Math.Clamp(ReadIntProp(block.Props, "level", 2), 1, 6))));
                cleaned.Content = CleanInline(block.Content, catalog, plain: false);
                break;
            case "checkListItem":
                cleaned.Props = Dict(("checked", JsonSerializer.SerializeToElement(
                    ReadBoolProp(block.Props, "checked", false))));
                cleaned.Content = CleanInline(block.Content, catalog, plain: false);
                break;
            case "numberedListItem":
                var start = ReadIntProp(block.Props, "start", 0);
                if (start > 1)
                    cleaned.Props = Dict(("start", JsonSerializer.SerializeToElement(Math.Min(start, 999))));
                cleaned.Content = CleanInline(block.Content, catalog, plain: false);
                break;
            case "callout":
                var tone = ReadStringProp(block.Props, "tone", "info").Trim().ToLowerInvariant();
                if (!AllowedTones.Contains(tone))
                    tone = "info";
                cleaned.Props = Dict(("tone", JsonSerializer.SerializeToElement(tone)));
                cleaned.Content = CleanInline(block.Content, catalog, plain: false);
                break;
            case "codeBlock":
                if (TryLanguage(ReadStringProp(block.Props, "language", ""), out var language))
                    cleaned.Props = Dict(("language", JsonSerializer.SerializeToElement(language)));
                cleaned.Content = CleanInline(block.Content, catalog, plain: true);
                break;
            case "mermaidDiagram":
                var source = CleanMermaid(ReadStringProp(block.Props, "source", ""));
                if (source.Length == 0)
                    return null;
                cleaned.Props = Dict(("source", JsonSerializer.SerializeToElement(source)));
                break;
            case "pageLink":
                if (!catalog.TryPage(ReadStringProp(block.Props, "pageId", ""), out var pageId))
                    return null;
                cleaned.Props = Dict(("pageId", JsonSerializer.SerializeToElement(pageId)));
                break;
            case "table":
                var table = CleanTable(block.Content, catalog);
                if (table is null)
                    return null;
                cleaned.Content = table;
                break;
            case "divider":
            case "slideBreak":
                break;
            default:
                cleaned.Content = CleanInline(block.Content, catalog, plain: false);
                break;
        }

        if (ChildTypes.Contains(type) && block.Children is { Count: > 0 } && depth < 4)
        {
            var children = new List<EditorAiBlock>();
            foreach (var child in block.Children)
            {
                if (children.Count >= 8 || budget <= 0)
                    break;
                var nested = CleanBlock(child, action, catalog, depth + 1, ref budget);
                if (nested is not null)
                    children.Add(nested);
            }

            if (children.Count > 0)
                cleaned.Children = children;
        }

        return cleaned;
    }

    private static JsonElement CleanInline(JsonElement? content, EditorAiCatalog catalog, bool plain)
    {
        if (content is null || content.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return JsonSerializer.SerializeToElement("");

        var value = content.Value;
        if (value.ValueKind == JsonValueKind.String)
            return JsonSerializer.SerializeToElement(LimitText(value.GetString() ?? "", plain));

        if (value.ValueKind == JsonValueKind.Array)
        {
            if (plain)
                return JsonSerializer.SerializeToElement(LimitText(PlainFromParts(value), true));

            var nodes = new JsonArray();
            foreach (var item in value.EnumerateArray())
            {
                if (nodes.Count >= 40)
                    break;
                if (CleanInlineNode(item, catalog) is { } node)
                    nodes.Add(node);
            }

            return nodes.Count == 0
                ? JsonSerializer.SerializeToElement("")
                : JsonSerializer.Deserialize<JsonElement>(nodes.ToJsonString())!;
        }

        if (!plain && value.ValueKind == JsonValueKind.Object && CleanInlineNode(value, catalog) is { } single)
        {
            var nodes = new JsonArray(single);
            return JsonSerializer.Deserialize<JsonElement>(nodes.ToJsonString())!;
        }

        return JsonSerializer.SerializeToElement("");
    }

    private static JsonNode? CleanInlineNode(JsonElement item, EditorAiCatalog catalog)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            var literal = LimitText(item.GetString() ?? "", false);
            return literal.Length == 0 ? null : TextNode(literal, null);
        }

        if (item.ValueKind != JsonValueKind.Object)
            return null;

        var type = item.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String
            ? typeEl.GetString()
            : "text";

        switch (type)
        {
            case "text":
            case null:
            {
                var text = ReadText(item);
                return text.Length == 0 ? null : TextNode(text, StylesOf(item));
            }
            case "link":
            {
                var href = item.TryGetProperty("href", out var hrefEl) && hrefEl.ValueKind == JsonValueKind.String
                    ? hrefEl.GetString() ?? ""
                    : "";
                if (!SafeHref(href, out var safe))
                {
                    var fallback = LinkFallback(item);
                    return fallback.Length == 0 ? null : TextNode(fallback, null);
                }

                JsonNode visible = item.TryGetProperty("content", out var linkContent)
                    ? LinkContent(linkContent, catalog)
                    : JsonValue.Create(safe)!;
                if (visible is JsonValue value
                    && value.GetValueKind() == JsonValueKind.String
                    && string.IsNullOrEmpty(value.GetValue<string>()))
                    visible = JsonValue.Create(safe)!;
                return new JsonObject { ["type"] = "link", ["href"] = safe, ["content"] = visible };
            }
            case "pageMention":
            {
                if (!catalog.TryPage(ReadNestedProp(item, "pageId"), out var pageId))
                    return null;
                return new JsonObject
                {
                    ["type"] = "pageMention",
                    ["props"] = new JsonObject { ["pageId"] = pageId }
                };
            }
            case "studentMention":
            {
                if (!catalog.TryMember(ReadNestedProp(item, "membershipId"), out var memberId)
                    || catalog.HoldIdText is null)
                    return null;
                return new JsonObject
                {
                    ["type"] = "studentMention",
                    ["props"] = new JsonObject
                    {
                        ["holdId"] = catalog.HoldIdText,
                        ["membershipId"] = memberId
                    }
                };
            }
            case "customEmoji":
            {
                if (!catalog.TryEmoji(ReadNestedProp(item, "emojiId"), out var emoji)) return null;
                return new JsonObject
                {
                    ["type"] = "customEmoji",
                    ["props"] = new JsonObject { ["emojiId"] = emoji.Id, ["name"] = emoji.Name }
                };
            }
            default:
                return null;
        }
    }

    private static JsonNode LinkContent(JsonElement content, EditorAiCatalog catalog)
    {
        if (content.ValueKind == JsonValueKind.String)
            return JsonValue.Create(LimitText(content.GetString() ?? "", false))!;

        if (content.ValueKind != JsonValueKind.Array)
            return JsonValue.Create("")!;

        var nodes = new JsonArray();
        foreach (var part in content.EnumerateArray())
        {
            if (nodes.Count >= 20)
                break;
            if (part.ValueKind == JsonValueKind.String)
            {
                var text = LimitText(part.GetString() ?? "", false);
                if (text.Length > 0)
                    nodes.Add(TextNode(text, null));
            }
            else if (part.ValueKind == JsonValueKind.Object)
            {
                var kind = part.TryGetProperty("type", out var kindEl) && kindEl.ValueKind == JsonValueKind.String ? kindEl.GetString() : "text";
                // Links cannot nest other links, but may display a known emoji.
                if ((kind is "text" or null or "customEmoji") && CleanInlineNode(part, catalog) is { } node)
                    nodes.Add(node);
            }
        }

        return nodes.Count == 0 ? JsonValue.Create("")! : nodes;
    }

    private static JsonElement? CleanTable(JsonElement? content, EditorAiCatalog catalog)
    {
        if (content is null || content.Value.ValueKind != JsonValueKind.Object)
            return null;

        var value = content.Value;
        if (value.TryGetProperty("type", out var typeEl)
            && typeEl.ValueKind == JsonValueKind.String
            && typeEl.GetString() != "tableContent")
            return null;
        if (!value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return null;

        var rowNodes = new List<JsonArray>();
        var width = 0;
        foreach (var row in rows.EnumerateArray())
        {
            if (rowNodes.Count >= 8)
                break;
            if (row.ValueKind != JsonValueKind.Object
                || !row.TryGetProperty("cells", out var cells)
                || cells.ValueKind != JsonValueKind.Array)
                continue;

            var cellNodes = new JsonArray();
            foreach (var cell in cells.EnumerateArray())
            {
                if (cellNodes.Count >= 6)
                    break;
                cellNodes.Add(CleanCell(cell, catalog));
            }

            if (cellNodes.Count == 0)
                continue;
            width = Math.Max(width, cellNodes.Count);
            rowNodes.Add(cellNodes);
        }

        if (rowNodes.Count == 0 || width == 0)
            return null;

        var serializedRows = new JsonArray();
        foreach (var cells in rowNodes)
        {
            while (cells.Count < width)
                cells.Add(JsonValue.Create("")!);
            serializedRows.Add(new JsonObject { ["cells"] = cells });
        }

        var table = new JsonObject
        {
            ["type"] = "tableContent",
            ["rows"] = serializedRows
        };
        if (value.TryGetProperty("headerRows", out var headerEl)
            && headerEl.ValueKind == JsonValueKind.Number
            && headerEl.TryGetInt32(out var header)
            && header > 0)
            table["headerRows"] = Math.Min(header, rowNodes.Count);

        return JsonSerializer.Deserialize<JsonElement>(table.ToJsonString());
    }

    private static JsonNode CleanCell(JsonElement cell, EditorAiCatalog catalog)
    {
        if (cell.ValueKind == JsonValueKind.String)
            return JsonValue.Create(LimitText(cell.GetString() ?? "", false))!;

        if (cell.ValueKind == JsonValueKind.Array)
            return JsonNode.Parse(CleanInline(cell, catalog, plain: false).GetRawText())!;

        if (cell.ValueKind == JsonValueKind.Object
            && cell.TryGetProperty("type", out var typeEl)
            && typeEl.GetString() == "tableCell")
        {
            JsonNode inner = cell.TryGetProperty("content", out var content)
                ? JsonNode.Parse(CleanInline(content, catalog, plain: false).GetRawText())!
                : JsonValue.Create("")!;
            return new JsonObject { ["type"] = "tableCell", ["content"] = inner };
        }

        if (cell.ValueKind == JsonValueKind.Object)
            return JsonNode.Parse(CleanInline(cell, catalog, plain: false).GetRawText())!;

        return JsonValue.Create("")!;
    }

    private static JsonObject TextNode(string text, JsonElement? styles)
    {
        var styleObj = new JsonObject();
        if (styles is { ValueKind: JsonValueKind.Object } known)
        {
            foreach (var name in BoolStyles)
            {
                if (known.TryGetProperty(name, out var flag) && flag.ValueKind == JsonValueKind.True)
                    styleObj[name] = true;
            }

            foreach (var name in ColorStyles)
            {
                if (!known.TryGetProperty(name, out var color) || color.ValueKind != JsonValueKind.String)
                    continue;
                var value = color.GetString()?.Trim().ToLowerInvariant();
                if (value is not null && Colors.Contains(value) && value != "default")
                    styleObj[name] = value;
            }
        }

        return new JsonObject { ["type"] = "text", ["text"] = text, ["styles"] = styleObj };
    }

    private static string ReadText(JsonElement item) =>
        item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
            ? LimitText(text.GetString() ?? "", false)
            : "";

    private static JsonElement? StylesOf(JsonElement item) =>
        item.TryGetProperty("styles", out var styles) ? styles : null;

    private static string ReadNestedProp(JsonElement item, string key)
    {
        if (item.TryGetProperty("props", out var props)
            && props.ValueKind == JsonValueKind.Object
            && props.TryGetProperty(key, out var nested)
            && nested.ValueKind == JsonValueKind.String)
            return nested.GetString() ?? "";
        if (item.TryGetProperty(key, out var direct) && direct.ValueKind == JsonValueKind.String)
            return direct.GetString() ?? "";
        return "";
    }

    private static string LinkFallback(JsonElement item)
    {
        if (!item.TryGetProperty("content", out var content))
            return "";
        if (content.ValueKind == JsonValueKind.String)
            return LimitText(content.GetString() ?? "", false);
        return content.ValueKind == JsonValueKind.Array ? PlainFromParts(content) : "";
    }

    private static string PlainFromParts(JsonElement parts)
    {
        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
                sb.Append(part.GetString());
            else if (part.ValueKind == JsonValueKind.Object
                && part.TryGetProperty("text", out var text)
                && text.ValueKind == JsonValueKind.String)
                sb.Append(text.GetString());
        }

        return sb.ToString();
    }

    private static string CleanMermaid(string source)
    {
        var text = source.Replace("\0", "").Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var newline = text.IndexOf('\n');
            if (newline >= 0)
                text = text[(newline + 1)..];
            if (text.EndsWith("```", StringComparison.Ordinal))
                text = text[..^3];
            text = text.Trim();
        }

        return Truncate(text, 8000);
    }

    private static bool TryLanguage(string raw, out string language)
    {
        language = "";
        var key = raw.Trim().ToLowerInvariant();
        return key.Length > 0 && Languages.TryGetValue(key, out language!);
    }

    private static bool SafeHref(string href, out string cleaned)
    {
        cleaned = href.Trim();
        if (cleaned.Length is 0 or > 2000 || cleaned.StartsWith("//", StringComparison.Ordinal) || cleaned.Contains('\\'))
            return false;
        if (cleaned.StartsWith('/'))
            return true;
        return Uri.TryCreate(cleaned, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http" or "mailto";
    }

    private static string UserPrompt(string action, string prompt, string selection, EditorAiRequest request)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(request.PageTitle))
            sb.Append("Sidetitel: ").AppendLine(OneLine(request.PageTitle, 160));
        if (!string.IsNullOrWhiteSpace(request.HoldId) && Guid.TryParse(request.HoldId, out var hold) && hold != Guid.Empty)
            sb.Append("HoldId: ").AppendLine(request.HoldId.Trim());
        sb.Append("Handling: ").AppendLine(action);
        sb.Append("Instruktion: ").AppendLine(prompt);
        AppendCatalog(sb, request);
        if (selection.Length > 0)
        {
            sb.AppendLine("Markering:");
            sb.AppendLine(selection);
        }

        return sb.ToString();
    }

    private static void AppendCatalog(StringBuilder sb, EditorAiRequest request)
    {
        var pages = (request.Pages ?? [])
            .Where(page => page is not null && Guid.TryParse(page.Id, out var id) && id != Guid.Empty && !string.IsNullOrWhiteSpace(page.Title))
            .Take(80)
            .ToList();
        if (pages.Count > 0)
        {
            sb.AppendLine("Sidekatalog (brug kun disse pageId):");
            foreach (var page in pages)
                sb.Append("- ").Append(page.Id.Trim()).Append(" | ").AppendLine(OneLine(page.Title, 80));
        }

        var holdReady = Guid.TryParse(request.HoldId, out var hold) && hold != Guid.Empty;
        var members = holdReady
            ? (request.Members ?? [])
                .Where(member => member is not null && Guid.TryParse(member.Id, out var id) && id != Guid.Empty && !string.IsNullOrWhiteSpace(member.DisplayName))
                .Take(120)
                .ToList()
            : [];
        if (members.Count > 0)
        {
            sb.Append("Elevkatalog (studentMention bruger holdId ").Append(request.HoldId!.Trim()).AppendLine(" og kun disse membershipId):");
            foreach (var member in members)
                sb.Append("- ").Append(member.Id.Trim()).Append(" | ").AppendLine(OneLine(member.DisplayName, 80));
        }

        var emojis = EditorAiCatalog.From(request).Emojis.Values;
        sb.AppendLine("Emoji-katalog (customEmoji må kun bruge disse gyldige emojiId'er og deres gemte navne):");
        if (emojis.Count == 0) sb.AppendLine("Kataloget er tomt. Brug almindelig tekst eller Unicode-emojis i stedet.");
        foreach (var emoji in emojis)
        {
            sb.AppendLine(JsonSerializer.Serialize(new
            {
                emojiId = emoji.Id,
                name = emoji.Name,
                altText = OneLine(emoji.AltText ?? "", 240),
                category = OneLine(emoji.Category ?? "", 40),
                keywords = (emoji.Keywords ?? []).Where(word => !string.IsNullOrWhiteSpace(word))
                    .Take(12).Select(word => OneLine(word, 40)).ToArray()
            }));
        }
    }

    private static string OneLine(string value, int max)
    {
        var sb = new StringBuilder(Math.Min(value.Length, max));
        foreach (var ch in value)
        {
            if (sb.Length >= max)
                break;
            sb.Append(ch is '\r' or '\n' or '\t' || char.IsControl(ch) ? ' ' : ch);
        }

        return sb.ToString().Trim();
    }

    private static string LoadBlockCatalog()
    {
        const string name = "Mercantec.Tech.Api.EditorBlockPrompt.txt";
        using var stream = typeof(OpenAiEditorClient).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("editor_block_prompt_missing");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd().Replace("\r\n", "\n").Trim();
        if (text.Length < 200)
            throw new InvalidOperationException("editor_block_prompt_empty");
        return text;
    }

    private static string LimitText(string value, bool plain)
    {
        var text = value.Replace("\0", "");
        return Truncate(text, plain ? 8000 : 4000);
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

    private static bool ReadBoolProp(Dictionary<string, JsonElement>? props, string key, bool fallback)
    {
        if (props is null || !props.TryGetValue(key, out var el))
            return fallback;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(el.GetString(), out var parsed) => parsed,
            _ => fallback
        };
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
