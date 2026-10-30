using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Mercantec.Tech.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same-origin via nginx in Docker/prod: /api/
// Absolute URL (http/https) only when explicitly configured for local `dotnet run`.
var apiBase = builder.Configuration["ApiBaseUrl"]?.Trim() ?? "/api/";
Uri baseUri;
if (Uri.TryCreate(apiBase, UriKind.Absolute, out var absolute)
    && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
{
    baseUri = absolute;
}
else
{
    var relative = apiBase.TrimStart('/');
    if (!relative.EndsWith('/'))
        relative += "/";
    baseUri = new Uri(new Uri(builder.HostEnvironment.BaseAddress), relative);
}

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = baseUri });

await builder.Build().RunAsync();
