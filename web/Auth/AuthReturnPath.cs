namespace Mercantec.Tech.Web.Auth;

/// <summary>Only app-local destinations may survive a trip through the login provider.</summary>
public static class AuthReturnPath
{
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 8192 || path[0] != '/'
            || path.Any(char.IsControl)) return null;

        var end = path.IndexOfAny(['?', '#']);
        var route = end < 0 ? path : path[..end];
        // Decode before validation so encoded separators and dot segments cannot hide
        // a different destination or send the browser back into the login flow.
        for (var i = 0; i < 8; i++)
        {
            if (route.Contains("//", StringComparison.Ordinal) || route.Contains('\\')
                || route.Any(char.IsControl)) return null;
            var decoded = Uri.UnescapeDataString(route);
            if (decoded == route) break;
            route = decoded;
            if (i == 7) return null;
        }

        if (!Uri.TryCreate(new Uri("https://lup.invalid/"), route, out var destination)
            || destination.Host != "lup.invalid") return null;
        var canonical = destination.AbsolutePath.TrimEnd('/');
        if (canonical.Equals("/login", StringComparison.OrdinalIgnoreCase)
            || canonical.Equals("/auth/callback", StringComparison.OrdinalIgnoreCase)) return null;
        return path;
    }

    public static string LoginUrl(string? path) => Normalize(path) is { } target
        ? "/login?returnUrl=" + Uri.EscapeDataString(target)
        : "/login";
}
