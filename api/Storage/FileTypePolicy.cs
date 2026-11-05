namespace Mercantec.Tech.Api.Storage;

/// <summary>Server-controlled media types. Active content is only offered as source or attachment.</summary>
public static class FileTypePolicy
{
    private static readonly Dictionary<string, (string Mime, string Kind)> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", "pdf"),
        [".png"] = ("image/png", "image"), [".jpg"] = ("image/jpeg", "image"),
        [".jpeg"] = ("image/jpeg", "image"), [".gif"] = ("image/gif", "image"),
        [".webp"] = ("image/webp", "image"), [".avif"] = ("image/avif", "image"),
        [".mp4"] = ("video/mp4", "video"), [".webm"] = ("video/webm", "video"),
        [".mp3"] = ("audio/mpeg", "audio"), [".wav"] = ("audio/wav", "audio"),
        [".ogg"] = ("audio/ogg", "audio"), [".m4a"] = ("audio/mp4", "audio"),
        [".flac"] = ("audio/flac", "audio"),
        [".zip"] = ("application/zip", "archive")
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".log", ".csv", ".tsv", ".ino", ".pde", ".c", ".cpp",
        ".h", ".hpp", ".cs", ".py", ".js", ".ts", ".jsx", ".tsx", ".gd", ".html", ".htm",
        ".css", ".scss", ".sql", ".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".cfg",
        ".shader", ".hlsl", ".glsl", ".svg", ".tscn", ".tres", ".uproject", ".csproj", ".sln",
        ".slnx", ".unity", ".prefab", ".mat", ".asm", ".sh", ".bat", ".ps1"
    };

    private static readonly HashSet<string> Attachments = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".docm", ".odt", ".rtf", ".ppt", ".pptx", ".pptm", ".odp",
        ".xls", ".xlsx", ".xlsm", ".ods", ".heic", ".heif", ".tif", ".tiff", ".bmp",
        ".mov", ".mkv", ".avi", ".wmv", ".aac", ".7z", ".rar", ".tar", ".gz", ".tgz",
        ".unitypackage", ".pck", ".uasset", ".umap", ".blend", ".fbx", ".obj", ".glb", ".gltf",
        ".stl", ".psd", ".ai", ".dwg", ".dxf", ".step", ".stp", ".bin", ".hex", ".apk",
        ".sb3", ".ipynb", ".jar", ".wasm", ".data", ".res", ".godot"
    };

    public static (string Mime, string Kind)? Classify(string name)
    {
        var extension = Path.GetExtension(name);
        if (Types.TryGetValue(extension, out var type)) return type;
        if (TextExtensions.Contains(extension)) return ("text/plain; charset=utf-8", "text");
        return Attachments.Contains(extension) ? ("application/octet-stream", "download") : null;
    }
}
