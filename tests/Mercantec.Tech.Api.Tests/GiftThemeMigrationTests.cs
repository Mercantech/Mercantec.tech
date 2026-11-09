using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mercantec.Tech.Api.Tests;

public sealed class GiftThemeMigrationTests
{
    [Fact]
    public async Task MigrationPreservesHistoricalGiftsAsUnknownInsteadOfAssumingChristmas()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE christmas_gifts (Id TEXT PRIMARY KEY, SenderSub TEXT NOT NULL, RecipientSub TEXT NOT NULL)");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO christmas_gifts (Id, SenderSub, RecipientSub) VALUES ('old-gift', 'sender', 'recipient')");
        var migration = new GiftThemesForAvatarRequirements();
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var command in generator.Generate(migration.UpOperations, db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        await using var query = connection.CreateCommand(); query.CommandText = "SELECT Theme FROM christmas_gifts WHERE Id='old-gift'";
        Assert.Equal("unknown", await query.ExecuteScalarAsync());
        await db.Database.ExecuteSqlRawAsync("INSERT INTO christmas_gifts (Id, SenderSub, RecipientSub) VALUES ('old-client', 'sender', 'recipient2')");
        query.CommandText = "SELECT Theme FROM christmas_gifts WHERE Id='old-client'";
        Assert.Equal("unknown", await query.ExecuteScalarAsync());
    }

    [Fact]
    public void PostgreSqlScriptAddsThemeAndCoveringIndexAndSnapshotMatchesTheModel()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=migration_test;Username=test;Password=test").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript("20261108065614_AvatarProfiles", "20261109130000_GiftThemesForAvatarRequirements",
            MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("ADD \"Theme\" character varying(16) NOT NULL DEFAULT 'unknown'", script);
        Assert.Contains("IX_christmas_gifts_SenderSub_Theme_RecipientSub", script);
        Assert.DoesNotContain("UPDATE christmas_gifts", script);
    }
}
