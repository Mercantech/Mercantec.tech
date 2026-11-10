using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class ElectivesControllerTests : IAsyncLifetime
{
    private static readonly Guid Student = Guid.NewGuid();
    private static readonly Guid Teacher = Guid.NewGuid();
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FixedClock _clock = new(new(2026, 11, 9, 23, 30, 0, TimeSpan.Zero));
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task OnlyTeachersCanManageAndDraftsStayPrivate()
    {
        var student = As(Student);
        var teacher = As(Teacher, true);
        Assert.IsType<UnauthorizedResult>(await As(null).List(default));
        Assert.IsType<ForbidResult>(await student.Create(Request(), default));
        var template = Read<ElectiveView>(await teacher.Create(Request() with { Status = "template" }, default));
        var archived = Read<ElectiveView>(await teacher.Create(Request() with { Status = "archived", TemplateId = template.Id }, default));
        var draft = Read<ElectiveView>(await teacher.Create(Request() with { Status = "draft", TemplateId = template.Id }, default));
        Assert.IsType<ForbidResult>(await student.Update(draft.Id, Request(), default));
        Assert.IsType<NotFoundResult>(await student.Enroll(draft.Id, new(true), default));
        Assert.IsType<ForbidResult>(await student.Participants(archived.Id, default));
        Assert.IsType<ForbidResult>(await teacher.Enroll(archived.Id, new(true), default));
        Assert.Equal(new[] { archived.Id, template.Id }.Order(),
            Read<ElectiveWorkspace>(await student.List(default)).Electives.Select(e => e.Id).Order());
        Assert.True(Read<ElectiveWorkspace>(await teacher.List(default)).CanManage);
        Assert.False(Read<ElectiveWorkspace>(await teacher.List(default, publishedOnly: true)).CanManage);
        Assert.DoesNotContain(Read<ElectiveWorkspace>(await teacher.List(default, publishedOnly: true)).Electives, e => e.Id == draft.Id);
        Assert.Equal("elective_template_in_use", Error(Assert.IsType<ConflictObjectResult>(
            await teacher.Update(template.Id, Request() with { Status = "archived", Version = template.Version }, default))));
    }

    [Fact]
    public async Task EnrollmentRequiresAgreementAndUsesCopenhagenDates()
    {
        var teacher = As(Teacher, true);
        var student = As(Student);
        var date = new DateOnly(2026, 11, 10);
        var item = Read<ElectiveView>(await teacher.Create(Request() with
        {
            RegistrationOpensOn = date,
            RegistrationClosesOn = date
        }, default));

        _clock.Now = _clock.Now.AddHours(-1);
        Assert.False(Assert.Single(Read<ElectiveWorkspace>(await student.List(default)).Electives).CanEnroll);
        Assert.Equal("elective_enrollment_closed", Error(Assert.IsType<ConflictObjectResult>(await student.Enroll(item.Id, new(true), default))));
        _clock.Now = _clock.Now.AddHours(1);
        Assert.True(Assert.Single(Read<ElectiveWorkspace>(await student.List(default)).Electives).CanEnroll);
        Assert.Equal("elective_employer_agreement_required", Error(Assert.IsType<BadRequestObjectResult>(await student.Enroll(item.Id, new(false), default))));
        Assert.Empty(await _db.ElectiveEnrollments.ToArrayAsync());

        Assert.True(Read<ElectiveView>(await student.Enroll(item.Id, new(true), default)).IsEnrolled);
        _clock.Now = _clock.Now.AddDays(1);
        Assert.True(Read<ElectiveView>(await student.Enroll(item.Id, new(true), default)).IsEnrolled);
        Assert.Equal("elective_withdrawal_closed", Error(Assert.IsType<ConflictObjectResult>(await student.Withdraw(item.Id, default))));
    }

    [Fact]
    public async Task ReservedSeatsAndLiveEnrollmentShareCapacity()
    {
        var teacher = As(Teacher, true);
        var first = As(Student);
        var second = As(Guid.NewGuid());
        var request = Request() with { Capacity = 2, ReservedSeats = 1 };
        var item = Read<ElectiveView>(await teacher.Create(request, default));

        var joined = Read<ElectiveView>(await first.Enroll(item.Id, new(true), default));
        Assert.Equal(2, joined.EnrollmentCount);
        Assert.True(joined.CanWithdraw);
        Assert.Equal(joined, Read<ElectiveView>(await first.Enroll(item.Id, new(true), default)));
        var participant = Assert.Single(Read<ElectiveParticipant[]>(await teacher.Participants(item.Id, default)));
        Assert.Equal(Student, participant.UserSub);
        Assert.Equal("Testperson", participant.DisplayName);
        Assert.Equal("elective_full", Error(Assert.IsType<ConflictObjectResult>(await second.Enroll(item.Id, new(true), default))));
        Assert.Equal("elective_capacity_below_enrollment", Error(Assert.IsType<ConflictObjectResult>(
            await teacher.Update(item.Id, request with { Capacity = 1, Version = item.Version }, default))));

        Assert.Equal(1, Read<ElectiveView>(await first.Withdraw(item.Id, default)).EnrollmentCount);
        Assert.Equal(1, Read<ElectiveView>(await first.Withdraw(item.Id, default)).EnrollmentCount);
        Assert.True(Read<ElectiveView>(await second.Enroll(item.Id, new(true), default)).IsEnrolled);
        Assert.False(Assert.Single(Read<ElectiveWorkspace>(await first.List(default)).Electives).IsEnrolled);
    }

    [Fact]
    public async Task ConcurrentRequestsCannotBothTakeTheLastSeat()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lup-elective-race-{Guid.NewGuid():N}.db");
        var connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 15 }.ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        try
        {
            await using var setup = new AppDbContext(options);
            await setup.Database.EnsureCreatedAsync();
            var item = Read<ElectiveView>(await As(Teacher, true, setup).Create(Request() with { Capacity = 1 }, default));

            await using var firstDb = new AppDbContext(options);
            await using var secondDb = new AppDbContext(options);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            Task<IActionResult> EnrollWhenReady(ElectivesController student, TaskCompletionSource ready) => Task.Run(async () =>
            {
                ready.SetResult();
                await start.Task;
                return await student.Enroll(item.Id, new(true), default);
            });

            var first = EnrollWhenReady(As(Student, database: firstDb), firstReady);
            var second = EnrollWhenReady(As(Guid.NewGuid(), database: secondDb), secondReady);
            await Task.WhenAll(firstReady.Task, secondReady.Task);
            start.SetResult();
            var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));

            Assert.Single(results.OfType<OkObjectResult>());
            Assert.Equal("elective_full", Error(Assert.Single(results.OfType<ConflictObjectResult>())));
            Assert.Single(await setup.ElectiveEnrollments.ToArrayAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SaveElective Request() =>
        new("Databaser", "Fagbeskrivelse", "Uge 14", null, null, null, null, null, 0, "open", null, null, 0);

    private ElectivesController As(Guid? sub, bool teacher = false, AppDbContext? database = null)
    {
        var claims = new List<Claim>();
        if (sub is Guid id)
        {
            claims.Add(new("sub", id.ToString()));
            claims.Add(new("name", "Testperson"));
        }
        if (teacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(database ?? _db, _clock)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
                { User = new(new ClaimsIdentity(claims, sub is null ? null : "test")) } }
        };
    }

    private static T Read<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private static string? Error(ObjectResult result) => JsonSerializer.SerializeToElement(result.Value).GetProperty("error").GetString();

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
