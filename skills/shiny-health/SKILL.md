---
name: shiny-health
description: Generate cross-platform health data queries, write health data, and observe real-time health changes using Shiny Health for Apple HealthKit and Android Health Connect
auto_invoke: true
triggers:
  - health data
  - health kit
  - healthkit
  - health connect
  - step count
  - heart rate
  - calories
  - distance
  - weight
  - height
  - body fat
  - blood pressure
  - oxygen saturation
  - sleep duration
  - hydration
  - resting heart rate
  - health metrics
  - health permissions
  - IHealthService
  - DataType
  - NumericHealthResult
  - BloodPressureResult
  - HealthResult
  - GetStepCounts
  - GetCalories
  - GetDistances
  - GetAverageHeartRate
  - GetWeight
  - GetHeight
  - GetBodyFatPercentage
  - GetRestingHeartRate
  - GetBloodPressure
  - GetOxygenSaturation
  - GetSleepDuration
  - GetHydration
  - GetMenstruationFlow
  - menstruation
  - menstrual flow
  - period tracking
  - cycle tracking
  - MenstruationFlowResult
  - MenstrualFlow
  - RequestPermissions
  - PermissionType
  - IsAvailable
  - health capability
  - write health
  - write steps
  - write weight
  - write calories
  - log health
  - save health
  - record health
  - Observe
  - observe health
  - monitor health
  - watch health
  - real-time health
  - health changes
  - AddHealthIntegration
  - Shiny.Health
  - blood glucose
  - body temperature
  - basal body temperature
  - respiratory rate
  - vo2 max
  - heart rate variability
  - HRV
  - lean body mass
  - basal energy
  - active energy
  - floors climbed
  - wheelchair pushes
  - speed
  - power
  - sexual activity
  - ovulation test
  - cervical mucus
  - intermenstrual bleeding
  - workout
  - exercise session
  - nutrition
  - macros
  - GetBloodGlucose
  - GetBodyTemperature
  - GetBasalBodyTemperature
  - GetRespiratoryRate
  - GetVo2Max
  - GetHeartRateVariability
  - GetLeanBodyMass
  - GetBasalEnergyBurned
  - GetActiveEnergyBurned
  - GetFloorsClimbed
  - GetWheelchairPushes
  - GetSpeed
  - GetPower
  - GetSexualActivity
  - GetOvulationTests
  - GetCervicalMucus
  - GetIntermenstrualBleeding
  - GetWorkouts
  - GetNutrition
  - SexualActivityResult
  - OvulationTestResult
  - CervicalMucusResult
  - IntermenstrualBleedingResult
  - WorkoutResult
  - NutritionResult
  - WorkoutType
  - MealType
  - SexualActivityProtection
  - OvulationTestOutcome
  - CervicalMucusAppearance
  - AI tools
  - AI agent
  - LLM tools
  - tool calling
  - function calling
  - Microsoft.Extensions.AI
  - AIFunction
  - AITool
  - IChatClient
  - chat tools
  - Shiny.Health.Extensions.AI
  - AddHealthAITools
  - HealthAITools
  - IHealthAIToolBuilder
  - HealthAICapabilities
  - health agent
---

# Shiny Health Skill

You are an expert in Shiny Health, a .NET MAUI library that provides a unified API for reading and writing health data from Apple HealthKit (iOS) and Android Health Connect.

## When to Use This Skill

Invoke this skill when the user wants to:
- Query health metrics (steps, heart rate, calories, distance, weight, height, body fat, blood pressure, oxygen saturation, sleep, hydration)
- Write/log health data (steps, weight, hydration, blood pressure, etc.)
- Observe real-time health data changes (e.g., monitor step counts or heart rate as they are recorded)
- Set up health data access in a .NET MAUI application
- Request health data permissions (read and/or write) on iOS or Android
- Work with time-bucketed health data aggregations
- Understand which health metrics are available cross-platform
- Configure iOS HealthKit entitlements or Android Health Connect permissions

## Library Overview

**GitHub**: https://github.com/shinyorg/health
**NuGet**: `Shiny.Health`
**Namespace**: `Shiny.Health`

Shiny Health provides:
- A single `IHealthService` interface that works on both iOS and Android
- Read and write support for 30+ cross-platform health metrics spanning activity, body, vitals,
  nutrition, reproductive/cycle tracking, and workouts
- Real-time observation of health data changes via `IAsyncEnumerable<HealthResult>`
- Time-bucketed aggregate queries at minute, hour, or day intervals
- Permission management with read/write granularity via `PermissionType`
- AOT-compatible implementation (no .NET reflection)

## Setup

### 1. Install NuGet Package
```bash
dotnet add package Shiny.Health
```

### 2. Configure in MauiProgram.cs
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

### 3. iOS Setup

Your app requires a provisioning profile with HealthKit capabilities enabled.

**Info.plist:**
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

**Entitlements.plist:**
```xml
<key>com.apple.developer.healthkit</key>
<true />
<key>com.apple.developer.healthkit.background-delivery</key>
<true />
```

### 4. Android Setup (Health Connect)

Android uses Health Connect (the replacement for the deprecated Google Fit API). Health Connect requires Android 9 (API 28) or higher.

**AndroidManifest.xml:**
```xml
<!-- Declare which health data your app reads -->
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
<uses-permission android:name="android.permission.health.READ_MENSTRUATION" />
<uses-permission android:name="android.permission.ACTIVITY_RECOGNITION" />

<!-- Optional: declare which health data your app writes (only include the types you need) -->
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
<uses-permission android:name="android.permission.health.WRITE_MENSTRUATION" />

<!-- Allow your app to discover Health Connect -->
<queries>
    <package android:name="com.google.android.apps.healthdata" />
</queries>
```

**Requirements:**
- The Health Connect app must be installed on the device
- Minimum SDK version must be set to **28** (Android 9)

## API Reference

### Core Types

```csharp
// Permission type for read/write access
[Flags]
public enum PermissionType
{
    Read = 1,
    Write = 2,
    ReadWrite = Read | Write
}

// Time interval for bucketed queries
public enum Interval { Minutes, Hours, Days }

// Available health data types
public enum DataType
{
    // numeric (NumericHealthResult)
    StepCount, HeartRate, Calories, Distance,
    Weight, Height, BodyFatPercentage, RestingHeartRate,
    BloodPressure, OxygenSaturation, SleepDuration, Hydration,
    BloodGlucose, BodyTemperature, BasalBodyTemperature, RespiratoryRate,
    Vo2Max, HeartRateVariability, LeanBodyMass, BasalEnergyBurned,
    ActiveEnergyBurned, FloorsClimbed, WheelchairPushes, Speed, Power,

    // categorical / event-based
    MenstruationFlow, SexualActivity, OvulationTest, CervicalMucus, IntermenstrualBleeding,

    // structured records
    Workout, Nutrition
}

// Menstrual flow level (categorical). None is iOS-only; Android maps it to Unspecified.
public enum MenstrualFlow { Unspecified, None, Light, Medium, Heavy }

// Reproductive / cycle-tracking enums
public enum SexualActivityProtection { Unspecified, Protected, Unprotected }
public enum OvulationTestOutcome { Inconclusive, Positive, High, Negative }
public enum CervicalMucusAppearance { Unspecified, Dry, Sticky, Creamy, Watery, EggWhite }

// Workout activity (subset mapped on both platforms; unmapped -> Other) and meal type
public enum WorkoutType { Other, Running, Walking, Hiking, Cycling, Swimming, Rowing, Elliptical,
    StairClimbing, StrengthTraining, HighIntensityIntervalTraining, Yoga, Pilates, Tennis,
    Basketball, Soccer, Baseball, Golf, Boxing, MartialArts, Dancing }
public enum MealType { Unknown, Breakfast, Lunch, Dinner, Snack }

// Categorical / structured result records
public record SexualActivityResult(DateTimeOffset Start, DateTimeOffset End, SexualActivityProtection Protection) : HealthResult(DataType.SexualActivity, Start, End);
public record OvulationTestResult(DateTimeOffset Start, DateTimeOffset End, OvulationTestOutcome Outcome) : HealthResult(DataType.OvulationTest, Start, End);
public record CervicalMucusResult(DateTimeOffset Start, DateTimeOffset End, CervicalMucusAppearance Appearance) : HealthResult(DataType.CervicalMucus, Start, End);
public record IntermenstrualBleedingResult(DateTimeOffset Start, DateTimeOffset End) : HealthResult(DataType.IntermenstrualBleeding, Start, End);
public record WorkoutResult(DateTimeOffset Start, DateTimeOffset End, WorkoutType Workout, double? TotalEnergyKilocalories = null, double? TotalDistanceMeters = null, string? Title = null) : HealthResult(DataType.Workout, Start, End);
public record NutritionResult(DateTimeOffset Start, DateTimeOffset End, MealType Meal = MealType.Unknown, string? Name = null, double? EnergyKilocalories = null, double? ProteinGrams = null, double? CarbohydratesGrams = null, double? TotalFatGrams = null, double? FiberGrams = null, double? SugarGrams = null, double? SodiumGrams = null, double? CholesterolGrams = null) : HealthResult(DataType.Nutrition, Start, End);

// Result for single-value metrics
public record NumericHealthResult(
    DataType DataType,
    DateTimeOffset Start,
    DateTimeOffset End,
    double Value
) : HealthResult(DataType, Start, End);

// Result for blood pressure (dual-value)
public record BloodPressureResult(
    DateTimeOffset Start,
    DateTimeOffset End,
    double Systolic,
    double Diastolic
) : HealthResult(DataType.BloodPressure, Start, End);

// Result for menstruation flow (categorical, event-based)
// IsCycleStart marks the first day of the cycle (iOS only; always false on Android)
public record MenstruationFlowResult(
    DateTimeOffset Start,
    DateTimeOffset End,
    MenstrualFlow Flow,
    bool IsCycleStart = false
) : HealthResult(DataType.MenstruationFlow, Start, End);
```

### IHealthService Interface

```csharp
public interface IHealthService
{
    // True if the platform health store is usable (iOS: always; Android: Health Connect installed & SDK OK)
    bool IsAvailable { get; }

    // Observe real-time health data changes (forward-only, yields new samples as recorded)
    // iOS: push-based HKAnchoredObjectQuery; Android: polls Health Connect change tokens
    IAsyncEnumerable<HealthResult> Observe(DataType dataType, TimeSpan? pollingInterval = null, CancellationToken cancelToken = default);

    // Request read permissions (backward compatible)
    Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params DataType[] dataTypes);
    // Request read, write, or both permissions (uniform for all types)
    Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(PermissionType permissionType, params DataType[] dataTypes);
    // Request per-metric read/write permissions in a single call
    Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params (PermissionType Permission, DataType Type)[] permissions);

    // Write health data
    Task Write(NumericHealthResult result, CancellationToken cancelToken = default);
    Task Write(BloodPressureResult result, CancellationToken cancelToken = default);
    Task Write(MenstruationFlowResult result, CancellationToken cancelToken = default);

    // Activity metrics
    Task<IList<NumericHealthResult>> GetStepCounts(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default);
    Task<IList<NumericHealthResult>> GetAverageHeartRate(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default);
    Task<IList<NumericHealthResult>> GetCalories(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default);
    Task<IList<NumericHealthResult>> GetDistances(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default);

    // Body metrics
    Task<IList<NumericHealthResult>> GetWeight(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default);
    Task<IList<NumericHealthResult>> GetHeight(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default);
    Task<IList<NumericHealthResult>> GetBodyFatPercentage(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default);
    Task<IList<NumericHealthResult>> GetRestingHeartRate(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default);

    // Vitals
    Task<IList<BloodPressureResult>> GetBloodPressure(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default);
    Task<IList<NumericHealthResult>> GetOxygenSaturation(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default);

    // Lifestyle
    Task<IList<NumericHealthResult>> GetSleepDuration(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default);
    Task<IList<NumericHealthResult>> GetHydration(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default);

    // Menstruation flow is categorical/event-based - no interval bucketing, returns individual records
    Task<IList<MenstruationFlowResult>> GetMenstruationFlow(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default);
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
| Sleep Duration | hours | SleepAnalysis (category) | SleepSessionRecord |
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
| Menstruation Flow | flow level | MenstrualFlow (category) | MenstruationFlowRecord |
| Sexual Activity | protection enum | SexualActivity (category) | SexualActivityRecord |
| Ovulation Test | result enum | OvulationTestResult (category) | OvulationTestRecord |
| Cervical Mucus | appearance enum | CervicalMucusQuality (category) | CervicalMucusRecord |
| Intermenstrual Bleeding | event | IntermenstrualBleeding (category) | IntermenstrualBleedingRecord |
| Workout | session | HKWorkout | ExerciseSessionRecord |
| Nutrition | food/macros | Food correlation (dietary types) | NutritionRecord |

> ¹ **HRV caveat**: iOS reports SDNN, Health Connect reports RMSSD - both are HRV in milliseconds but computed differently, so the values are not directly comparable across platforms.
> ² **Speed/Power caveat**: Health Connect's `SpeedRecord`/`PowerRecord` are generic; iOS has no generic equivalents, so Speed maps to walking speed and Power maps to cycling power.

> **The categorical, event-based, and structured metrics differ from the numeric ones**: they use their own
> result records (`SexualActivityResult`, `OvulationTestResult`, `CervicalMucusResult`, `IntermenstrualBleedingResult`,
> `WorkoutResult`, `NutritionResult`, plus `MenstruationFlowResult`), have **no `Interval` bucketing**, and are
> read via dedicated methods (`GetSexualActivity`, `GetOvulationTests`, `GetCervicalMucus`,
> `GetIntermenstrualBleeding`, `GetWorkouts`, `GetNutrition`). On Android, a `WorkoutResult`'s energy/distance are
> `null` on read (Health Connect stores those as separate records from the exercise session).

> **Menstruation flow is different from the other numeric metrics**: it is categorical (a `MenstrualFlow` level, not a `double`) and event-based, so it uses `MenstruationFlowResult`, has no `Interval` bucketing, and is read via `GetMenstruationFlow(start, end)`. iOS exposes a `None` level and an `IsCycleStart` flag (persisted via HealthKit cycle metadata); Health Connect has no `None` value (mapped to `Unspecified`) and ignores `IsCycleStart`.

## Usage Examples

### Request Permissions and Query Data
```csharp
IHealthService health; // inject via DI

// Request read permissions for the data types you need
var result = await health.RequestPermissions(
    DataType.StepCount,
    DataType.HeartRate,
    DataType.Calories,
    DataType.Distance
);

// Or request per-metric read/write permissions in a single call
var result2 = await health.RequestPermissions(
    (PermissionType.Read, DataType.StepCount),
    (PermissionType.Read, DataType.HeartRate),
    (PermissionType.Write, DataType.Weight),
    (PermissionType.ReadWrite, DataType.BloodPressure)
);

// Check which permissions were granted
foreach (var (type, success) in result)
{
    if (!success)
        Console.WriteLine($"Permission denied for {type}");
}

// Query data for the last 24 hours, bucketed by day
var end = DateTimeOffset.Now;
var start = end.AddDays(-1);

var steps = (await health.GetStepCounts(start, end, Interval.Days)).Sum(x => x.Value);
var calories = (await health.GetCalories(start, end, Interval.Days)).Sum(x => x.Value);
var distance = (await health.GetDistances(start, end, Interval.Days)).Sum(x => x.Value);
var heartRate = (await health.GetAverageHeartRate(start, end, Interval.Days)).Average(x => x.Value);
```

### Query Body Metrics
```csharp
var weight = (await health.GetWeight(start, end, Interval.Days)).Average(x => x.Value); // kg
var height = (await health.GetHeight(start, end, Interval.Days)).Average(x => x.Value); // meters
var bodyFat = (await health.GetBodyFatPercentage(start, end, Interval.Days)).Average(x => x.Value); // %
var restingHr = (await health.GetRestingHeartRate(start, end, Interval.Days)).Average(x => x.Value); // bpm
```

### Query Vitals
```csharp
// Blood pressure returns BloodPressureResult with Systolic and Diastolic
var bp = await health.GetBloodPressure(start, end, Interval.Days);
if (bp.Any())
{
    var avgSystolic = bp.Average(x => x.Systolic);   // mmHg
    var avgDiastolic = bp.Average(x => x.Diastolic);  // mmHg
}

var o2 = (await health.GetOxygenSaturation(start, end, Interval.Days)).Average(x => x.Value); // %
```

### Query Lifestyle
```csharp
var sleep = (await health.GetSleepDuration(start, end, Interval.Days)).Sum(x => x.Value); // hours
var water = (await health.GetHydration(start, end, Interval.Days)).Sum(x => x.Value); // liters
```

### Hourly Breakdown
```csharp
// Get hourly step counts for the past week
var weekStart = DateTimeOffset.Now.AddDays(-7);
var weekEnd = DateTimeOffset.Now;

var hourlySteps = await health.GetStepCounts(weekStart, weekEnd, Interval.Hours);
foreach (var bucket in hourlySteps)
{
    Console.WriteLine($"{bucket.Start:g} - {bucket.End:g}: {bucket.Value:N0} steps");
}
```

### ViewModel Pattern (with CommunityToolkit.Mvvm)
```csharp
public partial class HealthDashboardViewModel(IHealthService health) : ObservableObject
{
    [ObservableProperty]
    double steps;

    [ObservableProperty]
    double calories;

    [RelayCommand]
    async Task LoadDataAsync()
    {
        await health.RequestPermissions(DataType.StepCount, DataType.Calories);

        var start = DateTimeOffset.Now.Date;
        var end = DateTimeOffset.Now;

        Steps = (await health.GetStepCounts(start, end, Interval.Days)).Sum(x => x.Value);
        Calories = (await health.GetCalories(start, end, Interval.Days)).Sum(x => x.Value);
    }
}
```

### Writing Health Data
```csharp
IHealthService health; // inject via DI

// Request write permissions for the data types you need
await health.RequestPermissions(PermissionType.Write, DataType.Weight, DataType.StepCount, DataType.Hydration);

// Or request both read and write at once
await health.RequestPermissions(PermissionType.ReadWrite, DataType.Weight);

// Or mix read/write per metric in a single call
await health.RequestPermissions(
    (PermissionType.Write, DataType.Weight),
    (PermissionType.Write, DataType.StepCount),
    (PermissionType.ReadWrite, DataType.Hydration)
);

var now = DateTimeOffset.Now;

// Write a weight measurement (point-in-time: Start == End)
await health.Write(new NumericHealthResult(DataType.Weight, now, now, 75.0)); // kg

// Write step counts over a time range
await health.Write(new NumericHealthResult(DataType.StepCount, now.AddMinutes(-30), now, 500));

// Write hydration
await health.Write(new NumericHealthResult(DataType.Hydration, now.AddHours(-1), now, 0.5)); // liters

// Write blood pressure
await health.Write(new BloodPressureResult(now, now, 120.0, 80.0)); // mmHg

// Write sleep session
var sleepStart = now.AddHours(-8);
await health.Write(new NumericHealthResult(DataType.SleepDuration, sleepStart, now, 0)); // Value is ignored, duration derived from Start/End
```

### Menstruation / Period Tracking
```csharp
IHealthService health; // inject via DI

// Request read+write access for menstruation flow
await health.RequestPermissions(PermissionType.ReadWrite, DataType.MenstruationFlow);

var today = DateTimeOffset.Now;

// Log today's flow. IsCycleStart: true marks the first day of the period (iOS persists this;
// Android ignores it - model the period span separately there if needed).
await health.Write(new MenstruationFlowResult(today, today, MenstrualFlow.Medium, IsCycleStart: true));
await health.Write(new MenstruationFlowResult(today.AddDays(1), today.AddDays(1), MenstrualFlow.Light));

// Read back the cycle's records (categorical + event-based, so NO Interval bucketing)
var records = await health.GetMenstruationFlow(today.AddMonths(-1), today);
foreach (var r in records)
    Console.WriteLine($"{r.Start:d}: {r.Flow}{(r.IsCycleStart ? " (cycle start)" : "")}");
```

### Observing Real-Time Health Data
```csharp
IHealthService health; // inject via DI

// Request read permission for the data type you want to observe
await health.RequestPermissions(DataType.StepCount);

// Observe step count changes in real time using IAsyncEnumerable
// Use a CancellationTokenSource to stop observation when done
using var cts = new CancellationTokenSource();

await foreach (var result in health.Observe(DataType.StepCount, cancelToken: cts.Token))
{
    // result is a HealthResult — cast to NumericHealthResult for value
    if (result is NumericHealthResult numeric)
        Console.WriteLine($"Steps: {numeric.Value} ({numeric.Start:t} - {numeric.End:t})");
}

// On Android, you can customize the polling interval (default 5s, ignored on iOS)
await foreach (var result in health.Observe(DataType.HeartRate, pollingInterval: TimeSpan.FromSeconds(10), cancelToken: cts.Token))
{
    if (result is NumericHealthResult numeric)
        Console.WriteLine($"Heart rate: {numeric.Value} bpm");
}
```

## Platform Notes

### iOS
- `Observe` uses `HKAnchoredObjectQuery` for push-based real-time updates (no polling needed)
- HealthKit requires a real device (not simulator) for most data types
- `RequestPermissions` on iOS does NOT tell you if the user denied access (Apple privacy policy) - it may return `true` even when denied
- Sleep data uses `HKCategoryTypeIdentifier.SleepAnalysis` (category type, not quantity type) - the library handles this internally
- Blood pressure requires permissions for both systolic and diastolic types - the library handles this automatically
- Percentage values (body fat, O2 saturation) are returned as 0-100, not 0-1

### Android
- `Observe` uses Health Connect change tokens with polling (default 5s interval, configurable via `pollingInterval` parameter)
- The Health Connect app must be installed on the device
- Body fat percentage and oxygen saturation use `ReadRecords` instead of aggregate queries (Health Connect does not provide aggregate metrics for these types)
- Sleep duration uses `SleepSessionRecord.SleepDurationTotal` aggregate metric, returning hours
- Blood pressure uses `BloodPressureRecord.SystolicAvg` and `DiastolicAvg` aggregate metrics
- All Kotlin coroutine interop is handled internally via `IContinuation` bridge (AOT-safe, no reflection)

## Best Practices

1. **Gate on availability** - Check `IsAvailable` (Android: Health Connect installed & up to date) before reading/writing
2. **Always request permissions first** - Call `RequestPermissions` before reading or writing data. Use `PermissionType.Write` or `PermissionType.ReadWrite` when writing
2. **Use appropriate intervals** - Use `Interval.Days` for summaries, `Interval.Hours` for detailed breakdowns
3. **Handle empty results** - Check `.Any()` before calling `.Average()` to avoid `InvalidOperationException`
4. **Use CancellationToken** - Pass cancellation tokens for long-running queries
5. **Sum vs Average** - Use `.Sum()` for cumulative metrics (steps, calories, distance, hydration, sleep) and `.Average()` for point-in-time metrics (heart rate, weight, height, body fat, O2 sat, resting HR)
6. **Blood pressure is special** - It returns `BloodPressureResult` (not `NumericHealthResult`) with separate `Systolic` and `Diastolic` values
7. **Menstruation flow is special** - It is categorical and event-based: use `MenstruationFlowResult`/`MenstrualFlow`, read with `GetMenstruationFlow(start, end)` (no `Interval`), and remember `None`/`IsCycleStart` are iOS-only
8. **Register early** - Call `AddHealthIntegration()` in `MauiProgram.cs` during app startup

## AI Tool Integration (Shiny.Health.Extensions.AI)

The optional `Shiny.Health.Extensions.AI` package exposes `IHealthService` as `Microsoft.Extensions.AI` tool functions (`AIFunction`s) for LLM agents. It uses a small set of **parameterized** tools (one read tool covers all numeric metrics via a `metric` enum arg, not one tool per metric). Read-only by default; write is opt-in per area. AOT-compatible (hand-built schemas, `JsonNode` results — no reflection).

```csharp
using Shiny.Health;
using Shiny.Health.Extensions.AI;

builder.Services.AddHealthIntegration();          // registers IHealthService
builder.Services.AddHealthAITools(tools => tools
    .AddAllMetrics()                                          // read all numeric metrics
    .AddMetric(DataType.Weight, HealthAICapabilities.ReadWrite)
    .AddBloodPressure(HealthAICapabilities.ReadWrite)
    .AddCycleTracking()                                       // read cycle records
    .AddWorkouts(HealthAICapabilities.ReadWrite)
    .AddNutrition()
);

// resolve the bundle and pass the tools to any IChatClient
var tools = sp.GetRequiredService<HealthAITools>().Tools;
var response = await chatClient.GetResponseAsync(
    messages,
    new ChatOptions { Tools = [.. tools] }
);
```

Key types:
- `AddHealthAITools(Action<IHealthAIToolBuilder>)` — DI extension; throws if nothing is added.
- `IHealthAIToolBuilder` — `AddMetric(DataType, capabilities)`, `AddAllMetrics(...)`, `AddBloodPressure(...)`, `AddCycleTracking(...)`, `AddWorkouts(...)`, `AddNutrition(...)`. `AddMetric` throws for non-numeric `DataType`s.
- `HealthAICapabilities` `[Flags]` — `None`, `Read` (default), `Write`, `ReadWrite`.
- `HealthAITools` — resolve from DI; `.Tools` is `IReadOnlyList<AITool>`.

Generated tools (only for opted-in areas; enum args constrained to what you allowed): `get_health_metric`, `write_health_metric`, `get_blood_pressure`, `write_blood_pressure`, `get_cycle_records` (`kind` enum), `write_menstruation_flow`, `get_workouts`, `write_workout`, `get_nutrition`, `write_nutrition`. Dates are ISO-8601; `interval` is `minutes`/`hours`/`days`.

> The AI tools assume permissions are already granted — they do **not** trigger the platform permission UI (needs a foreground activity). Call `IHealthService.RequestPermissions(...)` from the app before invoking the agent.

## Common Packages

```bash
dotnet add package Shiny.Health                 # Core health data library
dotnet add package Shiny.Health.Extensions.AI   # Optional: Microsoft.Extensions.AI tool surface for LLM agents
dotnet add package Shiny.Core                   # Required dependency
```
