# Shiny Health

Apple HealthKit and Android Health Connect for your .NET MAUI apps.

## Features
* Read summary values between timestamps at specified intervals
* Write health data to Apple HealthKit and Android Health Connect
* Real-time observation of health data changes via `IAsyncEnumerable<HealthResult>`
* 30+ cross-platform data types covering activity, body, vitals, nutrition, reproductive/cycle tracking, and workouts
* Query activity (distance, step count, calories, active/basal energy, floors climbed, wheelchair pushes, speed, power) and heart rate (average, resting, variability)
* Query body metrics (weight, height, body fat, lean body mass) and vitals (blood pressure, oxygen saturation, blood glucose, body temperature, respiratory rate, VO2 max)
* Query lifestyle (sleep duration, hydration, nutrition) and workouts/exercise sessions
* Reproductive & cycle tracking (menstruation flow, sexual activity, ovulation tests, cervical mucus, intermenstrual bleeding)
* Permission management for both platforms with read/write support

## How To Use

```csharp
IHealthService health; // inject via DI

// is the platform health store available at all? (Android: Health Connect installed & up to date)
if (!health.IsAvailable)
    return;

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

// the user can grant a subset, so only query what was actually granted - on Android a query for an
// ungranted DataType throws (SecurityException) rather than returning an empty list
var granted = result.Where(x => x.Success).Select(x => x.Type).ToHashSet();

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

// --- Observing Real-Time Changes ---

// observe heart rate changes as they arrive
using var cts = new CancellationTokenSource();

await foreach (var result in health.Observe(DataType.HeartRate, cancelToken: cts.Token))
{
    if (result is NumericHealthResult numeric)
        Console.WriteLine($"Heart rate: {numeric.Value} bpm at {numeric.Start:T}");
}

// observe with custom polling interval (Android only, iOS ignores this - it's push-based)
await foreach (var result in health.Observe(DataType.StepCount, pollingInterval: TimeSpan.FromSeconds(10), cancelToken: cts.Token))
{
    if (result is NumericHealthResult numeric)
        Console.WriteLine($"Steps: {numeric.Value}");
}
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
| Blood Glucose | mg/dL | BloodGlucose | BloodGlucoseRecord |
| Body Temperature | °C | BodyTemperature | BodyTemperatureRecord |
| Basal Body Temperature | °C | BasalBodyTemperature | BasalBodyTemperatureRecord |
| Respiratory Rate | breaths/min | RespiratoryRate | RespiratoryRateRecord |
| VO2 Max | mL/kg/min | VO2Max | Vo2MaxRecord |
| Heart Rate Variability | ms | HeartRateVariabilitySDNN | HeartRateVariabilityRmssdRecord¹ |
| Lean Body Mass | kg | LeanBodyMass | LeanBodyMassRecord |
| Basal Energy Burned | kcal | BasalEnergyBurned | BasalMetabolicRateRecord |
| Active Energy Burned | kcal | ActiveEnergyBurned | ActiveCaloriesBurnedRecord |
| Floors Climbed | count | FlightsClimbed | FloorsClimbedRecord |
| Wheelchair Pushes | count | PushCount | WheelchairPushesRecord |
| Speed | m/s | WalkingSpeed² | SpeedRecord |
| Power | watts | CyclingPower² | PowerRecord |
| Menstruation Flow | flow level | MenstrualFlow | MenstruationFlowRecord |
| Sexual Activity | protection enum | SexualActivity | SexualActivityRecord |
| Ovulation Test | result enum | OvulationTestResult | OvulationTestRecord |
| Cervical Mucus | appearance enum | CervicalMucusQuality | CervicalMucusRecord |
| Intermenstrual Bleeding | event | IntermenstrualBleeding | IntermenstrualBleedingRecord |
| Workout | session | HKWorkout | ExerciseSessionRecord |
| Nutrition | food/macros | Food correlation | NutritionRecord |

> ¹ HealthKit reports HRV as **SDNN** while Health Connect reports **RMSSD** — both are in milliseconds but computed differently, so values are not directly comparable across platforms.
>
> ² Health Connect's `SpeedRecord`/`PowerRecord` are generic. HealthKit has no generic equivalents, so `Speed` maps to walking speed and `Power` maps to cycling power.

> **Categorical / event-based / structured metrics** (menstruation flow, sexual activity, ovulation tests, cervical mucus, intermenstrual bleeding, workouts, nutrition) are not numeric. Each uses its own result record (e.g. `MenstruationFlowResult`, `SexualActivityResult`, `WorkoutResult`, `NutritionResult`), has **no `Interval` bucketing**, and is read via a dedicated method (`GetMenstruationFlow`, `GetSexualActivity`, `GetOvulationTests`, `GetCervicalMucus`, `GetIntermenstrualBleeding`, `GetWorkouts`, `GetNutrition`). The `MenstrualFlow.None` level and `IsCycleStart` flag are iOS-only; Health Connect has no `None` value and ignores `IsCycleStart`. A `WorkoutResult`'s energy/distance are `null` on Android read (Health Connect stores them as separate records from the exercise session).

## AI Tools

[![NuGet](https://img.shields.io/nuget/v/Shiny.Health.Extensions.AI.svg?label=AI+Extensions)](https://www.nuget.org/packages/Shiny.Health.Extensions.AI/)

`Shiny.Health.Extensions.AI` exposes `IHealthService` as [`Microsoft.Extensions.AI`](https://learn.microsoft.com/dotnet/ai/) tool functions for LLM agents. It uses a few **parameterized** tools (one read tool covers all numeric metrics via a `metric` enum, instead of one tool per metric) so the model's tool list stays short. Opt-in exactly which areas the model can see — read-only by default, write per-area. Resolve `HealthAITools` from DI and pass `.Tools` to any `IChatClient`. AOT-compatible.

```bash
dotnet add package Shiny.Health.Extensions.AI
```

```csharp
using Shiny.Health.Extensions.AI;

builder.Services.AddHealthIntegration();
builder.Services.AddHealthAITools(tools => tools
    .AddAllMetrics()                                          // read every numeric metric
    .AddMetric(DataType.Weight, HealthAICapabilities.ReadWrite)
    .AddBloodPressure(HealthAICapabilities.ReadWrite)
    .AddCycleTracking()
    .AddWorkouts(HealthAICapabilities.ReadWrite)
    .AddNutrition()
);

// later, hand the tools to a chat client
var tools = sp.GetRequiredService<HealthAITools>().Tools;
var response = await chatClient.GetResponseAsync(
    messages,
    new ChatOptions { Tools = [.. tools] }
);
```

Generated tools (only for areas you opt-in to): `get_health_metric` / `write_health_metric`, `get_blood_pressure` / `write_blood_pressure`, `get_cycle_records` / `write_menstruation_flow`, `get_workouts` / `write_workout`, `get_nutrition` / `write_nutrition`. The tools assume permissions are already granted — call `RequestPermissions` from your app first.

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
<!-- Required: declare which health data your app reads (only include the types you use) -->
<uses-permission android:name="android.permission.health.READ_STEPS" />
<uses-permission android:name="android.permission.health.READ_HEART_RATE" />
<uses-permission android:name="android.permission.health.READ_TOTAL_CALORIES_BURNED" />
<uses-permission android:name="android.permission.health.READ_DISTANCE" />
<uses-permission android:name="android.permission.health.READ_WEIGHT" />
<uses-permission android:name="android.permission.health.READ_HEIGHT" />
<uses-permission android:name="android.permission.health.READ_BODY_FAT" />
<uses-permission android:name="android.permission.health.READ_RESTING_HEART_RATE" />
<uses-permission android:name="android.permission.health.READ_BLOOD_PRESSURE" />
<uses-permission android:name="android.permission.health.READ_OXYGEN_SATURATION" />
<uses-permission android:name="android.permission.health.READ_SLEEP" />
<uses-permission android:name="android.permission.health.READ_HYDRATION" />
<uses-permission android:name="android.permission.health.READ_MENSTRUATION" />
<uses-permission android:name="android.permission.health.READ_BLOOD_GLUCOSE" />
<uses-permission android:name="android.permission.health.READ_BODY_TEMPERATURE" />
<uses-permission android:name="android.permission.health.READ_BASAL_BODY_TEMPERATURE" />
<uses-permission android:name="android.permission.health.READ_RESPIRATORY_RATE" />
<uses-permission android:name="android.permission.health.READ_VO2_MAX" />
<uses-permission android:name="android.permission.health.READ_HEART_RATE_VARIABILITY" />
<uses-permission android:name="android.permission.health.READ_LEAN_BODY_MASS" />
<uses-permission android:name="android.permission.health.READ_BASAL_METABOLIC_RATE" />
<uses-permission android:name="android.permission.health.READ_ACTIVE_CALORIES_BURNED" />
<uses-permission android:name="android.permission.health.READ_FLOORS_CLIMBED" />
<uses-permission android:name="android.permission.health.READ_WHEELCHAIR_PUSHES" />
<uses-permission android:name="android.permission.health.READ_SPEED" />
<uses-permission android:name="android.permission.health.READ_POWER" />
<uses-permission android:name="android.permission.health.READ_SEXUAL_ACTIVITY" />
<uses-permission android:name="android.permission.health.READ_OVULATION_TEST" />
<uses-permission android:name="android.permission.health.READ_CERVICAL_MUCUS" />
<uses-permission android:name="android.permission.health.READ_INTERMENSTRUAL_BLEEDING" />
<uses-permission android:name="android.permission.health.READ_EXERCISE" />
<uses-permission android:name="android.permission.health.READ_NUTRITION" />
<uses-permission android:name="android.permission.ACTIVITY_RECOGNITION" />

<!-- Optional: declare which health data your app writes (only include the types you use) -->
<uses-permission android:name="android.permission.health.WRITE_STEPS" />
<uses-permission android:name="android.permission.health.WRITE_HEART_RATE" />
<uses-permission android:name="android.permission.health.WRITE_TOTAL_CALORIES_BURNED" />
<uses-permission android:name="android.permission.health.WRITE_DISTANCE" />
<uses-permission android:name="android.permission.health.WRITE_WEIGHT" />
<uses-permission android:name="android.permission.health.WRITE_HEIGHT" />
<uses-permission android:name="android.permission.health.WRITE_BODY_FAT" />
<uses-permission android:name="android.permission.health.WRITE_RESTING_HEART_RATE" />
<uses-permission android:name="android.permission.health.WRITE_BLOOD_PRESSURE" />
<uses-permission android:name="android.permission.health.WRITE_OXYGEN_SATURATION" />
<uses-permission android:name="android.permission.health.WRITE_SLEEP" />
<uses-permission android:name="android.permission.health.WRITE_HYDRATION" />
<uses-permission android:name="android.permission.health.WRITE_MENSTRUATION" />
<uses-permission android:name="android.permission.health.WRITE_BLOOD_GLUCOSE" />
<uses-permission android:name="android.permission.health.WRITE_BODY_TEMPERATURE" />
<uses-permission android:name="android.permission.health.WRITE_BASAL_BODY_TEMPERATURE" />
<uses-permission android:name="android.permission.health.WRITE_RESPIRATORY_RATE" />
<uses-permission android:name="android.permission.health.WRITE_VO2_MAX" />
<uses-permission android:name="android.permission.health.WRITE_HEART_RATE_VARIABILITY" />
<uses-permission android:name="android.permission.health.WRITE_LEAN_BODY_MASS" />
<uses-permission android:name="android.permission.health.WRITE_BASAL_METABOLIC_RATE" />
<uses-permission android:name="android.permission.health.WRITE_ACTIVE_CALORIES_BURNED" />
<uses-permission android:name="android.permission.health.WRITE_FLOORS_CLIMBED" />
<uses-permission android:name="android.permission.health.WRITE_WHEELCHAIR_PUSHES" />
<uses-permission android:name="android.permission.health.WRITE_SPEED" />
<uses-permission android:name="android.permission.health.WRITE_POWER" />
<uses-permission android:name="android.permission.health.WRITE_SEXUAL_ACTIVITY" />
<uses-permission android:name="android.permission.health.WRITE_OVULATION_TEST" />
<uses-permission android:name="android.permission.health.WRITE_CERVICAL_MUCUS" />
<uses-permission android:name="android.permission.health.WRITE_INTERMENSTRUAL_BLEEDING" />
<uses-permission android:name="android.permission.health.WRITE_EXERCISE" />
<uses-permission android:name="android.permission.health.WRITE_NUTRITION" />

<!-- Required: allow your app to discover Health Connect -->
<queries>
    <package android:name="com.google.android.apps.healthdata" />
</queries>

<!-- Required on Android 14+ (API 34), where Health Connect is part of the platform.  Without this
     alias Health Connect refuses to grant health permissions.  android:targetActivity must point at
     your MainActivity's android:name -->
<application>
    <activity-alias
        android:name="ViewPermissionUsageActivity"
        android:exported="true"
        android:targetActivity="com.companyname.myapp.MainActivity"
        android:permission="android.permission.START_VIEW_PERMISSION_USAGE">
        <intent-filter>
            <action android:name="android.intent.action.VIEW_PERMISSION_USAGE" />
            <category android:name="android.intent.category.HEALTH_PERMISSIONS" />
        </intent-filter>
    </activity-alias>
</application>
```

#### MainActivity.cs

Health Connect links to your privacy policy from the permission dialog and will not grant
permissions unless your activity handles the rationale action. Give `MainActivity` an explicit
`Name` so the `activity-alias` above can target it:

```csharp
[Activity(
    Name = "com.companyname.myapp.MainActivity",
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density
)]
[IntentFilter(["androidx.health.ACTION_SHOW_PERMISSIONS_RATIONALE"])]
public class MainActivity : MauiAppCompatActivity
{
}
```

#### Requirements

* The [Health Connect](https://play.google.com/store/apps/details?id=com.google.android.apps.healthdata) app must be installed on the device — on Android 14+ (API 34) it is built into the platform
* Minimum SDK version must be set to **28** (Android 9)
* Set `targetSdkVersion` to match the platform you build against — an empty `<uses-sdk />` in your manifest suppresses it and falls back to `minSdkVersion`, which recent Android versions block at install time ("Unsafe app blocked")
* Permission names follow the **Health Connect record**, not the HealthKit type — `DataType.Calories` reads `TotalCaloriesBurnedRecord` and needs `READ_TOTAL_CALORIES_BURNED`. A permission Android does not recognize is silently unknown: it can never be granted and every read of that type fails
