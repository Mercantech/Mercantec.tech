using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Emojis;

public sealed class ExtensionUploadAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, ExtensionUploadKeyService keys) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var raw = header[7..].Trim();
        var key = await keys.Authenticate(raw, Context.RequestAborted);
        if (key is null) return AuthenticateResult.Fail("Invalid extension upload credential.");
        var identity = new ClaimsIdentity([
            new Claim("sub", key.OwnerSub.ToString()), new Claim("email", key.OwnerEmail),
            new Claim("role", "Teacher"), new Claim(ExtensionUploadCredentials.KeyIdClaim, key.Id.ToString()),
            new Claim(ExtensionUploadCredentials.ScopeClaim, ExtensionUploadCredentials.UploadScope)
        ], ExtensionUploadCredentials.Scheme, "sub", "role");
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), ExtensionUploadCredentials.Scheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Response.WriteAsJsonAsync(new { error = "extension_access_invalid" });
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Response.WriteAsJsonAsync(new { error = "extension_access_forbidden" });
    }
}
