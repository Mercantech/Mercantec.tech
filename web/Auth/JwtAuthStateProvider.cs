using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Mercantec.Tech.Web.Auth;

public sealed class JwtAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly AuthService _auth;

    public JwtAuthStateProvider(AuthService auth)
    {
        _auth = auth;
        _auth.AuthStateChanged += OnAuthChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_auth.IsAuthenticated || string.IsNullOrEmpty(_auth.Subject))
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        var claims = new List<Claim>
        {
            new("sub", _auth.Subject),
            new(ClaimTypes.NameIdentifier, _auth.Subject)
        };

        if (!string.IsNullOrEmpty(_auth.DisplayName))
            claims.Add(new Claim("name", _auth.DisplayName));

        foreach (var role in _auth.Roles)
            claims.Add(new Claim("role", role));

        var identity = new ClaimsIdentity(claims, authenticationType: "MercantecJwt", nameType: "name", roleType: "role");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    private void OnAuthChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void Dispose() => _auth.AuthStateChanged -= OnAuthChanged;
}
