namespace Mercantec.Tech.Api.Storage;

/// <summary>Deletes only bounded, application-created job trees inside the configured spool.</summary>
public static class PreviewSpool
{
    private const int MaxEntries = 4096;
    private const int MaxDepth = 8;

    public static int CleanupAbandoned(string spoolPath, DateTime utcNow, ILogger logger)
    {
        if (!Directory.Exists(spoolPath)) return 0;
        var removed = 0;
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(spoolPath).Take(1000))
            {
                if (!Inspect(spoolPath, directory, out var tree)) continue;
                // Jobs normally finish within twelve minutes. A full day also protects in-flight recovery.
                if (tree.LatestWriteUtc >= utcNow.AddDays(-1)) continue;
                if (Delete(tree, logger)) removed++;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(error, "Could not inspect abandoned preview jobs");
        }
        return removed;
    }

    public static bool TryDeleteJob(string spoolPath, string directory, ILogger logger)
    {
        if (!Directory.Exists(directory)) return true;
        try
        {
            return Inspect(spoolPath, directory, out var tree) && Delete(tree, logger);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(error, "Could not clean preview job directory");
            return false;
        }
    }

    private static bool Inspect(string spoolPath, string directory, out JobTree tree)
    {
        tree = new();
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(spoolPath));
        var job = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetDirectoryName(job), root, comparison)
            || !Guid.TryParseExact(Path.GetFileName(job), "N", out _)
            || IsLink(root) || IsLink(job)) return false;
        tree.Root = root;
        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((job, 0));
        while (pending.TryPop(out var current))
        {
            if (current.Depth > MaxDepth || IsLink(current.Path)) return false;
            tree.Directories.Add(current.Path);
            tree.LatestWriteUtc = Later(tree.LatestWriteUtc, Directory.GetLastWriteTimeUtc(current.Path));
            foreach (var entry in Directory.EnumerateFileSystemEntries(current.Path))
            {
                if (tree.Files.Count + tree.Directories.Count + pending.Count >= MaxEntries || IsLink(entry)) return false;
                var full = Path.GetFullPath(entry);
                if (!full.StartsWith(job + Path.DirectorySeparatorChar, comparison)) return false;
                if (Directory.Exists(entry)) pending.Push((entry, current.Depth + 1));
                else
                {
                    tree.Files.Add(entry);
                    tree.LatestWriteUtc = Later(tree.LatestWriteUtc, File.GetLastWriteTimeUtc(entry));
                }
            }
        }
        return true;
    }

    private static bool Delete(JobTree tree, ILogger logger)
    {
        try
        {
            // Recheck each parent and entry. Never follow a linked directory or use recursive deletion.
            foreach (var file in tree.Files)
            {
                if (!SafeParents(file, tree.Root) || IsLink(file)) return false;
                File.Delete(file);
            }
            foreach (var directory in tree.Directories.AsEnumerable().Reverse())
            {
                if (!SafeParents(directory, tree.Root) || IsLink(directory)) return false;
                Directory.Delete(directory, recursive: false);
            }
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(error, "Could not remove preview job files");
            return false;
        }
    }

    private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    private static bool SafeParents(string path, string root)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (var parent = Path.GetDirectoryName(path); parent is not null; parent = Path.GetDirectoryName(parent))
        {
            if (IsLink(parent)) return false;
            if (string.Equals(parent, root, comparison)) return true;
        }
        return false;
    }
    private static DateTime Later(DateTime first, DateTime second) => first > second ? first : second;

    private sealed class JobTree
    {
        public string Root { get; set; } = "";
        public List<string> Files { get; } = [];
        public List<string> Directories { get; } = [];
        public DateTime LatestWriteUtc { get; set; } = DateTime.MinValue;
    }
}
