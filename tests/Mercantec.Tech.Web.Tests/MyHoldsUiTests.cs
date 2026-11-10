using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using HoldsPage = Mercantec.Tech.Web.Pages.Courses.Index;

namespace Mercantec.Tech.Web.Tests;

public sealed class MyHoldsUiTests
{
    [Fact]
    public async Task Teacher_can_rename_a_hold_and_filter_by_favorites_and_lectio_source()
    {
        using var api = new HoldApi();
        await using var context = await LearningUiTestContext.CreateAsync(api, teacher: true);
        var page = context.Render<HoldsPage>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".hold-card").Count));

        page.Find("button[aria-label='Omdøb H1A']").Click();
        page.Find(".hold-rename input").Input("Backend november");
        page.Find(".hold-rename").Submit();
        page.WaitForAssertion(() => Assert.Contains("Backend november", page.Find(".hold-card h2").TextContent));
        Assert.Equal("Backend november", api.SavedName);

        page.Find("button[aria-label='Favorit: Backend november']").Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("button[aria-label='Favorit: Backend november'][aria-pressed='true']")));
        Assert.True(api.SavedFavorite);
        page.Find(".hold-favorites-filter input").Change(true);
        Assert.Single(page.FindAll(".hold-card"));
        Assert.Contains("Backend november", page.Find(".hold-card").TextContent);
        Assert.Contains("2 uden LUP-konto", page.Find(".hold-card").TextContent);

        page.Find(".hold-filters select").Change("manual");
        Assert.Empty(page.FindAll(".hold-card"));
        Assert.Contains("Ingen hold matcher", page.Markup);
    }

    private sealed class HoldApi : HttpMessageHandler
    {
        private readonly Guid _imported = Guid.NewGuid();
        public string? SavedName { get; private set; }
        public bool SavedFavorite { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/holds/mine")
            {
                var created = DateTimeOffset.UtcNow;
                return new(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new[]
                    {
                        new MyHoldCard(_imported, Guid.NewGuid(), "H1A", "C# og databaser", null,
                            created, 12, 2, [new("lectio-h1a", "H1A")], true, false),
                        new MyHoldCard(Guid.NewGuid(), Guid.NewGuid(), "Manuelt hold", "Manuelt hold", null,
                            created, 4, 0, [], true, false)
                    })
                };
            }
            if (request.Method == HttpMethod.Patch && path == $"/api/holds/{_imported:D}/name")
            {
                SavedName = (await request.Content!.ReadFromJsonAsync<RenameHold>(ct))?.Name;
                return new(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Put && path == $"/api/holds/{_imported:D}/favorite")
            {
                SavedFavorite = true;
                return new(HttpStatusCode.NoContent);
            }
            return new(HttpStatusCode.NotFound);
        }
    }
}
