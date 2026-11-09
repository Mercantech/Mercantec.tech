using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class AvatarProfileTests : IAsyncLifetime
{
    // A real file and independent connections exercise persistence across
    // requests/server contexts rather than relying on a tracked EF entity.
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"avatar-profile-tests-{Guid.NewGuid():N}.db");
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000101");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000102");

    public async Task InitializeAsync()
    {
        await using var db = OpenDatabase();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            File.Delete(_databasePath + suffix);
        return Task.CompletedTask;
    }

    private AppDbContext OpenDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={_databasePath};Default Timeout=2;Pooling=False").Options);

    private static AvatarsController As(AppDbContext db, Guid? subject, bool authenticated = true)
    {
        Claim[] claims = subject is Guid id ? [new("sub", id.ToString())] : [];
        return new(db)
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null))
                }
            }
        };
    }

    private static AvatarProfileDto Success(ActionResult<AvatarProfileDto> result) =>
        Assert.IsType<AvatarProfileDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static void Failure(ActionResult<AvatarProfileDto> result, int status, string code)
    {
        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(status, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(code, problem.Extensions["code"]);
    }

    private static async Task<JsonDocument> ExportAs(AppDbContext db, Guid subject)
    {
        var controller = new PrivacyController(db) { ControllerContext = As(db, subject).ControllerContext };
        var download = Assert.IsType<FileContentResult>(await controller.Export(default));
        Assert.Equal("application/json", download.ContentType);
        return JsonDocument.Parse(download.FileContents);
    }

    [Fact]
    public async Task SantaPurchaseAndSelectionPersistAcrossConnectionsWithoutLeakingToAnotherAccount()
    {
        await SeedChristmasGifts(Alice);
        await using (var firstDevice = OpenDatabase())
        {
            var alice = As(firstDevice, Alice);
            Failure(await alice.Select(new(AvatarEconomy.SantaId), default), 409, "avatar_not_owned");
            var purchased = Success(await alice.Unlock(AvatarEconomy.SantaId, default));
            Assert.Equal(170, purchased.Gold);
            Assert.Equal(AvatarEconomy.SantaId, purchased.SelectedAvatarId);
            var retry = Success(await alice.Unlock(AvatarEconomy.SantaId, default));
            Assert.Equal(purchased.Revision, retry.Revision);
            Assert.Equal(purchased.Gold, retry.Gold);
        }
        await using (var secondDevice = OpenDatabase())
        {
            var restored = Success(await As(secondDevice, Alice).Get(default));
            Assert.Equal(AvatarEconomy.SantaId, restored.SelectedAvatarId);
            Assert.Contains(AvatarEconomy.SantaId, restored.OwnedAvatarIds);
            var bob = As(secondDevice, Bob);
            Assert.DoesNotContain(AvatarEconomy.SantaId, Success(await bob.Get(default)).OwnedAvatarIds);
            Failure(await bob.Select(new(AvatarEconomy.SantaId), default), 409, "avatar_not_owned");
            Success(await As(secondDevice, Alice).Select(new(AvatarEconomy.StarterId), default));
        }
        await using var thirdDevice = OpenDatabase();
        var final = Success(await As(thirdDevice, Alice).Get(default));
        Assert.Equal(AvatarEconomy.StarterId, final.SelectedAvatarId);
        Assert.Equal(170, final.Gold);
        Assert.Single(await thirdDevice.AvatarOwnerships.Where(x => x.UserSub == Alice && x.AvatarId == AvatarEconomy.SantaId).ToListAsync());
    }

    [Fact]
    public async Task SantaUsesDatabaseLevelAndWalletAndCannotBeGrantedByLegacyBrowserData()
    {
        await SeedChristmasGifts(Alice);
        await using var db = OpenDatabase();
        var controller = As(db, Alice);
        var imported = Success(await controller.ImportLegacy(new(["starter", "santa"], "santa", 320), default));
        Assert.Equal(AvatarEconomy.StarterId, imported.SelectedAvatarId);
        Assert.DoesNotContain(AvatarEconomy.SantaId, imported.OwnedAvatarIds);
        Assert.Equal(320, imported.Gold);
        var stored = await db.AvatarProfiles.SingleAsync(x => x.UserSub == Alice);
        stored.Level = 3;
        await db.SaveChangesAsync();
        Failure(await controller.Unlock(AvatarEconomy.SantaId, default), 409, "level_required");
        stored = await db.AvatarProfiles.SingleAsync(x => x.UserSub == Alice);
        stored.Level = 4;
        stored.Gold = 149;
        await db.SaveChangesAsync();
        Failure(await controller.Unlock(AvatarEconomy.SantaId, default), 409, "insufficient_gold");
        stored = await db.AvatarProfiles.SingleAsync(x => x.UserSub == Alice);
        stored.Gold = 150;
        await db.SaveChangesAsync();
        var purchased = Success(await controller.Unlock(AvatarEconomy.SantaId, default));
        Assert.Equal(0, purchased.Gold);
        Assert.Equal(AvatarEconomy.SantaId, purchased.SelectedAvatarId);
    }

    private async Task SeedChristmasGifts(Guid sender)
    {
        await using var db = OpenDatabase();
        for (var i = 0; i < 3; i++)
            db.ChristmasGifts.Add(new ChristmasGift
            {
                Id = Guid.NewGuid(), SenderSub = sender, RecipientSub = Guid.NewGuid(),
                SenderName = "Afsender", RecipientName = "Modtager", Theme = "christmas", CreatedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task PrivacyExportIncludesOnlyTheSignedInUsersStoredAvatarData()
    {
        await using var db = OpenDatabase();
        Success(await As(db, Alice).Unlock("bip01", default));
        var aliceStored = Success(await As(db, Alice).BuyItem("lamp", default));
        Success(await As(db, Bob).ImportLegacy(new(["starter"], "starter", 17), default));

        using var aliceExport = await ExportAs(db, Alice);
        Assert.Equal(Alice.ToString(), aliceExport.RootElement.GetProperty("Sub").GetString());
        var alice = aliceExport.RootElement.GetProperty("PlatformRecords").GetProperty("avatarProfile");
        Assert.Equal("bip01", alice.GetProperty("selectedAvatarId").GetString());
        Assert.Equal(90, alice.GetProperty("gold").GetInt32());
        Assert.Equal(7, alice.GetProperty("level").GetInt32());
        Assert.Equal(aliceStored.Revision, alice.GetProperty("revision").GetInt64());
        Assert.Equal(["bip01", "starter"], alice.GetProperty("ownedAvatarIds").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("lamp", alice.GetProperty("ownedItemIds").EnumerateArray().Select(x => x.GetString()));

        using var bobExport = await ExportAs(db, Bob);
        var bob = bobExport.RootElement.GetProperty("PlatformRecords").GetProperty("avatarProfile");
        Assert.Equal("starter", bob.GetProperty("selectedAvatarId").GetString());
        Assert.Equal(17, bob.GetProperty("gold").GetInt32());
        Assert.Equal(["starter"], bob.GetProperty("ownedAvatarIds").EnumerateArray().Select(x => x.GetString()));
        Assert.DoesNotContain("lamp", bob.GetProperty("ownedItemIds").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task PrivacyExportDoesNotCreateOrChangeAnAvatarProfile()
    {
        await using var db = OpenDatabase();
        using (var emptyExport = await ExportAs(db, Alice))
            Assert.Equal(JsonValueKind.Null, emptyExport.RootElement.GetProperty("PlatformRecords").GetProperty("avatarProfile").ValueKind);
        Assert.Empty(await db.AvatarProfiles.ToListAsync());

        Success(await As(db, Alice).Get(default));
        using var existingExport = await ExportAs(db, Alice);
        var stored = Assert.Single(await db.AvatarProfiles.ToListAsync());
        Assert.True(stored.LegacyImportAvailable);
        Assert.Equal(0, stored.Revision);
        Assert.Equal(320, stored.Gold);
        Assert.Single(await db.AvatarOwnerships.ToListAsync());
    }

    [Fact]
    public async Task AnonymousOrMissingIdentityCannotReadOrChangeProfiles()
    {
        await using var db = OpenDatabase();
        foreach (var controller in new[] { As(db, null), As(db, Alice, authenticated: false) })
        {
            Failure(await controller.Get(default), 401, "authentication_required");
            Failure(await controller.Unlock("bip01", default), 401, "authentication_required");
            Failure(await controller.Select(new("starter"), default), 401, "authentication_required");
            Failure(await controller.ImportLegacy(new(["starter", "bip01"], "bip01", 170), default), 401, "authentication_required");
            Failure(await controller.BuyItem("rug", default), 401, "authentication_required");
        }
        Assert.Empty(await db.AvatarProfiles.ToListAsync());
    }

    [Fact]
    public async Task StaleBrowserAccountHeaderCannotReadOrMutateTheNewlySignedInAccount()
    {
        await using var db = OpenDatabase();
        Success(await As(db, Alice).Get(default));
        var staleRequest = As(db, Bob);
        staleRequest.Request.Headers["X-LUP-Avatar-Subject"] = Alice.ToString();
        Failure(await staleRequest.Get(default), 409, "account_changed");
        Failure(await staleRequest.Unlock("bip01", default), 409, "account_changed");
        Failure(await staleRequest.Select(new("starter"), default), 409, "account_changed");
        Failure(await staleRequest.ImportLegacy(new(["starter", "bip01"], "bip01", 170), default), 409, "account_changed");
        Failure(await staleRequest.BuyItem("rug", default), 409, "account_changed");
        var profile = Assert.Single(await db.AvatarProfiles.ToListAsync());
        Assert.Equal(Alice, profile.UserSub);
        Assert.Equal("starter", profile.SelectedAvatarId);
        Assert.Equal(320, profile.Gold);
        Assert.Single(await db.AvatarOwnerships.ToListAsync());
    }

    [Fact]
    public async Task NewAccountHasOneStarterProfileAndReadingDoesNotCloseMigration()
    {
        await using var db = OpenDatabase();
        var first = Success(await As(db, Alice).Get(default));
        var again = Success(await As(db, Alice).Get(default));
        Assert.Equal("starter", first.SelectedAvatarId);
        Assert.Equal(["starter"], first.OwnedAvatarIds);
        Assert.Equal(320, first.Gold);
        Assert.Equal(7, first.Level);
        Assert.Equal(AvatarEconomy.InitialItemIds.Order(), first.OwnedItemIds.Order());
        Assert.True(first.CanImportLegacy);
        Assert.True(again.CanImportLegacy);
        Assert.Equal(0, again.Revision);
        Assert.Single(await db.AvatarProfiles.ToListAsync());
        Assert.Single(await db.AvatarOwnerships.ToListAsync());
    }

    [Fact]
    public async Task UnlockAndSelectionSurviveNewDatabaseConnectionsAndRemainPrivate()
    {
        await using (var firstDevice = OpenDatabase())
        {
            var unlocked = Success(await As(firstDevice, Alice).Unlock("bip01", default));
            Assert.Equal("bip01", unlocked.SelectedAvatarId);
            Assert.Equal(170, unlocked.Gold);
            Assert.False(unlocked.CanImportLegacy);
        }

        await using (var secondDevice = OpenDatabase())
        {
            var alice = Success(await As(secondDevice, Alice).Get(default));
            Assert.Equal("bip01", alice.SelectedAvatarId);
            Assert.Contains("bip01", alice.OwnedAvatarIds);
            Assert.Equal(170, alice.Gold);

            var bob = Success(await As(secondDevice, Bob).Get(default));
            Assert.Equal("starter", bob.SelectedAvatarId);
            Assert.DoesNotContain("bip01", bob.OwnedAvatarIds);
            Assert.Equal(320, bob.Gold);
            Success(await As(secondDevice, Alice).Select(new("starter"), default));
        }

        await using var laterRequest = OpenDatabase();
        Assert.Equal("starter", Success(await As(laterRequest, Alice).Get(default)).SelectedAvatarId);
        Assert.Contains("bip01", Success(await As(laterRequest, Alice).Get(default)).OwnedAvatarIds);
        Assert.Equal(2, await laterRequest.AvatarProfiles.CountAsync());
    }

    [Fact]
    public async Task RaymondAndRobotRemainOwnedAcrossConnectionsAndAccountSwitches()
    {
        await using (var firstDevice = OpenDatabase())
        {
            var controller = As(firstDevice, Alice);
            Failure(await controller.Select(new(AvatarEconomy.RaymondId), default), 409, "avatar_not_owned");
            Success(await controller.Unlock(AvatarEconomy.Bip01Id, default));
            var raymond = Success(await controller.Unlock(AvatarEconomy.RaymondId, default));
            Assert.Equal(AvatarEconomy.RaymondId, raymond.SelectedAvatarId);
            Assert.Equal(20, raymond.Gold);
            Assert.Equal(["bip01", "raymond", "starter"], raymond.OwnedAvatarIds);
            var retry = Success(await controller.Unlock(AvatarEconomy.RaymondId, default));
            Assert.Equal(20, retry.Gold);
            Assert.Equal(raymond.Revision, retry.Revision);
        }

        await using (var secondDevice = OpenDatabase())
        {
            var alice = As(secondDevice, Alice);
            Assert.Equal(AvatarEconomy.RaymondId, Success(await alice.Get(default)).SelectedAvatarId);
            Success(await alice.Select(new(AvatarEconomy.Bip01Id), default));
            var bob = As(secondDevice, Bob);
            Failure(await bob.Select(new(AvatarEconomy.RaymondId), default), 409, "avatar_not_owned");
            var separate = Success(await bob.Get(default));
            Assert.Equal(320, separate.Gold);
            Assert.Equal(["starter"], separate.OwnedAvatarIds);
        }

        await using var thirdDevice = OpenDatabase();
        var restored = Success(await As(thirdDevice, Alice).Get(default));
        Assert.Equal(AvatarEconomy.Bip01Id, restored.SelectedAvatarId);
        Assert.Equal(20, restored.Gold);
        Assert.Contains(AvatarEconomy.RaymondId, restored.OwnedAvatarIds);
        Assert.Single(await thirdDevice.AvatarOwnerships.Where(x => x.UserSub == Alice && x.AvatarId == AvatarEconomy.RaymondId).ToListAsync());
    }

    [Fact]
    public async Task RaymondRequiresTheDatabaseLevelBeforePayment()
    {
        await using var db = OpenDatabase();
        var controller = As(db, Alice);
        Success(await controller.Get(default));
        var stored = await db.AvatarProfiles.SingleAsync();
        stored.Level = 2;
        await db.SaveChangesAsync();

        Failure(await controller.Unlock(AvatarEconomy.RaymondId, default), 409, "level_required");
        var locked = Success(await controller.Get(default));
        Assert.Equal(320, locked.Gold);
        Assert.DoesNotContain(AvatarEconomy.RaymondId, locked.OwnedAvatarIds);

        stored = await db.AvatarProfiles.SingleAsync();
        stored.Level = 3;
        await db.SaveChangesAsync();
        var unlocked = Success(await controller.Unlock(AvatarEconomy.RaymondId, default));
        Assert.Equal(170, unlocked.Gold);
        Assert.Equal(AvatarEconomy.RaymondId, unlocked.SelectedAvatarId);
    }

    [Fact]
    public async Task UnlockRetryCannotChargeAgainOrDuplicateOwnership()
    {
        await using var firstRequest = OpenDatabase();
        var first = Success(await As(firstRequest, Alice).Unlock("bip01", default));
        await using var retryRequest = OpenDatabase();
        var retry = Success(await As(retryRequest, Alice).Unlock("bip01", default));
        Assert.Equal(170, retry.Gold);
        Assert.Equal(first.Revision, retry.Revision);
        Assert.Single(await retryRequest.AvatarOwnerships.Where(x => x.UserSub == Alice && x.AvatarId == "bip01").ToListAsync());
    }

    [Fact]
    public async Task UnknownAndUnownedAvatarsAreRejectedWithoutChangingWalletOrSelection()
    {
        await using var db = OpenDatabase();
        var controller = As(db, Alice);
        Success(await controller.Get(default));
        Failure(await controller.Unlock("future-robot", default), 404, "avatar_not_found");
        Failure(await controller.Select(new("future-robot"), default), 404, "avatar_not_found");
        Failure(await controller.Select(new("bip01"), default), 409, "avatar_not_owned");
        var profile = Success(await controller.Get(default));
        Assert.Equal("starter", profile.SelectedAvatarId);
        Assert.Equal(320, profile.Gold);
        Assert.Equal(["starter"], profile.OwnedAvatarIds);
        Assert.True(profile.CanImportLegacy);
    }

    [Fact]
    public async Task UnlockUsesDatabaseLevelAndGold()
    {
        await using var db = OpenDatabase();
        var controller = As(db, Alice);
        Success(await controller.Get(default));
        var stored = await db.AvatarProfiles.SingleAsync();
        stored.Level = 1;
        await db.SaveChangesAsync();
        Failure(await controller.Unlock("bip01", default), 409, "level_required");

        stored = await db.AvatarProfiles.SingleAsync();
        stored.Level = 7;
        stored.Gold = 149;
        await db.SaveChangesAsync();
        Failure(await controller.Unlock("bip01", default), 409, "insufficient_gold");
        var profile = Success(await controller.Get(default));
        Assert.Equal(149, profile.Gold);
        Assert.Equal(["starter"], profile.OwnedAvatarIds);
        Assert.Equal("starter", profile.SelectedAvatarId);
    }

    [Fact]
    public async Task FurnitureAndAvatarsUseTheSameWalletAndRepeatedPurchaseIsFree()
    {
        await using var db = OpenDatabase();
        var controller = As(db, Alice);
        var lamp = Success(await controller.BuyItem("lamp", default));
        Assert.Equal(240, lamp.Gold);
        Assert.Contains("lamp", lamp.OwnedItemIds);
        var robot = Success(await controller.Unlock("bip01", default));
        Assert.Equal(90, robot.Gold);
        Failure(await controller.BuyItem("bookcase", default), 409, "insufficient_gold");
        Failure(await controller.BuyItem("sofa", default), 409, "level_required");
        Failure(await controller.BuyItem("unknown-item", default), 404, "item_not_found");
        var retry = Success(await controller.BuyItem("lamp", default));
        Assert.Equal(90, retry.Gold);
        Assert.Equal(robot.Revision, retry.Revision);
        Assert.DoesNotContain("bookcase", retry.OwnedItemIds);

        await using var anotherDevice = OpenDatabase();
        var persisted = Success(await As(anotherDevice, Alice).Get(default));
        Assert.Equal(90, persisted.Gold);
        Assert.Contains("lamp", persisted.OwnedItemIds);
        Assert.Contains("bip01", persisted.OwnedAvatarIds);
    }

    [Theory]
    [InlineData(int.MaxValue, 170)]
    [InlineData(80, 80)]
    [InlineData(-10, 0)]
    public async Task LegacyImportFiltersUnknownIdsAndBoundsRemainingGold(int browserGold, int expectedGold)
    {
        await using var db = OpenDatabase();
        var imported = Success(await As(db, Alice).ImportLegacy(
            new(["starter", "bip01", "bip01", "unknown"], "bip01", browserGold), default));
        Assert.Equal(["bip01", "starter"], imported.OwnedAvatarIds);
        Assert.Equal("bip01", imported.SelectedAvatarId);
        Assert.Equal(expectedGold, imported.Gold);
        Assert.False(imported.CanImportLegacy);

        await using var anotherDevice = OpenDatabase();
        var persisted = Success(await As(anotherDevice, Alice).Get(default));
        Assert.Equal("bip01", persisted.SelectedAvatarId);
        Assert.Equal(expectedGold, persisted.Gold);
    }

    [Theory]
    [InlineData(false, 320)]
    [InlineData(true, 170)]
    public async Task LegacyBrowserDataCannotGrantNewRaymondAvatar(bool importRobot, int expectedBalance)
    {
        await using var db = OpenDatabase();
        var controller = As(db, Alice);
        string[] requested = importRobot ? ["starter", "bip01", "raymond"] : ["starter", "raymond"];
        var imported = Success(await controller.ImportLegacy(new(requested, AvatarEconomy.RaymondId, 320), default));
        Assert.DoesNotContain(AvatarEconomy.RaymondId, imported.OwnedAvatarIds);
        Assert.Equal(AvatarEconomy.StarterId, imported.SelectedAvatarId);
        Assert.Equal(expectedBalance, imported.Gold);
        Assert.Equal(importRobot, imported.OwnedAvatarIds.Contains(AvatarEconomy.Bip01Id));

        var paid = Success(await controller.Unlock(AvatarEconomy.RaymondId, default));
        Assert.Equal(expectedBalance - 150, paid.Gold);
        Assert.Equal(AvatarEconomy.RaymondId, paid.SelectedAvatarId);
    }

    [Fact]
    public async Task LegacySelectionMustBeOwnedAndImportCannotOverwriteAnExistingAccount()
    {
        await using var db = OpenDatabase();
        var controller = As(db, Alice);
        var imported = Success(await controller.ImportLegacy(new(["starter", "unknown"], "bip01", 85), default));
        Assert.Equal("starter", imported.SelectedAvatarId);
        Failure(await controller.ImportLegacy(new(["starter", "bip01"], "bip01", 320), default), 409, "legacy_import_closed");
        var after = Success(await controller.Get(default));
        Assert.Equal(85, after.Gold);
        Assert.Equal("starter", after.SelectedAvatarId);
        Assert.DoesNotContain("bip01", after.OwnedAvatarIds);
    }

    [Fact]
    public async Task FirstSuccessfulMutationClosesLegacyImportEvenIfSelectionWasAlreadyCurrent()
    {
        await using var db = OpenDatabase();
        var controller = As(db, Alice);
        Success(await controller.Get(default));
        var selected = Success(await controller.Select(new("starter"), default));
        Assert.False(selected.CanImportLegacy);
        Failure(await controller.ImportLegacy(new(["starter", "bip01"], "bip01", 170), default), 409, "legacy_import_closed");
        Assert.Equal(320, Success(await controller.Get(default)).Gold);
        Assert.Equal(1, await db.AvatarOwnerships.CountAsync());
    }

    [Fact]
    public async Task OversizedLegacyPayloadIsRejectedWithoutCreatingAProfile()
    {
        await using var db = OpenDatabase();
        Failure(await As(db, Alice).ImportLegacy(new(Enumerable.Repeat("starter", 129).ToArray(), "starter", 320), default),
            400, "invalid_legacy_profile");
        Assert.Empty(await db.AvatarProfiles.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentUnlockRequestsDeductOnce()
    {
        await using (var setup = OpenDatabase())
            Success(await As(setup, Alice).Get(default));

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<AvatarProfileDto> UnlockOnSeparateConnection()
        {
            await start.Task;
            await using var db = OpenDatabase();
            return Success(await As(db, Alice).Unlock("bip01", default));
        }
        var first = Task.Run(UnlockOnSeparateConnection);
        var second = Task.Run(UnlockOnSeparateConnection);
        start.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.All(results, profile => Assert.Equal(170, profile.Gold));
        await using var verify = OpenDatabase();
        var saved = Success(await As(verify, Alice).Get(default));
        Assert.Equal(170, saved.Gold);
        Assert.Equal(1, saved.Revision);
        Assert.Single(await verify.AvatarOwnerships.Where(x => x.UserSub == Alice && x.AvatarId == "bip01").ToListAsync());
    }

    [Fact]
    public async Task AStaleWalletRevisionCannotOverwriteAnotherRequest()
    {
        await using (var setup = OpenDatabase())
            Success(await As(setup, Alice).Get(default));
        await using var first = OpenDatabase();
        await using var stale = OpenDatabase();
        var current = await first.AvatarProfiles.SingleAsync(x => x.UserSub == Alice);
        var old = await stale.AvatarProfiles.SingleAsync(x => x.UserSub == Alice);
        current.Gold -= 150;
        current.Revision++;
        await first.SaveChangesAsync();
        old.Gold -= 80;
        old.Revision++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        await using var verify = OpenDatabase();
        Assert.Equal(170, (await verify.AvatarProfiles.SingleAsync()).Gold);
    }
}
