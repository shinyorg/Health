# Shiny Health

Apple HealthKit and Android Health Connect for your .NET MAUI apps.

## Features
* Read summary values between timestamps at specified intervals
* Write health data to Apple HealthKit and Android Health Connect
* Query distance, step count, calories, and heart rate
* Query weight, height, body fat percentage, and resting heart rate
* Query blood pressure (systolic/diastolic), oxygen saturation, sleep duration, and hydration
* Permission management for both platforms with read/write support

## How To Use

```csharp
IHealthService health; // inject via DI

// request read permissions
var result = await health.RequestPermissions(
    DataType.Calories,
    DataType.Distance,
    DataType.StepCount,
    DataType.HeartRate
);

// request per-metric read/write permissions in a single call
var result2 = await health.RequestPermissions(
    (PermissionType.Read, DataType.StepCount),
    (PermissionType.Read, DataType.HeartRate),
    (PermissionType.Write, DataType.Weight),
    (PermissionType.ReadWrite, DataType.BloodPressure)
);

var end = DateTimeOffset.Now;
var start = DateTimeOffset.Now.AddDays(-1);

// query data
var distance = (await health.GetDistances(start, end, Interval.Days)).Sum(x => x.Value);
var calories = (await health.GetCalories(start, end, Interval.Days)).Sum(x => x.Value);
var steps = (await health.GetStepCounts(start, end, Interval.Days)).Sum(x => x.Value);
var heartRate = (await health.GetAverageHeartRate(start, end, Interval.Days)).Average(x => x.Value);

// body metrics
var weight = (await health.GetWeight(start, end, Interval.Days)).Average(x => x.Value); // kg
var height = (await health.GetHeight(start, end, Interval.Days)).Average(x => x.Value); // meters
var bodyFat = (await health.GetBodyFatPercentage(start, end, Interval.Days)).Average(x => x.Value); // %
var restingHr = (await health.GetRestingHeartRate(start, end, Interval.Days)).Average(x => x.Value); // bpm

// vitals
var o2 = (await health.GetOxygenSaturation(start, end, Interval.Days)).Average(x => x.Value); // %
var bp = await health.GetBloodPressure(start, end, Interval.Days); // BloodPressureResult with Systolic/Diastolic (mmHg)

// lifestyle
var sleep = (await health.GetSleepDuration(start, end, Interval.Days)).Sum(x => x.Value); // hours
var water = (await health.GetHydration(start, end, Interval.Days)).Sum(x => x.Value); // liters

// --- Writing Data ---

// request write permissions (uniform)
await health.RequestPermissions(PermissionType.Write, DataType.Weight, DataType.StepCount, DataType.Hydration);

// or mix read/write per metric
await health.RequestPermissions(
    (PermissionType.Write, DataType.Weight),
    (PermissionType.ReadWrite, DataType.Hydration)
);

// write a weight measurement
await health.Write(new NumericHealthResult(DataType.Weight, DateTimeOffset.Now, DateTimeOffset.Now, 75.0)); // kg

// write step counts over a time range
await health.Write(new NumericHealthResult(DataType.StepCount, start, end, 500));

// write hydration
await health.Write(new NumericHealthResult(DataType.Hydration, start, end, 0.5)); // liters

// write blood pressure
await health.Write(new BloodPressureResult(DateTimeOffset.Now, DateTimeOffset.Now, 120.0, 80.0)); // mmHg
```

## Supported Metrics

| Metric | Unit | iOS (HealthKit) | Android (Health Connect) |
|--------|------|-----------------|--------------------------|
| Step Count | count | StepCount | StepsRecord |
| Heart Rate | bpm | HeartRate | HeartRateRecord |
| Calories | kcal | ActiveEnergyBurned | TotalCaloriesBurnedRecord |
| Distance | meters | DistanceWalkingRunning | DistanceRecord |
| Weight | kg | BodyMass | WeightRecord |
| Height | meters | Height | HeightRecord |
| Body Fat % | % | BodyFatPercentage | BodyFatRecord |
| Resting Heart Rate | bpm | RestingHeartRate | RestingHeartRateRecord |
| Blood Pressure | mmHg | BloodPressureSystolic/Diastolic | BloodPressureRecord |
| Oxygen Saturation | % | OxygenSaturation | OxygenSaturationRecord |
| Sleep Duration | hours | SleepAnalysis | SleepSessionRecord |
| Hydration | liters | DietaryWater | HydrationRecord |

## Setup

Install from NuGet: [![NuGet](https://img.shields.io/nuget/v/Shiny.Health.svg?maxAge=2592000)](https://www.nuget.org/packages/Shiny.Health/)

```bash
dotnet add package Shiny.Health
```

In your `MauiProgram.cs`:

```csharp
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp
        .CreateBuilder()
        .UseMauiApp<App>()
        .UseShiny();

    builder.Services.AddHealthIntegration();
    return builder.Build();
}
```

### iOS Setup

Your app requires a provisioning profile with HealthKit capabilities enabled.

#### Info.plist

```xml
<key>UIRequiredDeviceCapabilities</key>
<array>
    <string>healthkit</string>
</array>
<key>NSHealthUpdateUsageDescription</key>
<string>We need access to update your health data</string>
<key>NSHealthShareUsageDescription</key>
<string>We need access to read your health data</string>
```

#### Entitlements.plist

```xml
<key>com.apple.developer.healthkit</key>
<true />
<key>com.apple.developer.healthkit.background-delivery</key>
<true />
```

### Android Setup (Health Connect)

Android uses [Health Connect](https://developer.android.com/health-and-fitness/health-connect) (the replacement for the deprecated Google Fit API). Health Connect requires Android 9 (API 28) or higher.

#### AndroidManifest.xml

```xml
<!-- Required: declare which health data your app reads/writes -->
<uses-permission android:name="android.permission.health.READ_STEPS" />
<uses-permission android:name="android.permission.health.READ_HEART_RATE" />
<uses-permission android:name="android.permission.health.READ_TOTAL_ENERGY_BURNED" />
<uses-permission android:name="android.permission.health.READ_DISTANCE" />
<uses-permission android:name="android.permission.health.READ_WEIGHT" />
<uses-permission android:name="android.permission.health.READ_HEIGHT" />
<uses-permission android:name="android.permission.health.READ_BODY_FAT" />
<uses-permission android:name="android.permission.health.READ_RESTING_HEART_RATE" />
<uses-permission android:name="android.permission.health.READ_BLOOD_PRESSURE" />
<uses-permission android:name="android.permission.health.READ_OXYGEN_SATURATION" />
<uses-permission android:name="android.permission.health.READ_SLEEP" />
<uses-permission android:name="android.permission.health.READ_HYDRATION" />
<uses-permission android:name="android.permission.ACTIVITY_RECOGNITION" />

<!-- Optional: declare which health data your app writes -->
<uses-permission android:name="android.permission.health.WRITE_STEPS" />
<uses-permission android:name="android.permission.health.WRITE_HEART_RATE" />
<uses-permission android:name="android.permission.health.WRITE_TOTAL_ENERGY_BURNED" />
<uses-permission android:name="android.permission.health.WRITE_DISTANCE" />
<uses-permission android:name="android.permission.health.WRITE_WEIGHT" />
<uses-permission android:name="android.permission.health.WRITE_HEIGHT" />
<uses-permission android:name="android.permission.health.WRITE_BODY_FAT" />
<uses-permission android:name="android.permission.health.WRITE_RESTING_HEART_RATE" />
<uses-permission android:name="android.permission.health.WRITE_BLOOD_PRESSURE" />
<uses-permission android:name="android.permission.health.WRITE_OXYGEN_SATURATION" />
<uses-permission android:name="android.permission.health.WRITE_SLEEP" />
<uses-permission android:name="android.permission.health.WRITE_HYDRATION" />

<!-- Required: allow your app to discover Health Connect -->
<queries>
    <package android:name="com.google.android.apps.healthdata" />
</queries>
```

#### Requirements

* The [Health Connect](https://play.google.com/store/apps/details?id=com.google.android.apps.healthdata) app must be installed on the device
* Minimum SDK version must be set to **28** (Android 9)
