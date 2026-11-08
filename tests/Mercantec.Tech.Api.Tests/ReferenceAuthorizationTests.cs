using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.References;
using Mercantec.Tech.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

/// <summary>Reference metadata must follow the same authorization as the actual destination.</summary>
public sealed class ReferenceAuthorizationTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private ReferenceService service = null!;
    private readonly Guid teacher = Guid.NewGuid(), student = Guid.NewGuid(), peer = Guid.NewGuid(), outsider = Guid.NewGuid();
    private Hold hold = null!, foreign = null!;
    private Membership member = null!, peerMember = null!;
    private Notebook notebook = null!;
    private NotebookDocument shared = null!, hiddenNote = null!;
    private NotionPage published = null!, draft = null!, theory = null!;
    private static EntityReference Ref(string kind, Guid id) => new(kind, id.ToString("D"));
    private static ClaimsPrincipal User(Guid sub, bool teacher = false) => new(new ClaimsIdentity(teacher
        ? [new Claim("sub", sub.ToString()), new Claim("role", "Underviser")]
        : [new Claim("sub", sub.ToString())], "test"));
    private Task<ResolvedReference[]> Resolve(params EntityReference[] references) => service.ResolveAsync(User(student), references, null, false, default);

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(); service = new(db);
        hold = new() { Id = Guid.NewGuid(), Name = "Mit hold", Forloeb = new() { Id = Guid.NewGuid(), Name = "H1", CreatedBySub = teacher } };
        foreign = new() { Id = Guid.NewGuid(), Name = "Fremmed hold", Forloeb = new() { Id = Guid.NewGuid(), Name = "H2", CreatedBySub = outsider } };
        member = new() { Id = Guid.NewGuid(), Hold = hold, UserSub = student, DisplayName = "Elev" };
        peerMember = new() { Id = Guid.NewGuid(), Hold = hold, UserSub = peer, DisplayName = "Makker" };
        db.Hold.AddRange(hold, foreign); db.Memberships.AddRange(member, peerMember);
        db.NotebookProfiles.AddRange(new() { Sub = teacher, DisplayName = "Underviser", IsTeacher = true },
            new() { Sub = student, DisplayName = "Elev" }, new() { Sub = peer, DisplayName = "Makker" },
            new() { Sub = outsider, DisplayName = "Anden bruger" });
        notebook = new() { Id = Guid.NewGuid(), OwnerSub = peer };
        shared = new() { Id = Guid.NewGuid(), Notebook = notebook, Title = "Delt side", YjsState = GraphFixtures.Get("empty") };
        hiddenNote = new() { Id = Guid.NewGuid(), Notebook = notebook, Title = "Privat søsterside", YjsState = GraphFixtures.Get("empty") };
        db.NotebookDocuments.AddRange(shared, hiddenNote);
        db.NotebookShares.Add(new() { Id = Guid.NewGuid(), NotebookId = notebook.Id, RecipientSub = student, PageId = shared.Id });
        published = Page("Privat kladdetitel", hold.Id, true); draft = Page("Ikke udgivet", hold.Id, false); theory = Page("Fælles teori", null, true);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private NotionPage Page(string title, Guid? holdId, bool publish)
    {
        var id = Guid.NewGuid();
        var collection = new NotionCollection { Id = id, HoldId = holdId, CreatedBySub = teacher };
        var page = new NotionPage { Id = id, Collection = collection, HoldId = holdId, Title = title, CreatedBySub = teacher,
            YjsState = GraphFixtures.Get("empty"), PublishedTitle = publish ? "Udgivet: " + title : null,
            PublishedState = publish ? GraphFixtures.Get("empty") : null, PublishedAtUtc = publish ? DateTimeOffset.UtcNow : null };
        db.NotionPages.Add(page); return page;
    }
    private FileAsset File(Guid owner, Guid? holdId, string name)
    {
        var id = Guid.NewGuid();
        var file = new FileAsset { Id = id, Key = "test/" + id, OwnerSub = owner, HoldId = holdId, OriginalName = name };
        db.FileAssets.Add(file); return file;
    }
    private LearningQuest Quest(string title, bool publish = true, string audience = "hold")
    {
        var quest = new LearningQuest { Id = Guid.NewGuid(), HoldId = hold.Id, Title = title, Published = publish, AudienceMode = audience };
        db.LearningQuests.Add(quest); return quest;
    }

    [Fact]
    public async Task Exact_page_share_resolves_only_that_page_and_whole_share_uses_the_real_notebook_route()
    {
        var references = new[] { Ref("notebook", notebook.Id), Ref("notebook-page", shared.Id), Ref("notebook-page", hiddenNote.Id) };
        var partial = await Resolve(references);
        Assert.Equal(Ref("notebook-page", shared.Id), Assert.Single(partial).Reference);
        db.NotebookShares.Add(new() { Id = Guid.NewGuid(), NotebookId = notebook.Id, RecipientSub = student }); await db.SaveChangesAsync();
        var complete = await Resolve(references);
        Assert.Equal(3, complete.Length);
        Assert.Equal($"/notebook?owner={notebook.Id:D}", complete.Single(r => r.Reference.Kind == "notebook").Href);
        await db.NotebookShares.Where(s => s.NotebookId == notebook.Id).ExecuteDeleteAsync();
        Assert.Empty(await Resolve(references));
    }

    [Fact]
    public async Task Hold_people_group_calendar_course_poll_material_and_quest_targets_keep_their_own_read_policies()
    {
        var mine = new LearningGroup { Id = Guid.NewGuid(), HoldId = hold.Id, Name = "Min gruppe" };
        mine.Members.Add(new() { MembershipId = member.Id });
        var others = new LearningGroup { Id = Guid.NewGuid(), HoldId = hold.Id, Name = "Andres gruppe" };
        others.Members.Add(new() { MembershipId = peerMember.Id }); db.LearningGroups.AddRange(mine, others);
        var course = new TeachingCourse { Id = Guid.NewGuid(), HoldId = hold.Id, Title = "Udgivet forløb", Published = true };
        var courseDraft = new TeachingCourse { Id = Guid.NewGuid(), HoldId = hold.Id, Title = "Forløbskladde" };
        db.TeachingCourses.AddRange(course, courseDraft);
        var poll = new PollRound { Id = Guid.NewGuid(), HoldId = hold.Id, Question = "Åbent spørgsmål", State = "open" };
        var pollDraft = new PollRound { Id = Guid.NewGuid(), HoldId = hold.Id, Question = "Privat spørgsmål", State = "draft" };
        db.PollRounds.AddRange(poll, pollDraft);
        var material = new CourseMaterial { Id = Guid.NewGuid(), HoldId = hold.Id, FileAsset = File(teacher, hold.Id, "Teori.pdf"), Title = "Udgivet materiale", Published = true };
        var materialDraft = new CourseMaterial { Id = Guid.NewGuid(), HoldId = hold.Id, FileAsset = File(teacher, hold.Id, "Kladde.pdf"), Title = "Materialekladde" };
        db.CourseMaterials.AddRange(material, materialDraft);
        var calendar = new HoldCalendarEvent { Id = Guid.NewGuid(), HoldId = hold.Id, Title = "Holdets begivenhed" }; db.HoldCalendarEvents.Add(calendar);
        var quest = Quest("Til alle"); var targeted = Quest("Kun makkeren", audience: "students"); var questDraft = Quest("Questkladde", false);
        db.QuestAudienceMemberships.Add(new() { Quest = targeted, MembershipId = peerMember.Id });
        await db.SaveChangesAsync();
        EntityReference[] allowed = [Ref("hold", hold.Id), Ref("person", outsider), Ref("member", peerMember.Id),
            Ref("group", mine.Id), Ref("event", calendar.Id), Ref("teaching-course", course.Id), Ref("poll", poll.Id), Ref("material", material.Id), Ref("quest", quest.Id)];
        EntityReference[] denied = [Ref("hold", foreign.Id), Ref("group", others.Id), Ref("teaching-course", courseDraft.Id),
            Ref("poll", pollDraft.Id), Ref("material", materialDraft.Id), Ref("quest", targeted.Id), Ref("quest", questDraft.Id)];
        var result = await Resolve([.. allowed, .. denied]);
        Assert.Equal(allowed.ToHashSet(), result.Select(r => r.Reference).ToHashSet());
        Assert.Contains($"&group={mine.Id:D}", result.Single(r => r.Reference.Kind == "group").Href);
        Assert.Contains($"&material={material.Id:D}", result.Single(r => r.Reference.Kind == "material").Href);
        var unrelated = await service.ResolveAsync(User(outsider), [.. allowed.Where(r => r.Kind != "person")], null, false, default);
        Assert.Empty(unrelated);
    }

    [Fact]
    public async Task Published_pages_collections_media_and_drawings_never_expose_draft_labels_or_ungranted_attachments()
    {
        var granted = new NotionAsset { Id = Guid.NewGuid(), PageId = published.Id, Key = "public-asset", OriginalName = "Udgivet billede.png" };
        var privateAsset = new NotionAsset { Id = Guid.NewGuid(), PageId = published.Id, Key = "draft-asset", OriginalName = "Kladdebillede.png" };
        var drawing = new Drawing { Id = Guid.NewGuid(), NotionPageId = published.Id, BlockId = "public-block" };
        var draftDrawing = new Drawing { Id = Guid.NewGuid(), NotionPageId = published.Id, BlockId = "draft-block" };
        db.NotionAssets.AddRange(granted, privateAsset); db.Drawings.AddRange(drawing, draftDrawing);
        await db.SaveChangesAsync();
        await db.NotionPages.Where(p => p.Id == published.Id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.PublishedAssetIdsJson, JsonSerializer.Serialize(new[] { granted.Id }))
            .SetProperty(p => p.PublishedDrawingsJson, JsonSerializer.Serialize(new[] { new DrawingPublicationBinding(drawing.Id, drawing.BlockId, "live", null, 1, new string('a', 64)) }, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        EntityReference[] allowed = [Ref("notion-page", published.Id), Ref("collection", published.Id), Ref("notion-page", theory.Id),
            Ref("notion-asset", granted.Id), Ref("drawing", drawing.Id)];
        EntityReference[] denied = [Ref("notion-page", draft.Id), Ref("collection", draft.Id), Ref("notion-asset", privateAsset.Id), Ref("drawing", draftDrawing.Id)];
        var studentView = await Resolve([.. allowed, .. denied]);
        Assert.Equal(allowed.ToHashSet(), studentView.Select(r => r.Reference).ToHashSet());
        Assert.All(studentView.Where(r => r.Reference.Kind is "notion-page" or "collection"), r => Assert.StartsWith("Udgivet:", r.Title));
        Assert.All(studentView, r => Assert.False(r.CanRename));
        var preview = await service.ResolveAsync(User(teacher, true), [.. allowed, .. denied], null, true, default);
        Assert.Equal(allowed.ToHashSet(), preview.Select(r => r.Reference).ToHashSet());
        var manager = await service.ResolveAsync(User(teacher, true), denied, null, false, default);
        Assert.Equal(denied.ToHashSet(), manager.Select(r => r.Reference).ToHashSet());
        await db.NotionPages.Where(p => p.Id == published.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.PublishedAtUtc, (DateTimeOffset?)null));
        Assert.Empty(await Resolve(Ref("notion-page", published.Id), Ref("collection", published.Id), Ref("notion-asset", granted.Id), Ref("drawing", drawing.Id)));
    }

    [Fact]
    public async Task Personal_attachment_grants_and_revocation_apply_to_files_and_drawings_as_well_as_pages()
    {
        var granted = new NotebookAsset { Id = Guid.NewGuid(), DocumentId = shared.Id, Key = "granted-note-file", OriginalName = "Delt billede.png" };
        var unreferenced = new NotebookAsset { Id = Guid.NewGuid(), DocumentId = shared.Id, Key = "unused-note-file", OriginalName = "Privat upload.png" };
        var drawing = new Drawing { Id = Guid.NewGuid(), NotebookDocumentId = shared.Id, BlockId = "note-drawing" };
        db.NotebookAssets.AddRange(granted, unreferenced); db.Drawings.Add(drawing); await db.SaveChangesAsync();
        await db.NotebookDocuments.Where(d => d.Id == shared.Id).ExecuteUpdateAsync(s => s.SetProperty(d => d.AssetIdsJson, JsonSerializer.Serialize(new[] { granted.Id })));
        var refs = new[] { Ref("notebook-asset", granted.Id), Ref("notebook-asset", unreferenced.Id), Ref("drawing", drawing.Id) };
        Assert.Equal(new[] { refs[0], refs[2] }.ToHashSet(), (await Resolve(refs)).Select(r => r.Reference).ToHashSet());
        Assert.Equal(3, (await service.ResolveAsync(User(peer), refs, null, false, default)).Length);
        Assert.Equal(2, (await service.ResolveAsync(User(teacher, true), refs, null, false, default)).Length);
        await db.NotebookShares.Where(s => s.NotebookId == notebook.Id).ExecuteDeleteAsync();
        Assert.Empty(await Resolve(refs));
    }

    [Fact]
    public async Task Generic_file_and_submission_targets_follow_historical_participation_and_current_hold_access()
    {
        var own = File(student, null, "Min egen fil.txt"); var foreignOwned = File(student, foreign.Id, "Tilhoerer fremmed hold.txt");
        var materialFile = File(teacher, hold.Id, "Offentligt materiale.pdf"); var hiddenFile = File(peer, hold.Id, "Privat aflevering.zip");
        db.CourseMaterials.Add(new() { Id = Guid.NewGuid(), HoldId = hold.Id, FileAsset = materialFile, Title = "Materiale", Published = true });
        var quest = Quest("Historisk aflevering", audience: "students");
        db.QuestAudienceMemberships.Add(new() { Quest = quest, MembershipId = peerMember.Id });
        var submission = new QuestSubmission { Id = Guid.NewGuid(), Quest = quest, OwnerKey = "u:" + student.ToString("N"), CreatedBySub = student, State = "submitted" };
        var revisionFile = File(peer, hold.Id, "Historisk fil.zip");
        var revision = new SubmissionRevision { Id = Guid.NewGuid(), Submission = submission, Number = 1, SubmittedBySub = student };
        revision.Participants.Add(new() { MembershipId = member.Id, UserSub = student, DisplayName = "Elev" });
        revision.Files.Add(new() { FileAsset = revisionFile }); db.SubmissionRevisions.Add(revision);
        var otherSubmission = new QuestSubmission { Id = Guid.NewGuid(), Quest = quest, OwnerKey = "u:" + peer.ToString("N"), CreatedBySub = peer };
        db.QuestSubmissions.Add(otherSubmission); await db.SaveChangesAsync();
        var refs = new[] { Ref("file", own.Id), Ref("file", foreignOwned.Id), Ref("file", materialFile.Id), Ref("file", hiddenFile.Id), Ref("file", revisionFile.Id),
            Ref("submission", submission.Id), Ref("submission", otherSubmission.Id) };
        var expected = new[] { refs[0], refs[2], refs[4], refs[5] }.ToHashSet();
        Assert.Equal(expected, (await Resolve(refs)).Select(r => r.Reference).ToHashSet());
        var policy = new FileAuthorization(db);
        foreach (var file in new[] { own, foreignOwned, materialFile, hiddenFile, revisionFile })
            Assert.Equal(await policy.CanReadAsync(file, User(student), default), (await Resolve(Ref("file", file.Id))).Length == 1);
        await db.Memberships.Where(m => m.Id == member.Id).ExecuteDeleteAsync();
        Assert.Empty(await Resolve(Ref("file", revisionFile.Id), Ref("submission", submission.Id)));
    }

    [Fact]
    public async Task Roster_and_person_links_are_graph_edges_while_only_explicit_mentions_notify()
    {
        const string roster = "AQMUAAcBDmRvY3VtZW50LXN0b3JlAwtjbGFzc1Jvc3RlcigAFAAGaG9sZElkAXckMTExMTExMTEtMTExMS00MTExLTgxMTEtMTExMTExMTExMTExKAAUAA1tZW1iZXJzaGlwSWRzAXckMjIyMjIyMjItMjIyMi00MjIyLTgyMjItMjIyMjIyMjIyMjIyAA==";
        const string link = "AQMUAAcBDmRvY3VtZW50LXN0b3JlAwplbnRpdHlMaW5rKAAUAARraW5kAXcGcGVyc29uKAAUAAJpZAF3JDMzMzMzMzMzLTMzMzMtNDMzMy04MzMzLTMzMzMzMzMzMzMzMwA=";
        const string mention = "AQMUAAcBDmRvY3VtZW50LXN0b3JlAw1lbnRpdHlNZW50aW9uKAAUAARraW5kAXcGcGVyc29uKAAUAAJpZAF3JDMzMzMzMzMzLTMzMzMtNDMzMy04MzMzLTMzMzMzMzMzMzMzMwA=";
        byte[] Fixture(string encoded)
        {
            var state = Convert.FromBase64String(encoded);
            foreach (var (from, to) in new[] { ("11111111-1111-4111-8111-111111111111", hold.Id),
                ("22222222-2222-4222-8222-222222222222", member.Id), ("33333333-3333-4333-8333-333333333333", student) })
            {
                var at = state.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(from));
                if (at >= 0) System.Text.Encoding.UTF8.GetBytes(to.ToString("D")).CopyTo(state.AsSpan(at));
            }
            return state;
        }
        async Task Set(string encoded)
        {
            await db.NotionPages.Where(p => p.Id == published.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.PublishedState, Fixture(encoded)));
            await service.RefreshNotionAsync(published.Id, default, published: true);
        }
        await service.BaselineNotionAsync(published.Id, default);
        await Set(roster);
        Assert.Empty(await db.ReferenceMentionEvents.ToArrayAsync());
        Assert.Contains(await service.BacklinksAsync(User(student), Ref("member", member.Id), true, default), r => r.Reference == Ref("notion-page", published.Id));
        await Set(link);
        Assert.Empty(await db.ReferenceMentionEvents.ToArrayAsync());
        await Set(mention);
        Assert.Single(await service.MentionsAsync(User(student), default));
        await Set(link);
        Assert.Empty(await service.MentionsAsync(User(student), default));
        Assert.Contains(await service.BacklinksAsync(User(student), Ref("person", student), true, default), r => r.Reference == Ref("notion-page", published.Id));
    }

    [Fact]
    public async Task Public_catalog_types_are_available_to_authenticated_people_but_no_reference_resolves_anonymously()
    {
        var search = await service.SearchAsync(User(student), "", null, null, 0, 1000, false, default);
        string[] kinds = ["learning-plan", "subject", "lesson", "project", "badge", "character"];
        var targets = kinds.Select(kind => search.Items.First(r => r.Reference.Kind == kind).Reference).ToArray();
        Assert.Equal(kinds.Length, (await Resolve(targets)).Length);
        Assert.Empty(await service.ResolveAsync(new(), targets, null, false, default));
        Assert.Empty((await service.SearchAsync(new(), "", null, null, 0, 30, false, default)).Items);
    }
}
