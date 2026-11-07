using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Emojis;
using Mercantec.Tech.Api.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed class ExtensionUploadTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly FakeClock _clock = new();
    private readonly ExtensionUploadOptions _options = new() { AllowedEmails = "mags@mercantec.dk;kasc@mercantec.dk" };
    private readonly Guid _mathias = Guid.NewGuid(), _kasper = Guid.NewGuid();
    private AppDbContext _db = null!;
    private ExtensionUploadKeyService _keys = null!;
    private readonly FakeStorage _storage = new();

    public async Task InitializeAsync()
    {
        SQLitePCL.Batteries_V2.Init();
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _keys = new(_db, Options.Create(_options), _clock);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }

    [Fact]
    public async Task OnlyAllowedAuthenticatedTeachersWithStableSubjectsCanIssueKeys()
    {
        var owner = Teacher(_mathias, "MAGS@MERCANTEC.DK");
        Assert.True(_keys.CanManage(owner));
        foreach (var bad in new[] {
            Teacher(_mathias, "other@mercantec.dk"), Teacher(Guid.Empty, "mags@mercantec.dk"),
            Teacher(_mathias, "mags@mercantec.dk", teacher: false), new ClaimsPrincipal(new ClaimsIdentity()) })
        {
            Assert.False(_keys.CanManage(bad));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _keys.Create(bad, "Chrome", default));
        }
        _options.AllowedEmails = "";
        Assert.False(_keys.CanManage(owner));
    }

    [Fact]
    public async Task RawCredentialIsReturnedOnceAndOnlyItsHashIsStored()
    {
        var owner = Teacher(_mathias, "mags@mercantec.dk");
        var created = Assert.IsType<CreatedExtensionUploadKey>(await _keys.Create(owner, "Min Chrome", default));
        Assert.True(ExtensionUploadCredentials.IsWellFormed(created.Key));
        var stored = Assert.Single(await _db.ExtensionUploadKeys.ToListAsync());
        Assert.Equal(ExtensionUploadCredentials.Hash(created.Key), stored.KeyHash);
        Assert.Equal(64, stored.KeyHash.Length);
        Assert.DoesNotContain(created.Key, JsonSerializer.Serialize(stored));
        Assert.Equal(_mathias, stored.OwnerSub);
        Assert.Equal(_clock.GetUtcNow().AddDays(90), stored.ExpiresAtUtc);
        var list = await _keys.List(owner, default);
        var visible = JsonSerializer.Serialize(list);
        Assert.DoesNotContain(created.Key, visible);
        Assert.DoesNotContain(stored.KeyHash, visible);
        Assert.Equal("Min Chrome", Assert.Single(list).Label);
    }

    [Fact]
    public async Task KnownAuthSubjectSupportsItsEmailAliasWithoutGrantingOtherAccounts()
    {
        _options.AllowedSubjects = _mathias.ToString();
        _options.AllowedEmails = "kasc@mercantec.dk";
        var alias = Teacher(_mathias, "mathiasgsteenberg@gmail.com");
        Assert.True(_keys.CanManage(alias));
        Assert.False(_keys.CanManage(Teacher(Guid.NewGuid(), "mathiasgsteenberg@gmail.com")));
        Assert.False(_keys.CanManage(Teacher(_mathias, "")));
        var key = (await _keys.Create(alias, "Chrome", default))!;
        Assert.NotNull(await _keys.Authenticate(key.Key, default));
        _options.AllowedSubjects = "";
        Assert.Null(await _keys.Authenticate(key.Key, default));
    }

    [Fact]
    public async Task RevocationExpiryAndRemovalFromTheAllowlistTakeEffectOnTheNextRequest()
    {
        var owner = Teacher(_mathias, "mags@mercantec.dk");
        var created = (await _keys.Create(owner, "Chrome", default))!;
        Assert.NotNull(await _keys.Authenticate(created.Key, default));
        Assert.NotNull((await _db.ExtensionUploadKeys.SingleAsync()).LastUsedAtUtc);
        _options.AllowedEmails = "kasc@mercantec.dk";
        Assert.Null(await _keys.Authenticate(created.Key, default));
        _options.AllowedEmails = "mags@mercantec.dk;kasc@mercantec.dk";
        _clock.Advance(TimeSpan.FromDays(90));
        Assert.Null(await _keys.Authenticate(created.Key, default));
        var replacement = (await _keys.Create(owner, "Ny Chrome", default))!;
        Assert.NotNull(await _keys.Authenticate(replacement.Key, default));
        Assert.True(await _keys.Revoke(owner, replacement.Id, default));
        Assert.Null(await _keys.Authenticate(replacement.Key, default));
    }

    [Fact]
    public async Task AUserCannotReadOrRevokeTheOtherUsersKeysAndActiveKeysAreBounded()
    {
        var owner = Teacher(_mathias, "mags@mercantec.dk");
        var other = Teacher(_kasper, "kasc@mercantec.dk");
        var first = (await _keys.Create(owner, "Chrome", default))!;
        Assert.Empty(await _keys.List(other, default));
        Assert.False(await _keys.Revoke(other, first.Id, default));
        for (var i = 1; i < ExtensionUploadCredentials.MaxActiveKeys; i++) Assert.NotNull(await _keys.Create(owner, "Chrome", default));
        Assert.Null(await _keys.Create(owner, "For mange", default));
        await _keys.Revoke(owner, first.Id, default);
        Assert.NotNull(await _keys.Create(owner, "Erstatning", default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lup_bad")]
    [InlineData("eyJhbGciOiJSUzI1NiJ9.fake.jwt")]
    [InlineData("lup_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task BadAndUnknownCredentialsNeverAuthenticate(string? raw) => Assert.Null(await _keys.Authenticate(raw, default));

    [Fact]
    public async Task ScopedCredentialAuthenticatesOnlyUnderTheExplicitExtensionScheme()
    {
        var created = (await _keys.Create(Teacher(_mathias, "mags@mercantec.dk"), "Chrome", default))!;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_keys);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(_ => { })
            .AddScheme<AuthenticationSchemeOptions, ExtensionUploadAuthenticationHandler>(ExtensionUploadCredentials.Scheme, _ => { });
        await using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Headers.Authorization = "Bearer " + created.Key;
        var auth = provider.GetRequiredService<IAuthenticationService>();
        var scoped = await auth.AuthenticateAsync(http, ExtensionUploadCredentials.Scheme);
        Assert.True(scoped.Succeeded);
        Assert.Equal(_mathias, UserClaims.GetSub(scoped.Principal!));
        Assert.True(scoped.Principal!.HasClaim(ExtensionUploadCredentials.ScopeClaim, "emoji:create"));
        Assert.False(_keys.CanManage(scoped.Principal));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _keys.Create(scoped.Principal, "Escalation", default));
        Assert.False((await auth.AuthenticateAsync(http, JwtBearerDefaults.AuthenticationScheme)).Succeeded);
    }

    [Fact]
    public async Task UploadUsesTheRealOwnerAndRetainsExistingImageValidation()
    {
        var raw = (await _keys.Create(Teacher(_mathias, "mags@mercantec.dk"), "Chrome", default))!;
        var key = (await _keys.Authenticate(raw.Key, default))!;
        var principal = Teacher(key.OwnerSub, key.OwnerEmail);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new(ExtensionUploadCredentials.KeyIdClaim, key.Id.ToString()));
        ((ClaimsIdentity)principal.Identity!).AddClaim(new(ExtensionUploadCredentials.ScopeClaim, "emoji:create"));
        var controller = Controller(principal);
        var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1 };
        var file = new FormFile(new MemoryStream(png), 0, png.Length, "file", "billede.png") { Headers = new HeaderDictionary() };
        var result = await controller.Upload(file, "billede", default, "Beskrivelse", "custom", "[]");
        Assert.IsType<OkObjectResult>(result);
        var row = await _db.CustomEmojis.SingleAsync();
        Assert.Equal(_mathias, row.CreatedBySub);
        Assert.Equal("Beskrivelse", row.AltText);
        Assert.Equal(png, Assert.Single(_storage.Bytes));
        Assert.IsType<BadRequestObjectResult>(await controller.Upload(null, "nyt", default));
        Assert.IsType<ForbidResult>(await Controller(Teacher(_mathias, "mags@mercantec.dk")).Upload(file, "nyt", default));
    }

    [Fact]
    public void PerCredentialBudgetDoesNotAffectAnotherUserAndResets()
    {
        var limit = new ExtensionUploadRateLimit(_clock);
        var first = Guid.NewGuid();
        for (var i = 0; i < 30; i++) Assert.True(limit.TryAcquire(first));
        Assert.False(limit.TryAcquire(first));
        Assert.True(limit.TryAcquire(Guid.NewGuid()));
        _clock.Advance(TimeSpan.FromSeconds(61));
        Assert.True(limit.TryAcquire(first));
    }

    private ExtensionAccessController Controller(ClaimsPrincipal principal) => new(_keys, _db, _storage,
        new EphemeralDataProtectionProvider(), new ExtensionUploadRateLimit(_clock))
    { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } } };

    private static ClaimsPrincipal Teacher(Guid sub, string email, bool teacher = true) => new(new ClaimsIdentity([
        new("sub", sub.ToString()), new("email", email), new("role", teacher ? "Teacher" : "Student")
    ], "Bearer", "sub", "role"));

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class FakeStorage : IObjectStorage
    {
        public List<byte[]> Bytes { get; } = [];
        public bool IsConfigured => true;
        public string Bucket => "test";
        public async Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
        { using var data = new MemoryStream(); await content.CopyToAsync(data, cancellationToken); Bytes.Add(data.ToArray()); return new(key, Bucket, contentType, data.Length); }
        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<ObjectGetResult?>(null);
        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult<Uri?>(null);
    }
}
