using Mercantec.Tech.Web.Auth;

namespace Mercantec.Tech.Web.Tests;

public class ViewAsStudentContextTests
{
    [Fact]
    public void Enter_sets_preview_state_without_auth_mutation()
    {
        var ctx = new ViewAsStudentContext();
        var fired = 0;
        ctx.Changed += () => fired++;

        ctx.Enter("abc", "Maja Nielsen", "H3", Guid.Parse("11111111-1111-1111-1111-111111111111"), "/lectio");

        Assert.True(ctx.IsActive);
        Assert.Equal("abc", ctx.LectioId);
        Assert.Equal("Maja Nielsen", ctx.DisplayName);
        Assert.Equal("H3", ctx.ClassName);
        Assert.Equal("Maja Nielsen (H3)", ctx.BannerLabel);
        Assert.Equal("/lectio", ctx.ReturnUrl);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Exit_clears_preview()
    {
        var ctx = new ViewAsStudentContext();
        ctx.Enter("abc", "Maja");
        ctx.Exit();

        Assert.False(ctx.IsActive);
        Assert.Null(ctx.LectioId);
        Assert.Equal("", ctx.BannerLabel);
    }
}
