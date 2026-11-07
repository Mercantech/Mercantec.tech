using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Drawings;

public sealed class DrawingException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public static class DrawingValidation
{
    public const int MaxSceneBytes = 1024 * 1024;
    public const int MaxImageBytes = 3 * 1024 * 1024;
    public static bool Fingerprint(string? value) => value is { Length: 64 } && value.All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9');
    public static DrawingException Invalid(string code = "invalid_drawing") => new(400, code, "Tegningen indeholder ugyldige oplysninger.");
    public static DrawingException Changed() => new(409, "drawing_changed", "Tegningen er ændret. Vent på lagring, og prøv igen.");
    public static DrawingException Denied() => new(404, "drawing_not_found", "Tegningen er ikke tilgængelig for din konto.");

    public static byte[] Validate(StoreDrawingDocument input)
    {
        if (!Fingerprint(input.Fingerprint) || input.StateBase64 is null || input.StateBase64.Length > (MaxSceneBytes + 2) / 3 * 4
            || input.SceneJson is null || Encoding.UTF8.GetByteCount(input.SceneJson) > MaxSceneBytes
            || input.AssetIds is null || input.AssetIds.Length > 1000 || input.AssetIds.Any(id => id == Guid.Empty)
            || input.AssetIds.Distinct().Count() != input.AssetIds.Length) throw Invalid();
        byte[] state;
        try { state = Convert.FromBase64String(input.StateBase64); } catch (FormatException) { throw Invalid(); }
        if (state.Length == 0 || state.Length > MaxSceneBytes) throw Invalid();
        try
        {
            using var document = JsonDocument.Parse(input.SceneJson, new JsonDocumentOptions { MaxDepth = 64 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("elements", out var elements)
                || elements.ValueKind != JsonValueKind.Array || elements.GetArrayLength() > 10000
                || !root.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("appState", out var app) || app.ValueKind != JsonValueKind.Object) throw Invalid();
            var ids = new HashSet<string>();
            foreach (var element in elements.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("id", out var id)
                    || id.ValueKind != JsonValueKind.String || id.GetString() is not { Length: > 0 and <= 128 } name || !ids.Add(name)) throw Invalid();
                if (element.TryGetProperty("link", out var link) && link.ValueKind == JsonValueKind.String && link.GetString() is { Length: > 0 } url
                    && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")
                    && !(url.StartsWith('/') && !url.StartsWith("//"))) throw Invalid();
            }
            var fileIds = new HashSet<Guid>();
            foreach (var file in files.EnumerateObject())
            {
                if (file.Name.Length is 0 or > 128 || file.Value.ValueKind != JsonValueKind.Object
                    || file.Value.EnumerateObject().Count() != 1 || !file.Value.TryGetProperty("assetId", out var id)
                    || id.ValueKind != JsonValueKind.String || !Guid.TryParse(id.GetString(), out var asset) || asset == Guid.Empty) throw Invalid();
                fileIds.Add(asset);
            }
            if (!fileIds.SetEquals(input.AssetIds)) throw Invalid();
        }
        catch (JsonException) { throw Invalid(); }
        return state;
    }

    public static string SnapshotScene(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        var elements = root["elements"]!.AsArray().Where(e => e?["isDeleted"]?.GetValue<bool>() != true).Select(e => e!.DeepClone()).ToArray();
        var wanted = elements.Select(e => e["fileId"]?.GetValue<string>()).Where(s => s is not null).ToHashSet();
        var files = new JsonObject();
        foreach (var (id, value) in root["files"]!.AsObject()) if (wanted.Contains(id)) files[id] = value!.DeepClone();
        return new JsonObject { ["elements"] = new JsonArray(elements), ["files"] = files, ["appState"] = root["appState"]!.DeepClone() }.ToJsonString();
    }

    public static bool IsPreviewPng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 24 or > MaxImageBytes || !bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8)) return false;
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4));
        return width is > 0 and <= 2048 && height is > 0 and <= 2048;
    }
}
