using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class PollsControllerTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private AppDbContext _db = null!;
    private readonly Guid _teacher = Guid.NewGuid(), _student = Guid.NewGuid(), _peer = Guid.NewGuid(), _stranger = Guid.NewGuid();
    private Guid _hold;
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        var course = new Forloeb { Id = Guid.NewGuid(), Name = "H3", CreatedBySub = _teacher, CreatedAtUtc = DateTimeOffset.UtcNow };
        var hold = new Hold { Id = Guid.NewGuid(), Name = "H3 · 2026", Forloeb = course, CreatedAtUtc = DateTimeOffset.UtcNow };
        _hold = hold.Id;
        _db.Memberships.AddRange(new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = _student, Source = "manual" }, new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = _peer, Source = "manual" });
        await _db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }
    private PollsController As(Guid? sub, bool teacher = false)
    {
        var claims = new List<Claim>();
        if (sub is Guid id) claims.Add(new("sub", id.ToString()));
        if (teacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(_db) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, sub is null ? null : "test")) } } };
    }
    private CreatePoll Draft(string state = "open", string mode = "after-vote", string kind = "choice") => new(_hold, "Hvad skal vi arbejde med?", kind, ["API", "Database", "Realtime"], mode, state);
    private static PollView Read(IActionResult result) => Assert.IsType<PollView>(Assert.IsType<OkObjectResult>(result).Value);
    private async Task<PollView> Create(CreatePoll? draft = null) => Read(await As(_teacher, true).Create(draft ?? Draft(), default));

    [Fact] public async Task AnonymousIsRejectedAndStudentsCannotCreate()
    {
        Assert.IsType<UnauthorizedResult>(await As(null).List(default));
        Assert.IsType<UnauthorizedResult>(await As(null).Create(Draft(), default));
        Assert.IsType<ForbidResult>(await As(_student).Create(Draft(), default));
    }
    [Fact] public async Task HoldsAndRoundsAreLimitedToMembershipOrTeachingAccess()
    {
        var round = await Create();
        Assert.IsType<NotFoundResult>(await As(_stranger).Get(round.Id, default));
        Assert.IsType<NotFoundResult>(await As(_stranger, true).Create(Draft(), default));
        Assert.Empty(Assert.IsType<List<PollView>>(Assert.IsType<OkObjectResult>(await As(_stranger).List(default)).Value));
        Assert.Single(Assert.IsType<List<PollHold>>(Assert.IsType<OkObjectResult>(await As(_student).Holds(default)).Value));
    }
    [Fact] public async Task DraftIsPrivateUntilTeacherStartsIt()
    {
        var round = await Create(Draft("draft"));
        Assert.IsType<NotFoundResult>(await As(_student).Get(round.Id, default));
        Assert.IsType<NotFoundResult>(await As(_student).Answer(round.Id, new(0, null), default));
        await As(_teacher, true).ChangeState(round.Id, new("open"), default);
        Assert.Equal("open", Read(await As(_student).Get(round.Id, default)).State);
    }
    [Fact] public async Task ManagingTeacherDoesNotCountAsAStudentParticipant()
    {
        var round = await Create();
        Assert.IsType<ForbidResult>(await As(_teacher, true).Answer(round.Id, new(0, null), default));
        Assert.Empty(await _db.PollResponses.ToListAsync());
    }
    [Fact] public async Task ChangingAnswerDoesNotCreateAnotherParticipant()
    {
        var round = await Create();
        Assert.Null(Read(await As(_student).Get(round.Id, default)).Counts);
        var result = Read(await As(_student).Answer(round.Id, new(0, null), default));
        Assert.Equal(1, result.TotalResponses);
        result = Read(await As(_student).Answer(round.Id, new(2, null), default));
        Assert.Equal(1, result.TotalResponses);
        Assert.Equal(new[] { 0, 0, 1 }, result.Counts);
        Assert.Single(await _db.PollResponses.ToListAsync());
        Assert.Null(Read(await As(_peer).Get(round.Id, default)).Counts);
    }
    [Fact] public async Task TeacherCanCloseAndReopenButOthersCannot()
    {
        var round = await Create();
        Assert.IsType<NotFoundResult>(await As(_student).ChangeState(round.Id, new("closed"), default));
        Assert.IsType<NotFoundResult>(await As(_stranger, true).ChangeState(round.Id, new("closed"), default));
        await As(_teacher, true).ChangeState(round.Id, new("closed"), default);
        Assert.IsType<ConflictObjectResult>(await As(_student).Answer(round.Id, new(0, null), default));
        Assert.Empty(await _db.PollResponses.ToListAsync());
        await As(_teacher, true).ChangeState(round.Id, new("open"), default);
        Assert.True(Read(await As(_student).Answer(round.Id, new(0, null), default)).HasResponded);
    }
    [Fact] public async Task AfterVotePolicyDoesNotExposeResultsToNonRespondentsWhenClosed()
    {
        var round = await Create();
        await As(_student).Answer(round.Id, new(0, null), default);
        await As(_teacher, true).ChangeState(round.Id, new("closed"), default);
        Assert.NotNull(Read(await As(_student).Get(round.Id, default)).Counts);
        var peer = Read(await As(_peer).Get(round.Id, default));
        Assert.Null(peer.Counts);
        Assert.Null(peer.TotalResponses);
    }
    [Theory]
    [InlineData("closed")]
    [InlineData("teacher")]
    public async Task ResultsPolicyIsEnforcedByTheApi(string mode)
    {
        var round = await Create(Draft(mode: mode));
        Assert.Null(Read(await As(_student).Answer(round.Id, new(1, null), default)).Counts);
        Assert.NotNull(Read(await As(_teacher, true).Get(round.Id, default)).Counts);
        await As(_teacher, true).ChangeState(round.Id, new("closed"), default);
        var student = Read(await As(_student).Get(round.Id, default));
        Assert.Equal(mode == "closed", student.Counts is not null);
    }
    [Fact] public async Task InvalidQuestionsOptionsAndAnswersAreRejected()
    {
        Assert.IsType<BadRequestObjectResult>(await As(_teacher, true).Create(Draft() with { Options = ["Samme", "samme"] }, default));
        Assert.IsType<BadRequestObjectResult>(await As(_teacher, true).Create(Draft() with { Question = new string('x', 201) }, default));
        var round = await Create();
        Assert.IsType<BadRequestObjectResult>(await As(_student).Answer(round.Id, new(99, null), default));
        Assert.IsType<NotFoundResult>(await As(_stranger).Answer(round.Id, new(0, null), default));
    }
    [Fact] public async Task TextIsLengthLimitedAndNeverReleasedToOtherStudents()
    {
        var round = await Create(Draft(kind: "text"));
        Assert.Equal("teacher", round.ResultsMode);
        Assert.IsType<BadRequestObjectResult>(await As(_student).Answer(round.Id, new(null, new string('x', 281)), default));
        var result = Read(await As(_student).Answer(round.Id, new(null, "<b>Et spørgsmål</b>"), default));
        Assert.Equal("<b>Et spørgsmål</b>", result.YourText);
        Assert.Null(result.Texts);
        Assert.Equal("<b>Et spørgsmål</b>", Assert.Single(Read(await As(_teacher, true).Get(round.Id, default)).Texts!));
        await As(_teacher, true).ChangeState(round.Id, new("closed"), default);
        Assert.Null(Read(await As(_peer).Get(round.Id, default)).Texts);
    }
    [Fact] public async Task NewRoundKeepsHistoryAndHasNoOldAnswers()
    {
        var old = await Create(Draft(kind: "mood"));
        Assert.Equal(5, old.Options.Length);
        await As(_student).Answer(old.Id, new(3, null), default);
        await As(_teacher, true).ChangeState(old.Id, new("closed"), default);
        var next = await Create(Draft(kind: "mood"));
        Assert.NotEqual(old.Id, next.Id);
        Assert.Equal(0, next.TotalResponses);
        Assert.Equal(1, Read(await As(_teacher, true).Get(old.Id, default)).TotalResponses);
    }

    private async Task<Guid> AnotherHold(bool enrollStudent = true, Guid? owner = null)
    {
        var course = new Forloeb { Id = Guid.NewGuid(), Name = "H4", CreatedBySub = owner ?? _teacher, CreatedAtUtc = DateTimeOffset.UtcNow };
        var hold = new Hold { Id = Guid.NewGuid(), Name = "H4 · 2026", Forloeb = course, CreatedAtUtc = DateTimeOffset.UtcNow };
        _db.Hold.Add(hold);
        if (enrollStudent) _db.Memberships.Add(new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = _student, Source = "manual" });
        await _db.SaveChangesAsync();
        return hold.Id;
    }
    private static List<PollView> Rounds(IActionResult result) => Assert.IsType<List<PollView>>(Assert.IsType<OkObjectResult>(result).Value);

    [Fact] public async Task HoldFilterExcludesOtherAccessibleHoldsAndKeepsDraftsPrivate()
    {
        var other = await AnotherHold();
        var open = await Create();
        var draft = await Create(Draft("draft"));
        var closed = await Create();
        await As(_teacher, true).ChangeState(closed.Id, new("closed"), default);
        await Create(Draft() with { HoldId = other });
        var teacher = Rounds(await As(_teacher, true).List(default, _hold));
        Assert.Equal(3, teacher.Count);
        Assert.All(teacher, p => { Assert.Equal(_hold, p.HoldId); Assert.True(p.CanManage); });
        Assert.Contains(teacher, p => p.Id == draft.Id);
        var student = Rounds(await As(_student).List(default, _hold));
        Assert.Equal(2, student.Count);
        Assert.All(student, p => { Assert.Equal(_hold, p.HoldId); Assert.False(p.CanManage); Assert.NotEqual("draft", p.State); });
        Assert.Contains(student, p => p.Id == open.Id);
        Assert.Contains(student, p => p.Id == closed.Id);
        Assert.Single(Rounds(await As(_student).List(default, other)));
        Assert.Equal(3, Rounds(await As(_student).List(default)).Count);
    }

    [Fact] public async Task InaccessibleOrUnknownHoldFilterNeverFallsBackToOtherHolds()
    {
        await Create();
        var inaccessible = await AnotherHold(enrollStudent: false, owner: _stranger);
        Assert.IsType<NotFoundResult>(await As(_student).List(default, inaccessible));
        Assert.IsType<NotFoundResult>(await As(_teacher, true).List(default, inaccessible));
        Assert.IsType<NotFoundResult>(await As(_student).List(default, Guid.NewGuid()));
        Assert.IsType<NotFoundResult>(await As(_student).List(default, Guid.Empty));
        Assert.IsType<UnauthorizedResult>(await As(null).List(default, _hold));
        Assert.Single(Rounds(await As(_teacher, true).List(default, _hold)));
    }

    [Fact] public async Task HoldFilterIsAppliedBeforeTheRoundLimit()
    {
        var selected = await Create();
        var other = await AnotherHold();
        _db.PollRounds.AddRange(Enumerable.Range(1, 101).Select(i => new PollRound
        {
            Id = Guid.NewGuid(), HoldId = other, CreatedBySub = _teacher, Question = $"Spørgsmål {i}",
            Kind = "choice", State = "open", ResultsMode = "after-vote", OptionsJson = "[\"Ja\",\"Nej\"]",
            CreatedAtUtc = selected.CreatedAtUtc.AddMinutes(i)
        }));
        await _db.SaveChangesAsync();
        Assert.Equal(100, Rounds(await As(_student).List(default)).Count);
        Assert.Equal(selected.Id, Assert.Single(Rounds(await As(_student).List(default, _hold))).Id);
    }

    [Fact] public async Task HoldScopedListEnforcesResultsAndTextPrivacyForAllFormats()
    {
        var choice = await Create();
        var mood = await Create(Draft(mode: "closed", kind: "mood"));
        var scale = await Create(Draft(mode: "teacher", kind: "scale"));
        var text = await Create(Draft(kind: "text"));
        foreach (var poll in new[] { choice, mood, scale }) await As(_peer).Answer(poll.Id, new(1, null), default);
        await As(_peer).Answer(text.Id, new(null, "Privat elevsvar"), default);
        var before = Rounds(await As(_student).List(default, _hold));
        Assert.Equal(4, before.Count);
        Assert.All(before, p => { Assert.Null(p.TotalResponses); Assert.Null(p.Counts); Assert.Null(p.Texts); Assert.Null(p.YourText); });
        await As(_student).Answer(choice.Id, new(0, null), default);
        await As(_student).Answer(scale.Id, new(2, null), default);
        await As(_student).Answer(text.Id, new(null, "Mit eget svar"), default);
        await As(_teacher, true).ChangeState(mood.Id, new("closed"), default);
        var after = Rounds(await As(_student).List(default, _hold));
        Assert.Equal(2, after.Single(p => p.Id == choice.Id).TotalResponses);
        Assert.Equal(1, after.Single(p => p.Id == mood.Id).TotalResponses);
        Assert.Null(after.Single(p => p.Id == scale.Id).Counts);
        var ownText = after.Single(p => p.Id == text.Id);
        Assert.Equal("Mit eget svar", ownText.YourText);
        Assert.Null(ownText.Texts);
        Assert.All(after, p => Assert.Null(p.Texts));
        var teacher = Rounds(await As(_teacher, true).List(default, _hold));
        Assert.All(teacher, p => { Assert.True(p.CanManage); Assert.NotNull(p.TotalResponses); });
        Assert.Equal(2, teacher.Single(p => p.Id == text.Id).Texts!.Length);
    }
}
