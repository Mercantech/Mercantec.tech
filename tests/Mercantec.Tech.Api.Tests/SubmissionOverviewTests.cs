using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class SubmissionOverviewTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), outsider = Guid.NewGuid();
    private readonly DateTimeOffset now = DateTimeOffset.Parse("2026-11-06T10:00:00Z");
    private Hold hold = null!;
    private Membership[] pupils = [];
    private LearningQuest individual = null!, team = null!, optional = null!;
    private LearningGroup group = null!, secondGroup = null!;
    private LearningGroupSet set = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        hold = new() { Id = Guid.NewGuid(), Name = "H3", Forloeb = new() { Id = Guid.NewGuid(), Name = "H3", CreatedBySub = teacher } };
        db.Hold.Add(hold);
        pupils = Enumerable.Range(1, 5).Select(i => new Membership { Id = Guid.NewGuid(), Hold = hold,
            UserSub = i == 3 ? null : Guid.NewGuid(), LectioId = $"pupil-{i}", DisplayName = $"Elev {i}" }).ToArray();
        db.Memberships.AddRange(pupils);
        db.Memberships.Add(new() { Id = Guid.NewGuid(), Hold = hold, UserSub = teacher, DisplayName = "Underviser" });
        set = new() { Id = Guid.NewGuid(), HoldId = hold.Id, Name = "Projektgrupper" };
        db.LearningGroupSets.Add(set);
        group = AddGroup("Projekt 1", set.Id, pupils[0], pupils[2], pupils[3]);
        secondGroup = AddGroup("Projekt 2", set.Id, pupils[3], pupils[4]);
        individual = AddQuest("Individuel", "either");
        team = AddQuest("Gruppeopgave", "group"); team.GroupSetId = set.Id;
        optional = AddQuest("Frivillig", "individual"); optional.Required = false; optional.Kind = "side";
        var draft = AddQuest("Kladde", "individual"); draft.Published = false;
        AddSubmission(individual, pupils[0], "approved", now.AddHours(-1));
        AddSubmission(individual, pupils[1], "submitted", now.AddDays(-3));
        AddSubmission(individual, pupils[3], "draft", null);
        AddSubmission(individual, pupils[4], "changes-requested", now.AddDays(-2));
        // Participant snapshot includes pupil 2, even though the group has since changed to pupil 4.
        AddSubmission(team, pupils[0], "submitted", now.AddDays(-3), group, [pupils[0], pupils[1], pupils[2]]);
        AddSubmission(team, pupils[3], "draft", null, secondGroup);
        // An overlapping group draft must not erase an earlier individual delivery.
        var sparringGroup = AddGroup("Sparring", set.Id, pupils[0], pupils[4]);
        AddSubmission(individual, pupils[0], "draft", null, sparringGroup);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    [Fact]
    public async Task CountsPupilsAndMissingDraftsWithoutTreatingZeroReviewsAsComplete()
    {
        var view = await Build();
        var quest = view.Quests.Single(q => q.QuestId == individual.Id);
        Assert.Equal(new SubmissionProgress(5, 3, 2, 2, 1, 1, 1, 1, 0), quest.Progress);
        Assert.Equal("approved", quest.Members.Single(m => m.MembershipId == pupils[0].Id).State);
        Assert.True(quest.Members.Single(m => m.MembershipId == pupils[0].Id).SubmittedLate);
        Assert.Equal("missing", quest.Members.Single(m => m.MembershipId == pupils[2].Id).State);
        Assert.DoesNotContain(quest.Members, m => m.DisplayName == "Underviser");
    }

    [Fact]
    public async Task GroupDeliveryUsesSubmittedSnapshotIncludingShadowPupilAndCountsEachPupilOnce()
    {
        var quest = (await Build()).Quests.Single(q => q.QuestId == team.Id);
        Assert.Equal(new SubmissionProgress(5, 3, 2, 2, 3, 0, 0, 2, 0), quest.Progress);
        Assert.NotNull(quest.Members.Single(m => m.MembershipId == pupils[1].Id).SubmittedAtUtc);
        Assert.NotNull(quest.Members.Single(m => m.MembershipId == pupils[2].Id).SubmittedAtUtc);
        Assert.Null(quest.Members.Single(m => m.MembershipId == pupils[3].Id).SubmittedAtUtc);
    }

    [Fact]
    public async Task OptionalAndDraftQuestsDoNotCreateRequiredMissingAlerts()
    {
        var view = await Build();
        Assert.Equal(10, view.RequiredProgress.Expected);
        Assert.Equal(4, view.RequiredProgress.Missing);
        Assert.Equal(4, view.RequiredProgress.Overdue);
        Assert.Equal(5, view.Quests.Single(q => q.QuestId == optional.Id).Progress.Missing);
        Assert.Equal(0, view.Quests.Single(q => q.QuestId == optional.Id).Progress.Overdue);
        Assert.Empty(view.Quests.Single(q => !q.Published).Members);
    }

    [Fact]
    public async Task PreviousGroupParticipantsKeepTheirOwnRevisionAndDecision()
    {
        var submission = await db.QuestSubmissions.Include(s => s.Revisions).ThenInclude(r => r.Participants)
            .SingleAsync(s => s.QuestId == team.Id && s.GroupId == group.Id);
        var first = submission.Revisions.Single();
        db.SubmissionFeedback.Add(new() { Id = Guid.NewGuid(), RevisionId = first.Id, TeacherSub = teacher,
            Text = "Godkendt", Decision = "approved", CreatedAtUtc = now.AddDays(-2) });
        var second = new SubmissionRevision { Id = Guid.NewGuid(), SubmissionId = submission.Id,
            Number = 2, SubmittedAtUtc = now.AddHours(-1), SubmittedBySub = pupils[0].UserSub!.Value };
        foreach (var pupil in new[] { pupils[0], pupils[3] }) second.Participants.Add(new()
            { MembershipId = pupil.Id, UserSub = pupil.UserSub, DisplayName = pupil.DisplayName! });
        db.SubmissionRevisions.Add(second);
        await db.SaveChangesAsync();
        var rows = (await Build()).Quests.Single(q => q.QuestId == team.Id).Members;
        var originalPupil = rows.Single(r => r.MembershipId == pupils[1].Id);
        Assert.Equal("approved", originalPupil.State);
        Assert.Equal(1, originalPupil.RevisionNumber);
        Assert.False(originalPupil.IsCurrentRevision);
        Assert.Equal(2, rows.Single(r => r.MembershipId == pupils[3].Id).RevisionNumber);
    }

    [Fact]
    public async Task AccessIsRestrictedToManagingTeachersAndCourseFilterCannotCrossHold()
    {
        Assert.IsType<OkObjectResult>(await Api(teacher, true).Get(hold.Id, null, default));
        Assert.IsType<NotFoundResult>(await Api(pupils[0].UserSub!.Value).Get(hold.Id, null, default));
        Assert.IsType<NotFoundResult>(await Api(outsider, true).Get(hold.Id, null, default));
        Assert.IsType<NotFoundResult>(await Api(teacher, true).Get(hold.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task PublishedEmptyQuestShowsEveryPupilMissingAndUngroupedWhereRelevant()
    {
        var quest = AddQuest("Ny gruppeopgave", "group");
        var empty = new LearningGroupSet { Id = Guid.NewGuid(), HoldId = hold.Id, Name = "Endnu ingen grupper" };
        db.LearningGroupSets.Add(empty); quest.GroupSetId = empty.Id;
        await db.SaveChangesAsync();
        var row = (await Build()).Quests.Single(q => q.QuestId == quest.Id);
        Assert.Equal(5, row.Progress.Missing);
        Assert.Equal(5, row.Progress.Ungrouped);
        Assert.Equal(0, row.Progress.Delivered);
    }

    [Fact]
    public async Task ReviewTasksCountActualSubmissionsEvenWhenOnePupilHasANewerApprovedGroupDelivery()
    {
        var old = await db.QuestSubmissions.SingleAsync(s => s.QuestId == individual.Id && s.GroupId == null && s.CreatedBySub == pupils[0].UserSub);
        old.State = "submitted";
        var newer = await db.QuestSubmissions.SingleAsync(s => s.QuestId == individual.Id && s.GroupId != null);
        newer.State = "approved";
        var revision = new SubmissionRevision { Id = Guid.NewGuid(), SubmissionId = newer.Id, Number = 1,
            SubmittedAtUtc = now.AddMinutes(1), SubmittedBySub = pupils[0].UserSub!.Value };
        foreach (var pupil in new[] { pupils[0], pupils[4] }) revision.Participants.Add(new()
            { MembershipId = pupil.Id, UserSub = pupil.UserSub, DisplayName = pupil.DisplayName! });
        db.SubmissionRevisions.Add(revision);
        await db.SaveChangesAsync();
        var result = (await Build()).Quests.Single(q => q.QuestId == individual.Id);
        Assert.Equal(2, result.ReviewTasks);
        Assert.Equal(3, result.Progress.Delivered);
        Assert.Equal(1, result.Progress.PendingReview);
    }

    [Fact]
    public async Task AudienceRestrictsExpectedRosterAndResubmissionDoesNotMarkAnOntimeFirstAttemptLate()
    {
        individual.AudienceMode = "students";
        db.LearningQuests.Update(individual);
        db.QuestAudienceMemberships.Add(new() { QuestId = individual.Id, MembershipId = pupils[1].Id });
        var submission = await db.QuestSubmissions.SingleAsync(s => s.QuestId == individual.Id && s.CreatedBySub == pupils[1].UserSub);
        var revision = new SubmissionRevision { Id = Guid.NewGuid(), SubmissionId = submission.Id, Number = 2,
            SubmittedAtUtc = now, SubmittedBySub = pupils[1].UserSub!.Value };
        revision.Participants.Add(new() { MembershipId = pupils[1].Id, UserSub = pupils[1].UserSub, DisplayName = pupils[1].DisplayName! });
        db.SubmissionRevisions.Add(revision);
        await db.SaveChangesAsync();
        var result = (await Build()).Quests.Single(q => q.QuestId == individual.Id);
        var member = Assert.Single(result.Members);
        Assert.Equal(pupils[1].Id, member.MembershipId);
        Assert.Equal(2, member.RevisionNumber);
        Assert.False(member.SubmittedLate);
        Assert.Equal(1, result.Progress.Expected);
    }

    [Fact]
    public async Task FutureRepeatedQuestDoesNotInflateTodaysMissingCount()
    {
        var quest = AddQuest("Morgendagens daily", "individual");
        quest.SeriesId = Guid.NewGuid(); quest.OccurrenceDate = new DateOnly(2026, 11, 7);
        quest.DeadlineUtc = now.AddDays(1);
        await db.SaveChangesAsync();
        var view = await Build();
        Assert.True(view.Quests.Single(q => q.QuestId == quest.Id).Upcoming);
        Assert.Equal(10, view.RequiredProgress.Expected);
        Assert.Equal(4, view.RequiredProgress.Missing);
    }

    [Fact]
    public async Task TargetedPupilNeedsAGroupWhoseEntireMembershipIsInTheAudience()
    {
        var quest = AddQuest("Kun elev 1", "group");
        quest.AudienceMode = "students"; quest.GroupSetId = set.Id;
        db.QuestAudienceMemberships.Add(new() { QuestId = quest.Id, MembershipId = pupils[0].Id });
        AddSubmission(quest, pupils[0], "draft", null, group);
        await db.SaveChangesAsync();

        var result = (await Build()).Quests.Single(q => q.QuestId == quest.Id);
        var pupil = Assert.Single(result.Members);
        Assert.Equal(pupils[0].Id, pupil.MembershipId);
        Assert.True(pupil.Ungrouped);
        Assert.Equal("draft", pupil.State);
        Assert.NotNull(pupil.SubmissionId);

        AddGroup("Gyldig målgruppe", set.Id, pupils[0]);
        await db.SaveChangesAsync();
        Assert.False(Assert.Single((await Build()).Quests.Single(q => q.QuestId == quest.Id).Members).Ungrouped);
    }

    [Fact]
    public async Task ArchivedGroupsRemainVisibleButDoNotSatisfyAnUnsubmittedPupilsGroupRequirement()
    {
        var quest = AddQuest("Arkiveret gruppekladde", "group"); quest.GroupSetId = set.Id;
        AddSubmission(quest, pupils[0], "draft", null, group);
        set.Archived = true; db.LearningGroupSets.Update(set);
        await db.SaveChangesAsync();

        var view = await Build();
        var result = view.Quests.Single(q => q.QuestId == quest.Id);
        Assert.Equal(5, result.Progress.Ungrouped);
        var draft = result.Members.Single(m => m.MembershipId == pupils[0].Id);
        Assert.Equal("draft", draft.State);
        Assert.NotNull(draft.SubmissionId);
        Assert.Equal(group.Id, draft.GroupId);
        var historical = view.Quests.Single(q => q.QuestId == team.Id);
        Assert.Equal(3, historical.Progress.Delivered);
        Assert.Equal(2, historical.Progress.Ungrouped);
        Assert.All(historical.Members.Where(m => m.SubmittedAtUtc != null), m => Assert.False(m.Ungrouped));
    }

    [Fact]
    public async Task GroupAudienceRequiresTheNamedGroupEvenWhenItsPupilHasAnotherEligibleComposition()
    {
        var quest = AddQuest("Kun projekt 1", "group"); quest.AudienceMode = "groups";
        db.QuestAudienceGroups.Add(new() { QuestId = quest.Id, GroupId = group.Id });
        var alternativeSet = new LearningGroupSet { Id = Guid.NewGuid(), HoldId = hold.Id, Name = "Aktive makkerpar" };
        db.LearningGroupSets.Add(alternativeSet);
        AddGroup("Andet gyldigt medlemssæt", alternativeSet.Id, pupils[0]);
        set.Archived = true; db.LearningGroupSets.Update(set);
        await db.SaveChangesAsync();

        var result = (await Build()).Quests.Single(q => q.QuestId == quest.Id);
        Assert.Equal(3, result.Progress.Expected);
        Assert.Equal(3, result.Progress.Ungrouped);
        Assert.True(result.Members.Single(m => m.MembershipId == pupils[0].Id).Ungrouped);
    }

    private Task<HoldSubmissionOverview> Build() => new SubmissionOverviewBuilder(db).BuildAsync(hold.Id, null, default, now);
    private SubmissionOverviewController Api(Guid sub, bool teacherRole = false) => new(db, new(db))
    { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(
        new[] { new Claim("sub", sub.ToString()) }.Concat(teacherRole ? [new Claim(ClaimTypes.Role, "Underviser")] : []), "test")) } } };
    private LearningQuest AddQuest(string title, string mode)
    {
        var quest = new LearningQuest { Id = Guid.NewGuid(), HoldId = hold.Id, Title = title,
            Published = true, Required = true, SubmissionMode = mode, DeadlineUtc = now.AddDays(-1) };
        db.LearningQuests.Add(quest); return quest;
    }
    private LearningGroup AddGroup(string name, Guid setId, params Membership[] members)
    {
        var group = new LearningGroup { Id = Guid.NewGuid(), HoldId = hold.Id, Name = name, GroupSetId = setId };
        foreach (var member in members) group.Members.Add(new() { MembershipId = member.Id });
        db.LearningGroups.Add(group); return group;
    }
    private void AddSubmission(LearningQuest quest, Membership actor, string state, DateTimeOffset? submitted,
        LearningGroup? group = null, Membership[]? participants = null)
    {
        var submission = new QuestSubmission { Id = Guid.NewGuid(), QuestId = quest.Id, State = state,
            CreatedBySub = actor.UserSub!.Value, GroupId = group?.Id, UpdatedAtUtc = now,
            OwnerKey = group is null ? $"u:{actor.UserSub:N}" : $"g:{group.Id:N}" };
        if (submitted is DateTimeOffset at)
        {
            var revision = new SubmissionRevision { Id = Guid.NewGuid(), Number = 1,
                SubmittedBySub = actor.UserSub.Value, SubmittedAtUtc = at };
            foreach (var member in participants ?? [actor]) revision.Participants.Add(new()
                { MembershipId = member.Id, UserSub = member.UserSub, DisplayName = member.DisplayName! });
            submission.Revisions.Add(revision);
        }
        db.QuestSubmissions.Add(submission);
    }
}
