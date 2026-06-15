using System;

namespace Shiny.Health;

[Flags]
public enum PermissionType
{
    Read = 1,
    Write = 2,
    ReadWrite = Read | Write
}

public enum Interval
{
    Minutes,
    Hours,
    Days
}

public enum DataType
{
    StepCount,
    HeartRate,
    Calories,
    Distance,
    Weight,
    Height,
    BodyFatPercentage,
    RestingHeartRate,
    BloodPressure,
    OxygenSaturation,
    SleepDuration,
    Hydration,
    MenstruationFlow,

    // Tier 1 - numeric metrics
    BloodGlucose,
    BodyTemperature,
    BasalBodyTemperature,
    RespiratoryRate,
    Vo2Max,
    HeartRateVariability,
    LeanBodyMass,
    BasalEnergyBurned,
    ActiveEnergyBurned,
    FloorsClimbed,
    WheelchairPushes,

    // Tier 3 - fitness numeric metrics
    Speed,
    Power,

    // Tier 2 - reproductive / cycle-tracking (categorical & event based)
    SexualActivity,
    OvulationTest,
    CervicalMucus,
    IntermenstrualBleeding,

    // Tier 3 - structured records
    Workout,
    Nutrition
}

/// <summary>
/// Menstrual flow level for a single day's menstruation record.
/// </summary>
public enum MenstrualFlow
{
    /// <summary>Flow was recorded but the level is unspecified.</summary>
    Unspecified,
    /// <summary>No flow (spotting/none). iOS only - Android maps this to <see cref="Unspecified"/>.</summary>
    None,
    /// <summary>Light flow.</summary>
    Light,
    /// <summary>Medium flow.</summary>
    Medium,
    /// <summary>Heavy flow.</summary>
    Heavy
}

/// <summary>
/// Whether protection was used during a sexual activity record.
/// </summary>
public enum SexualActivityProtection
{
    /// <summary>Protection use was not specified.</summary>
    Unspecified,
    /// <summary>Protection was used.</summary>
    Protected,
    /// <summary>Protection was not used.</summary>
    Unprotected
}

/// <summary>
/// Result of an ovulation (luteinizing hormone) test.
/// </summary>
public enum OvulationTestOutcome
{
    /// <summary>The result was inconclusive/indeterminate.</summary>
    Inconclusive,
    /// <summary>Positive result (LH surge detected).</summary>
    Positive,
    /// <summary>
    /// High fertility result. iOS maps this to HealthKit's "estrogen surge";
    /// not all platforms distinguish High from Positive.
    /// </summary>
    High,
    /// <summary>Negative result.</summary>
    Negative
}

/// <summary>
/// Cervical mucus appearance/quality observation.
/// </summary>
public enum CervicalMucusAppearance
{
    /// <summary>Appearance was recorded but is unspecified. Android's "unusual" value also maps here.</summary>
    Unspecified,
    /// <summary>Dry.</summary>
    Dry,
    /// <summary>Sticky.</summary>
    Sticky,
    /// <summary>Creamy.</summary>
    Creamy,
    /// <summary>Watery.</summary>
    Watery,
    /// <summary>Egg-white (most fertile).</summary>
    EggWhite
}

/// <summary>
/// Meal type associated with a nutrition record.
/// </summary>
public enum MealType
{
    Unknown,
    Breakfast,
    Lunch,
    Dinner,
    Snack
}

/// <summary>
/// Cross-platform workout/exercise activity type. Values map to a subset of
/// HealthKit's <c>HKWorkoutActivityType</c> and Health Connect's exercise types
/// that are present on both platforms. Anything unmapped is reported as <see cref="Other"/>.
/// </summary>
public enum WorkoutType
{
    Other,
    Running,
    Walking,
    Hiking,
    Cycling,
    Swimming,
    Rowing,
    Elliptical,
    StairClimbing,
    StrengthTraining,
    HighIntensityIntervalTraining,
    Yoga,
    Pilates,
    Tennis,
    Basketball,
    Soccer,
    Baseball,
    Golf,
    Boxing,
    MartialArts,
    Dancing
}

public abstract record HealthResult(
    DataType Type,
    DateTimeOffset Start,
    DateTimeOffset End
);

public record NumericHealthResult(
    DataType DataType,
    DateTimeOffset Start,
    DateTimeOffset End,
    double Value
) : HealthResult(DataType, Start, End);

public record BloodPressureResult(
    DateTimeOffset Start,
    DateTimeOffset End,
    double Systolic,
    double Diastolic
) : HealthResult(DataType.BloodPressure, Start, End);

/// <summary>
/// A menstruation flow record for a point in time (typically a single day).
/// </summary>
/// <param name="Start">The start of the record.</param>
/// <param name="End">The end of the record. For point-in-time records this equals <paramref name="Start"/>.</param>
/// <param name="Flow">The menstrual flow level.</param>
/// <param name="IsCycleStart">
/// Whether this record marks the first day of the menstrual cycle.
/// iOS persists this via HealthKit's menstrual cycle metadata; on Android it is ignored on write
/// and always returned as <c>false</c> (Health Connect models the period span as a separate record).
/// </param>
public record MenstruationFlowResult(
    DateTimeOffset Start,
    DateTimeOffset End,
    MenstrualFlow Flow,
    bool IsCycleStart = false
) : HealthResult(DataType.MenstruationFlow, Start, End);

/// <summary>
/// A sexual activity record (point in time).
/// </summary>
public record SexualActivityResult(
    DateTimeOffset Start,
    DateTimeOffset End,
    SexualActivityProtection Protection
) : HealthResult(DataType.SexualActivity, Start, End);

/// <summary>
/// An ovulation test result record (point in time).
/// </summary>
public record OvulationTestResult(
    DateTimeOffset Start,
    DateTimeOffset End,
    OvulationTestOutcome Outcome
) : HealthResult(DataType.OvulationTest, Start, End);

/// <summary>
/// A cervical mucus observation record (point in time).
/// </summary>
public record CervicalMucusResult(
    DateTimeOffset Start,
    DateTimeOffset End,
    CervicalMucusAppearance Appearance
) : HealthResult(DataType.CervicalMucus, Start, End);

/// <summary>
/// An intermenstrual bleeding (spotting) event record (point in time). The event has no value.
/// </summary>
public record IntermenstrualBleedingResult(
    DateTimeOffset Start,
    DateTimeOffset End
) : HealthResult(DataType.IntermenstrualBleeding, Start, End);

/// <summary>
/// A workout / exercise session.
/// </summary>
/// <param name="Start">When the session started.</param>
/// <param name="End">When the session ended.</param>
/// <param name="Workout">The activity type.</param>
/// <param name="TotalEnergyKilocalories">
/// Total energy burned in kcal, when available. On Android (Health Connect) energy is stored as a
/// separate record from the exercise session, so this is <c>null</c> on read.
/// </param>
/// <param name="TotalDistanceMeters">
/// Total distance in meters, when available. <c>null</c> on Android read for the same reason as energy.
/// </param>
/// <param name="Title">Optional human-readable title/notes for the session.</param>
public record WorkoutResult(
    DateTimeOffset Start,
    DateTimeOffset End,
    WorkoutType Workout,
    double? TotalEnergyKilocalories = null,
    double? TotalDistanceMeters = null,
    string? Title = null
) : HealthResult(DataType.Workout, Start, End);

/// <summary>
/// A nutrition / food intake record. All nutrient values are optional; only the values you set are written.
/// Masses are in grams, energy in kilocalories.
/// </summary>
public record NutritionResult(
    DateTimeOffset Start,
    DateTimeOffset End,
    MealType Meal = MealType.Unknown,
    string? Name = null,
    double? EnergyKilocalories = null,
    double? ProteinGrams = null,
    double? CarbohydratesGrams = null,
    double? TotalFatGrams = null,
    double? FiberGrams = null,
    double? SugarGrams = null,
    double? SodiumGrams = null,
    double? CholesterolGrams = null
) : HealthResult(DataType.Nutrition, Start, End);
