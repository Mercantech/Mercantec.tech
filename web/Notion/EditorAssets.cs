using System.Reflection;

namespace Mercantec.Tech.Web.Notion;

internal static class EditorAssets
{
    // The build hashes the actual JS/CSS, including inside Docker where .git is absent.
    // A previous service worker must not resolve a new app's import to its old bare URL.
    private static readonly string Version = typeof(EditorAssets).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == "NotionEditorAssets")?.Value ?? "development";

    internal static string Script => $"/notion-editor/editor.js?v={Version}";
    internal static string Style => $"/notion-editor/editor.css?v={Version}";
}
