---
name: shiny-health
description: Generate cross-platform health data queries and write health data using Shiny Health for Apple HealthKit and Android Health Connect
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
  - RequestPermissions
  - PermissionType
  - write health
  - write steps
  - write weight
  - write calories
  - log health
  - save health
  - record health
  - AddHealthIntegration
  - Shiny.Health
---

# Shiny Health Skill

You are an expert in Shiny Health, a .NET MAUI library that provides a unified API for reading and writing health data from Apple HealthKit (iOS) and Android Health Connect.

## When to Use This Skill

Invoke this skill when the user wants to:
- Query health metrics (steps, heart rate, calories, distance, weight, height, body fat, blood pressure, oxygen saturation, sleep, hydration)
- Write/log health data (steps, weight, hydration, blood pressure, etc.)
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
- Read and write support for all 12 cross-platform health metrics
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
    StepCount, HeartRate, Calories, Distance,
    Weight, Height, BodyFatPercentage, RestingHeartRate,
    BloodPressure, OxygenSaturation, SleepDuration, Hydration
}

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
```

### IHealthService Interface

```csharp
public interface IHealthService
{
    // Request read permissions (backward compatible)
    Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params DataType[] dataTypes);
    // Request read, write, or both permissions (uniform for all types)
    Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(PermissionType permissionType, params DataType[] dataTypes);
    // Request per-metric read/write permissions in a single call
    Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params (PermissionType Permission, DataType Type)[] permissions);

    // Write health data
    Task Write(NumericHealthResult result, CancellationToken cancelToken = default);
    Task Write(BloodPressureResult result, CancellationToken cancelToken = default);

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

## Platform Notes

### iOS
- HealthKit requires a real device (not simulator) for most data types
- `RequestPermissions` on iOS does NOT tell you if the user denied access (Apple privacy policy) - it may return `true` even when denied
- Sleep data uses `HKCategoryTypeIdentifier.SleepAnalysis` (category type, not quantity type) - the library handles this internally
- Blood pressure requires permissions for both systolic and diastolic types - the library handles this automatically
- Percentage values (body fat, O2 saturation) are returned as 0-100, not 0-1

### Android
- The Health Connect app must be installed on the device
- Body fat percentage and oxygen saturation use `ReadRecords` instead of aggregate queries (Health Connect does not provide aggregate metrics for these types)
- Sleep duration uses `SleepSessionRecord.SleepDurationTotal` aggregate metric, returning hours
- Blood pressure uses `BloodPressureRecord.SystolicAvg` and `DiastolicAvg` aggregate metrics
- All Kotlin coroutine interop is handled internally via `IContinuation` bridge (AOT-safe, no reflection)

## Best Practices

1. **Always request permissions first** - Call `RequestPermissions` before reading or writing data. Use `PermissionType.Write` or `PermissionType.ReadWrite` when writing
2. **Use appropriate intervals** - Use `Interval.Days` for summaries, `Interval.Hours` for detailed breakdowns
3. **Handle empty results** - Check `.Any()` before calling `.Average()` to avoid `InvalidOperationException`
4. **Use CancellationToken** - Pass cancellation tokens for long-running queries
5. **Sum vs Average** - Use `.Sum()` for cumulative metrics (steps, calories, distance, hydration, sleep) and `.Average()` for point-in-time metrics (heart rate, weight, height, body fat, O2 sat, resting HR)
6. **Blood pressure is special** - It returns `BloodPressureResult` (not `NumericHealthResult`) with separate `Systolic` and `Diastolic` values
7. **Register early** - Call `AddHealthIntegration()` in `MauiProgram.cs` during app startup

## Common Packages

```bash
dotnet add package Shiny.Health          # Core health data library
dotnet add package Shiny.Core            # Required dependency
```
