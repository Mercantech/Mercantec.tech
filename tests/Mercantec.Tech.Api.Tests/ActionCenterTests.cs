using System.Security.Claims;
using System.Text.Json;
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

public sealed partial class ActionCenterTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;
    private readonly Guid _teacher = Guid.NewGuid(), _otherTeacher = Guid.NewGuid(), _student = Guid.NewGuid(), _peer = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private Hold _hold = null!, _otherHold = null!;
    private Membership _member = null!, _peerMember = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _hold = Hold("H3", _teacher); _otherHold = Hold("H4", _otherTeacher);
        _member = Member(_hold, _student); _peerMember = Member(_hold, _peer);
        Member(_otherHold, _student);
        await _db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }
    private Hold Hold(string name, Guid owner)
    {
        var hold = new Hold { Id = Guid.NewGuid(), Name = name, Forloeb = new() { Id = Guid.NewGuid(), Name = name, CreatedBySub = owner } };
        _db.Hold.Add(hold); return hold;
    }
    private Membership Member(Hold hold, Guid user)
    {
        var member = new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = user, DisplayName = "Elev" };
        _db.Memberships.Add(member); return member;
    }
    private LearningQuest Quest(string title, Hold? hold = null, DateTimeOffset? deadline = null, string audience = "hold")
    {
        var quest = new LearningQuest { Id = Guid.NewGuid(), HoldId = (hold ?? _hold).Id, Title = title,
            Description = "Sensitive assignment body", Published = true, Required = true, DeadlineUtc = deadline, AudienceMode = audience };
        _db.LearningQuests.Add(quest); return quest;
    }
    private LearningGroup Group(params Membership[] members)
    {
        var group = new LearningGroup { Id = Guid.NewGuid(), HoldId = _hold.Id, Name = "Projektgruppe" };
        foreach (var member in members) group.Members.Add(new() { MembershipId = member.Id });
        _db.LearningGroups.Add(group); return group;
    }
    private QuestSubmission Submission(LearningQuest quest, string state, Membership[] participants, LearningGroup? group = null, DateTimeOffset? submitted = null)
    {
        var submission = new QuestSubmission { Id = Guid.NewGuid(), QuestId = quest.Id, CreatedBySub = _student,
            GroupId = group?.Id, OwnerKey = group is not null ? $"g:{group.Id:N}" : $"u:{_student:N}",
            State = state, UpdatedAtUtc = _now, DraftText = "Private draft text" };
        _db.QuestSubmissions.Add(submission);
        if (participants.Length > 0) Revision(submission, 1, participants, submitted ?? _now.AddDays(-1));
        return submission;
    }
    private SubmissionRevision Revision(QuestSubmission submission, int number, Membership[] participants, DateTimeOffset submitted)
    {
        var revision = new SubmissionRevision { Id = Guid.NewGuid(), Submission = submission, Number = number,
            SubmittedBySub = _student, SubmittedAtUtc = submitted, Text = "Private submitted text" };
        foreach (var member in participants) revision.Participants.Add(new() { MembershipId = member.Id, UserSub = member.UserSub, DisplayName = "Private participant" });
        _db.SubmissionRevisions.Add(revision); return revision;
    }
    private PollRound Poll(string question, string state = "open", Hold? hold = null)
    {
        var poll = new PollRound { Id = Guid.NewGuid(), HoldId = (hold ?? _hold).Id, Question = question, State = state,
            Kind = "text", CreatedBySub = _teacher, CreatedAtUtc = _now.UtcDateTime };
        _db.PollRounds.Add(poll); return poll;
    }
    private ActionCenterController Api(Guid? subject, bool teacher = false)
    {
        var claims = new List<Claim>();
        if (subject.HasValue) claims.Add(new("sub", subject.Value.ToString()));
        if (teacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(_db, new(_db)) { ControllerContext = new() { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity(claims, subject.HasValue ? "test" : null)) } } };
    }
    private static ActionCenterSnapshot Read(IActionResult result) => Assert.IsType<ActionCenterSnapshot>(Assert.IsType<OkObjectResult>(result).Value);
    private async Task<ActionCenterSnapshot> Student() { await _db.SaveChangesAsync(); return Read(await Api(_student).Get(null, default)); }

    [Fact]
    public async Task Caller_identity_and_preview_target_are_authorized_independently()
    {
        Assert.IsType<UnauthorizedResult>(await Api(null).Get(null, default));
        Assert.IsType<UnauthorizedResult>(await Api(Guid.Empty).Get(null, default));
        Assert.IsType<ForbidResult>(await Api(_student).Get(_peer, default));
        Assert.IsType<ForbidResult>(await Api(_student).Get(_student, default));
        Assert.IsType<BadRequestObjectResult>(await Api(_teacher, true).Get(Guid.Empty, default));
        Assert.IsType<NotFoundResult>(await Api(Guid.NewGuid(), true).Get(_student, default));
        Assert.IsType<NotFoundResult>(await Api(_teacher, true).Get(Guid.NewGuid(), default));
        Assert.Empty(Read(await Api(Guid.NewGuid()).Get(null, default)).Items);
    }

    [Fact]
    public async Task Required_current_assignments_distinguish_overdue_future_and_unset_deadlines()
    {
        var overdue = Quest("Forfalden", deadline: _now.AddDays(-1));
        var future = Quest("Frist i fremtiden", deadline: _now.AddDays(2));
        var unset = Quest("Ingen frist");
        var optional = Quest("Frivillig", deadline: _now.AddDays(-1)); optional.Required = false;
        var draft = Quest("Kladde", deadline: _now.AddDays(-1)); draft.Published = false;
        var repeated = Quest("Morgendagens daily", deadline: _now.AddDays(1)); repeated.OccurrenceDate = DateOnly.FromDateTime(_now.AddDays(2).DateTime);
        var course = new TeachingCourse { Id = Guid.NewGuid(), HoldId = _hold.Id, Title = "Web API", Published = true };
        course.Resources.Add(new() { Kind = "quest", ResourceId = overdue.Id }); _db.TeachingCourses.Add(course);
        var result = await Student();
        Assert.Equal(3, result.Items.Length);
        var red = result.Items.Single(i => i.ResourceId == overdue.Id);
        Assert.Equal("important", red.Severity); Assert.Equal("overdue", red.State);
        Assert.Equal(course.Id, red.CourseId); Assert.Equal(_hold.ForloebId, red.WorkspaceId);
        Assert.Equal(overdue.Id, result.Items[0].ResourceId);
        Assert.All(result.Items.Where(i => i.ResourceId != overdue.Id), i => { Assert.Equal("optional", i.Severity); Assert.Equal("missing", i.State); });
        Assert.Contains(result.Items, i => i.ResourceId == future.Id);
        Assert.Contains(result.Items, i => i.ResourceId == unset.Id);
    }

    [Fact]
    public async Task Current_students_and_groups_audiences_do_not_turn_historical_read_access_into_new_actions()
    {
        var targeted = Quest("Kun mig", audience: "students");
        var excluded = Quest("Kun anden elev", audience: "students");
        _db.QuestAudienceMemberships.AddRange(new() { QuestId = targeted.Id, MembershipId = _member.Id }, new() { QuestId = excluded.Id, MembershipId = _peerMember.Id });
        var ownGroup = Group(_member); var otherGroup = Group(_peerMember);
        var grouped = Quest("Min gruppe", audience: "groups");
        var otherGrouped = Quest("Anden gruppe", audience: "groups");
        _db.QuestAudienceGroups.AddRange(new() { QuestId = grouped.Id, GroupId = ownGroup.Id }, new() { QuestId = otherGrouped.Id, GroupId = otherGroup.Id });
        Submission(excluded, "changes-requested", [_member]);
        var result = await Student();
        Assert.True(await new QuestAudienceAccess(_db).CanReadAsync(excluded, _student, default));
        Assert.Equal(new[] { grouped.Id, targeted.Id }.Order(), result.Items.Select(i => i.ResourceId).Order());
    }

    [Fact]
    public async Task New_draft_and_changed_group_do_not_erase_a_personal_approved_receipt()
    {
        var quest = Quest("Allerede afleveret", deadline: _now.AddDays(-2));
        Submission(quest, "approved", [_member]);
        Submission(quest, "draft", [], Group(_member));
        var team = Quest("Gruppen har ændret sig", deadline: _now.AddDays(-2)); team.SubmissionMode = "group";
        var group = Group(_peerMember);
        var submission = Submission(team, "changes-requested", [_member, _peerMember], group);
        var original = submission.Revisions.Single();
        _db.SubmissionFeedback.Add(new() { Id = Guid.NewGuid(), RevisionId = original.Id, TeacherSub = _teacher,
            Decision = "approved", Text = "Private feedback", CreatedAtUtc = _now.AddHours(-3) });
        Revision(submission, 2, [_peerMember], _now.AddHours(-1));
        Assert.Empty((await Student()).Items);
    }

    [Fact]
    public async Task Returned_work_is_important_and_only_the_targets_personal_receipts_are_credited()
    {
        var returned = Quest("Retur", deadline: _now.AddDays(3));
        var returnedSubmission = Submission(returned, "changes-requested", [_member]);
        var reassigned = Quest("Forkert snapshot-identitet", deadline: _now.AddDays(-1));
        var other = Submission(reassigned, "approved", [_member]);
        other.Revisions.Single().Participants.Single().UserSub = _peer;
        var claimed = Quest("Claimed shadow");
        var shadow = Submission(claimed, "submitted", [_member]);
        shadow.Revisions.Single().Participants.Single().UserSub = null;
        var result = await Student();
        Assert.Equal(2, result.Items.Length);
        var action = result.Items.Single(i => i.ResourceId == returned.Id);
        Assert.Equal("changes-requested", action.State); Assert.Equal("important", action.Severity);
        Assert.Equal(returnedSubmission.Id, action.SubmissionId);
        Assert.Equal("overdue", result.Items.Single(i => i.ResourceId == reassigned.Id).State);
        Assert.DoesNotContain(result.Items, i => i.ResourceId == claimed.Id);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("Private", json); Assert.DoesNotContain("Sensitive", json);
    }

    [Fact]
    public async Task Preview_is_the_students_actions_in_shared_managed_holds_and_never_the_teachers_pending_reviews()
    {
        var own = Quest("Fælles hold", deadline: _now.AddDays(-1));
        Quest("Elevens andet hold", _otherHold, _now.AddDays(-1));
        var submitted = Quest("Afventer lærer"); Submission(submitted, "submitted", [_peerMember]);
        var poll = Poll("Elevens ubesvarede spørgsmål");
        await _db.SaveChangesAsync();
        var preview = Read(await Api(_teacher, true).Get(_student, default));
        Assert.All(preview.Items, i => Assert.Equal(_hold.Id, i.HoldId));
        Assert.DoesNotContain(preview.Items, i => i.Kind == "review");
        Assert.Contains(preview.Items, i => i.ResourceId == own.Id);
        Assert.Contains(preview.Items, i => i.ResourceId == poll.Id);
        var teacher = Read(await Api(_teacher, true).Get(null, default));
        var review = Assert.Single(teacher.Items);
        Assert.Equal("review", review.Kind); Assert.Equal(submitted.Id, review.ResourceId);
        Assert.Equal("optional", review.Severity);
    }

    [Fact]
    public async Task Old_unanswered_polls_are_not_hidden_by_a_hundred_newer_closed_rounds_and_answers_are_private()
    {
        var outstanding = Poll("Stadig ubesvaret"); outstanding.CreatedAtUtc = _now.AddMonths(-1).UtcDateTime;
        var answered = Poll("Besvaret");
        answered.Responses.Add(new() { UserSub = _student, Text = "Private poll answer" });
        Poll("Ikke startet", "draft");
        for (var i = 0; i < 101; i++) Poll($"Lukket {i}", "closed");
        Poll("Intet medlemskab", hold: Hold("Fremmed", _otherTeacher));
        var result = await Student();
        var item = Assert.Single(result.Items);
        Assert.Equal(outstanding.Id, item.ResourceId); Assert.Equal("poll", item.Kind);
        Assert.Equal("unanswered", item.State); Assert.Equal("optional", item.Severity);
        Assert.DoesNotContain("Private poll answer", JsonSerializer.Serialize(result));
        outstanding.Responses.Add(new() { UserSub = _student, Text = "Nu besvaret" });
        Assert.Empty((await Student()).Items);
    }

    [Fact]
    public async Task Review_count_is_actual_submissions_not_participant_count_or_drafts()
    {
        var quest = Quest("To afleveringer");
        Submission(quest, "submitted", [_member, _peerMember], Group(_member, _peerMember));
        Submission(quest, "submitted", [_member]);
        Submission(quest, "submitted", [], Group(_peerMember));
        var approved = Quest("Godkendt"); Submission(approved, "approved", [_member]);
        var returned = Quest("Returneret"); Submission(returned, "changes-requested", [_member]);
        var foreign = Quest("Anden lærers aflevering", _otherHold); Submission(foreign, "submitted", [_member]);
        await _db.SaveChangesAsync();
        var result = Read(await Api(_teacher, true).Get(null, default));
        var item = Assert.Single(result.Items);
        Assert.Equal(quest.Id, item.ResourceId); Assert.Equal(2, item.Count);
        Assert.Equal("pending-review", item.State);
    }

    [Fact]
    public async Task Repeated_daily_actions_start_on_the_occurrence_date_in_Copenhagen()
    {
        var at = DateTimeOffset.Parse("2026-11-06T23:30:00Z");
        var today = Quest("I dag"); today.OccurrenceDate = new(2026, 11, 7);
        var tomorrow = Quest("I morgen"); tomorrow.OccurrenceDate = new(2026, 11, 8);
        var series = Guid.NewGuid(); today.SeriesId = tomorrow.SeriesId = series;
        await _db.SaveChangesAsync();
        var snapshot = await new ActionCenterBuilder(_db).BuildAsync(_student, [new(_hold.Id, _hold.ForloebId, _hold.Name)], false, default, at);
        Assert.Equal(at, snapshot.GeneratedAtUtc);
        Assert.Equal(today.Id, Assert.Single(snapshot.Items).ResourceId);
    }

    private ChristmasGift Gift(Guid recipient, bool opened = false, Guid? sender = null)
    {
        var gift = new ChristmasGift { Id = Guid.NewGuid(), RecipientSub = recipient, SenderSub = sender ?? _peer,
            SenderName = "Mathias", RecipientName = "Private recipient", Message = "Private gift message",
            Kind = "warmth", Wrapping = "gold", CreatedAtUtc = _now.UtcDateTime,
            OpenedAtUtc = opened ? _now.UtcDateTime : null };
        _db.ChristmasGifts.Add(gift); return gift;
    }
    private LearningQuest Optional(string title, string kind = "side", DateTimeOffset? deadline = null, string audience = "hold")
    {
        var quest = Quest(title, deadline: deadline, audience: audience); quest.Required = false; quest.Kind = kind; return quest;
    }

    [Fact]
    public async Task Version_one_default_and_explicit_remain_compatible_with_old_clients()
    {
        var required = Quest("Obligatorisk");
        Optional("Valgfri"); Gift(_student);
        await _db.SaveChangesAsync();
        foreach (var snapshot in new[] { Read(await Api(_student).Get(null, default)), Read(await Api(_student).Get(null, default, 1)) })
        {
            var item = Assert.Single(snapshot.Items);
            Assert.Equal(required.Id, item.ResourceId); Assert.Equal("submission", item.Kind);
            Assert.NotNull(item.HoldId); Assert.NotNull(item.WorkspaceId);
        }
        Assert.IsType<BadRequestObjectResult>(await Api(_student).Get(null, default, 0));
        Assert.IsType<BadRequestObjectResult>(await Api(_student).Get(null, default, 3));
    }

    [Fact]
    public async Task Version_two_includes_only_own_unopened_gifts_without_message_or_hold_fabrication()
    {
        var expected = Gift(_student); expected.CreatedAtUtc = _now.AddMonths(-1).UtcDateTime;
        for (var i = 0; i < 55; i++) Gift(_student, opened: true);
        Gift(_teacher); Gift(_peer, sender: _student);
        await _db.SaveChangesAsync();
        var result = Read(await Api(_student).Get(null, default, 2));
        var item = Assert.Single(result.Items);
        Assert.Equal(expected.Id, item.ResourceId); Assert.Equal("gift", item.Kind);
        Assert.Equal("optional", item.Severity); Assert.Equal("unopened", item.State); Assert.Equal(1, item.Count);
        Assert.Null(item.HoldId); Assert.Null(item.WorkspaceId); Assert.Null(item.CourseId); Assert.Null(item.SubmissionId);
        Assert.Equal("Gave fra Mathias", item.Title);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("Private gift message", json); Assert.DoesNotContain("Private recipient", json);
        Assert.DoesNotContain("warmth", json); Assert.DoesNotContain("gold", json);
        expected.OpenedAtUtc = _now.UtcDateTime; await _db.SaveChangesAsync();
        Assert.Empty(Read(await Api(_student).Get(null, default, 2)).Items);
    }

    [Fact]
    public async Task Personal_gifts_work_without_hold_membership_for_students_and_teachers()
    {
        var unlinked = Guid.NewGuid(); var gift = Gift(unlinked);
        await _db.SaveChangesAsync();
        Assert.Equal(gift.Id, Assert.Single(Read(await Api(unlinked).Get(null, default, 2)).Items).ResourceId);
        Assert.Equal(gift.Id, Assert.Single(Read(await Api(unlinked, true).Get(null, default, 2)).Items).ResourceId);
        Assert.Empty(Read(await Api(unlinked, true).Get(null, default, 1)).Items);
    }

    [Fact]
    public async Task Teacher_has_own_gifts_and_reviews_but_preview_never_exposes_any_mailbox()
    {
        var personal = Gift(_teacher); Gift(_student);
        var pending = Quest("Til vurdering"); Submission(pending, "submitted", [_peerMember]);
        var choice = Optional("Valgfri mulighed");
        await _db.SaveChangesAsync();
        var teacher = Read(await Api(_teacher, true).Get(null, default, 2));
        Assert.Equal(2, teacher.Items.Length);
        Assert.Contains(teacher.Items, i => i.Kind == "gift" && i.ResourceId == personal.Id);
        Assert.Contains(teacher.Items, i => i.Kind == "review" && i.ResourceId == pending.Id);
        Assert.DoesNotContain(teacher.Items, i => i.Kind == "quest");
        var preview = Read(await Api(_teacher, true).Get(_student, default, 2));
        Assert.DoesNotContain(preview.Items, i => i.Kind is "gift" or "review");
        Assert.Contains(preview.Items, i => i.Kind == "quest" && i.ResourceId == choice.Id);
        Assert.All(preview.Items, i => Assert.Equal(_hold.Id, i.HoldId));
    }

    [Fact]
    public async Task Available_optional_daily_side_and_other_quests_are_orange_but_expired_and_future_repetitions_are_not_actions()
    {
        var daily = Optional("Dagens mulighed", "daily", _now.AddHours(3));
        var side = Optional("Ekstra udfordring", "side");
        var other = Optional("Frivillig main", "main", _now.AddDays(2));
        Optional("Udløbet mulighed", deadline: _now.AddDays(-1));
        var tomorrow = Optional("I morgen", "daily", _now.AddDays(2)); tomorrow.OccurrenceDate = DateOnly.FromDateTime(_now.AddDays(2).DateTime);
        var unpublished = Optional("Ikke udgivet"); unpublished.Published = false;
        await _db.SaveChangesAsync();
        var result = Read(await Api(_student).Get(null, default, 2));
        Assert.Equal(3, result.Items.Length);
        Assert.All(result.Items, i => { Assert.Equal("quest", i.Kind); Assert.Equal("optional", i.Severity); Assert.Equal(1, i.Count); });
        Assert.Equal("daily", result.Items.Single(i => i.ResourceId == daily.Id).State);
        Assert.Equal("side", result.Items.Single(i => i.ResourceId == side.Id).State);
        Assert.Equal("optional", result.Items.Single(i => i.ResourceId == other.Id).State);
    }

    [Fact]
    public async Task Submitted_or_approved_optional_quests_are_complete_and_returned_optional_work_stays_orange()
    {
        var submitted = Optional("Indsendt"); Submission(submitted, "submitted", [_member]);
        var approved = Optional("Godkendt"); Submission(approved, "approved", [_member]);
        Submission(approved, "draft", [], Group(_member));
        var returned = Optional("Valgfri retur", deadline: _now.AddDays(-1));
        var receipt = Submission(returned, "changes-requested", [_member]);
        var required = Quest("Obligatorisk retur", deadline: _now.AddDays(1)); Submission(required, "changes-requested", [_member]);
        await _db.SaveChangesAsync();
        var result = Read(await Api(_student).Get(null, default, 2));
        Assert.Equal(2, result.Items.Length);
        var action = result.Items.Single(i => i.ResourceId == returned.Id);
        Assert.Equal("quest", action.Kind); Assert.Equal("optional", action.Severity);
        Assert.Equal("changes-requested", action.State); Assert.Equal(receipt.Id, action.SubmissionId);
        var requiredAction = result.Items.Single(i => i.ResourceId == required.Id);
        Assert.Equal("submission", requiredAction.Kind); Assert.Equal("important", requiredAction.Severity);
    }

    [Fact]
    public async Task Optional_quests_respect_current_personal_and_group_audiences_including_preview_intersection()
    {
        var own = Optional("Kun mig", audience: "students");
        var excluded = Optional("Anden elev", audience: "students");
        _db.QuestAudienceMemberships.AddRange(new() { QuestId = own.Id, MembershipId = _member.Id }, new() { QuestId = excluded.Id, MembershipId = _peerMember.Id });
        var grouped = Optional("Min gruppe", audience: "groups"); var group = Group(_member);
        var otherGrouped = Optional("Anden gruppe", audience: "groups"); var otherGroup = Group(_peerMember);
        _db.QuestAudienceGroups.AddRange(new() { QuestId = grouped.Id, GroupId = group.Id }, new() { QuestId = otherGrouped.Id, GroupId = otherGroup.Id });
        var foreign = Quest("Anden lærers frivillige opgave", _otherHold); foreign.Required = false;
        await _db.SaveChangesAsync();
        var student = Read(await Api(_student).Get(null, default, 2));
        Assert.Equal(3, student.Items.Length);
        Assert.Contains(student.Items, i => i.ResourceId == foreign.Id);
        var preview = Read(await Api(_teacher, true).Get(_student, default, 2));
        Assert.Equal(new[] { own.Id, grouped.Id }.Order(), preview.Items.Select(i => i.ResourceId).Order());
    }
}
