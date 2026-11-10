using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Data.Migrations;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mercantec.Tech.Api.Tests;

public sealed class UploadedGameTests : IAsyncLifetime
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000301");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000302");
    private readonly string path = Path.Combine(Path.GetTempPath(), $"uploaded-games-{Guid.NewGuid():N}.db");
    private AppDbContext Open(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={path};Pooling=False").AddInterceptors(interceptors).Options);
    public async Task InitializeAsync() { await using var db = Open(); await db.Database.EnsureCreatedAsync(); }
    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" }) System.IO.File.Delete(path + suffix);
        return Task.CompletedTask;
    }
    private static ControllerContext Context(Guid subject) => new() { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", subject.ToString())], "test")) } };
    private static UploadedGamesController Games(AppDbContext db, MemoryStorage storage, Guid? subject = null) =>
        new(db, storage) { ControllerContext = Context(subject ?? Alice) };
    private static AvatarsController Avatars(AppDbContext db, Guid? subject = null) => new(db) { ControllerContext = Context(subject ?? Alice) };
    private static AvatarProfileDto Profile(ActionResult<AvatarProfileDto> value) => Assert.IsType<AvatarProfileDto>(Assert.IsType<OkObjectResult>(value.Result).Value);
    private static AvatarUnlockProgressDto Progress(AvatarProfileDto profile) => Assert.Single(profile.UnlockProgress!, p => p.AvatarId == "lupp");
    private static IFormFile Html(string text = "<!doctype html><button>Spil</button>") => File("spil.html", Encoding.UTF8.GetBytes(text));
    private static IFormFile File(string name, byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);
    private static IFormFile Zip(params (string Path, string Content)[] entries)
    {
        using var bytes = new MemoryStream();
        using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, true))
            foreach (var entry in entries)
            { using var writer = new StreamWriter(archive.CreateEntry(entry.Path).Open()); writer.Write(entry.Content); }
        return File("spil.zip", bytes.ToArray());
    }

    [Fact]
    public async Task OnlyACompletedOwnedUploadUnlocksLuppForFreeAndFollowsTheAccountToAnotherDevice()
    {
        var storage = new MemoryStorage(); Guid fileId;
        await using (var db = Open())
        {
            var before = Profile(await Avatars(db).Get(default));
            Assert.False(Progress(before).IsMet);
            Assert.IsType<ObjectResult>((await Avatars(db).Unlock("lupp", default)).Result);
            // An ordinary ZIP attachment and another student's upload are not this user's game.
            db.FileAssets.Add(new FileAsset { Id = Guid.NewGuid(), OwnerSub = Alice, OriginalName = "attachment.zip", Key = "attachment" });
            await db.SaveChangesAsync();
            Assert.IsType<CreatedResult>(await Games(db, storage, Bob).Upload(Html(), "Bobs spil", Guid.NewGuid(), default));
            Assert.False(Progress(Profile(await Avatars(db).Get(default))).IsMet);
            var uploaded = Assert.IsType<UploadedGameView>(Assert.IsType<CreatedResult>(await Games(db, storage).Upload(Html(), "Mit spil", Guid.NewGuid(), default)).Value);
            fileId = uploaded.FileId;
            Assert.Equal("index.html", uploaded.EntryPoint);
            Assert.Null((await db.FileAssets.FindAsync(fileId))!.HoldId);
            Assert.True(Progress(Profile(await Avatars(db).Get(default))).IsMet);
            var claimed = Profile(await Avatars(db).Unlock("lupp", default));
            Assert.Equal(before.Gold, claimed.Gold);
            Assert.Equal("lupp", claimed.SelectedAvatarId);
            Assert.Equal(claimed.Revision, Profile(await Avatars(db).Unlock("lupp", default)).Revision);
        }
        await using var secondDevice = Open();
        var saved = Profile(await Avatars(secondDevice).Get(default));
        Assert.Equal("lupp", saved.SelectedAvatarId);
        Assert.Contains("lupp", saved.OwnedAvatarIds);
        Assert.Equal(1, Progress(saved).Current);
        var own = Assert.IsType<UploadedGameView[]>(Assert.IsType<OkObjectResult>(await Games(secondDevice, storage).List(default)).Value);
        Assert.Equal(fileId, Assert.Single(own).FileId);
        // Removing a source file removes proof, but an earned avatar stays owned permanently.
        secondDevice.FileAssets.Remove((await secondDevice.FileAssets.FindAsync(fileId))!); await secondDevice.SaveChangesAsync();
        Assert.False(Progress(Profile(await Avatars(secondDevice).Get(default))).IsMet);
        Profile(await Avatars(secondDevice).Select(new("starter"), default));
        Assert.Equal("lupp", Profile(await Avatars(secondDevice).Select(new("lupp"), default)).SelectedAvatarId);
    }

    [Fact]
    public async Task RetryIsIdempotentAndCannotReplaceContentTitleOrAnotherAccountsUpload()
    {
        await using var db = Open(); var storage = new MemoryStorage(); var request = Guid.NewGuid();
        var created = Assert.IsType<UploadedGameView>(Assert.IsType<CreatedResult>(await Games(db, storage).Upload(Html(), "Spil", request, default)).Value);
        var count = storage.Objects.Count;
        Assert.Equal(created, Assert.IsType<UploadedGameView>(Assert.IsType<OkObjectResult>(await Games(db, storage).Upload(Html(), "Spil", request, default)).Value));
        Assert.Equal(count, storage.Objects.Count);
        Assert.IsType<ConflictObjectResult>(await Games(db, storage).Upload(Html("<button>Ændret</button>"), "Spil", request, default));
        Assert.IsType<ConflictObjectResult>(await Games(db, storage).Upload(Html(), "Andet navn", request, default));
        Assert.IsType<CreatedResult>(await Games(db, storage, Bob).Upload(Html(), "Bobs spil", request, default));
        Assert.Equal(2, await db.UploadedGames.CountAsync());
        Assert.Equal(1, Progress(Profile(await Avatars(db).Get(default))).Current);
    }

    [Theory]
    [InlineData("no-entry")]
    [InlineData("unsafe-path")]
    [InlineData("duplicate")]
    [InlineData("corrupt")]
    [InlineData("link")]
    public async Task FailedOrUntrustedBuildsCreateNeitherProofNorFilesAndCleanPartialObjects(string scenario)
    {
        await using var db = Open(); var storage = new MemoryStorage();
        var file = scenario switch
        {
            "no-entry" => Zip(("readme.txt", "Tekst")),
            "unsafe-path" => Zip(("index.html", "<button>Spil</button>"), ("../escape.js", "alert(1)")),
            "duplicate" => Zip(("index.html", "a"), ("index.html", "b")),
            "corrupt" => File("spil.zip", [1, 2, 3]),
            _ => File("spil.url", Encoding.UTF8.GetBytes("https://example.com"))
        };
        Assert.IsType<BadRequestObjectResult>(await Games(db, storage).Upload(file, "Spil", Guid.NewGuid(), default));
        Assert.Empty(storage.Objects);
        Assert.Equal(0, await db.UploadedGames.CountAsync()); Assert.Equal(0, await db.FileAssets.CountAsync());
        Assert.False(Progress(Profile(await Avatars(db).Get(default))).IsMet);
    }

    [Fact]
    public async Task StorageOrDatabaseFailureCleansEveryDerivativeAndCannotGrantTheAchievement()
    {
        var storage = new MemoryStorage { FailAtPut = 3 };
        await using (var db = Open())
        {
            await Assert.ThrowsAsync<IOException>(() => Games(db, storage).Upload(Zip(("index.html", "Spil"), ("main.js", "1")), "Spil", Guid.NewGuid(), default));
            Assert.Empty(storage.Objects); Assert.Equal(0, await db.UploadedGames.CountAsync());
        }
        storage = new MemoryStorage();
        await using (var db = Open(new RejectGameWrites()))
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => Games(db, storage).Upload(Html(), "Spil", Guid.NewGuid(), default));
            Assert.Empty(storage.Objects);
        }
        await using var verify = Open();
        Assert.Equal(0, await verify.UploadedGames.CountAsync()); Assert.Equal(0, await verify.FileAssets.CountAsync());
    }

    [Fact]
    public async Task StaleAccountHeaderAndBrowserImportCannotGrantLuppAndExportContainsOnlyOwnGames()
    {
        await using var db = Open(); var storage = new MemoryStorage(); var api = Games(db, storage);
        api.Request.Headers["X-LUP-Game-Subject"] = Bob.ToString();
        Assert.IsType<ConflictObjectResult>(await api.Upload(Html(), "Spil", Guid.NewGuid(), default));
        Assert.Empty(storage.Objects);
        var imported = Profile(await Avatars(db).ImportLegacy(new(["starter", "lupp"], "lupp", 320), default));
        Assert.DoesNotContain("lupp", imported.OwnedAvatarIds); Assert.Equal("starter", imported.SelectedAvatarId);
        Assert.IsType<CreatedResult>(await Games(db, storage).Upload(Zip(("build/index.html", "Spil"), ("build/main.js", "1")), "Eget spil", Guid.NewGuid(), default));
        Assert.IsType<CreatedResult>(await Games(db, storage, Bob).Upload(Html(), "Andens spil", Guid.NewGuid(), default));
        var export = Assert.IsType<FileContentResult>(await new PrivacyController(db) { ControllerContext = Context(Alice) }.Export(default));
        using var json = JsonDocument.Parse(export.FileContents);
        var ownGame = Assert.Single(json.RootElement.GetProperty("PlatformRecords").GetProperty("uploadedGames").EnumerateArray());
        Assert.Equal("Eget spil", ownGame.GetProperty("Title").GetString());
        Assert.False(ownGame.TryGetProperty("Key", out _)); Assert.False(ownGame.TryGetProperty("ContentSha256", out _));
    }

    [Fact]
    public void MigrationMatchesPostgresModelAndCreatesOwnerScopedUniqueProofWithCascadeDeletion()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript("20261109130000_GiftThemesForAvatarRequirements", "20261110085326_UploadedGamesForAvatarRequirements", MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE uploaded_games", script);
        Assert.Contains("IX_uploaded_games_OwnerSub_RequestId", script);
        Assert.Contains("ON DELETE CASCADE", script);
        Assert.DoesNotContain("INSERT INTO uploaded_games", script);
    }

    private sealed class RejectGameWrites : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (data.Context!.ChangeTracker.Entries<UploadedGame>().Any()) throw new DbUpdateException("Synthetic failure");
            return base.SavingChangesAsync(data, result, cancellationToken);
        }
    }
    private sealed class MemoryStorage : IObjectStorage
    {
        public Dictionary<string, byte[]> Objects { get; } = [];
        public int FailAtPut { get; init; }
        private int puts;
        public bool IsConfigured => true;
        public string Bucket => "test";
        public async Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            using var output = new MemoryStream(); await content.CopyToAsync(output, ct);
            Objects[key] = output.ToArray();
            if (++puts == FailAtPut) throw new IOException("Synthetic partial write");
            return new(key, Bucket, contentType, output.Length);
        }
        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(Objects.TryGetValue(key, out var bytes) ? new ObjectGetResult(new MemoryStream(bytes), "application/octet-stream", bytes.Length) : null);
        public Task DeleteAsync(string key, CancellationToken ct = default) { Objects.Remove(key); return Task.CompletedTask; }
        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default) => Task.FromResult<Uri?>(null);
    }
}
