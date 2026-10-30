using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Mercantec.Tech.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = builder.Configuration["ApiBaseUrl"] ?? "api/";
var baseUri = Uri.TryCreate(apiBase, UriKind.Absolute, out var absolute)
    ? absolute
    : new Uri(new Uri(builder.HostEnvironment.BaseAddress), apiBase);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = baseUri });

await builder.Build().RunAsync();
