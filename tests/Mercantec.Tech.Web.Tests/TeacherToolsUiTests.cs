using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Notion;
using Mercantec.Tech.Web.Pages.Teacher;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class TeacherToolsUiTests
{
    [Fact]
    public async Task Teacher_opens_scoped_tools_only_for_manageable_holds()
    {
        using var api = new HoldApi();
        await using var context = await CreateContext(api, teacher: true);
        var page = context.Render<Tools>();

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".teacher-hold-picker")));
        Assert.Contains(page.FindAll("a"), link => link.GetAttribute("href") == "/courses?import=lectio");
        Assert.DoesNotContain(page.FindAll("option"), option => option.TextContent == "Læsning");
        Assert.Empty(page.FindAll(".teacher-hold-shortcuts"));

        page.Find(".teacher-hold-picker select").Change(api.Second.ToString());

        var shortcuts = page.FindAll(".teacher-hold-shortcuts a").Select(link => link.GetAttribute("href")).ToArray();
        Assert.Contains($"/holds/{api.Second:D}?view=submissions", shortcuts);
        Assert.Contains($"/holds/{api.Second:D}/roulette?hold={api.Second:D}", shortcuts);
        Assert.DoesNotContain(shortcuts, href => href?.Contains(api.First.ToString("D"), StringComparison.Ordinal) == true);

        context.ViewAs.Enter("elev", "Elev i forhåndsvisning");
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".teacher-hold-shortcuts")));
        Assert.Equal(1, api.Requests);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Teacher_tools_do_not_load_hold_data_in_student_view(bool teacher, bool preview)
    {
        using var api = new HoldApi();
        await using var context = await CreateContext(api, teacher, preview);
        var page = context.Render<Tools>();

        Assert.Contains("Denne side er for undervisere", page.Markup);
        Assert.Empty(page.FindAll(".teacher-tools-sections"));
        Assert.Equal(0, api.Requests);
    }

    private static Task<LearningUiTestContext> CreateContext(HoldApi api, bool teacher, bool preview = false) =>
        LearningUiTestContext.CreateAsync(api, teacher, preview,
            services => services.AddSingleton<NotionClient>());

    private sealed class HoldApi : HttpMessageHandler
    {
        public Guid First { get; } = Guid.NewGuid();
        public Guid Second { get; } = Guid.NewGuid();
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/notion/holds", request.RequestUri?.AbsolutePath);
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new NotionHold[]
                {
                    new(First, "Første hold", true),
                    new(Second, "Andet hold", true),
                    new(Guid.NewGuid(), "Læsning", false)
                })
            });
        }
    }
}
