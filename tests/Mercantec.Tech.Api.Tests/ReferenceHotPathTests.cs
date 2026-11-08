using System.Data.Common;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.References;
using Mercantec.Tech.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mercantec.Tech.Api.Tests;

public sealed class ReferenceHotPathTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly Commands audit = new();
    private AppDbContext db = null!;
    private ReferenceService service = null!;
    private readonly Guid reader = Guid.Parse("22222222-2222-4222-8222-222222222222"), author = Guid.NewGuid();
    private readonly Guid target = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private readonly Guid hold = Guid.NewGuid();
    private static EntityReference Ref(string kind, Guid id) => new(kind, id.ToString("D"));
    private ClaimsPrincipal Reader => new(new ClaimsIdentity([new Claim("sub", reader.ToString())], "test"));
    private const string Person = "AQMUAAcBDmRvY3VtZW50LXN0b3JlAw1lbnRpdHlNZW50aW9uKAAUAARraW5kAXcGcGVyc29uKAAUAAJpZAF3JDIyMjIyMjIyLTIyMjItNDIyMi04MjIyLTIyMjIyMjIyMjIyMgA=";
    private const string Linked = "AQYUAAcBDmRvY3VtZW50LXN0b3JlAw1lbnRpdHlNZW50aW9uKAAUAARraW5kAXcGcGVyc29uKAAUAAJpZAF3JDIyMjIyMjIyLTIyMjItNDIyMi04MjIyLTIyMjIyMjIyMjIyMocUAAMKZW50aXR5TGluaygAFAMEa2luZAF3DW5vdGVib29rLXBhZ2UoABQDAmlkAXckMzMzMzMzMzMtMzMzMy00MzMzLTgzMzMtMzMzMzMzMzMzMzMzAA==";

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(audit).Options);
        await db.Database.EnsureCreatedAsync(); service = new(db);
        db.Hold.Add(new() { Id = hold, Name = "Hold", Forloeb = new() { Id = Guid.NewGuid(), Name = "Hold", CreatedBySub = author } });
        db.Memberships.Add(new() { Id = Guid.NewGuid(), HoldId = hold, UserSub = reader });
        db.NotebookProfiles.Add(new() { Sub = reader, DisplayName = "Elev" });
        var notebook = new Notebook { Id = Guid.NewGuid(), OwnerSub = reader };
        db.NotebookDocuments.AddRange(new() { Id = target, Notebook = notebook, Title = "Målside" },
            new() { Id = Guid.NewGuid(), Notebook = notebook, Title = "Urelateret note, der ikke skal hentes" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    private async Task<NotionPage> Source(string state)
    {
        var id = Guid.NewGuid();
        var page = new NotionPage { Id = id, HoldId = hold, Collection = new() { Id = id, HoldId = hold, CreatedBySub = author },
            Title = "Kladde", PublishedTitle = "Udgivet kilde", CreatedBySub = author, PublishedAtUtc = DateTimeOffset.UtcNow,
            YjsState = GraphFixtures.Get("empty"), PublishedState = GraphFixtures.Get("empty") };
        db.NotionPages.Add(page); await db.SaveChangesAsync();
        await service.BaselineNotionAsync(id, default);
        await db.NotionPages.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.PublishedState, Convert.FromBase64String(state)));
        await service.RefreshNotionAsync(id, default, published: true);
        return page;
    }

    [Fact]
    public async Task BacklinksSelectOnlyTheRequestedTargetAndItsIncomingSources()
    {
        var source = await Source(Linked); audit.Text.Clear();
        var result = await service.BacklinksAsync(Reader, Ref("notebook-page", target), false, default);
        Assert.Equal(Ref("notion-page", source.Id), Assert.Single(result).Reference);
        AssertBoundedDocumentMetadata();
        var edges = Assert.Single(audit.Text, q => q.Contains("FROM \"entity_reference_links\""));
        Assert.Contains("WHERE", edges); Assert.Contains("\"TargetKind\" =", edges); Assert.Contains("\"TargetId\" =", edges);
    }

    [Fact]
    public async Task MentionInboxResolvesOnlyEventSourcesAndBatchesPresenceChecks()
    {
        for (var i = 0; i < 3; i++) await Source(Person);
        audit.Text.Clear();
        Assert.Equal(3, (await service.MentionsAsync(Reader, default)).Length);
        AssertBoundedDocumentMetadata();
        Assert.Single(audit.Text, q => q.Contains("FROM \"entity_reference_links\""));
    }

    private void AssertBoundedDocumentMetadata()
    {
        // These are the projections that a global catalogue would use to enumerate every readable
        // note/page. Hot paths must constrain their primary IDs or issue an empty-provider query.
        const string projection = "^SELECT \\\"(?<alias>[^\\\"]+)\\\"\\.\\\"Id\\\", \\\"[^\\\"]+\\\"\\.\\\"(?:Title|HoldId)\\\"";
        var queries = audit.Text.Where(q => Regex.IsMatch(q, projection))
            .Where(q => q.Contains("FROM \"notebook_documents\"") || q.Contains("FROM \"notion_pages\"")).ToArray();
        Assert.NotEmpty(queries);
        Assert.All(queries, q =>
        {
            var alias = Regex.Match(q, projection).Groups["alias"].Value;
            Assert.True(q.Contains("WHERE 0") || Regex.IsMatch(q, "\\\"" + Regex.Escape(alias) + "\\\"\\.\\\"Id\\\" (?:IN|=)"), q);
        });
    }

    private sealed class Commands : DbCommandInterceptor
    {
        public List<string> Text { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Text.Add(command.CommandText); return ValueTask.FromResult(result); }
    }
}
