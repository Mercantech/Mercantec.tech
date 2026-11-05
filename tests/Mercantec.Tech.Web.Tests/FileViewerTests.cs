using System.Net;
using System.Net.Http.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Learning;
using Mercantec.Tech.Web.Pages.Learning;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class FileViewerTests
{
    [Fact]
    public async Task LargeZipKeepsOriginalDownloadAndWebLaunchWhenArchiveListingIsUnavailable()
    {
        var fileId = Guid.NewGuid();
        using var http = new HttpClient(new LargeZipHandler(fileId)) { BaseAddress = new Uri("https://app.example/api/") };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new LearningClient(http));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<FileViewer>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(FileViewer.FileId)] = fileId }));
            return rendered.ToHtmlString();
        });

        Assert.Contains("Hent original", html);
        Assert.Contains("Start webspil eller hjemmeside", html);
        Assert.Contains($"https://app.example/api/files/{fileId}/content?download=true", html);
        Assert.Contains("Forh&#xE5;ndsvisningen kunne ikke vises", html);
    }

    private sealed class LargeZipHandler(Guid id) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/preview", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new FilePreview(id, "unity-build.zip", "application/zip",
                        30 * 1024 * 1024, "archive", $"/api/files/{id}/content",
                        $"/api/files/{id}/content?download=true", null, DateTimeOffset.UtcNow.AddMinutes(5)))
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent.Create(new { error = "archive_preview_too_large" })
            });
        }
    }
}
