using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Microsoft.AspNetCore.DataProtection;

namespace Mercantec.Tech.Api.Storage;

public sealed class FileViewerTickets(IDataProtectionProvider protection)
{
    private readonly IDataProtector protector = protection.CreateProtector("Mercantec.FileViewer.v1");

    public string Create(Guid fileId, ClaimsPrincipal viewer, DateTimeOffset expires)
    {
        var roles = viewer.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type is "role" or "roles")
            .Select(c => c.Value).Distinct().ToArray();
        return protector.Protect(JsonSerializer.Serialize(new Ticket(fileId,
            UserClaims.GetSub(viewer)!.Value, roles, expires)));
    }

    public ClaimsPrincipal? Resolve(Guid fileId, string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        try
        {
            var ticket = JsonSerializer.Deserialize<Ticket>(protector.Unprotect(token));
            if (ticket is null || ticket.FileId != fileId || ticket.ExpiresAtUtc <= DateTimeOffset.UtcNow) return null;
            var claims = new List<Claim> { new("sub", ticket.UserSub.ToString()) };
            claims.AddRange(ticket.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "file-ticket"));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { return null; }
    }

    private sealed record Ticket(Guid FileId, Guid UserSub, string[] Roles, DateTimeOffset ExpiresAtUtc);
}

public static class WebPreviewOrigins
{
    public static bool TryGet(IConfiguration configuration, out Uri origin, out Uri appOrigin)
    {
        origin = null!;
        appOrigin = null!;
        return Valid(configuration["WebPreview:Origin"], out origin)
            && Valid(configuration["WebPreview:AppOrigin"], out appOrigin)
            && !origin.Host.Equals(appOrigin.Host, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Valid(string? value, out Uri uri)
    {
        uri = null!;
        return Uri.TryCreate(value, UriKind.Absolute, out uri!)
            && uri.Scheme == "https" && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.UserInfo);
    }
}
