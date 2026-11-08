using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Bunit;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Emojis;
using Mercantec.Tech.Web.Learning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

/// <summary>Exercises the real account lifecycle with browser token storage and a local HTTP transport.</summary>
internal sealed class LearningUiTestContext : BunitContext
{
    public static readonly Guid AccountId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public TestJavaScriptRuntime Javascript { get; }
    public AuthService Auth => Services.GetRequiredService<AuthService>();
    public ViewAsStudentContext ViewAs => Services.GetRequiredService<ViewAsStudentContext>();

    private LearningUiTestContext(HttpMessageHandler handler, bool teacher, Action<IServiceCollection>? configure)
    {
        var claims = new List<Claim> { new("sub", AccountId.ToString()), new("name", "Testelev") };
        if (teacher) claims.Add(new("role", "Teacher"));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims: claims));
        Javascript = new(token);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://app.example/api/") };
        Services.AddOptions();
        Services.AddSingleton<IJSRuntime>(Javascript);
        Services.AddSingleton<TokenStore>();
        Services.AddSingleton<AuthService>();
        Services.AddSingleton<ViewAsStudentContext>();
        Services.AddSingleton<RoleResolver>();
        Services.AddSingleton<AuthRedirect>();
        Services.AddSingleton<Mercantec.Tech.Web.Activity.ActionCenterChanges>();
        Services.AddSingleton<Mercantec.Tech.Web.Activity.ActionCenterService>();
        Services.AddSingleton(http);
        Services.AddSingleton(new LearningClient(http));
        Services.AddSingleton(new EmojiClient(http));
        configure?.Invoke(Services);
    }

    public static async Task<LearningUiTestContext> CreateAsync(HttpMessageHandler handler,
        bool teacher = false, bool preview = false, Action<IServiceCollection>? configure = null)
    {
        var context = new LearningUiTestContext(handler, teacher, configure);
        await context.Auth.InitializeAsync();
        if (preview) context.ViewAs.Enter("preview-student", "Elev i forhåndsvisning", "H2", Guid.NewGuid());
        return context;
    }
}

internal sealed class TestJavaScriptRuntime(string token) : IJSRuntime
{
    public bool ConfirmResult { get; set; } = true;
    public List<string> Confirmations { get; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (identifier == "mercantecAuth.getTokens")
        {
            var json = JsonSerializer.Serialize(new { access = token, refresh = (string?)null,
                expiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() });
            return ValueTask.FromResult(JsonSerializer.Deserialize<TValue>(json)!);
        }
        if (identifier == "confirm")
        {
            Confirmations.Add(args?.FirstOrDefault()?.ToString() ?? "");
            return ValueTask.FromResult((TValue)(object)ConfirmResult);
        }
        // InputFile and NavigationLock install browser listeners; these tests dispatch DOM events themselves.
        return ValueTask.FromResult(default(TValue)!);
    }
}
