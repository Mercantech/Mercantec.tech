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
        ToAppRole(RoleValues(user)) == "underviser";

    public static string? EditorName(ClaimsPrincipal user) => CleanEditorName(user.FindFirstValue("name"));

    public static string? CleanEditorName(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name) || name.Any(char.IsControl)) return null;
        if (name.Length <= 200) return name;
        return name[..(char.IsHighSurrogate(name[199]) ? 199 : 200)];
    }

    public static string ToAppRole(IEnumerable<string> roles) =>
        roles.Any(TeacherRoleNames.Contains) ? "underviser" : "elev";

    private static IEnumerable<string> RoleValues(ClaimsPrincipal user) =>
        user.Claims
            .Where(c =>
                c.Type is "role" or "roles"
                || c.Type.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value);
}
