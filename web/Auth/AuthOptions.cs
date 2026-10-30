namespace Mercantec.Tech.Web.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string ClientId { get; set; } = "mercantec-tech";
    public string Authority { get; set; } = "https://auth.mercantec.tech";
    public string AuthorizePath { get; set; } = "/oauth/authorize";
    public string TokenPath { get; set; } = "/oauth/token";
    public string RedirectPath { get; set; } = "/auth/callback";
    public string Scope { get; set; } = "openid profile email offline_access";

    public string AuthorizeUrl => $"{Authority.TrimEnd('/')}{AuthorizePath}";
    public string TokenUrl => $"{Authority.TrimEnd('/')}{TokenPath}";
}
