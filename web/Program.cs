using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Mercantec.Tech.Web;
using Mercantec.Tech.Web.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<RoleOptions>(builder.Configuration.GetSection(RoleOptions.SectionName));
builder.Services.AddScoped<RoleResolver>();
builder.Services.AddScoped<AuthRedirect>();
builder.Services.AddScoped<ViewAsStudentContext>();

var apiBase = builder.Configuration["ApiBaseUrl"]?.Trim() ?? "/api/";
Uri apiBaseUri;
if (Uri.TryCreate(apiBase, UriKind.Absolute, out var absolute)
    && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
{
    apiBaseUri = absolute;
}
else
{
    var relative = apiBase.TrimStart('/');
    if (!relative.EndsWith('/'))
        relative += "/";
    apiBaseUri = new Uri(new Uri(builder.HostEnvironment.BaseAddress), relative);
}

builder.Services.AddScoped<Mercantec.Tech.Web.Demo.LearningContext>();
builder.Services.AddScoped<Mercantec.Tech.Web.Avatars.IAvatarClient, Mercantec.Tech.Web.Avatars.AvatarClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Demo.DemoWorld>();
builder.Services.AddScoped<Mercantec.Tech.Web.Courses.CourseTheoryDocumentLoader>();

builder.Services.AddScoped<Mercantec.Tech.Web.Activity.ActionCenterChanges>();
builder.Services.AddScoped<Mercantec.Tech.Web.Activity.ActionCenterService>();
builder.Services.AddScoped<Mercantec.Tech.Web.Gifts.GiftMailboxClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Games.UploadedGamesClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Polls.PollClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Electives.ElectiveClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Learning.LearningClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Learning.TeachingCoursesClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Learning.GroupSetsClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Learning.RouletteSpeechClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Notion.NotionClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Notion.NotionMcpClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Notebooks.NotebookClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Emojis.EmojiClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Extensions.ExtensionAccessClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Calendar.TeacherCalendarClient>();
builder.Services.AddScoped<Mercantec.Tech.Web.Updates.PlatformUpdatesClient>();

builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthStateProvider>();
builder.Services.AddAuthorizationCore();

builder.Services.AddTransient<AuthDelegatingHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthDelegatingHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = apiBaseUri };
});

await builder.Build().RunAsync();
