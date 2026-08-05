using Android.App;
using Android.Content.PM;

namespace Sample;


[Activity(
    // Health Connect's permission usage activity-alias targets this by name
    Name = "org.shiny.healthsample.MainActivity",
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density
)]
// Health Connect shows this when the user taps through to your privacy policy - required or it
// will refuse to grant health permissions
[IntentFilter(["androidx.health.ACTION_SHOW_PERMISSIONS_RATIONALE"])]
public class MainActivity : MauiAppCompatActivity
{
}

