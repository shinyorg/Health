using Microsoft.Extensions.DependencyInjection;
using Shiny.Health.Extensions.AI.Internal;

namespace Shiny.Health.Extensions.AI;

/// <summary>
/// Dependency-injection extensions for exposing <see cref="IHealthService"/> as LLM tools.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="HealthAITools"/> singleton whose tools wrap <see cref="IHealthService"/>
    /// for the areas you opt-in to. Requires <c>AddHealthIntegration()</c> to have registered
    /// <see cref="IHealthService"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Builder callback used to opt-in health areas and capabilities.</param>
    /// <remarks>
    /// The generated tools assume the relevant health permissions are already granted — they do not
    /// trigger the platform permission UI (which needs a foreground activity). Call
    /// <c>IHealthService.RequestPermissions</c> from your app before invoking the agent.
    /// </remarks>
    public static IServiceCollection AddHealthAITools(
        this IServiceCollection services,
        Action<IHealthAIToolBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new HealthAIToolBuilder();
        configure(builder);

        if (builder.IsEmpty)
            throw new InvalidOperationException(
                "AddHealthAITools requires at least one Add… call. " +
                "An empty registration would expose no tools to the LLM.");

        services.AddSingleton(sp =>
        {
            var health = sp.GetRequiredService<IHealthService>();
            var tools = HealthAIFunctionFactory.Build(health, builder);
            return new HealthAITools(tools);
        });

        return services;
    }
}
