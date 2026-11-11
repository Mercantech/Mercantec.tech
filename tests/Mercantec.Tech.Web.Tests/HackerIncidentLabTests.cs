using Mercantec.Tech.Web.Theming;

namespace Mercantec.Tech.Web.Tests;

public sealed class HackerIncidentLabTests
{
    [Fact]
    public void Investigating_then_recovering_changes_the_catalog_account_and_activity()
    {
        var lab = new HackerIncidentLab();
        Assert.Contains("4/6", lab.Execute("status"));
        Assert.Contains("kladde", lab.Execute("cat catalog.txt"));

        Assert.Contains("K-271", lab.Execute("cat auth.log"));
        Assert.Contains("K-271", lab.Execute("cat changes.log"));
        lab.Execute("isolate catalog");
        Assert.Equal(HackerIncidentStage.Isolated, lab.Stage);
        Assert.Contains("catalog · isoleret", lab.Execute("status"));

        lab.Execute("disable catalog-preview");
        Assert.Equal(HackerIncidentStage.Contained, lab.Stage);
        Assert.Contains("catalog-preview  spærret", lab.Execute("cat accounts.txt"));
        lab.Execute("cat backups.txt");
        lab.Execute("restore 0730");

        Assert.Equal(HackerIncidentStage.Restored, lab.Stage);
        Assert.DoesNotContain("kladde", lab.Execute("cat catalog.txt"));
        Assert.Contains("MOD-06", lab.Execute("cat catalog.txt"));
        Assert.Contains("6/6", lab.Execute("status"));

        lab.Execute("reopen catalog");
        Assert.Equal(HackerIncidentStage.Restored, lab.Stage);
        lab.Execute("verify");
        Assert.Equal(HackerIncidentStage.Verified, lab.Stage);
        lab.Execute("reopen catalog");
        Assert.Equal(HackerIncidentStage.Recovered, lab.Stage);
        Assert.Contains("catalog · åben", lab.Execute("status"));
        Assert.Contains("catalog-preview  spærret", lab.Execute("cat accounts.txt"));
        Assert.Contains("GENÅBNET", lab.Execute("cat activity.log"));
        Assert.Contains("4/6", new HackerIncidentLab().Execute("status"));
    }

    [Fact]
    public void Uninvestigated_actions_and_failed_backup_leave_the_fixture_unchanged()
    {
        var lab = new HackerIncidentLab();
        var initialCatalog = lab.Execute("cat catalog.txt");

        lab.Execute("isolate catalog");
        lab.Execute("restore 0730");
        Assert.Equal(HackerIncidentStage.Investigating, lab.Stage);

        lab.Execute("  CAT   AUTH.LOG ");
        lab.Execute("cat changes.log");
        lab.Execute("isolate status");
        Assert.Equal(HackerIncidentStage.Investigating, lab.Stage);
        lab.Execute("isolate catalog");
        lab.Execute("disable teacher-demo");
        Assert.Equal(HackerIncidentStage.Isolated, lab.Stage);

        lab.Execute("disable catalog-preview");
        lab.Execute("restore 0730");
        Assert.Equal(HackerIncidentStage.Contained, lab.Stage);
        lab.Execute("cat backups.txt");
        lab.Execute("restore 0835");
        Assert.Equal(HackerIncidentStage.Contained, lab.Stage);
        Assert.Equal(initialCatalog, lab.Execute("cat catalog.txt"));
        Assert.DoesNotContain("GENDANNET", lab.Execute("cat activity.log"));
    }
}
