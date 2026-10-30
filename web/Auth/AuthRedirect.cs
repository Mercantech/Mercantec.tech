namespace Mercantec.Tech.Web.Auth;

public sealed class AuthRedirect(AuthService auth, RoleResolver roles)
{
    public string PostLoginPath()
    {
        if (!auth.IsAuthenticated)
            return "/login";

        var appRole = roles.Resolve(auth.Roles);
        return roles.HomePath(appRole);
    }

    public AppRole CurrentAppRole() => roles.Resolve(auth.Roles);
}
