// Explicit loopback-only fixture. This executable is never referenced by the production app or Docker image.
using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Drawings;
using Mercantec.Tech.Api.Emojis;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

if (Environment.GetEnvironmentVariable("NOTEBOOK_BROWSER_QA") != "1")
    throw new InvalidOperationException("Run the explicit loopback notebook QA launcher; this fixture cannot host production traffic.");

SQLitePCL.Batteries_V2.Init();
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:8088");
builder.Configuration["Notion:ServiceSecret"] = QaIdentities.ServiceKey;
builder.Configuration["Notebook:CollaborationBaseUrl"] = "http://127.0.0.1:9099";
var connectionString = $"Data Source=notebook-browser-qa-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
await using var keepAlive = new SqliteConnection(connectionString);
await keepAlive.OpenAsync();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<NotebookAccess>();
builder.Services.AddScoped<CourseAccess>();
builder.Services.AddScoped<DrawingAccess>();
builder.Services.AddScoped<DrawingService>();
builder.Services.AddScoped<FileAuthorization>();
builder.Services.AddSingleton<NotebookServiceKey>();
builder.Services.AddHttpClient<NotebookEngineClient>(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<IObjectStorage, QaMemoryStorage>();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddAuthentication("NotebookBrowserQa")
    .AddScheme<AuthenticationSchemeOptions, QaAuthenticationHandler>("NotebookBrowserQa", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddControllers().AddApplicationPart(typeof(NotebooksController).Assembly);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote))
    { context.Response.StatusCode = 403; return; }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/_qa/health", () => Results.Json(new { status = "ok", fixture = "fictional-notebook-browser-qa" }));
app.MapGet("/_qa/identities", () => Results.Json(QaIdentities.All.Values.Select(identity => new
{
    identity.Key, identity.Name, identity.Sub, identity.Token,
    expiresAt = QaIdentities.ExpiresAt.ToUnixTimeSeconds(),
    home = identity.Key == "teacher" ? "/teacher/notebooks" : "/notebook"
})));
app.MapControllers();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await NotebookDefaults.EnsureAsync(db);
    var now = DateTimeOffset.UtcNow;
    foreach (var identity in QaIdentities.All.Values)
        db.NotebookProfiles.Add(new NotebookProfile { Sub = identity.Sub, DisplayName = identity.Name,
            IsTeacher = identity.Key == "teacher", UpdatedAtUtc = now });
    var course = new Forloeb { Id = new("11111111-aaaa-4111-8111-111111111111"), Name = "QA Fiktivt programmeringsforløb",
        CreatedBySub = QaIdentities.All["teacher"].Sub, CreatedAtUtc = now };
    var hold = new Hold { Id = new("22222222-aaaa-4222-8222-222222222222"), Forloeb = course,
        Name = "QA Fiktivt hold", CreatedAtUtc = now };
    db.Hold.Add(hold);
    foreach (var key in new[] { "pupil", "peer" })
        db.Memberships.Add(new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = QaIdentities.All[key].Sub,
            DisplayName = QaIdentities.All[key].Name, CreatedAtUtc = now });
    // Use the real embedded standard graphics and real emoji/content controllers; no AI provider is registered.
    foreach (var key in typeof(StandardEmojiAssets).Assembly.GetManifestResourceNames().Where(StandardEmojiAssets.IsStandard).Order())
    {
        await using var image = StandardEmojiAssets.Open(key)!;
        var metadata = StandardEmojiMetadata.Find(key);
        db.CustomEmojis.Add(new CustomEmoji { Id = Guid.NewGuid(), Name = "qa-" + Path.GetFileNameWithoutExtension(key), Key = key,
            ContentType = "image/png", SizeBytes = image.Length, AltText = "QA standardemoji " + (metadata?.Keywords.FirstOrDefault() ?? "emoji"),
            Category = metadata?.Category ?? "custom", KeywordsJson = JsonSerializer.Serialize(metadata?.Keywords ?? []),
            CreatedBySub = QaIdentities.All["teacher"].Sub, CreatedAtUtc = now, UpdatedAtUtc = now });
    }
    await using var customSource = StandardEmojiAssets.Open("standard-emojis/1f680.png")!;
    using var customImage = new MemoryStream();
    await customSource.CopyToAsync(customImage);
    var customBytes = customImage.ToArray();
    const string customKey = "qa-emojis/custom-sticker.png";
    await scope.ServiceProvider.GetRequiredService<IObjectStorage>().PutAsync(customKey, new MemoryStream(customBytes), "image/png");
    db.CustomEmojis.Add(new CustomEmoji { Id = Guid.NewGuid(), Name = "qa-custom-sticker", Key = customKey,
        ContentType = "image/png", SizeBytes = customBytes.Length, AltText = "Raket som QA-klistermærke", Category = "custom", KeywordsJson = "[\"QA\",\"sticker\",\"raket\"]",
        CreatedBySub = QaIdentities.All["teacher"].Sub, CreatedAtUtc = now, UpdatedAtUtc = now });
    await db.SaveChangesAsync();
}
await app.RunAsync();

internal sealed record QaIdentity(string Key, Guid Sub, string Name, string Token);

internal static class QaIdentities
{
    internal const string ServiceKey = "qa-notebooks-only-service-key-32-characters";
    internal static readonly DateTimeOffset ExpiresAt = DateTimeOffset.UtcNow.AddHours(8);
    internal static readonly IReadOnlyDictionary<string, QaIdentity> All = new[]
    {
        Create("pupil", "aaaaaaaa-1111-4111-8111-111111111111", "QA Elev Anna", "Elev"),
        Create("peer", "bbbbbbbb-2222-4222-8222-222222222222", "QA Elev Emil", "Elev"),
        Create("outsider", "dddddddd-4444-4444-8444-444444444444", "QA Elev uden hold", "Elev"),
        Create("teacher", "cccccccc-3333-4333-8333-333333333333", "QA Underviser", "Underviser")
    }.ToDictionary(identity => identity.Key);

    private static QaIdentity Create(string key, string sub, string name, string role)
    {
        static string Encode(object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = Encode(new { alg = "HS256", typ = "JWT" }) + "." + Encode(new
        { sub, name, role, exp = ExpiresAt.ToUnixTimeSeconds(), iss = "notebook-loopback-qa-only", aud = "fictional-fixture" })
            + ".cWEtZml4dHVyZS1vbmx5LW5vdC1hLXJlYWwtc2lnbmF0dXJl";
        return new(key, Guid.Parse(sub), name, token);
    }
}

internal sealed class QaAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        var identity = QaIdentities.All.Values.SingleOrDefault(item => header == "Bearer " + item.Token);
        if (identity is null || DateTimeOffset.UtcNow >= QaIdentities.ExpiresAt)
            return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new[] { new Claim("sub", identity.Sub.ToString()), new Claim("name", identity.Name),
            new Claim("role", identity.Key == "teacher" ? "Underviser" : "Elev") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}

internal sealed class QaMemoryStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string Type)> files = new();
    public bool IsConfigured => true;
    public bool SupportsRanges => true;
    public string Bucket => "fictional-notebook-qa-memory";
    public async Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, cancellationToken);
        files[key] = (memory.ToArray(), contentType);
        return new(key, Bucket, contentType, memory.Length);
    }
    public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(files.TryGetValue(key, out var file)
            ? new ObjectGetResult(new MemoryStream(file.Bytes, writable: false), file.Type, file.Bytes.Length) : null);
    public Task<ObjectGetResult?> GetRangeAsync(string key, long start, long end, CancellationToken cancellationToken = default) =>
        Task.FromResult(files.TryGetValue(key, out var file)
            ? new ObjectGetResult(new MemoryStream(file.Bytes[(int)start..((int)end + 1)], writable: false), file.Type, end - start + 1) : null);
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { files.TryRemove(key, out _); return Task.CompletedTask; }
    public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult<Uri?>(null);
}
