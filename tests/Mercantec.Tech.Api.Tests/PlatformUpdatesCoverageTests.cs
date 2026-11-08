using System.Text.Json;
using Mercantec.Tech.Api.Updates;

namespace Mercantec.Tech.Api.Tests;

public sealed class PlatformUpdatesCoverageTests
{
    [Fact]
    public void ReviewedHistoryHasNoMissingNotesOrUnexplainedExclusions()
    {
        using var stream = typeof(PlatformUpdatesCatalog).Assembly.GetManifestResourceStream("platform-updates/coverage.json");
        Assert.NotNull(stream);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        Assert.True(root.GetProperty("fullHistory").GetBoolean());
        Assert.Matches("^[0-9a-f]{40}$", root.GetProperty("baselineCommit").GetString()!);
        var entries = root.GetProperty("entries").EnumerateArray().ToArray();
        var ids = PlatformUpdatesCatalog.Entries.Select(e => e.Id).ToHashSet();
        Assert.Equal(ids.Order(), entries.Select(e => e.GetProperty("id").GetString()!).Order());
        Assert.Equal(ids.Count, root.GetProperty("catalogEntries").GetInt32());
        var commits = root.GetProperty("commits").EnumerateArray().ToArray();
        Assert.Equal(root.GetProperty("totalCommits").GetInt32(), commits.Length);
        Assert.Equal(commits.Length, commits.Select(c => c.GetProperty("commit").GetString()).Distinct().Count());
        foreach (var commit in commits)
        {
            Assert.Matches("^[0-9a-f]{40}$", commit.GetProperty("commit").GetString()!);
            var targets = commit.GetProperty("entryIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
            Assert.All(targets, id => Assert.Contains(id, ids));
            var classification = commit.GetProperty("classification").GetString();
            if (classification == "user_facing") Assert.NotEmpty(targets);
            else
            {
                Assert.Contains(classification, new[] { "excluded_non_user_facing", "merge_only" });
                Assert.False(string.IsNullOrWhiteSpace(commit.GetProperty("reason").GetString()));
            }
        }
        foreach (var entry in entries)
        {
            Assert.NotEmpty(entry.GetProperty("commits").EnumerateArray());
            Assert.NotEmpty(entry.GetProperty("changes").EnumerateArray());
            var update = PlatformUpdatesCatalog.Entries.Single(update => update.Id == entry.GetProperty("id").GetString());
            Assert.NotEmpty(update.Authors);
            Assert.Equal(update.Authors.Count, update.Authors.Distinct().Count());
            Assert.All(update.Authors, author => Assert.Contains(author, new[] { "Mathias", "Kasper" }));
            Assert.Equal(update.Authors, entry.GetProperty("authors").EnumerateArray().Select(value => value.GetString()!).ToArray());
            var evidence = entry.GetProperty("authorEvidence").EnumerateArray().ToArray();
            Assert.Equal(update.Authors.Order(), evidence.Select(item => item.GetProperty("author").GetString()!).Order());
            foreach (var contribution in evidence)
            {
                var references = contribution.GetProperty("commits").EnumerateArray().ToArray();
                Assert.NotEmpty(references);
                Assert.All(references, reference => Assert.Matches("^[0-9a-f]{40}$", reference.GetString()!));
            }
        }
    }
}
