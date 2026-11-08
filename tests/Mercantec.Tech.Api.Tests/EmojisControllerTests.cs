using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Data.Migrations;
using Mercantec.Tech.Api.Emojis;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Mercantec.Tech.Api.Tests;

public sealed class EmojisControllerTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;
    private readonly FakeEmojiStorage _storage = new();
    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();
    private readonly Guid _teacher = Guid.NewGuid();
    private readonly Guid _student = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        SQLitePCL.Batteries_V2.Init();
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task AnonymousCannotListAndStudentsCannotUpload()
    {
        Assert.IsType<UnauthorizedResult>(await As(null).List(default));
        Assert.IsType<UnauthorizedResult>(await As(null).Upload(Png("lup.png"), "lup", default));
        Assert.IsType<ForbidResult>(await As(_student).Upload(Png("lup.png"), "lup", default));
        Assert.Empty(ReadStoredList(await As(_student).List(default)));
    }

    [Fact]
    public async Task TeacherAndAdminCanUploadAndEveryoneLoggedInCanRead()
    {
        var created = Read(await As(_teacher, teacher: true).Upload(Png("lup.png"), ":Lup:", default));
        Assert.Equal("lup", created.Name);
        Assert.Equal("image/png", created.ContentType);
        Assert.False(created.IsStandard);
        Assert.Equal("custom", created.Category);
        Assert.Empty(created.Keywords!);
        Assert.StartsWith($"/api/emojis/{created.Id}/content?ticket=", created.ContentUrl);
        Assert.Equal("lup", Assert.Single(ReadStoredList(await As(_student).List(default))).Name);

        var admin = Read(await As(Guid.NewGuid(), admin: true).Upload(Gif("ok.gif"), "ok", default));
        Assert.Equal("image/gif", admin.ContentType);
        Assert.Equal(2, ReadStoredList(await As(_student).List(default)).Count);
    }

    [Fact]
    public async Task HyphenatedNamesAreNormalizedAndKeepExistingUniquenessRules()
    {
        var controller = As(_teacher, teacher: true);
        var created = Read(await controller.Upload(Png("Vatson-Vinker.png"), ":Vatson-Vinker:", default));
        Assert.Equal("vatson-vinker", created.Name);
        Assert.Equal("name_taken", Error(await controller.Upload(Png("another.png"), "VATSON-VINKER", default)));
        Assert.Equal("thumbs_up", EmojiName.Normalize(":THUMBS_UP:"));
        Assert.Null(EmojiName.Normalize("vatson/ugyldig"));
        Assert.Null(EmojiName.Normalize("vatson-" + new string('a', 26)));
        var renamed = Read(await controller.Rename(created.Id, new("Vatson-Tommel-Op"), default));
        Assert.Equal("vatson-tommel-op", renamed.Name);
    }

    [Fact]
    public async Task ContentRequiresLoginOrAMatchingTicket()
    {
        var created = Read(await As(_teacher, teacher: true).Upload(Png("lup.png"), "lup", default));
        Assert.IsType<UnauthorizedResult>(await As(null).Content(created.Id, default));
        var ticket = QueryHelpers.ParseQuery(new Uri("https://lup.test" + created.ContentUrl).Query)["ticket"].ToString();
        var viaTicket = Assert.IsType<FileStreamResult>(await As(null).Content(created.Id, default, ticket));
        Assert.Equal("image/png", viaTicket.ContentType);
        using var buffer = new MemoryStream();
        await viaTicket.FileStream.CopyToAsync(buffer);
        Assert.Equal(PngBytes, buffer.ToArray());
        Assert.IsType<UnauthorizedResult>(await As(null).Content(Guid.NewGuid(), default, ticket));
        Assert.IsType<UnauthorizedResult>(await As(null).Content(created.Id, default, "not-a-ticket"));
        var viaLogin = Assert.IsType<FileStreamResult>(await As(_student).Content(created.Id, default));
        Assert.Equal("image/png", viaLogin.ContentType);
    }

    [Fact]
    public async Task InvalidNamesTypesAndDuplicatesAreRejected()
    {
        var teacher = As(_teacher, teacher: true);
        Assert.IsType<BadRequestObjectResult>(await teacher.Upload(Png("lup.png"), "l", default));
        Assert.IsType<BadRequestObjectResult>(await teacher.Upload(Png("lup.png"), "med mellemrum", default));
        Assert.IsType<BadRequestObjectResult>(await teacher.Upload(Png("lup.png"), "æble", default));
        Assert.IsType<BadRequestObjectResult>(await teacher.Upload(Form("note.svg", "<svg>"u8.ToArray()), "svg", default));
        Assert.IsType<BadRequestObjectResult>(await teacher.Upload(Form("fake.png", "ikke-et-billede"u8.ToArray()), "fake", default));
        Assert.IsType<BadRequestObjectResult>(await teacher.Upload(Form("big.png", new byte[EmojisController.MaxBytes + 1]), "big", default));
        await teacher.Upload(Png("lup.png"), "lup", default);
        Assert.Equal("name_taken", Error(await teacher.Upload(Png("anden.png"), "LUP", default)));
        Assert.Single(await _db.CustomEmojis.ToListAsync());
        Assert.Single(_storage.Objects);
    }

    [Fact]
    public async Task RenameAndDeleteAreTeacherOnlyAndFreeTheName()
    {
        var created = Read(await As(_teacher, teacher: true).Upload(Png("lup.png"), "lup", default));
        Assert.IsType<ForbidResult>(await As(_student).Rename(created.Id, new("wolf"), default));
        Assert.IsType<ForbidResult>(await As(_student).Delete(created.Id, default));
        Assert.Equal("invalid_name", Error(await As(_teacher, teacher: true).Rename(created.Id, new("x"), default)));
        var renamed = Read(await As(_teacher, teacher: true).Rename(created.Id, new(":Wolf:"), default));
        Assert.Equal("wolf", renamed.Name);
        Assert.Equal(created.Id, renamed.Id);
        var again = Read(await As(_teacher, teacher: true).Upload(Png("lup.png"), "lup", default));
        Assert.NotEqual(created.Id, again.Id);
        Assert.Equal("name_taken", Error(await As(_teacher, teacher: true).Rename(again.Id, new("wolf"), default)));
        Assert.IsType<NoContentResult>(await As(_teacher, teacher: true).Delete(created.Id, default));
        Assert.IsType<NotFoundResult>(await As(_student).Content(created.Id, default));
        Assert.DoesNotContain(_storage.Objects.Keys, key => key.Contains(created.Id.ToString("N")));
        Assert.Equal("lup", Assert.Single(ReadStoredList(await As(_student).List(default))).Name);
    }

    [Fact]
    public async Task UploadIsRejectedWhenStorageIsDisabled()
    {
        var controller = As(_teacher, teacher: true, storage: new DisabledObjectStorage());
        Assert.Equal(503, Assert.IsType<ObjectResult>(await controller.Upload(Png("lup.png"), "lup", default)).StatusCode);
        Assert.Empty(await _db.CustomEmojis.ToListAsync());
    }

    [Fact]
    public async Task StandardImportIsIdempotentAndPreservesExistingNames()
    {
        Assert.Contains("20261107090000_SeedStandardEmojis", _db.Database.GetMigrations());
        var existing = Read(await As(_teacher, teacher: true).Upload(Png("smile.png"), "smile", default));
        await ImportStandards();
        await ImportStandards();

        var rows = await _db.CustomEmojis.ToListAsync();
        Assert.Equal(40, rows.Count);
        Assert.Equal(existing.Id, Assert.Single(rows, row => row.Name == "smile").Id);
        Assert.Equal(39, rows.Count(row => StandardEmojiAssets.IsStandard(row.Key)));
        var ownSmile = Assert.Single(ReadList(await As(_student).List(default)), item => item.Name == "smile");
        Assert.False(ownSmile.IsStandard);
        Assert.Equal("custom", ownSmile.Category);
        Assert.Single(_storage.Objects);
    }

    [Fact]
    public async Task StandardImagesUseNormalTicketsAndWorkWithoutObjectStorage()
    {
        await ImportStandards();
        var items = ReadStoredList(await As(_student).List(default));
        Assert.Equal(40, items.Count);
        var controller = As(null, storage: new DisabledObjectStorage());
        foreach (var item in items)
        {
            Assert.True(item.IsStandard);
            Assert.False(item.IsReadOnly);
            Assert.Contains(item.Category, new[] { "smileys", "gestures", "symbols", "learning", "technology", "everyday" });
            Assert.NotEmpty(item.Keywords!);
            Assert.IsType<UnauthorizedResult>(await controller.Content(item.Id, default));
            var ticket = QueryHelpers.ParseQuery(new Uri("https://lup.test" + item.ContentUrl).Query)["ticket"].ToString();
            var file = Assert.IsType<FileStreamResult>(await controller.Content(item.Id, default, ticket));
            await using var stream = file.FileStream;
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            Assert.Equal("image/png", file.ContentType);
            Assert.Equal(item.SizeBytes, buffer.Length);
            var bytes = buffer.ToArray();
            Assert.Equal(PngBytes[..8], bytes[..8]);
            Assert.Equal(72, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
            Assert.Equal(72, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
        }
        Assert.Empty(_storage.Objects);
    }

    [Fact]
    public async Task TeacherCanRenameAndDeleteImportedEmojiWithoutObjectStorage()
    {
        await ImportStandards();
        var standard = Assert.Single(ReadList(await As(_student).List(default)), item => item.Name == "rocket");
        var teacher = As(_teacher, teacher: true, storage: new DisabledObjectStorage());
        var renamed = Read(await teacher.Rename(standard.Id, new("raket"), default));
        Assert.Equal(standard.Id, renamed.Id);
        Assert.Equal("raket", renamed.Name);
        Assert.True(renamed.IsStandard);
        Assert.Equal("technology", renamed.Category);
        Assert.Contains("rocket", renamed.Keywords!);
        Assert.Contains("raket", renamed.Keywords!);
        Assert.Equal(standard.Keywords, renamed.Keywords);

        Assert.IsType<NoContentResult>(await teacher.Delete(standard.Id, default));
        Assert.IsType<NotFoundResult>(await As(_student).Content(standard.Id, default));
        Assert.Equal(39, ReadStoredList(await As(_student).List(default)).Count);
        Assert.Empty(_storage.Objects);
    }

    [Fact]
    public async Task CharacterEmojisAreAvailableWithoutUploadsOrObjectStorage()
    {
        var items = ReadList(await As(_student, storage: new DisabledObjectStorage()).List(default));
        Assert.Equal(168, items.Count);
        Assert.Equal(48, CharacterEmojiCatalog.All.Count(item => item.CharacterSlug == "lupp"));
        Assert.Equal(48, CharacterEmojiCatalog.All.Count(item => item.CharacterSlug == "raymond"));
        Assert.Equal(48, CharacterEmojiCatalog.All.Count(item => item.CharacterSlug == "chameleon"));
        Assert.Equal(12, CharacterEmojiCatalog.All.Count(item => item.CharacterSlug == "beaver"));
        Assert.Equal(12, CharacterEmojiCatalog.All.Count(item => item.CharacterSlug == "fox"));
        Assert.Equal("Tænkepause", CharacterEmojiCatalog.All.Single(item => item.Name == "beaver_thinking").Title);
        Assert.All(CharacterEmojiCatalog.All.Where(item => item.CharacterSlug == "beaver"), item => Assert.StartsWith("Bæver", item.Alt));
        Assert.Equal(items.Count, items.Select(item => item.Id).Distinct().Count());
        Assert.Equal(items.Count, items.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(items.OrderBy(item => item.Name, StringComparer.Ordinal), items);
        foreach (var asset in CharacterEmojiCatalog.All)
        {
            var item = Assert.Single(items, item => item.Id == asset.Id);
            Assert.Equal(asset.Name, item.Name);
            Assert.Equal(asset.Name, EmojiName.Normalize(asset.Name));
            Assert.Equal(asset.Preview, item.ContentUrl);
            Assert.StartsWith($"/images/characters/{asset.CharacterSlug}/reactions/", item.ContentUrl);
            Assert.EndsWith(".webp", item.ContentUrl.Split('?')[0]);
            Assert.DoesNotContain("ticket=", item.ContentUrl, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("image/webp", item.ContentType);
            Assert.Equal(asset.PreviewSizeBytes, item.SizeBytes);
            Assert.True(item.SizeBytes > 0);
            Assert.True(item.IsStandard);
            Assert.True(item.IsReadOnly);
            Assert.Contains(item.Category, EmojiMetadataPolicy.Categories);
            Assert.Contains(asset.CharacterSlug, item.Keywords!);
            Assert.Equal(asset.Alt, item.AltText);
            if (asset.CharacterSlug == "lupp")
            {
                Assert.StartsWith("LUPP", item.AltText);
                Assert.Contains("game design", item.Keywords!);
                Assert.Contains("spil", item.Keywords!);
                Assert.Contains("gameplay", item.Keywords!);
            }
            if (asset.CharacterSlug == "chameleon")
            {
                Assert.StartsWith("Kamæleonen", item.AltText);
                Assert.Contains("machine learning", item.Keywords!);
                Assert.Contains("maskinlæring", item.Keywords!);
                Assert.Contains("kamæleon", item.Keywords!);
            }
            Assert.Equal(new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero), item.UpdatedAtUtc);
        }
        Assert.Equal("learning", items.Single(item => item.Name == "lupp_idea").Category);
        Assert.Equal("smileys", items.Single(item => item.Name == "fox_idea").Category);
        Assert.Equal("technology", items.Single(item => item.Name == "chameleon_classify").Category);
        Assert.Equal("everyday", items.Single(item => item.Name == "chameleon_banjo").Category);
        Assert.Empty(await _db.CustomEmojis.ToListAsync());
        Assert.Empty(_storage.Objects);
    }

    [Theory]
    [InlineData("raymond")]
    [InlineData("beaver")]
    [InlineData("fox")]
    [InlineData("lupp")]
    [InlineData("chameleon")]
    public async Task BuiltInCharacterEmojisCannotBeRenamedOrDeleted(string character)
    {
        var asset = CharacterEmojiCatalog.All.First(item => item.CharacterSlug == character);
        var teacher = As(_teacher, teacher: true, storage: new DisabledObjectStorage());
        Assert.IsType<ForbidResult>(await teacher.Rename(asset.Id, new("renamed"), default));
        Assert.IsType<ForbidResult>(await teacher.Delete(asset.Id, default));
        Assert.IsType<ForbidResult>(await As(_student).Rename(asset.Id, new("renamed"), default));
        Assert.IsType<ForbidResult>(await As(_student).Delete(asset.Id, default));
        Assert.IsType<UnauthorizedResult>(await As(null).List(default));
        Assert.IsType<UnauthorizedResult>(await As(null).Rename(asset.Id, new("renamed"), default));
        Assert.IsType<UnauthorizedResult>(await As(null).Delete(asset.Id, default));
        Assert.Single(ReadList(await As(_student).List(default)), item => item.Id == asset.Id && item.Name == asset.Name);
        Assert.Empty(await _db.CustomEmojis.ToListAsync());
        Assert.Empty(_storage.Objects);
    }

    [Fact]
    public async Task CharacterShortcodesCannotBeClaimedByNewUploadsOrRenames()
    {
        var teacher = As(_teacher, teacher: true);
        var own = Read(await teacher.Upload(Png("ours.png"), "ours", default));
        foreach (var asset in CharacterEmojiCatalog.All.GroupBy(item => item.CharacterSlug).Select(group => group.First()))
        {
            Assert.Equal("name_taken", Error(await teacher.Upload(Png("other.png"), asset.Name.ToUpperInvariant(), default)));
            Assert.Equal("name_taken", Error(await teacher.Rename(own.Id, new(asset.Name), default)));
        }
        Assert.Equal("ours", Assert.Single(await _db.CustomEmojis.ToListAsync()).Name);
        Assert.Single(_storage.Objects);
    }

    [Fact]
    public async Task LegacyUploadedNamesAndIdsRemainAuthoritativeInTheCombinedCatalog()
    {
        var first = CharacterEmojiCatalog.All[0];
        var second = CharacterEmojiCatalog.All[1];
        var legacyName = new CustomEmoji
        {
            Id = Guid.NewGuid(), Name = first.Name, Key = "emojis/legacy-name", ContentType = "image/png",
            SizeBytes = PngBytes.Length, CreatedBySub = _teacher,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var legacyId = new CustomEmoji
        {
            Id = second.Id, Name = "legacy_id", Key = "emojis/legacy-id", ContentType = "image/png",
            SizeBytes = PngBytes.Length, CreatedBySub = _teacher,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        _db.CustomEmojis.AddRange(legacyName, legacyId);
        await _db.SaveChangesAsync();
        var items = ReadList(await As(_student).List(default));
        Assert.Equal(CharacterEmojiCatalog.All.Count, items.Count);
        var byName = Assert.Single(items, item => item.Name == first.Name);
        Assert.Equal(legacyName.Id, byName.Id);
        Assert.False(byName.IsStandard);
        Assert.False(byName.IsReadOnly);
        Assert.StartsWith("/api/emojis/", byName.ContentUrl);
        var byId = Assert.Single(items, item => item.Id == second.Id);
        Assert.Equal("legacy_id", byId.Name);
        Assert.False(byId.IsStandard);
        Assert.False(byId.IsReadOnly);
        Assert.DoesNotContain(items, item => item.Id == first.Id);
        Assert.Equal(items.Count, items.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(items.Count, items.Select(item => item.Id).Distinct().Count());
        var renamed = Read(await As(_teacher, teacher: true).Rename(legacyId.Id, new("legacy_renamed"), default));
        Assert.Equal(legacyId.Id, renamed.Id);
        Assert.Equal("legacy_renamed", renamed.Name);
        Assert.False(renamed.IsStandard);
    }

    private Task ImportStandards()
    {
        var migration = new SeedStandardEmojis();
        var operation = Assert.IsType<SqlOperation>(Assert.Single(migration.UpOperations));
        return _db.Database.ExecuteSqlRawAsync(operation.Sql);
    }

    [Fact]
    public async Task UploadMetadataSurvivesRenameAndCanBeEditedWithoutChangingTheImage()
    {
        var controller = As(_teacher, teacher: true);
        var created = Read(await controller.Upload(Png("kat.png"), "kat", default,
            altText: "Lilla kat med et stort smil", category: "smileys", keywords: "[\"kat\",\"glad\",\"cat\"]"));
        Assert.False(created.IsStandard);
        Assert.Equal("Lilla kat med et stort smil", created.AltText);
        Assert.Equal("smileys", created.Category);
        Assert.Equal(new[] { "kat", "glad", "cat" }, created.Keywords);
        var renamed = Read(await controller.Rename(created.Id, new("vores_kat"), default));
        Assert.Equal(created.AltText, renamed.AltText);
        Assert.Equal(created.Keywords, renamed.Keywords);
        var patchJson = JsonSerializer.Serialize(new
        {
            name = "vores_kat", altText = "Kat til teamets fejring", category = "symbols",
            keywords = new[] { " Fejring ", "FEJRING", "  " }
        });
        var patch = JsonSerializer.Deserialize<EmojisController.RenameEmoji>(patchJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(new[] { " Fejring ", "FEJRING", "  " }, patch!.Keywords);
        var edited = Read(await controller.Rename(created.Id, patch, default));
        Assert.Equal("Kat til teamets fejring", edited.AltText);
        Assert.Equal("symbols", edited.Category);
        Assert.Equal(new[] { "fejring" }, edited.Keywords);
        Assert.Single(_storage.Objects);
        Assert.Equal("invalid_metadata", Error(await controller.Upload(Png("bad.png"), "bad_metadata", default, keywords: "ikke JSON")));
    }

    [Fact]
    public async Task AnalysisIsTeacherOnlyReadOnlyAndDoesNotRequireObjectStorage()
    {
        Assert.IsType<UnauthorizedResult>(await As(null).Analyze(Png("kat.png"), default));
        Assert.IsType<ForbidResult>(await As(_student).Analyze(Png("kat.png"), default));
        await As(_teacher, teacher: true).Upload(Png("existing.png"), "glad_kat", default);
        using var transport = new OpenAiEmojiAnalysisClientTests.AnalysisTransport();
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://openai.test/v1/") };
        var controller = As(_teacher, teacher: true, storage: new DisabledObjectStorage(), analysis: OpenAiEmojiAnalysisClientTests.Client(http));
        var response = Assert.IsType<OkObjectResult>(await controller.Analyze(Png("kat.png"), default));
        var result = Assert.IsType<EmojiAnalysisResult>(response.Value);
        Assert.Equal("glad_kat_2", result.Name);
        Assert.Equal("Smilende lilla kat", result.AltText);
        Assert.Equal(1, transport.Requests);
        Assert.Single(await _db.CustomEmojis.ToListAsync());
        Assert.Single(_storage.Objects);
        Assert.IsType<BadRequestObjectResult>(await controller.Analyze(Form("fake.png", "not image"u8.ToArray()), default));
        Assert.Equal(1, transport.Requests);
    }

    [Fact]
    public async Task AnalysisWithoutProviderReturnsManualFallbackWithoutSaving()
    {
        var unavailable = Assert.IsType<ObjectResult>(await As(_teacher, teacher: true).Analyze(Png("kat.png"), default));
        Assert.Equal(503, unavailable.StatusCode);
        Assert.Empty(await _db.CustomEmojis.ToListAsync());
        Assert.Empty(_storage.Objects);
    }

    private EmojisController As(Guid? sub, bool teacher = false, bool admin = false, IObjectStorage? storage = null, OpenAiEmojiAnalysisClient? analysis = null)
    {
        var claims = new List<Claim>();
        if (sub is Guid id) claims.Add(new("sub", id.ToString()));
        if (teacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        if (admin) claims.Add(new(ClaimTypes.Role, "Admin"));
        return new EmojisController(_db, storage ?? _storage, _protection, analysis)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, sub is null ? null : "test"))
                }
            }
        };
    }

    private static EmojisController.CustomEmojiView Read(IActionResult result) =>
        Assert.IsType<EmojisController.CustomEmojiView>(Assert.IsType<OkObjectResult>(result).Value);

    private static List<EmojisController.CustomEmojiView> ReadList(IActionResult result) =>
        Assert.IsType<List<EmojisController.CustomEmojiView>>(Assert.IsType<OkObjectResult>(result).Value);

    private static List<EmojisController.CustomEmojiView> ReadStoredList(IActionResult result) =>
        ReadList(result).Where(item => item.ContentUrl.StartsWith("/api/emojis/", StringComparison.Ordinal)).ToList();

    private static string? Error(IActionResult result) =>
        result switch
        {
            BadRequestObjectResult bad => bad.Value?.GetType().GetProperty("error")?.GetValue(bad.Value)?.ToString(),
            ConflictObjectResult conflict => conflict.Value?.GetType().GetProperty("error")?.GetValue(conflict.Value)?.ToString(),
            _ => null
        };

    private static FormFile Png(string name) => Form(name, PngBytes);
    private static FormFile Gif(string name) => Form(name, "GIF89a"u8.ToArray());
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private static FormFile Form(string name, byte[] bytes) =>
        new(new MemoryStream(bytes), 0, bytes.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = "application/octet-stream" };

    private sealed class FakeEmojiStorage : IObjectStorage
    {
        public Dictionary<string, (byte[] Bytes, string Mime)> Objects { get; } = new();
        public bool IsConfigured => true;
        public string Bucket => "test";

        public async Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Objects[key] = (buffer.ToArray(), contentType);
            return new(key, Bucket, contentType, buffer.Length);
        }

        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<ObjectGetResult?>(Objects.TryGetValue(key, out var obj)
                ? new ObjectGetResult(new MemoryStream(obj.Bytes), obj.Mime, obj.Bytes.Length) : null);

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            Objects.Remove(key);
            return Task.CompletedTask;
        }

        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) =>
            Task.FromResult<Uri?>(null);
    }
}
