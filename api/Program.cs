using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Integrations.Azure;
using Mercantec.Tech.Api.Integrations.Lectio;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Api.Services;
using Mercantec.Tech.Api.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

if (builder.Configuration.GetValue<bool>("PreviewConverter:Only"))
{
    builder.Services.AddHostedService<PreviewConverter>();
    var converter = builder.Build();
    await converter.RunAsync();
    return;
}

builder.Services.AddControllers();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    foreach (var address in (builder.Configuration["ReverseProxy:KnownProxies"] ?? "")
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        options.KnownProxies.Add(System.Net.IPAddress.Parse(address));
});

builder.Services.AddDataProtection();
builder.Services.AddScoped<Mercantec.Tech.Api.Learning.CourseAccess>();
builder.Services.AddScoped<Mercantec.Tech.Api.Learning.FileAuthorization>();
builder.Services.Configure<Mercantec.Tech.Api.Speech.RouletteSpeechOptions>(
    builder.Configuration.GetSection(Mercantec.Tech.Api.Speech.RouletteSpeechOptions.SectionName));
builder.Services.AddHttpClient("rost", client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });
builder.Services.AddSingleton<Mercantec.Tech.Api.Speech.IRouletteSpeechGenerator, Mercantec.Tech.Api.Speech.RostSpeechGenerator>();
builder.Services.AddSingleton<Mercantec.Tech.Api.Speech.RouletteSpeechService>();
builder.Services.AddHostedService<NotionUploadCleanup>();
if (builder.Configuration.GetValue<bool>("PreviewWorker:Enabled"))
    builder.Services.AddHostedService<PreviewWorker>();

builder.Services.Configure<LectioOptions>(builder.Configuration.GetSection(LectioOptions.SectionName));
builder.Services.AddHttpClient<LectioCollectorClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LectioOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(opts.BaseUrl)
        ? "https://lc.mags.dk/api/"
        : opts.BaseUrl.TrimEnd('/') + "/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<ILectioMembershipSync>(sp => sp.GetRequiredService<LectioCollectorClient>());
builder.Services.AddHttpClient("auth-admin", client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.Configure<AzureGraphOptions>(builder.Configuration.GetSection(AzureGraphOptions.SectionName));
builder.Services.AddHttpClient("azure-graph", client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddSingleton<AzureGraphCalendarClient>();

builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection(OpenAiOptions.SectionName));
builder.Services.AddHttpClient("openai", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(opts.BaseUrl)
        ? "https://api.openai.com/v1/"
        : opts.BaseUrl.TrimEnd('/') + "/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(90);
});
builder.Services.AddSingleton<OpenAiEditorClient>();
builder.Services.AddScoped<MembershipClaimService>();

builder.Services.Configure<ObjectStorageOptions>(builder.Configuration.GetSection(ObjectStorageOptions.SectionName));
builder.Services.AddSingleton<IObjectStorage>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
    return opts.IsConfigured ? new S3ObjectStorage(sp.GetRequiredService<IOptions<ObjectStorageOptions>>()) : new DisabledObjectStorage();
});

var connectionString =
    builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["CONNECTION_STRING"]
    ?? "";

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Missing ConnectionStrings:Default or CONNECTION_STRING (use Host=mercantec-tech-db in Docker).");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Azure Graph: understøt både Azure__* (compose) og flade AZURE_* env-navne
var azure = builder.Configuration.GetSection(AzureGraphOptions.SectionName);
if (string.IsNullOrWhiteSpace(azure["TenantId"]))
    builder.Configuration["Azure:TenantId"] = builder.Configuration["AZURE_TENANT_ID"];
if (string.IsNullOrWhiteSpace(azure["ClientId"]))
    builder.Configuration["Azure:ClientId"] = builder.Configuration["AZURE_CLIENT_ID"];
if (string.IsNullOrWhiteSpace(azure["ClientSecret"]))
    builder.Configuration["Azure:ClientSecret"] = builder.Configuration["AZURE_CLIENT_SECRET"];
if (string.IsNullOrWhiteSpace(azure["TeacherUpnMap"]) && !string.IsNullOrWhiteSpace(builder.Configuration["AZURE_TEACHER_UPN_MAP"]))
    builder.Configuration["Azure:TeacherUpnMap"] = builder.Configuration["AZURE_TEACHER_UPN_MAP"];

// OpenAI: understøt både OpenAI__* (compose) og flade OPENAI_* env-navne
var openAi = builder.Configuration.GetSection(OpenAiOptions.SectionName);
if (string.IsNullOrWhiteSpace(openAi["ApiKey"]))
    builder.Configuration["OpenAI:ApiKey"] = builder.Configuration["OPENAI_API_KEY"];
if (string.IsNullOrWhiteSpace(openAi["Model"]) && !string.IsNullOrWhiteSpace(builder.Configuration["OPENAI_MODEL"]))
    builder.Configuration["OpenAI:Model"] = builder.Configuration["OPENAI_MODEL"];

var authIssuer = builder.Configuration["AUTH_ISSUER"]
    ?? "https://auth.mercantec.tech";
var authAudience = builder.Configuration["AUTH_AUDIENCE"]
    ?? "mercantec-apps";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authIssuer;
        options.Audience = authAudience;
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.MetadataAddress = $"{authIssuer.TrimEnd('/')}/.well-known/openid-configuration";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authIssuer,
            ValidateAudience = true,
            ValidAudience = authAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "sub",
            RoleClaimType = "role",
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("""{"error":"unauthorized"}""");
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("""{"error":"forbidden"}""");
            }
        };
    });

builder.Services.AddAuthorization();

var corsOrigins = builder.Configuration["Cors:SpaOrigins"]?
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Spa", policy =>
    {
        if (corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseForwardedHeaders();
app.UseCors("Spa");
app.UseAuthentication();
app.UseAuthorization();
if (!builder.Configuration.GetValue<bool>("PreviewWorker:Only"))
    app.MapControllers();

app.Run();
