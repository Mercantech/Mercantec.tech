using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Mercantec.Tech.Web;
using Mercantec.Tech.Web.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));

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
