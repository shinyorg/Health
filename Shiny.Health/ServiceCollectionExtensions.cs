using Microsoft.Extensions.DependencyInjection;

namespace Shiny;


/// <summary>
/// Dependency-injection extensions for registering Shiny.Health.
/// </summary>
public static class HealthServiceCollectionExtensions
{
    /// <summary>
    /// Registers the cross-platform <see cref="Shiny.Health.IHealthService"/> implementation backed by
    /// Apple HealthKit (iOS) and Android Health Connect. On Android it also wires up the activity-result
    /// lifecycle hook used to complete the Health Connect permission flow. No-op on unsupported platforms.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
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
