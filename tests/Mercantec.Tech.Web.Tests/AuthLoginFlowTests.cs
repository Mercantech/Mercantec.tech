using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Bunit;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

public sealed class AuthLoginFlowTests
{
    private const string Target = "/notebook/11111111-1111-4111-8111-111111111111?owner=22222222-2222-4222-8222-222222222222#kommentarer";

    [Fact]
    public async Task Protected_deep_link_survives_login_page_and_full_provider_navigation()
    {
        using var context = new LoginContext();
        context.Navigation.NavigateTo(Target);
        await context.Auth.InitializeAsync();

        var login = context.Auth.LoginPath();
        Assert.Equal(AuthReturnPath.LoginUrl(Target), login);
        context.Navigation.NavigateTo(login);
        await context.Auth.BeginLoginAsync();

        Assert.Equal(Target, context.Browser.ReturnPath);
        Assert.False(string.IsNullOrEmpty(context.Browser.Verifier));
        Assert.False(string.IsNullOrEmpty(context.Browser.State));
        Assert.StartsWith("https://auth.mercantec.tech/oauth/authorize?", context.Navigation.Uri);
        Assert.Contains("code_challenge_method=S256", context.Navigation.Uri);
        Assert.DoesNotContain("returnUrl", context.Navigation.Uri);
    }

    [Fact]
    public async Task Header_login_remembers_the_current_public_page()
    {
        using var context = new LoginContext();
        context.Navigation.NavigateTo("/library?tag=C%23#eksempler");
        await context.Auth.InitializeAsync();

        await context.Auth.BeginLoginAsync();

        Assert.Equal("/library?tag=C%23#eksempler", context.Browser.ReturnPath);
    }

    [Fact]
    public async Task Authenticated_login_page_returns_to_the_requested_page_instead_of_dashboard()
    {
        using var context = new LoginContext(authenticated: true);
        context.Navigation.NavigateTo(AuthReturnPath.LoginUrl(Target));
        await context.Auth.InitializeAsync();

        context.Render<Login>();

        Assert.Equal("http://localhost" + Target, context.Navigation.Uri);
    }

    [Fact]
    public async Task Return_path_is_consumed_once_and_external_stored_values_are_ignored()
    {
        using var context = new LoginContext();
        context.Browser.ReturnPath = Target;
        Assert.Equal(Target, await context.Auth.ConsumeLoginReturnPathAsync());
        Assert.Null(await context.Auth.ConsumeLoginReturnPathAsync());

        context.Browser.ReturnPath = "//evil.example/";
        Assert.Null(await context.Auth.ConsumeLoginReturnPathAsync());
        Assert.Null(context.Browser.ReturnPath);
    }

    [Fact]
    public async Task Invalid_callback_state_cannot_complete_login_or_consume_the_return_path()
    {
        using var context = new LoginContext();
        context.Browser.ReturnPath = Target;
        context.Browser.Verifier = "verifier";
        context.Browser.State = "expected-state";
        context.Navigation.NavigateTo("/auth/callback?code=unused&state=wrong-state");

        var ok = await context.Auth.CompleteLoginAsync("unused", "wrong-state", null);

        Assert.False(ok);
        Assert.False(context.Auth.IsAuthenticated);
        Assert.Equal(Target, context.Browser.ReturnPath);
        Assert.Null(context.Browser.Verifier);
        Assert.Contains("/auth/callback?", context.Navigation.Uri);
    }

    [Fact]
    public async Task Explicit_logout_clears_pending_return_and_only_logs_out_locally()
    {
        using var context = new LoginContext(authenticated: true);
        context.Navigation.NavigateTo(Target);
        await context.Auth.InitializeAsync();
        context.Browser.ReturnPath = Target;
        context.Browser.Verifier = "verifier";

        await context.Auth.LogoutLocalAsync();

        Assert.Null(context.Browser.ReturnPath);
        Assert.Null(context.Browser.Verifier);
        Assert.Null(context.Browser.AccessToken);
        Assert.False(context.Auth.IsAuthenticated);
        Assert.Equal("http://localhost" + Target, context.Navigation.Uri);
    }

    [Fact]
    public async Task Expired_credentials_on_callback_do_not_erase_pending_login_or_return_path()
    {
        using var context = new LoginContext(authenticated: true);
        context.Browser.ExpiresAt = 1;
        context.Browser.ReturnPath = Target;
        context.Browser.Verifier = "verifier";
        context.Browser.State = "expected-state";
        context.Navigation.NavigateTo("/auth/callback?code=unused&state=expected-state");

        await context.Auth.InitializeAsync();

        Assert.False(context.Auth.IsAuthenticated);
        Assert.Null(context.Browser.AccessToken);
        Assert.Equal(Target, context.Browser.ReturnPath);
        Assert.Equal("expected-state", context.Browser.State);
        Assert.Equal("verifier", context.Browser.Verifier);
    }

    private sealed class LoginContext : BunitContext
    {
        public BrowserStorage Browser { get; } = new();
        public NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();
        public AuthService Auth => Services.GetRequiredService<AuthService>();

        public LoginContext(bool authenticated = false)
        {
            if (authenticated)
                Browser.AccessToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                    claims: [new Claim("sub", Guid.NewGuid().ToString()), new Claim("name", "Testelev")]));
            Services.AddOptions();
            Services.AddSingleton<IJSRuntime>(Browser);
            Services.AddSingleton<TokenStore>();
            Services.AddSingleton<AuthService>();
            Services.AddSingleton<RoleResolver>();
            Services.AddSingleton<ViewAsStudentContext>();
            Services.AddSingleton<AuthRedirect>();
        }
    }

    private sealed class BrowserStorage : IJSRuntime
    {
        public string? AccessToken { get; set; }
        public long ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        public string? ReturnPath { get; set; }
        public string? Verifier { get; set; }
        public string? State { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            object? result = null;
            switch (identifier)
            {
                case "mercantecAuth.getTokens":
                    return ValueTask.FromResult(JsonSerializer.Deserialize<TValue>(JsonSerializer.Serialize(new
                        { access = AccessToken, refresh = (string?)null, expiresAt = ExpiresAt }))!);
                case "mercantecAuth.getPkce":
                    return ValueTask.FromResult(JsonSerializer.Deserialize<TValue>(JsonSerializer.Serialize(new
                        { verifier = Verifier, state = State }))!);
                case "mercantecAuth.setPkce":
                    Verifier = (string?)args![0]; State = (string?)args[1]; break;
                case "mercantecAuth.clearPkce": Verifier = State = null; break;
                case "mercantecAuth.clearTokens": AccessToken = null; break;
                case "mercantecAuth.setLoginReturnPath": ReturnPath = (string?)args![0]; break;
                case "mercantecAuth.consumeLoginReturnPath": result = ReturnPath; ReturnPath = null; break;
                case "mercantecAuth.clearLoginReturnPath": ReturnPath = null; break;
            }
            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }
    }
}
