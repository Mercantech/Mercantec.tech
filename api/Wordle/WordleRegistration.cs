using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mercantec.Tech.Api.Wordle;

public static class WordleRegistration
{
    public static IServiceCollection AddWordle(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(_ => WordleLexicon.Load());
        return services;
    }
}
