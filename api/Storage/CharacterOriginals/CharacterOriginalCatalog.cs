using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mercantec.Tech.Api.Storage.CharacterOriginals;

public sealed partial class CharacterOriginalCatalog
{
    private readonly Dictionary<string, Original> _files;

    private CharacterOriginalCatalog(Dictionary<string, Original> files) => _files = files;

    internal IEnumerable<Original> Files => _files.Values;

    internal Original? Find(string path) => _files.GetValueOrDefault(path);

    public static CharacterOriginalCatalog Load()
    {
        using var source = typeof(CharacterOriginalCatalog).Assembly.GetManifestResourceStream("character-originals/manifest.json")
            ?? throw new InvalidDataException("Missing character originals manifest.");
        return Load(source);
    }

    internal static CharacterOriginalCatalog Load(Stream source)
    {
        var manifest = JsonSerializer.Deserialize<Manifest>(source, JsonSerializerOptions.Web);
        if (manifest is not { SchemaVersion: 1, Files.Length: > 0 })
            throw new InvalidDataException("Invalid character originals manifest.");

        var files = new Dictionary<string, Original>(StringComparer.Ordinal);
        foreach (var file in manifest.Files)
        {
            if (file is not { Path.Length: > 0 and <= 256, Sha256.Length: 64, Bytes: > 0 }
                || !RelativePng().IsMatch(file.Path)
                || !file.Sha256.All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9')
                || !files.TryAdd(file.Path, file))
                throw new InvalidDataException("Invalid or duplicate character original.");
        }
        return new CharacterOriginalCatalog(files);
    }

    [GeneratedRegex(@"\A[a-zA-Z0-9][a-zA-Z0-9._-]*(?:/[a-zA-Z0-9][a-zA-Z0-9._-]*)+\.png\z", RegexOptions.CultureInvariant)]
    private static partial Regex RelativePng();

    private sealed record Manifest(int SchemaVersion, Original[] Files);

    internal sealed record Original(string Path, string Sha256, long Bytes)
    {
        internal string Key => $"public/character-originals/sha256/{Sha256}.png";
    }
}
