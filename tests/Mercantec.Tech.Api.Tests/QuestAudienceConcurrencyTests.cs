using System.Data.Common;
using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mercantec.Tech.Api.Tests;

public sealed class QuestAudienceConcurrencyTests
{
    [Fact]
    public async Task ChangingAudienceAndOpeningFirstDraftCannotBothUseTheOldAudience()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lup-quest-race-{Guid.NewGuid():N}.db");
        var connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 15 }.ToString();
        var teacher = Guid.NewGuid(); var anna = Guid.NewGuid(); var bo = Guid.NewGuid();
        var holdId = Guid.NewGuid(); var questId = Guid.NewGuid(); var annaId = Guid.NewGuid(); var boId = Guid.NewGuid();
        try
        {
            await using (var setup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options))
            {
                await setup.Database.EnsureCreatedAsync();
                await setup.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                var hold = new Hold { Id = holdId, Name = "H3", Forloeb = new() { Id = Guid.NewGuid(), Name = "H3", CreatedBySub = teacher } };
                var a = new Membership { Id = annaId, Hold = hold, UserSub = anna, DisplayName = "Anna" };
                setup.Memberships.AddRange(a, new() { Id = boId, Hold = hold, UserSub = bo, DisplayName = "Bo" });
                var quest = new LearningQuest { Id = questId, HoldId = holdId, Title = "Aflevering", Published = true, AudienceMode = "students" };
                setup.QuestAudienceMemberships.Add(new() { Quest = quest, Membership = a });
                await setup.SaveChangesAsync();
            }
            var teacherGate = new EmptySubmissionCheckGate(); var studentAttempt = new TransactionAttempt();
            await using var teacherDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(teacherGate).Options);
            await using var studentDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(studentAttempt).Options);
            var teacherApi = As(new QuestsController(teacherDb, new(teacherDb)), teacher, true);
            var studentApi = As(new SubmissionsController(studentDb, new(studentDb)), anna);
            var update = Task.Run(() => teacherApi.Update(questId,
                new(holdId, "Aflevering", "", "individual", true, null, Audience: new("students", [boId])), default));
            Task<IActionResult>? start = null;
            try
            {
                // Stop after the teacher has observed an empty submission table, while its quest lock is held.
                await teacherGate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
                start = Task.Run(() => studentApi.Start(new(questId, null), default));
                await studentAttempt.Attempted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally { teacherGate.Release.TrySetResult(true); }
            var results = await Task.WhenAll(update, start!).WaitAsync(TimeSpan.FromSeconds(20));
            Assert.IsType<OkObjectResult>(results[0]);
            Assert.IsType<NotFoundResult>(results[1]);
            await using var check = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            Assert.Empty(await check.QuestSubmissions.ToArrayAsync());
            Assert.Equal(boId, Assert.Single(await check.QuestAudienceMemberships.ToArrayAsync()).MembershipId);
        }
        finally
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                if (File.Exists(file)) File.Delete(file);
        }
    }

    private static T As<T>(T api, Guid sub, bool teacher = false) where T : ControllerBase
    {
        var claims = new List<Claim> { new("sub", sub.ToString()) }; if (teacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        api.ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) } };
        return api;
    }

    private sealed class EmptySubmissionCheckGate : DbCommandInterceptor
    {
        public TaskCompletionSource<bool> Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("quest_submissions", StringComparison.Ordinal) && command.CommandText.Contains("EXISTS", StringComparison.Ordinal))
            { Reached.TrySetResult(true); await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken); }
            return result;
        }
    }
    private sealed class TransactionAttempt : DbTransactionInterceptor
    {
        public TaskCompletionSource<bool> Attempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection,
            TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        { Attempted.TrySetResult(true); return new(result); }
    }
}
