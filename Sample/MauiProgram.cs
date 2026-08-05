using CommunityToolkit.Maui;
using Shiny.Health;
using Shiny.Health.Extensions.AI;
#if DEBUG
using Microsoft.Maui.DevFlow.Agent;
#endif

namespace Sample;


public static class MauiProgram
{
    public static MauiApp CreateMauiApp() => MauiApp
        .CreateBuilder()
        .UseMauiApp<App>()
        .UseMauiCommunityToolkit()
        .UseShiny()
        .UseShinyShell(x => x.AddGeneratedMaps())
        .ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        })
        .RegisterServices()
        .Build();


    static MauiAppBuilder RegisterServices(this MauiAppBuilder builder)
    {
#if DEBUG
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddDebug();
        builder.AddMauiDevFlowAgent();
#endif
        builder.Services.AddHealthIntegration();

        // Expose a curated, opt-in slice of the health store to an LLM as Microsoft.Extensions.AI
        // tools. Resolve HealthAITools from DI and pass its Tools to your IChatClient (ChatOptions.Tools).
        // Permissions are NOT requested by the tools - call IHealthService.RequestPermissions first.
        builder.Services.AddHealthAITools(tools => tools
            .AddMetric(DataType.StepCount)
            .AddMetric(DataType.HeartRate)
            .AddWorkouts()
            .AddBloodPressure()
            .AddNutrition(HealthAICapabilities.ReadWrite)
        );
        return builder;
    }
}
