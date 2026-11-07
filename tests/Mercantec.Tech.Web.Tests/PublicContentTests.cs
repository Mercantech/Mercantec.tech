using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Public;

namespace Mercantec.Tech.Web.Tests;

public sealed class PublicContentTests
{
    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/search?q=Arduino")]
    [InlineData("/library")]
    [InlineData("/courses/template/h2#theory")]
    [InlineData("/subjects/iot/mqtt")]
    [InlineData("/explore/modules")]
    [InlineData("/explore/projects/sensorstation")]
    [InlineData("/badges/hawaii")]
    [InlineData("/characters")]
    [InlineData("/characters/lupp")]
    [InlineData("/characters/vatson?from=library")]
    [InlineData("/characters/raymond#appearances")]
    [InlineData("/characters/beaver")]
    [InlineData("/characters/fox")]
    public void PublicReadingRoutesDoNotRequireLogin(string path) => Assert.True(PublicPageAccess.Allows(path));

    [Theory]
    [InlineData("/student")]
    [InlineData("/universe")]
    [InlineData("/settings")]
    [InlineData("/teacher/tools")]
    [InlineData("/teacher")]
    [InlineData("/teacher/view-as/123?published=true")]
    [InlineData("/profile")]
    [InlineData("/rights")]
    [InlineData("/lectio")]
    [InlineData("/notion/30000000-0000-0000-0000-000000000001?published=true")]
    [InlineData("/courses/30000000-0000-0000-0000-000000000001")]
    [InlineData("/projects/mine")]
    [InlineData("/progress/badges")]
    [InlineData("/calendar")]
    [InlineData("/learning/h3/days/2026-11-06")]
    [InlineData("/quests/30000000-0000-0000-0000-000000000001/submit")]
    [InlineData("/universe/shop")]
    [InlineData("/notifications")]
    [InlineData("/polls/30000000-0000-0000-0000-000000000001")]
    [InlineData("/courses/template-private/123")]
    [InlineData("/characters/lupp/private")]
    public void PrivateRoutesRemainProtectedEvenWithPublicLookingQueries(string path) => Assert.False(PublicPageAccess.Allows(path));

    [Fact]
    public void EveryPublicSearchResultOpensAnAllowedRoute()
    {
        var results = new[] { "H", "API", "Arduino", "Game", "badge", "DEMO", "Git" }.SelectMany(PublicCatalog.Search).ToArray();
        Assert.NotEmpty(results);
        Assert.All(results, h => Assert.True(PublicPageAccess.Allows(h.Href), h.Href));
        Assert.DoesNotContain(results, h => h.Kind == "Quest");
        Assert.Empty(PublicCatalog.Search("Alex Jensen"));
        Assert.Empty(PublicCatalog.Search("Maja Holm"));
    }

    [Theory]
    [InlineData("LUPP", "/characters/lupp")]
    [InlineData("Vatson", "/characters/vatson")]
    [InlineData("VATEK", "/characters/vatson")]
    [InlineData("Raymond", "/characters/raymond")]
    [InlineData("Bæver", "/characters/beaver")]
    [InlineData("Ræv", "/characters/fox")]
    [InlineData("Blå team", "/characters/beaver")]
    [InlineData("Rød team", "/characters/fox")]
    [InlineData("Sikkerhed og test", "/characters/beaver")]
    [InlineData("Sikkerhed og test", "/characters/fox")]
    [InlineData("Game Design", "/characters/lupp")]
    public void CharacterSearchFindsThePublicProfile(string query, string path)
    {
        var hit = Assert.Single(PublicCatalog.Search(query), hit => hit.Href == path);
        Assert.Equal("Karakter", hit.Kind);
        Assert.True(PublicPageAccess.Allows(hit.Href));
    }

    [Fact]
    public void ProjectCatalogContainsOnlyExplicitDemoPresentationsAndNoClassVisibilityMapping()
    {
        Assert.Equal(3, PublicCatalog.Projects.Count);
        Assert.All(PublicCatalog.Projects, project => Assert.True(project.IsDemo));
        Assert.Null(PublicCatalog.FindProject("iot")); // Existing private project must not become public.
        Assert.Null(PublicCatalog.FindProject("rune")); // Shared with a class is not public consent.
    }

    [Fact]
    public void PublicBadgeContractDoesNotContainPersonalAwardFields()
    {
        Assert.NotEmpty(PublicCatalog.Badges);
        var names = typeof(PublicBadge).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("Rank", names);
        Assert.DoesNotContain("Owner", names);
        Assert.DoesNotContain("UserSub", names);
        Assert.DoesNotContain("AwardedAt", names);
    }
}
