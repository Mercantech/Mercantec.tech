using System.Security.Claims;
using Mercantec.Tech.Api.Auth;

namespace Mercantec.Tech.Api.Emojis;

public sealed class ExtensionUploadOptions
{
    public const string SectionName = "ExtensionUpload";
    public string AllowedEmails { get; set; } = "";
    public string AllowedSubjects { get; set; } = "";

    public bool Allows(Guid subject, string? email) => subject != Guid.Empty &&
        (AllowedSubjects.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(value => Guid.TryParse(value, out var id) && id == subject) || Allows(email));

    public bool Allows(string? email) => !string.IsNullOrWhiteSpace(email)
        && AllowedEmails.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(email.Trim(), StringComparer.OrdinalIgnoreCase);

    public bool CanManage(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        && !user.Identities.Any(identity => identity.AuthenticationType == ExtensionUploadCredentials.Scheme)
        && UserClaims.GetSub(user) is Guid sub && sub != Guid.Empty && UserClaims.IsTeacher(user)
        && Email(user) is { Length: > 0 and <= 254 } email && !string.IsNullOrWhiteSpace(email)
        && !email.Any(char.IsControl) && Allows(sub, email);

    public static string? Email(ClaimsPrincipal user) => user.FindFirstValue("email") ?? user.FindFirstValue(ClaimTypes.Email);
}
