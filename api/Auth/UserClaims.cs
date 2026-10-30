using System.Security.Claims;

namespace Mercantec.Tech.Api.Auth;

public static class UserClaims
{
    private static readonly HashSet<string> TeacherRoleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin", "Underviser", "Teacher", "Instructor"
    };

    public static Guid? GetSub(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var sub) ? sub : null;
    }

    public static bool IsTeacher(ClaimsPrincipal user) =>
        user.Claims
            .Where(c =>
                c.Type is "role" or "roles"
                || c.Type.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .Any(v => TeacherRoleNames.Contains(v));
}
