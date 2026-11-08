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

    public static string[] Words(string text) => WordPattern().Matches(text.ToLowerInvariant()).Select(x => NormalizeWord(x.Value))
        .Where(x => (x.Length > 2 || x is "c#" or "f#" or "c++") && !StopWords.Contains(x)).Distinct().Take(2048).ToArray();
    private static string NormalizeWord(string word) => word switch
    {
        "csharp" => "c#", "fsharp" => "f#", ".net" or "net" or "asp.net" or "aspnet" => "dotnet",
        "interfaces" or "interfacet" or "interfacets" => "interface",
        "classes" or "klasser" or "klasse" => "class", "objekter" or "objekt" or "objects" => "object",
        "polymorfi" => "polymorphism", "nedarvning" or "arv" => "inheritance",
        "databaser" => "database", "funktioner" or "functions" => "function", "metoder" or "methods" => "method",
        _ => word
    };
    private static readonly HashSet<string> StopWords = ["den", "det", "der", "som", "til", "med", "for", "fra", "the", "and", "eller", "skal", "kan", "har", "om", "lav", "skriv", "mere", "gør", "denne", "dette",
        "erstat", "eksisterende", "afsnit", "afsnittet", "afsnittene", "kort", "kortere", "kortfattet", "faglig", "fagligt", "korrekt", "forklaring", "forklaringen", "forklar", "forklare", "forklarer",
        "elever", "eleverne", "elev", "underviser", "undervisere", "undervisning", "lærer", "lærere", "h1", "h2", "h3", "h4", "h5", "h6", "grundforløb", "hovedforløb",
        "tekst", "teksten", "tekster", "side", "siden", "dokument", "dokumentet", "indhold", "indholdet", "forslag", "forslaget", "overskrift", "overskrifter", "afsnits",
        "brug", "bruge", "bruger", "behold", "bevar", "bevare", "tilføj", "tilføje", "omskriv", "ændr", "ændre", "ændringer", "opdater", "opdatere", "fortsæt", "vis", "vise", "beskriv", "beskrive",
        "eksempel", "eksempler", "eksemplet", "lille", "lillebitte", "enkelt", "enkle", "enkel", "enklere", "simpel", "simpelt", "simple", "bedre", "tydelig", "tydeligt", "tydelige", "endnu", "meget",
        "konkret", "konkrete", "relevant", "relevante", "rigtig", "rigtigt", "korrekte", "forståeligt", "forståelig", "overskuelig", "overskueligt", "begræns", "højst", "maks", "max", "mindst", "maksimalt",
        "punkt", "punkter", "liste", "listen", "resten", "uden", "ikke", "aldrig", "altid", "både", "også", "igen", "bare", "gerne", "kun", "samt", "men", "når", "hvis", "hvad", "hvor", "hvordan", "hvorfor",
        "jeg", "mig", "min", "mit", "mine", "din", "dine", "dit", "vores", "jeres", "deres", "sig", "sin", "sit", "sine", "dem", "man", "ved", "have", "være", "være", "bliver", "blevet", "mellem", "efter", "før", "over", "under",
        "hele", "alle", "del", "dele", "delene", "derfor", "fordi", "sådan", "her", "herfra", "hertil", "svar", "svaret", "indledning", "introduktion", "opsummering", "forkort", "gennemgå", "gennemgang", "pædagogisk", "pædagogiske", "let", "lette", "lettere", "letforståelig", "læse", "læser", "læs", "læsning", "forstå", "forstår", "forståelse", "begreb", "begrebet",
        "dansk", "danske", "engelsk", "engelske", "sprog", "sproget", "nuværende", "aktuelle", "demo", "testside", "skrivepartner", "notion", "editor", "native", "review", "qa", "write", "rewrite", "short", "shorter", "explain", "example", "existing", "please", "make", "using", "into", "with", "this", "that", "from", "your", "text", "document"];
    [GeneratedRegex(@"[cf]#|c\+\+|\.?net\b|[\p{L}_][\p{L}\p{N}_]*(?:\.[\p{L}_][\p{L}\p{N}_]*)*")]
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
