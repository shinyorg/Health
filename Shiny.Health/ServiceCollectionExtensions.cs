using Microsoft.Extensions.DependencyInjection;

namespace Shiny;


public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHealthIntegration(this IServiceCollection services)
    {
#if IOS || ANDROID
        services.AddShinyService<Shiny.Health.HealthService>();
#endif
        return services;
    }
}
