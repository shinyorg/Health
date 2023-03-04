using AiForms.Settings;
using CommunityToolkit.Maui;
using Prism.DryIoc;

namespace Sample;


public static class MauiProgram
{
    public static MauiApp CreateMauiApp() => MauiApp
        .CreateBuilder()
        .UseMauiApp<App>()
        .UseMauiCommunityToolkit()
        .ConfigureMauiHandlers(handlers =>
            handlers.AddSettingsViewHandler()
        )
        .UseShinyFramework(
            new DryIocContainerExtension(),
            prism => prism.OnAppStart("NavigationPage/HealthTestPage")
        )
        .ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold"); 
        })
        .RegisterInfrastructure()
        .RegisterViews()
        .Build();


    static MauiAppBuilder RegisterInfrastructure(this MauiAppBuilder builder)
    {
#if DEBUG
        builder.Logging.AddConsole();
#endif
        builder.Services.AddSingleton(DeviceDisplay.Current);
        builder.Services.AddHealthIntegration();
        builder.Services.AddGlobalCommandExceptionHandler(new(
#if DEBUG
            ErrorAlertType.FullError
#else
            ErrorAlertType.NoLocalize
#endif
        ));
        return builder;
    }


    static MauiAppBuilder RegisterViews(this MauiAppBuilder builder)
    {
        builder.Services.RegisterForNavigation<HealthTestPage, HealthTestViewModel>();
        return builder;
    }
}
