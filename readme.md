# Shiny Health

Apple Health and Google Fit for your .NET7+ .NET Mobile apps

## Features
* Read summary values between timestamps and specified interval
* Query distance, step count, calory intake, & heart rate
* TODO: Write values

## How To Use

```csharp
IHealthService health; // inject, resolve, etc

// request permissions
var result = await health.RequestPermission(
    new Permission(DistanceHealthMetric.Default, PermissionType.Read),
    new Permission(CaloriesHealthMetric.Default, PermissionType.Read),
    new Permission(StepCountHealthMetric.Default, PermissionType.Read),
    new Permission(HeartRateHealthMetric.Default, PermissionType.Read)
);
if (!result)
{
    // say something useful
}

var end = DateTimeOffset.Now;
var start = DateTimeOffset.Now.AddDays(-1);

// now run your queries
var distance = (await health.Query(DistanceHealthMetric.Default, start, end, Interval.Days)).Sum(x => x.Value);
var calories = (await health.Query(CaloriesHealthMetric.Default, start, end, Interval.Days)).Sum(x => x.Value);
var steps = (await health.Query(StepCountHealthMetric.Default, start, end, Interval.Days)).Sum(x => x.Value);
var heartRate = (await health.Query(HeartRateHealthMetric.Default, start, end, Interval.Days)).Average(x => x.Value);
```

## Setup

### MAUI

Install Shiny.Health from [![NuGet](https://img.shields.io/nuget/v/Shiny.Health.svg?maxAge=2592000)](https://www.nuget.org/packages/Shiny.Health/)

Now, in your MauiProgram.cs, add:

```cshar

public static MauiApp CreateMauiApp()
{
    var builder = MauiApp
        .CreateBuilder()
        .UseShiny()
        .UseMauiApp<App>()
        .ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold"); 
        });

    builder.Services.AddHealthIntegration();
    return builder.Build();
}
```

### iOS

To use iOS (and therefore the sample), you need to have a provisioning profile with all
of the necessary Apple Health setup for your application.

Take a look at the Xamarin docs for [more info](https://learn.microsoft.com/en-us/xamarin/ios/platform/healthkit)

Add the following:

#### Info.plist

```xml
<key>UIRequiredDeviceCapabilities</key>
<array>
        <string>healthkithealthkit</string>
</array>
<key>NSHealthUpdateUsageDescription</key>
<string>We need to say something useful here</string>
<key>NSHealthShareUsageDescription</key>
<string>We need to say something useful here</string>
```

#### Entitlements.plist
```xml
<key>com.apple.developer.healthkit</key>
<true />
<key>com.apple.developer.healthkit.background-delivery</key>
<true />
```

#### Google Fit

[Official Android Documentation](https://developer.android.com/guide/health-and-fitness/health-connect)
