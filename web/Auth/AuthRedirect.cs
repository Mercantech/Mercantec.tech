namespace Mercantec.Tech.Web.Auth;

public sealed class AuthRedirect(AuthService auth, RoleResolver roles, ViewAsStudentContext viewAs)
{
    public string PostLoginPath()
    {
        if (!auth.IsAuthenticated)
            return "/login";

        if (viewAs.IsActive)
            return "/student";

        var appRole = roles.Resolve(auth.Roles);
        return roles.HomePath(appRole);
    }

    public AppRole CurrentAppRole() => roles.Resolve(auth.Roles);

    /// <summary>
    /// UI-rolle til chrome/navigation. Under «se som elev» er det altid elev —
    /// underviserens JWT/Auth-rolle forbliver uændret.
    /// </summary>
    public AppRole EffectiveUiRole() =>
        viewAs.IsActive ? AppRole.Elev : CurrentAppRole();

    public bool ShowsTeacherChrome =>
        auth.IsAuthenticated && CurrentAppRole() == AppRole.Underviser && !viewAs.IsActive;
}
