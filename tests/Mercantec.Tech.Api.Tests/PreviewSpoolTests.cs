using Mercantec.Tech.Api.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mercantec.Tech.Api.Tests;

public sealed class PreviewSpoolTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mercantec-spool-test-" + Guid.NewGuid().ToString("N"));

    public PreviewSpoolTests() => Directory.CreateDirectory(root);

    [Fact]
    public void RemovesAbandonedJobButKeepsRecentProcessingAndUnrelatedDirectories()
    {
        var stale = Job();
        File.WriteAllText(Path.Combine(stale, "input.docx"), "old input");
        File.WriteAllText(Path.Combine(stale, "processing"), "");
        Age(stale);
        var active = Job();
        File.WriteAllText(Path.Combine(active, "input.docx"), "active input");
        Age(active);
        File.WriteAllText(Path.Combine(active, "processing"), "");
        var unrelated = Path.Combine(root, "teacher-materials");
        Directory.CreateDirectory(unrelated);
        File.WriteAllText(Path.Combine(unrelated, "keep.txt"), "keep");
        Age(unrelated);

        Assert.Equal(1, PreviewSpool.CleanupAbandoned(root, DateTime.UtcNow, NullLogger.Instance));
        Assert.False(Directory.Exists(stale));
        Assert.True(File.Exists(Path.Combine(active, "input.docx")));
        Assert.True(File.Exists(Path.Combine(unrelated, "keep.txt")));
    }

    [Fact]
    public void RefusesPathsOutsideSpoolAndSpoolRootItself()
    {
        var outside = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            File.WriteAllText(Path.Combine(outside, "keep.txt"), "keep");
            Assert.False(PreviewSpool.TryDeleteJob(root, outside, NullLogger.Instance));
            Assert.False(PreviewSpool.TryDeleteJob(root, root, NullLogger.Instance));
            Assert.True(File.Exists(Path.Combine(outside, "keep.txt")));
            Assert.True(Directory.Exists(root));
        }
        finally { Directory.Delete(outside, true); }
    }

    [Fact]
    public void RefusesExcessivelyDeepJobWithoutDeletingAnyContents()
    {
        var job = Job();
        var nested = job;
        for (var depth = 0; depth < 10; depth++) nested = Directory.CreateDirectory(Path.Combine(nested, "nested")).FullName;
        var original = Path.Combine(nested, "input.docx");
        File.WriteAllText(original, "keep");
        Assert.False(PreviewSpool.TryDeleteJob(root, job, NullLogger.Instance));
        Assert.True(File.Exists(original));
    }

    [Fact]
    public void CompletedJobCleanupRemovesOnlyItsOwnTree()
    {
        var finished = Job();
        var profile = Directory.CreateDirectory(Path.Combine(finished, "profile", "user")).FullName;
        File.WriteAllText(Path.Combine(profile, "registrymodifications.xcu"), "profile");
        File.WriteAllText(Path.Combine(finished, "input.pdf"), "PDF output");
        var other = Job();
        File.WriteAllText(Path.Combine(other, "input.docx"), "keep");
        Assert.True(PreviewSpool.TryDeleteJob(root, finished, NullLogger.Instance));
        Assert.False(Directory.Exists(finished));
        Assert.True(File.Exists(Path.Combine(other, "input.docx")));
    }

    private string Job() => Directory.CreateDirectory(Path.Combine(root, Guid.NewGuid().ToString("N"))).FullName;

    private static void Age(string directory)
    {
        var past = DateTime.UtcNow.AddDays(-2);
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) File.SetLastWriteTimeUtc(file, past);
        foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories).Reverse())
            Directory.SetLastWriteTimeUtc(child, past);
        Directory.SetLastWriteTimeUtc(directory, past);
    }

    public void Dispose()
    {
        // This root is created by this fixture and never comes from application or user data.
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
