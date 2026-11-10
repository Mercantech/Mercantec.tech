using Bunit;
using Mercantec.Tech.Web.Branding;
using Mercantec.Tech.Web.Components;

namespace Mercantec.Tech.Web.Tests;

public sealed class AboutLupTests
{
    [Fact]
    public void About_can_be_read_without_auth_services_and_explains_both_audiences()
    {
        using var context = new BunitContext();
        var page = context.Render<AboutLup>();

        Assert.Equal("Om LUP", Assert.Single(page.FindAll("h1")).TextContent);
        var pupil = page.Find("#for-elever");
        var teacher = page.Find("#for-undervisere");
        Assert.Contains("Find dagens arbejde", pupil.TextContent);
        Assert.Contains("Aflevér dit arbejde", pupil.TextContent);
        Assert.Contains("Saml dit forløb", teacher.TextContent);
        Assert.Contains("giv feedback", teacher.TextContent);
        Assert.Contains("Kasper og Mathias", page.Markup);
        foreach (var roleLink in page.FindAll(".about-role-links a"))
        {
            var href = roleLink.GetAttribute("href")!;
            Assert.StartsWith("/sources#", href); // A bare fragment resolves against the app’s / base, away from Om LUP.
            Assert.Single(page.FindAll(href[href.IndexOf('#')..]));
        }
    }

    [Fact]
    public void Overview_and_login_links_offer_a_way_forward_without_replacing_existing_routes()
    {
        using var context = new BunitContext();
        var page = context.Render<AboutLup>();

        Assert.Contains("Oversigt", page.Find("a[href='/library']").TextContent);
        Assert.NotEmpty(page.FindAll("a[href='/login']"));
        Assert.Single(page.FindAll("a[href='/library?category=courses']"));
        Assert.Single(page.FindAll("a[href='/characters']"));
        Assert.DoesNotContain(page.FindAll("a"), a => a.GetAttribute("href") is "/overview" or "/source");
    }

    [Fact]
    public void Character_links_use_existing_profiles_and_the_lupp_visual_has_an_accessible_description()
    {
        using var context = new BunitContext();
        var page = context.Render<AboutLup>();

        Assert.Contains("LUPP", page.Find(".about-mascot img").GetAttribute("alt"));
        foreach (var profile in new[] { "lupp", "raymond", "bee", "beaver", "fox" })
            Assert.Single(page.FindAll($"a[href='/characters/{profile}']"));
        Assert.All(page.FindAll(".about-character-list img"), image => Assert.Equal("lazy", image.GetAttribute("loading")));
    }

    [Fact]
    public void The_name_explanations_share_the_footer_catalogue_and_keep_the_original_attribution()
    {
        using var context = new BunitContext();
        var page = context.Render<AboutLup>();

        var explanations = page.Find(".about-expansions");
        foreach (var expansion in LupIdentity.Expansions)
        {
            Assert.Contains(expansion.Name, explanations.TextContent);
            Assert.Contains(expansion.Description, explanations.TextContent);
        }
        Assert.Contains(LupIdentity.OfficialName, Assert.Single(page.FindAll(".about-expansions .is-official")).TextContent);
        var attribution = page.Find(".about-attribution a");
        Assert.Contains("github.com/git/git/", attribution.GetAttribute("href"));
        Assert.Equal("noopener", attribution.GetAttribute("rel"));
        Assert.Single(page.FindAll("a[href='https://github.com/Mercantech/Mercantec.tech']"));
    }
}
