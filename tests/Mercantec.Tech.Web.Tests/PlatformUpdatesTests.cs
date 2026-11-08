using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Updates;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using UpdatesPage = Mercantec.Tech.Web.Pages.Teacher.Updates;

namespace Mercantec.Tech.Web.Tests;

public sealed class PlatformUpdatesTests
{
    private static Task<LearningUiTestContext> Context(UpdatesApi api, bool teacher = true, bool preview = false) =>
        LearningUiTestContext.CreateAsync(api, teacher, preview,
            services => services.AddSingleton<PlatformUpdatesClient>());

    [Fact]
    public async Task Teacher_reads_newest_updates_first_with_authenticated_before_and_after_images()
    {
        using var api = new UpdatesApi();
        await using var context = await Context(api);
        var page = context.Render<UpdatesPage>();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("article.update-entry").Count));
        var entries = page.FindAll("article.update-entry");
        Assert.Equal(api.Newer.Title, entries[0].QuerySelector("h2")!.TextContent);
        Assert.Equal(api.Older.Title, entries[1].QuerySelector("h2")!.TextContent);
        Assert.Equal("2026-11-08", entries[0].QuerySelector("time")!.GetAttribute("datetime"));
        Assert.Contains(api.Newer.Description, entries[0].TextContent);
        Assert.Equal(new[] { "Før", "Efter" }, entries[0].QuerySelectorAll("figcaption").Select(e => e.TextContent));
        Assert.Equal("Nærbillede", entries[1].QuerySelector("figcaption")!.TextContent);
        Assert.Equal(3, page.FindAll("article img").Count);
        Assert.All(page.FindAll("article img"), image =>
        {
            Assert.StartsWith("data:image/webp;base64,", image.GetAttribute("src"));
            Assert.False(string.IsNullOrWhiteSpace(image.GetAttribute("alt")));
        });
        Assert.Contains(page.FindAll("article img"), image => image.GetAttribute("alt") == api.Newer.Before!.Alt);
        Assert.Contains(page.FindAll("article img"), image => image.GetAttribute("alt") == api.Newer.After.Alt);
        Assert.Equal(1, api.ListRequests);
        Assert.Equal(3, api.Requests.Count(p => p.StartsWith("platform-updates/images/", StringComparison.Ordinal)));
        Assert.Empty(api.Unexpected);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Student_and_teacher_in_student_preview_do_not_fetch_or_reveal_teacher_updates(bool teacher, bool preview)
    {
        using var api = new UpdatesApi();
        await using var context = await Context(api, teacher, preview);
        var page = context.Render<UpdatesPage>();

        Assert.Contains("Denne side er kun for undervisere.", page.Markup);
        Assert.Empty(page.FindAll("article, img"));
        Assert.DoesNotContain(api.Newer.Title, page.Markup);
        Assert.DoesNotContain(api.Newer.Description, page.Markup);
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task Entering_student_preview_clears_loaded_updates_and_leaving_fetches_a_fresh_feed()
    {
        using var api = new UpdatesApi();
        await using var context = await Context(api);
        var page = context.Render<UpdatesPage>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("article img").Count));

        await page.InvokeAsync(() => context.ViewAs.Enter("preview-student", "Elev", "H2", Guid.NewGuid()));

        page.WaitForAssertion(() => Assert.Contains("Denne side er kun for undervisere.", page.Markup));
        Assert.Empty(page.FindAll("article, img, dialog"));
        Assert.DoesNotContain(api.Newer.Title, page.Markup);
        Assert.DoesNotContain("data:image", page.Markup);
        Assert.Equal(1, api.ListRequests);

        await page.InvokeAsync(context.ViewAs.Exit);

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("article.update-entry").Count));
        Assert.Equal(2, api.ListRequests);
    }

    [Fact]
    public async Task Logging_out_removes_loaded_titles_images_and_the_open_image_viewer()
    {
        using var api = new UpdatesApi();
        await using var context = await Context(api);
        var page = context.Render<UpdatesPage>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("article img").Count));
        page.Find(".update-image-button").Click();
        Assert.Single(page.FindAll("dialog"));

        await page.InvokeAsync(context.Auth.LogoutLocalAsync);

        page.WaitForAssertion(() => Assert.Contains("Log ind som underviser", page.Markup));
        Assert.Empty(page.FindAll("article, img, dialog"));
        Assert.DoesNotContain(api.Newer.Title, page.Markup);
        Assert.DoesNotContain("data:image", page.Markup);
        Assert.Equal(1, api.ListRequests);
    }

    [Fact]
    public async Task Logout_cancels_pending_image_reads_and_late_responses_cannot_reveal_the_feed()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var api = new UpdatesApi { ImageGate = gate };
        await using var context = await Context(api);
        var page = context.Render<UpdatesPage>();
        page.WaitForAssertion(() => Assert.Equal(3, api.ImageRequestsStarted));
        Assert.Contains("Henter opdateringer og billeder", page.Markup);
        Assert.Empty(page.FindAll("article, img"));

        await page.InvokeAsync(context.Auth.LogoutLocalAsync);

        page.WaitForAssertion(() => Assert.Equal(3, api.ImageRequestsCancelled));
        gate.TrySetResult(true);
        await api.ImageResponsesReleased.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await page.InvokeAsync(async () => await Task.Yield());
        page.WaitForAssertion(() => Assert.Contains("Log ind som underviser", page.Markup));
        Assert.Empty(page.FindAll("article, img"));
        Assert.DoesNotContain(api.Newer.Title, page.Markup);
        Assert.DoesNotContain("data:image", page.Markup);
        Assert.Equal(1, api.ListRequests);
    }

    [Fact]
    public async Task Failed_image_keeps_the_update_readable_and_retry_only_fetches_that_image()
    {
        using var api = new UpdatesApi { FailImage = "editor-after" };
        await using var context = await Context(api);
        var page = context.Render<UpdatesPage>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".update-image-error")));
        Assert.Equal(2, page.FindAll("article.update-entry").Count);
        Assert.Contains(api.Newer.Title, page.Markup);
        Assert.Contains(api.Newer.Description, page.Markup);
        Assert.Equal(2, page.FindAll("article img").Count);
        Assert.Equal(1, api.ListRequests);

        api.FailImage = null;
        await page.Find(".update-image-error button").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("article img").Count));
        Assert.Empty(page.FindAll(".update-image-error"));
        Assert.Equal(1, api.ListRequests);
        Assert.Equal(2, api.Requests.Count(p => p == "platform-updates/images/editor-after"));
        Assert.Equal(1, api.Requests.Count(p => p == "platform-updates/images/editor-before"));
        Assert.Equal(1, api.Requests.Count(p => p == "platform-updates/images/calendar-after"));
        Assert.Empty(api.Unexpected);
    }

    [Fact]
    public async Task Failed_feed_shows_a_retry_instead_of_claiming_there_are_no_updates()
    {
        using var api = new UpdatesApi { FailList = true };
        await using var context = await Context(api);
        var page = context.Render<UpdatesPage>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("article, img"));
        Assert.DoesNotContain("Her kommer de næste opdateringer", page.Markup);

        api.FailList = false;
        await page.Find("[role=alert] button").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("article.update-entry").Count));
        Assert.Empty(page.FindAll("[role=alert]"));
        Assert.Equal(2, api.ListRequests);
        Assert.Empty(api.Unexpected);
    }

    private sealed class UpdatesApi : HttpMessageHandler
    {
        public PlatformUpdate Older { get; } = new("calendar", new(2026, 11, 7), "En tydeligere holdkalender",
            "Holdets aftaler vises i en samlet uge.", new("calendar-after", "Holdkalender med ugens aftaler"));
        public PlatformUpdate Newer { get; } = new("editor", new(2026, 11, 8), "Fast elevudgivelse i editoren",
            "Elever læser udgivelsen, mens underviseren arbejder videre i kladden.",
            new("editor-after", "Den udgivne elevside"), new("editor-before", "Editoren før udgivelsesknappen"));
        public ConcurrentQueue<string> Requests { get; } = [];
        public ConcurrentQueue<string> Unexpected { get; } = [];
        public bool FailList { get; set; }
        public string? FailImage { get; set; }
        public TaskCompletionSource<bool>? ImageGate { get; init; }
        public TaskCompletionSource<bool> ImageResponsesReleased { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _imageRequestsStarted, _imageRequestsCancelled, _imageResponsesReleased;
        public int ImageRequestsStarted => Volatile.Read(ref _imageRequestsStarted);
        public int ImageRequestsCancelled => Volatile.Read(ref _imageRequestsCancelled);
        public int ListRequests => Requests.Count(p => p == "platform-updates");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery["/api/".Length..];
            Requests.Enqueue(path);
            if (request.Method != HttpMethod.Get)
            {
                Unexpected.Enqueue($"{request.Method} {path}");
                return new(HttpStatusCode.MethodNotAllowed);
            }
            if (path == "platform-updates")
                return FailList ? new(HttpStatusCode.ServiceUnavailable)
                    : new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Older, Newer }) };
            if (path.StartsWith("platform-updates/images/", StringComparison.Ordinal))
            {
                var id = path["platform-updates/images/".Length..];
                if (id is not ("calendar-after" or "editor-after" or "editor-before"))
                {
                    Unexpected.Enqueue(path);
                    return new(HttpStatusCode.NotFound);
                }
                Interlocked.Increment(ref _imageRequestsStarted);
                if (ImageGate is { } gate)
                {
                    using var cancellation = ct.Register(() => Interlocked.Increment(ref _imageRequestsCancelled));
                    // A deliberately uncooperative transport completes after cancellation to exercise stale-result handling.
                    await gate.Task;
                    if (Interlocked.Increment(ref _imageResponsesReleased) == 3) ImageResponsesReleased.TrySetResult(true);
                }
                if (FailImage == id) return new(HttpStatusCode.ServiceUnavailable);
                var content = new ByteArrayContent(Encoding.UTF8.GetBytes($"RIFF-test-{id}"));
                content.Headers.ContentType = new MediaTypeHeaderValue("image/webp");
                return new(HttpStatusCode.OK) { Content = content };
            }
            Unexpected.Enqueue(path);
            return new(HttpStatusCode.NotFound);
        }
    }
}
