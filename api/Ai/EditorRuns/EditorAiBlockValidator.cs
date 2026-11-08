using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Ai.EditorRuns;

/// <summary>Lossless validation. Invalid proposals fail as a whole; nothing is silently normalised away.</summary>
public static class EditorAiBlockValidator
{
    private static readonly HashSet<string> InlineBlocks = ["paragraph", "heading", "bulletListItem", "numberedListItem", "checkListItem", "toggleListItem", "quote", "codeBlock", "learningGoal", "callout"];
    private static readonly HashSet<string> OtherBlocks = ["table", "mermaidDiagram", "pageLink", "entityLink", "divider", "slideBreak"];
    private static readonly HashSet<string> Styles = ["bold", "italic", "underline", "strike", "code", "textColor", "backgroundColor"];
    private static readonly HashSet<string> BlockKeys = ["id", "type", "props", "content", "children"];
    private static string[] PropertyNames(string type) => type switch
    {
        "heading" => ["backgroundColor", "textColor", "textAlignment", "level", "isToggleable"],
        "numberedListItem" => ["backgroundColor", "textColor", "textAlignment", "start"],
        "checkListItem" => ["backgroundColor", "textColor", "textAlignment", "checked"],
        "paragraph" or "bulletListItem" or "toggleListItem" => ["backgroundColor", "textColor", "textAlignment"],
        "quote" => ["backgroundColor", "textColor"],
        "table" => ["textColor"], "codeBlock" => ["language"],
        "callout" => ["schemaVersion", "tone", "emojiId", "emojiName"],
        "mermaidDiagram" => ["schemaVersion", "source"],
        "pageLink" => ["schemaVersion", "pageId"], "entityLink" => ["schemaVersion", "kind", "id"],
        "learningGoal" or "slideBreak" => ["schemaVersion"], _ => []
    };

    public static JsonElement[] ValidateBlocks(JsonElement blocks, IReadOnlyDictionary<string, JsonElement> originalBlocks, IReadOnlyList<EditorAiSource> sources)
    {
        if (blocks.ValueKind != JsonValueKind.Array) throw Invalid("Forslaget skal være en liste af blokke.");
        if (Encoding.UTF8.GetByteCount(blocks.GetRawText()) > 8 * 1024 * 1024) throw Invalid("Forslaget overstiger 8 MiB. Fortsæt opgaven i flere dele.");
        var knownReferences = new HashSet<string>(StringComparer.Ordinal);
        foreach (var original in originalBlocks.Values) CollectReferences(original, knownReferences);
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        foreach (var block in blocks.EnumerateArray()) ValidateBlock(block, 0);
        return blocks.EnumerateArray().Select(x => x.Clone()).ToArray();

        void ValidateBlock(JsonElement block, int depth)
        {
            if (++count > 100000 || depth > 64) throw Invalid("Forslaget har for mange indlejrede blokke. Del opgaven op.");
            Object(block);
            var type = String(block, "type");
            var id = OptionalString(block, "id");
            if (id is not null && !usedIds.Add(id)) throw Invalid("En blok må kun forekomme én gang i et forslag.");
            if (id is not null && originalBlocks.TryGetValue(id, out var original) && Fingerprint(original) == Fingerprint(block)) return;
            if (id is not null && !originalBlocks.ContainsKey(id)) throw Invalid("Nye blokke skal oprettes uden et eksisterende blok-id.");
            if (!InlineBlocks.Contains(type) && !OtherBlocks.Contains(type)) throw Invalid($"Bloktypen '{type}' kan kun bevares uændret. Redigér teksten omkring den.");
            foreach (var key in block.EnumerateObject()) if (!BlockKeys.Contains(key.Name)) throw Invalid($"Blokegenskaben '{key.Name}' understøttes ikke. Intet indhold er fjernet.");
            var props = block.TryGetProperty("props", out var p) ? p : default;
            if (props.ValueKind != JsonValueKind.Undefined)
            {
                Object(props);
                foreach (var property in props.EnumerateObject())
                {
                    if (!PropertyNames(type).Contains(property.Name)) throw Invalid($"Egenskaben '{property.Name}' understøttes ikke på '{type}'. Få forslaget justeret.");
                    if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null)) throw Invalid("Blokegenskaber skal have enkle værdier.");
                    if (property.Name is "checked" or "isToggleable")
                    { if (property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Invalid("En afkrydsning skal være sand eller falsk."); }
                    else if (property.Name is "level" or "start" or "schemaVersion")
                    { if (property.Value.ValueKind != JsonValueKind.Null && !property.Value.TryGetInt32(out _)) throw Invalid("Blokkens talværdi er ugyldig."); }
                    else if (property.Value.ValueKind != JsonValueKind.String) throw Invalid("Blokkens tekstegenskab har et ugyldigt format.");
                    if (property.Name == "textAlignment" && property.Value.GetString() is not ("left" or "center" or "right" or "justify")) throw Invalid("Tekstens justering understøttes ikke.");
                    if (property.Name == "schemaVersion" && property.Value.GetInt32() != 1) throw Invalid("Blokkens formatversion understøttes ikke.");
                }
            }
            switch (type)
            {
                case "heading":
                    if (props.ValueKind == JsonValueKind.Object && props.TryGetProperty("level", out var level) && (!level.TryGetInt32(out var n) || n is < 1 or > 6)) throw Invalid("En overskrift skal have niveau 1–6.");
                    break;
                case "mermaidDiagram":
                    if (props.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(OptionalString(props, "source"))) throw Invalid("Diagrammet mangler Mermaid-kilde.");
                    break;
                case "callout":
                    if (OptionalString(props, "tone") is string tone && tone is not ("info" or "tip" or "warning" or "important")) throw Invalid("Calloutens type understøttes ikke.");
                    if (OptionalString(props, "emojiId") is { Length: > 0 } emojiId
                        && !(id is not null && originalBlocks.TryGetValue(id, out var previous)
                            && previous.TryGetProperty("props", out var previousProps) && OptionalString(previousProps, "emojiId") == emojiId))
                        Reference("emoji", emojiId, block);
                    break;
                case "pageLink": Reference("notion-page", String(props, "pageId"), block); break;
                case "entityLink": Reference(String(props, "kind"), String(props, "id"), block); break;
            }
            if (block.TryGetProperty("content", out var content) && content.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                if (type == "table") ValidateTable(content);
                else if (InlineBlocks.Contains(type)) ValidateInline(content, type == "codeBlock");
                else if (!(content.ValueKind == JsonValueKind.Array && content.GetArrayLength() == 0) && !(content.ValueKind == JsonValueKind.String && content.GetString() == "")) throw Invalid($"Blokken '{type}' kan ikke indeholde skjult tekst.");
            }
            else if (type == "table") throw Invalid("Tabellen mangler rækker.");
            if (block.TryGetProperty("children", out var children))
            {
                if (children.ValueKind != JsonValueKind.Array) throw Invalid("Underblokke skal være en liste.");
                foreach (var child in children.EnumerateArray()) ValidateBlock(child, depth + 1);
            }
        }

        void ValidateInline(JsonElement value, bool code = false, bool allowLinks = true)
        {
            if (value.ValueKind == JsonValueKind.String) return;
            if (value.ValueKind != JsonValueKind.Array) throw Invalid("Tekstindhold har et ukendt format.");
            foreach (var item in value.EnumerateArray())
            {
                if (++count > 1000000) throw Invalid("Forslaget er for komplekst. Del opgaven op.");
                Object(item);
                switch (String(item, "type"))
                {
                    case "text":
                        _ = String(item, "text", allowEmpty: true);
                        if (item.TryGetProperty("styles", out var styles))
                        {
                            Object(styles);
                            foreach (var style in styles.EnumerateObject())
                            {
                                if (!Styles.Contains(style.Name)) throw Invalid("Teksten har en ukendt formatering.");
                                var color = style.Name is "textColor" or "backgroundColor";
                                if (color ? style.Value.ValueKind != JsonValueKind.String : style.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Invalid("Tekstens formatering har et ugyldigt format.");
                                if (code && (color ? style.Value.GetString() != "default" : style.Value.ValueKind == JsonValueKind.True)) throw Invalid("Kode skal have ren kildetekst uden tekstformatering.");
                            }
                        }
                        break;
                    case "link" when !code && allowLinks:
                        SafeUrl(String(item, "href"));
                        if (!item.TryGetProperty("content", out var linkContent)) throw Invalid("Linket mangler tekst.");
                        ValidateInline(linkContent, allowLinks: false);
                        break;
                    case "pageMention" when !code: Reference("notion-page", String(item.GetProperty("props"), "pageId"), item); break;
                    case "entityMention" when !code:
                        var props = item.GetProperty("props");
                        Reference(String(props, "kind"), String(props, "id"), item);
                        break;
                    case "customEmoji" when !code: Reference("emoji", String(item.GetProperty("props"), "emojiId"), item); break;
                    default:
                        if (!knownReferences.Contains(Fingerprint(item))) throw Invalid("Ukendt eller ikke valideret reference i teksten.");
                        break;
                }
            }
        }

        void ValidateTable(JsonElement table)
        {
            Object(table);
            if (String(table, "type") != "tableContent" || !table.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0) throw Invalid("Tabellen mangler gyldige rækker.");
            int? columns = null;
            foreach (var row in rows.EnumerateArray())
            {
                Object(row);
                if (!row.TryGetProperty("cells", out var cells) || cells.ValueKind != JsonValueKind.Array || cells.GetArrayLength() == 0) throw Invalid("Tabellen har en tom række.");
                columns ??= cells.GetArrayLength();
                if (columns != cells.GetArrayLength()) throw Invalid("Alle tabelrækker skal have samme antal celler.");
                foreach (var cell in cells.EnumerateArray())
                {
                    if (cell.ValueKind == JsonValueKind.Object)
                    {
                        if (String(cell, "type") != "tableCell" || !cell.TryGetProperty("content", out var inner)) throw Invalid("Ukendt tabelcelle.");
                        ValidateInline(inner);
                    }
                    else ValidateInline(cell);
                }
            }
            if (table.TryGetProperty("headerRows", out var headers) && (!headers.TryGetInt32(out var h) || h < 0 || h > rows.GetArrayLength())) throw Invalid("Antallet af overskriftsrækker er ugyldigt.");
            if (table.TryGetProperty("columnWidths", out var widths))
            {
                if (widths.ValueKind != JsonValueKind.Array || widths.GetArrayLength() != columns) throw Invalid("Tabelbredderne matcher ikke kolonnerne.");
                foreach (var width in widths.EnumerateArray()) if (width.ValueKind != JsonValueKind.Null && (!width.TryGetDouble(out var w) || !double.IsFinite(w) || w <= 0)) throw Invalid("En tabelbredde er ugyldig.");
            }
        }

        void Reference(string kind, string sourceId, JsonElement item)
        {
            if (knownReferences.Contains(Fingerprint(item))) return;
            if (!sources.Any(s => s.Kind == kind && string.Equals(s.SourceId, sourceId, StringComparison.OrdinalIgnoreCase) && s.State == "ready")) throw Invalid("Forslaget refererer til en kilde, som ikke er valideret for denne opgave.");
        }
    }

    public static string Fingerprint(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) WriteCanonical(writer, value);
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                Object(value);
                writer.WriteStartObject();
                foreach (var p in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(p.Name); WriteCanonical(writer, p.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray(); break;
            default: value.WriteTo(writer); break;
        }
    }

    private static void CollectReferences(JsonElement value, HashSet<string> result)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() is "pageMention" or "studentMention" or "entityMention" or "customEmoji" or "pageLink" or "entityLink") result.Add(Fingerprint(value));
            foreach (var property in value.EnumerateObject()) CollectReferences(property.Value, result);
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) CollectReferences(item, result);
    }

    private static void Object(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Invalid("Forslaget har en ugyldig struktur.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in value.EnumerateObject()) if (!names.Add(p.Name)) throw Invalid("Forslaget indeholder gentagne egenskaber.");
    }
    private static string String(JsonElement value, string property, bool allowEmpty = false)
    {
        var text = OptionalString(value, property);
        return text is not null && (allowEmpty || !string.IsNullOrWhiteSpace(text)) ? text : throw Invalid($"Forslaget mangler '{property}'.");
    }
    private static string? OptionalString(JsonElement value, string property) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    private static void SafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Any(char.IsControl) || url.Contains('\\')) throw Invalid("Linket har en ugyldig adresse.");
        if (url.StartsWith('/') && !url.StartsWith("//")) return;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" or "mailto") return;
        throw Invalid("Links skal bruge https, http, mailto eller en lokal sidesti.");
    }
    private static ArgumentException Invalid(string message) => new(message);
}
