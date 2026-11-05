using Mercantec.Tech.Api.Integrations.Azure;

namespace Mercantec.Tech.Api.Tests;

public sealed class AzureGraphOptionsTests
{
    private const string MathiasSub = "d969fced-f7be-46d4-9bb9-5a2f03ca6974";

    [Fact]
    public void DefaultMap_MathiasSubMapsToMagsUpn()
    {
        var opts = new AzureGraphOptions();
        Assert.Equal("mags@mercantec.dk", opts.ResolveTeacherUpn(MathiasSub, "mathiasgsteenberg@gmail.com"));
        // Sub alene er nok — også uden e-mail
        Assert.Equal("mags@mercantec.dk", opts.ResolveTeacherUpn(MathiasSub, null));
    }

    [Fact]
    public void DefaultMap_MathiasGmailMapsToMagsUpn()
    {
        var opts = new AzureGraphOptions();
        Assert.Equal("mags@mercantec.dk", opts.ResolveTeacherUpn(null, "mathiasgsteenberg@gmail.com"));
        Assert.Equal("mags@mercantec.dk", opts.ResolveTeacherUpn(null, "MAGS@mercantec.dk"));
    }

    [Theory]
    [InlineData("kasc@mercantec.dk", "kasc@mercantec.dk")]
    [InlineData("KASC@mercantec.dk", "kasc@mercantec.dk")]
    public void DefaultMap_ResolvesKasperEmail(string email, string expectedUpn)
    {
        var opts = new AzureGraphOptions();
        Assert.Equal(expectedUpn, opts.ResolveTeacherUpn(null, email));
    }

    [Fact]
    public void ResolveTeacherUpn_FallsBackToEmail()
    {
        var opts = new AzureGraphOptions { TeacherUpnMap = "" };
        Assert.Equal("chly@mercantec.dk", opts.ResolveTeacherUpn(null, "chly@mercantec.dk"));
    }

    [Fact]
    public void ResolveTeacherUpn_SubTakesPrecedenceOverEmail()
    {
        var opts = new AzureGraphOptions
        {
            TeacherUpnMap = $"{MathiasSub}=mags@mercantec.dk;other@example.com=kasc@mercantec.dk"
        };
        Assert.Equal("mags@mercantec.dk", opts.ResolveTeacherUpn(MathiasSub, "other@example.com"));
    }

    [Fact]
    public void ResolveTeacherUpn_UsesCustomOverride()
    {
        var opts = new AzureGraphOptions
        {
            TeacherUpnMap = "kasper@mercantec.dk=kasc@mercantec.dk;mathias@example.com=mags@mercantec.dk"
        };
        Assert.Equal("kasc@mercantec.dk", opts.ResolveTeacherUpn(null, "kasper@mercantec.dk"));
        Assert.Equal("mags@mercantec.dk", opts.ResolveTeacherUpn(null, "mathias@example.com"));
    }

    [Fact]
    public void IsConfigured_RequiresAllThreeSecrets()
    {
        var opts = new AzureGraphOptions
        {
            TenantId = "t",
            ClientId = "c",
            ClientSecret = ""
        };
        Assert.False(opts.IsConfigured);
        opts.ClientSecret = "s";
        Assert.True(opts.IsConfigured);
    }
}
