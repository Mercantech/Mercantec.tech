using Mercantec.Tech.Api.Integrations.Azure;

namespace Mercantec.Tech.Api.Tests;

public sealed class AzureGraphOptionsTests
{
    [Theory]
    [InlineData("kasc@mercantec.dk", "kasc@mercantec.dk")]
    [InlineData("KASC@mercantec.dk", "kasc@mercantec.dk")]
    [InlineData("mags@mercantec.dk", "mags@mercantec.dk")]
    [InlineData("MAGS@mercantec.dk", "mags@mercantec.dk")]
    public void DefaultMap_ResolvesKasperAndMathias(string email, string expectedUpn)
    {
        var opts = new AzureGraphOptions();
        Assert.Equal(expectedUpn, opts.ResolveTeacherUpn(email));
    }

    [Fact]
    public void ResolveTeacherUpn_FallsBackToEmail()
    {
        var opts = new AzureGraphOptions { TeacherUpnMap = "" };
        Assert.Equal("chly@mercantec.dk", opts.ResolveTeacherUpn("chly@mercantec.dk"));
    }

    [Fact]
    public void ResolveTeacherUpn_UsesCustomOverride()
    {
        var opts = new AzureGraphOptions
        {
            TeacherUpnMap = "kasper@mercantec.dk=kasc@mercantec.dk;mathias@example.com=mags@mercantec.dk"
        };
        Assert.Equal("kasc@mercantec.dk", opts.ResolveTeacherUpn("kasper@mercantec.dk"));
        Assert.Equal("mags@mercantec.dk", opts.ResolveTeacherUpn("mathias@example.com"));
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
