using Mercantec.Tech.Api.Auth;
using ModelContextProtocol.AspNetCore;

namespace Mercantec.Tech.Api.Mcp;

public static class NotionMcpEndpoints
{
    public static Uri? PublicUri(IConfiguration configuration)
        => Uri.TryCreate(configuration["Mcp:PublicUrl"], UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && uri.AbsolutePath == "/api/mcp"
            && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) ? uri : null;

    public static string? ResourceMetadataUrl(IConfiguration configuration)
        => PublicUri(configuration) is { } uri ? uri.GetLeftPart(UriPartial.Authority) + "/.well-known/oauth-protected-resource/api/mcp" : null;

    public static string? Bearer(HttpRequest? request)
    {
        var value = request?.Headers.Authorization.ToString();
        return value?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true && value.Length > 7 ? value[7..] : null;
    }

    public static IServiceCollection AddNotionMcp(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<NotionMcpService>();
        services.AddHttpClient("notion-mcp", client => client.Timeout = TimeSpan.FromSeconds(90));
        services.AddAuthorization(options => options.AddPolicy("NotionMcpTeacher", policy => policy
            .RequireAuthenticatedUser().RequireAssertion(context => UserClaims.IsTeacher(context.User) && UserClaims.GetSub(context.User) != null)));
        services.AddMcpServer().WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithTools<NotionMcpTools>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
                { MaxDepth = 512, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() });
        return services;
    }

    public static void MapNotionMcp(this WebApplication app)
    {
        // Metadata is public; all MCP methods (including tool discovery) require the teacher policy.
        object Metadata() => new
        {
            resource = PublicUri(app.Configuration)?.AbsoluteUri,
            authorization_servers = new[] { app.Configuration["AUTH_ISSUER"] ?? "https://auth.mercantec.tech" },
            bearer_methods_supported = new[] { "header" },
            resource_name = "LUP teaching draft editor"
        };
        foreach (var route in new[] { "/.well-known/oauth-protected-resource/api/mcp", "/.well-known/oauth-protected-resource", "/api/mcp/.well-known/oauth-protected-resource" })
            app.MapGet(route, () => PublicUri(app.Configuration) == null ? Results.StatusCode(503) : Results.Json(Metadata())).AllowAnonymous();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/mcp"))
            {
                var uri = PublicUri(app.Configuration);
                if (uri == null) { context.Response.StatusCode = 503; return; }
                var origin = context.Request.Headers.Origin.ToString();
                if (!string.IsNullOrEmpty(origin) && origin != uri.GetLeftPart(UriPartial.Authority))
                { context.Response.StatusCode = 403; return; }
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Accel-Buffering"] = "no";
            }
            await next(context);
        });
        app.MapMcp("/api/mcp").RequireAuthorization("NotionMcpTeacher");
    }
}
