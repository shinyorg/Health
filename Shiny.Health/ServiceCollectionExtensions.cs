using Microsoft.Extensions.DependencyInjection;

namespace Shiny;


public static class HealthServiceCollectionExtensions
{
    public static IServiceCollection AddHealthIntegration(this IServiceCollection services)
    {
#if IOS || ANDROID
        // Shiny 5.0 removed AddShinyService<T>; register the implementation and forward each
        // Shiny interface it implements to the same singleton (IHealthService, plus the Android
        // activity-result lifecycle hook used to complete the Health Connect permission flow).
        services.AddSingleton<Shiny.Health.HealthService>();
        services.AddSingleton<Shiny.Health.IHealthService>(sp => sp.GetRequiredService<Shiny.Health.HealthService>());
#if ANDROID
        services.AddSingleton<Shiny.Hosting.IAndroidLifecycle.IOnActivityResult>(
            sp => sp.GetRequiredService<Shiny.Health.HealthService>()
        );
#endif
#endif
        return services;
    }
}
