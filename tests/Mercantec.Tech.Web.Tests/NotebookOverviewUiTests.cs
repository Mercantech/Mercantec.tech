using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Notebooks;
using Mercantec.Tech.Web.Notion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Home = Mercantec.Tech.Web.Pages.Notion.Home;

namespace Mercantec.Tech.Web.Tests;

public sealed class NotebookOverviewUiTests
{
    [Fact]
    public async Task Overview_filters_collections_and_shares_without_loading_all_student_notebooks()
    {
        using var api = new OverviewApi();
        await using var context = await Create(api, teacher: true);
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Equal(4, page.FindAll(".books-card").Count));
        Assert.Equal(0, api.StudentRequests);

        Button(page, "Teori").Click();
        Assert.Single(page.FindAll(".books-card"));
        Assert.Contains("Fælles SQL-teori", page.Find(".books-card").TextContent);
        page.Find(".books-filters input").Input("findes ikke");
        Assert.Empty(page.FindAll(".books-card"));
        page.Find(".books-filters input").Input("");

        Button(page, "Mine og delte").Click();
        Assert.Equal(2, page.FindAll(".books-card").Count);
        var shared = page.FindAll(".books-card").Single(card => card.TextContent.Contains("Delt med mig", StringComparison.Ordinal));
        Assert.Equal("/notebook/" + api.Shared.Id, shared.QuerySelector("h2 a")!.GetAttribute("href"));
        Assert.Single(shared.QuerySelectorAll("nav[aria-label='Delte sider'] a"));

        Button(page, "Elevnotesbøger").Click();
        page.WaitForAssertion(() => Assert.Equal(1, api.StudentRequests));
        page.WaitForAssertion(() => Assert.Contains("Elev Annas notesbog", page.Find(".books-card").TextContent));
        Assert.Empty(api.Unexpected);
    }

    [Fact]
    public async Task Teacher_creates_a_theory_notebook_without_a_hold_and_opens_its_stable_root()
    {
        using var api = new OverviewApi();
        await using var context = await Create(api, teacher: true);
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Equal(4, page.FindAll(".books-card").Count));
        Button(page, "Ny notesbog").Click();
        page.Find(".books-create input").Input("Ny fælles teori");
        page.Find(".books-create select").Change("");
        Button(page, "Opret").Click();
        page.WaitForAssertion(() => Assert.NotNull(api.Created));
        Assert.Null(api.Created!.HoldId);
        Assert.Equal("Ny fælles teori", api.Created.Title);
        Assert.EndsWith("/notion/collections/" + api.NewRoot, context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        Assert.Empty(api.Unexpected);
    }

    [Fact]
    public async Task Cards_show_editor_information_and_sort_by_title_or_most_recent_edit()
    {
        using var api = new OverviewApi();
        await using var context = await Create(api, teacher: true);
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Equal(4, page.FindAll(".books-card").Count));

        Assert.Contains("H1 undervisning", page.Find(".books-card h2").TextContent);
        Assert.Contains("Redigeret af Berit", page.Find(".books-card").TextContent);
        Assert.Single(page.FindAll(".books-card[data-kind=hold] .books-kind"));
        Assert.Equal("Forløb", page.Find(".books-card[data-kind=hold] .books-kind").TextContent);
        Assert.NotNull(page.Find(".books-card time").GetAttribute("datetime"));

        page.FindAll(".books-filters select").Last().Change("asc");
        Assert.Contains("Fælles SQL-teori", page.Find(".books-card h2").TextContent);
        page.FindAll(".books-filters select").Last().Change("desc");
        Assert.Contains("Min notesbog", page.Find(".books-card h2").TextContent);
    }

    [Fact]
    public async Task A_pupil_without_holds_can_find_published_theory_and_has_no_author_controls()
    {
        using var api = new OverviewApi { NoHolds = true };
        await using var context = await Create(api, teacher: false);
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Fælles SQL-teori", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), button => button.TextContent.Trim() is "Ny notesbog" or "Elevnotesbøger");
        Assert.Equal(0, api.StudentRequests);
        Assert.Empty(api.Unexpected);
    }

    private static Task<LearningUiTestContext> Create(OverviewApi api, bool teacher) => LearningUiTestContext.CreateAsync(api, teacher: teacher, configure: services =>
    { services.AddSingleton<NotionClient>(); services.AddSingleton<NotebookClient>(); });
    private static AngleSharp.Dom.IElement Button(IRenderedComponent<Home> page, string text) => page.FindAll("button").Single(button => button.TextContent.Trim() == text);

    private sealed class OverviewApi : HttpMessageHandler
    {
        public bool NoHolds;
        private readonly Guid hold = Guid.NewGuid(), own = Guid.NewGuid();
        public Guid NewRoot { get; } = Guid.NewGuid();
        private static readonly DateTimeOffset Updated = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        public NotebookPageSummary Shared { get; } = new(Guid.NewGuid(), Guid.NewGuid(), "Kun den delte side", null, "note", [], null, null, null, Updated.AddDays(-1), 1);
        public CreateNotionCollection? Created;
        public int StudentRequests;
        public List<string> Unexpected { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/notion/collections" && request.Method == HttpMethod.Post)
            {
                Created = await request.Content!.ReadFromJsonAsync<CreateNotionCollection>(cancellationToken);
                return Json(new NotionCollectionView(NewRoot, NewRoot, Created!.Title, Created.HoldId, null, true, DateTimeOffset.UtcNow));
            }
            if (path == "/api/notebooks/teacher") { StudentRequests++; return Json(new[] { new TeacherNotebookView(Guid.NewGuid(), Guid.NewGuid(), "Elev Anna", ["H1"], null, DateTimeOffset.UtcNow) }); }
            return path switch
            {
                "/api/notion/collections" => Json(new[] { new NotionCollectionView(Guid.NewGuid(), Guid.NewGuid(), "Fælles SQL-teori", null, null, false, Updated.AddDays(-2)) }.Concat(NoHolds ? [] : new[] { new NotionCollectionView(Guid.NewGuid(), Guid.NewGuid(), "H1 undervisning", hold, "H1", true, Updated, LastEditedByName: "Berit") }).ToArray()),
                "/api/notion/holds" => Json(NoHolds ? Array.Empty<NotionHold>() : new[] { new NotionHold(hold, "H1", true) }),
                "/api/notebooks/mine" => Json(new NotebookInfo(own, LearningUiTestContext.AccountId, "Mit navn", true, true)),
                "/api/notebooks/shared" => Json(new[] { Shared }),
                var value when value == $"/api/notebooks/{own}/pages" => Json(new NotebookPageList([], 0)),
                _ => Missing(path)
            };
        }
        private HttpResponseMessage Missing(string path) { Unexpected.Add(path); return new(HttpStatusCode.NotFound); }
        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
