using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Web.Components;

namespace Mercantec.Tech.Web.Tests;

public sealed class CourseLectioImportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inline_flow_previews_roster_then_creates_or_opens_existing_hold(bool alreadyImported)
    {
        using var handler = new LectioApi(alreadyImported);
        await using var context = await LearningUiTestContext.CreateAsync(handler, teacher: true);
        var reloads = 0;
        var component = context.Render<CourseLectioImport>(parameters =>
            parameters.Add(import => import.OnImported, () => reloads++));

        Assert.Empty(handler.Requests);
        component.Find("button[aria-controls='lectio-import-panel']").Click();
        component.WaitForElement("button[aria-label='Vælg Programmering']");
        component.Find("button[aria-label='Vælg Programmering']").Click();
        component.WaitForAssertion(() => Assert.Contains("Ada Lovelace", component.Find(".course-lectio-students").TextContent));
        Assert.Equal("Programmering", component.Find(".course-lectio-form input").GetAttribute("value"));
        Assert.Null(handler.Import);

        component.Find(".course-lectio-form input").Input("Programmering H2");
        component.Find("form").Submit();
        component.WaitForAssertion(() =>
        {
            Assert.Equal(new ImportPayload("h2/pro", "Programmering H2", "Programmering H2"), handler.Import);
            Assert.Equal($"/courses/{handler.WorkspaceId:D}?view=overview", component.Find(".course-lectio-result a").GetAttribute("href"));
            Assert.Contains(alreadyImported ? "allerede oprettet" : "1 elev importeret", component.Find(".course-lectio-result").TextContent);
            Assert.Equal(1, reloads);
        });
        Assert.Equal(new[] { "GET /api/lectio/holds/tracked", "GET /api/lectio/holds/by-lectio/h2%2Fpro", "POST /api/forloeb/from-lectio" }, handler.Requests);
    }

    [Fact]
    public async Task Student_preview_cannot_open_import_from_query()
    {
        using var handler = new LectioApi(false);
        await using var context = await LearningUiTestContext.CreateAsync(handler, teacher: true, preview: true);
        var component = context.Render<CourseLectioImport>(parameters => parameters.Add(import => import.OpenInitially, true));

        Assert.Empty(component.FindAll("#lectio-import"));
        Assert.Empty(handler.Requests);
    }

    private sealed record ImportPayload(string LectioHoldKey, string Name, string HoldName);

    private sealed class LectioApi(bool alreadyImported) : HttpMessageHandler
    {
        public Guid WorkspaceId { get; } = Guid.NewGuid();
        public List<string> Requests { get; } = [];
        public ImportPayload? Import { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add($"{request.Method} {path}");
            if (path == "/api/lectio/holds/tracked")
            {
                return Json(new
                {
                    holds = new[] { new { lectioHoldKey = "h2/pro", name = "Programmering", year = "2026", memberCount = 1 } }
                });
            }
            if (path == "/api/lectio/holds/by-lectio/h2%2Fpro")
            {
                return Json(new
                {
                    hold = new { members = new[] { new { studentLectioId = "1", studentName = "Ada Lovelace" } } }
                });
            }
            if (path == "/api/forloeb/from-lectio" && request.Method == HttpMethod.Post)
            {
                Import = await request.Content!.ReadFromJsonAsync<ImportPayload>(ct);
                if (alreadyImported)
                    return Json(new { conflicts = new[] { new { lectioHoldKey = "h2/pro", forloebId = WorkspaceId } } }, HttpStatusCode.Conflict);
                return Json(new { id = WorkspaceId, name = Import!.Name, imported = new { total = 1 } }, HttpStatusCode.Created);
            }
            return new(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = JsonContent.Create(body) };
    }
}
