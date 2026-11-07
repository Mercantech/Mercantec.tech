using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mercantec.Tech.Api.Emojis;

public static class ExtensionUploadRegistration
{
    public static IServiceCollection AddExtensionUpload(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ExtensionUploadOptions>(configuration.GetSection(ExtensionUploadOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ExtensionUploadKeyService>();
        services.AddSingleton<ExtensionUploadRateLimit>();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, ExtensionUploadAuthenticationHandler>(ExtensionUploadCredentials.Scheme, _ => { });
        return services;
    }
}

/// <summary>Small per-process upload budget. Every credential remains revocable in the database.</summary>
public sealed class ExtensionUploadRateLimit(TimeProvider clock)
{
    private readonly ConcurrentDictionary<Guid, Window> _windows = new();

    public bool TryAcquire(Guid id)
    {
        var now = clock.GetUtcNow();
        foreach (var old in _windows.Where(item => item.Value.Start < now.AddMinutes(-2))) _windows.TryRemove(old.Key, out _);
        var window = _windows.GetOrAdd(id, _ => new Window(now));
        lock (window)
        {
            if (window.Start < now.AddMinutes(-1)) { window.Start = now; window.Count = 0; }
            if (window.Count >= 30) return false;
            window.Count++;
            return true;
        }
    }

    private sealed class Window(DateTimeOffset start)
    {
        public DateTimeOffset Start = start;
        public int Count;
    }
}
