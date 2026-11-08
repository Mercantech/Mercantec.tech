using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mercantec.Tech.Api.Ai.EditorRuns;

public static partial class EditorAiText
{
    public const int ChunkChars = 4800;
    public const int OverlapChars = 600;
    public const int MaxFileBytes = 32 * 1024 * 1024;
    public const int MaxExtractedChars = 8 * 1024 * 1024;
    public static int Tokens(string text) => (text.Length + 2) / 3;
    public static string Fingerprint(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    public static string FileFingerprint(Data.Entities.FileAsset file) => Fingerprint($"text-v1:{file.Id}:{file.Key}:{file.SizeBytes}:{file.CreatedAtUtc:O}");

    public static string[] Words(string text) => WordPattern().Matches(text.ToLowerInvariant()).Select(x => x.Value)
        .Where(x => x.Length > 2 && !StopWords.Contains(x)).Distinct().Take(20).ToArray();
    private static readonly HashSet<string> StopWords = ["den", "det", "der", "som", "til", "med", "for", "fra", "the", "and", "eller", "skal", "kan", "har", "om", "lav", "skriv", "mere", "gør", "denne", "dette"];
    [GeneratedRegex(@"[\p{L}\p{N}_+#.-]+")]
    private static partial Regex WordPattern();

    public static string Plain(JsonElement value)
    {
        var output = new StringBuilder();
        Walk(value, output, 0);
        return output.ToString().Trim();
    }
    private static void Walk(JsonElement value, StringBuilder output, int depth)
    {
        if (depth > 64) throw new ArgumentException("document_too_deep");
        if (value.ValueKind == JsonValueKind.String) { output.Append(value.GetString()); return; }
        if (value.ValueKind == JsonValueKind.Array) { foreach (var item in value.EnumerateArray()) Walk(item, output, depth + 1); return; }
        if (value.ValueKind != JsonValueKind.Object) return;
        if (value.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String) output.Append(text.GetString());
        else if (value.TryGetProperty("content", out var content)) Walk(content, output, depth + 1);
        if (value.TryGetProperty("rows", out var rows)) Walk(rows, output, depth + 1);
        if (value.TryGetProperty("cells", out var cells)) { Walk(cells, output, depth + 1); output.Append(" | "); }
        if (value.TryGetProperty("props", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            if (props.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.String) output.Append(source.GetString());
            if (props.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String) output.Append(':').Append(name.GetString()).Append(':');
        }
        if (value.TryGetProperty("children", out var children)) Walk(children, output, depth + 1);
        output.AppendLine();
    }

    public static IEnumerable<(int Ordinal, string Text, string Location)> Chunks(string text, string? location = null)
    {
        var ordinal = 0; var page = 0;
        foreach (var pageText in text.Replace("\r\n", "\n").Split('\f'))
        {
            page++;
            for (var offset = 0; offset < pageText.Length;)
            {
                var length = Math.Min(ChunkChars, pageText.Length - offset);
                if (length < pageText.Length - offset)
                {
                    var newline = pageText.LastIndexOf('\n', offset + length - 1, Math.Min(length, 800));
                    if (newline > offset + length / 2) length = newline - offset + 1;
                }
                var chunk = pageText.Substring(offset, length).Trim();
                if (chunk.Length > 0) yield return (ordinal++, chunk, location ?? $"Side {page}");
                if (offset + length >= pageText.Length) break;
                offset += Math.Max(1, length - OverlapChars);
            }
        }
    }
}
