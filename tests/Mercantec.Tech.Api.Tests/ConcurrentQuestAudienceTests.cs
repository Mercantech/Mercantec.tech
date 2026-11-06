using System.Data.Common;
using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mercantec.Tech.Api.Tests;

public sealed class ConcurrentQuestAudienceTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly AudienceRaceInterceptor race = new();
    private AppDbContext db = null!;
    private readonly Guid student = Guid.NewGuid(), groupId = Guid.NewGuid(), submissionId = Guid.NewGuid();
    private Guid targetMember;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(race).Options);
        await db.Database.EnsureCreatedAsync();
        var hold = new Hold { Id = Guid.NewGuid(), Name = "H3", Forloeb = new() { Id = Guid.NewGuid(), Name = "Programmering", CreatedBySub = Guid.NewGuid() } };
        var ada = new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = student, DisplayName = "Ada" };
        var bo = new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = Guid.NewGuid(), DisplayName = "Bo" };
        targetMember = ada.Id;
        var group = new LearningGroup { Id = groupId, HoldId = hold.Id, Name = "Projektgruppen" };
        group.Members.Add(new() { Membership = ada, MembershipId = ada.Id });
        var quest = new LearningQuest { Id = Guid.NewGuid(), HoldId = hold.Id, Title = "Kun til Ada", Published = true, SubmissionMode = "group", AudienceMode = "students" };
        db.Memberships.AddRange(ada, bo); db.LearningGroups.Add(group); db.LearningQuests.Add(quest);
        db.QuestAudienceMemberships.Add(new() { Quest = quest, MembershipId = ada.Id });
        db.QuestSubmissions.Add(new() { Id = submissionId, Quest = quest, GroupId = groupId,
            CreatedBySub = student, OwnerKey = $"g:{groupId:N}", DraftText = "Adas besvarelse", State = "draft" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        race.GroupId = groupId; race.AddedMemberId = bo.Id;
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private SubmissionsController Api() => new(db, new(db)) { ControllerContext = new()
        { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity([new Claim("sub", student.ToString())], "test")) } } };

    [Fact]
    public async Task SubmitRechecksAudienceAfterLockBeforeSnapshottingNewGroupMember()
    {
        race.Armed = true;
        Assert.IsType<ConflictObjectResult>(await Api().Submit(submissionId, new(0), default));
        Assert.True(race.Fired, "The member must be added after the initial permission check and group UPDATE.");
        db.ChangeTracker.Clear();
        Assert.Empty(await db.SubmissionRevisions.ToListAsync());
        Assert.Empty(await db.SubmissionParticipants.ToListAsync());
        Assert.Equal("draft", (await db.QuestSubmissions.SingleAsync()).State);
    }

    [Fact]
    public async Task SubmitStillSucceedsWhenEveryCurrentGroupMemberIsTargeted()
    {
        Assert.IsType<OkObjectResult>(await Api().Submit(submissionId, new(0), default));
        Assert.False(race.Fired);
        Assert.Equal(targetMember, (await db.SubmissionParticipants.SingleAsync()).MembershipId);
    }

    private sealed class AudienceRaceInterceptor : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public bool Fired { get; private set; }
        public Guid GroupId { get; set; }
        public Guid AddedMemberId { get; set; }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (Armed && !Fired && command.CommandText.TrimStart().StartsWith("UPDATE ", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("\"learning_groups\"", StringComparison.Ordinal))
            {
                Fired = true;
                // Use the same provider transaction so the permission check sees the changed membership.
                using var insert = command.Connection!.CreateCommand(); insert.Transaction = command.Transaction;
                insert.CommandText = "INSERT INTO learning_group_members (GroupId, MembershipId) VALUES ($group, $member)";
                insert.Parameters.Add(new SqliteParameter("$group", GroupId));
                insert.Parameters.Add(new SqliteParameter("$member", AddedMemberId));
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            return result;
        }
    }
}
