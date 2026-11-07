namespace Mercantec.Tech.Web.Auth;

/// <summary>UI allowlist. API authorization remains the authority for all personal data.</summary>
public static class PublicPageAccess
{
    public static bool Allows(string relativePath)
    {
        var path = relativePath.Split('?', '#')[0].Trim('/');
        if (path is "" or "login" or "auth/callback" or "privacy" or "help" or "sources" or "extension"
            or "library" or "search" or "explore/modules" or "subjects" or "badges" or "explore/projects" or "courses/template" or "lup" or "characters")
            return true;

        var parts = path.Split('/');
        return parts switch
        {
            ["courses", "template", _] => true,
            ["subjects", _] or ["subjects", _, _] => true,
            ["badges", _] => true,
            ["explore", "projects", _] => true,
            ["characters", _] => true,
            _ => false
        };
    }
}
