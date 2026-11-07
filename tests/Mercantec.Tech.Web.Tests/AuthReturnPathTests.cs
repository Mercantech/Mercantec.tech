using Mercantec.Tech.Web.Auth;

namespace Mercantec.Tech.Web.Tests;

public sealed class AuthReturnPathTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/journal")]
    [InlineData("/notebook/a0a88af6-1080-46f5-8f32-c8690867f196?owner=abc&kind=journal#kommentarer")]
    [InlineData("/search?q=https%3A%2F%2Fexample.com%2Fa%3Fb%3D1#resultater")]
    [InlineData("/library?query=returnUrl%3D%2Flogin&tag=C%23")]
    public void Local_destination_preserves_query_and_fragment(string path)
    {
        Assert.Equal(path, AuthReturnPath.Normalize(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("https://evil.example/notebook")]
    [InlineData("http://localhost/notebook")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hello")]
    [InlineData("//evil.example/notebook")]
    [InlineData("///evil.example/notebook")]
    [InlineData("notebook/123")]
    [InlineData("?returnUrl=/notebook/123")]
    [InlineData("#notebook")]
    [InlineData("/\\evil.example")]
    [InlineData("/notebook\\..\\login")]
    [InlineData("/%2fevil.example")]
    [InlineData("/%2Fevil.example")]
    [InlineData("/%5cevil.example")]
    [InlineData("/notebook%5c..%5clogin")]
    [InlineData("/notebook\r\nLocation:https://evil.example")]
    [InlineData("/notebook%0d%0aLocation:evil")]
    [InlineData("/note%00book")]
    [InlineData("/note\tbook")]
    public void Invalid_or_external_destination_is_rejected(string? path)
    {
        Assert.Null(AuthReturnPath.Normalize(path));
        Assert.Equal("/login", AuthReturnPath.LoginUrl(path));
    }

    [Theory]
    [InlineData("/login")]
    [InlineData("/LOGIN")]
    [InlineData("/login/?returnUrl=%2Fjournal")]
    [InlineData("/login#again")]
    [InlineData("/auth/callback")]
    [InlineData("/auth/callback?code=one-time-code&state=state")]
    [InlineData("/AUTH/CALLBACK/")]
    [InlineData("/notebook/../login")]
    [InlineData("/notebook/../auth/callback")]
    [InlineData("/auth/extra/../callback")]
    [InlineData("/%6cogin")]
    [InlineData("/auth/%63allback")]
    [InlineData("/notebook/%2e%2e/login")]
    [InlineData("/x%3Fq/../login")]
    [InlineData("/x%23fragment/../auth/callback")]
    public void Authentication_routes_cannot_be_return_destinations(string path)
    {
        Assert.Null(AuthReturnPath.Normalize(path));
    }

    [Fact]
    public void Login_link_encodes_the_whole_local_destination_as_one_parameter()
    {
        const string path = "/notebook/123?owner=abc&tag=C%23#kommentarer";

        Assert.Equal("/login?returnUrl=%2Fnotebook%2F123%3Fowner%3Dabc%26tag%3DC%2523%23kommentarer",
            AuthReturnPath.LoginUrl(path));
    }
}
