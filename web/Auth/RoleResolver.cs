using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Web.Auth;

public sealed class RoleOptions
{
    public const string SectionName = "Roles";

    /// <summary>Auth-rollenavne der giver underviser-shell (ud over defaults).</summary>
    public string[] TeacherRoleNames { get; set; } =
    [
        "Admin",
        "Underviser",
        "Teacher",
        "Instructor"
    ];
}

/// <summary>
/// Bestemmer elev vs underviser ud fra Auth role-claims.
/// Platform-rolle (lokal override) kan sættes senere via API — indtil da kun Auth.
/// </summary>
public sealed class RoleResolver(IOptions<RoleOptions> options)
{
    private readonly HashSet<string> _teacherRoles = new(
        options.Value.TeacherRoleNames,
        StringComparer.OrdinalIgnoreCase);

    public AppRole Resolve(IEnumerable<string> authRoles, string? platformRole = null)
    {
        if (!string.IsNullOrWhiteSpace(platformRole))
        {
            if (_teacherRoles.Contains(platformRole) ||
                platformRole.Equals("underviser", StringComparison.OrdinalIgnoreCase))
                return AppRole.Underviser;

            if (platformRole.Equals("elev", StringComparison.OrdinalIgnoreCase) ||
                platformRole.Equals("student", StringComparison.OrdinalIgnoreCase))
                return AppRole.Elev;
        }

        foreach (var role in authRoles)
        {
            if (_teacherRoles.Contains(role))
                return AppRole.Underviser;
        }

        return AppRole.Elev;
    }

    public string HomePath(AppRole role) => role switch
    {
        AppRole.Underviser => "/underviser",
        _ => "/elev"
    };
}
