using System;

namespace Shiny.Health;

/// <summary>
/// The kind of access being requested for a health data type.
/// </summary>
[Flags]
public enum PermissionType
{
    /// <summary>Permission to read the data type.</summary>
    Read = 1,
    /// <summary>Permission to write the data type.</summary>
    Write = 2,
    /// <summary>Permission to both read and write the data type.</summary>
    ReadWrite = Read | Write
}

/// <summary>
/// The bucket size used when aggregating numeric metrics over a time range.
/// </summary>
public enum Interval
{
    /// <summary>Aggregate into one bucket per minute.</summary>
    Minutes,
    /// <summary>Aggregate into one bucket per hour.</summary>
    Hours,
    /// <summary>Aggregate into one bucket per day.</summary>
    Days
}

/// <summary>
/// A health data type that can be read, written, observed, or have permissions requested for it.
/// </summary>
public enum DataType
{
    /// <summary>Number of steps taken.</summary>
    StepCount,
    /// <summary>Heart rate (beats per minute).</summary>
    HeartRate,
    /// <summary>Energy burned (kilocalories).</summary>
    Calories,
    /// <summary>Distance travelled (meters).</summary>
    Distance,
    /// <summary>Body weight (kilograms).</summary>
    Weight,
    /// <summary>Body height (meters).</summary>
    Height,
    /// <summary>Body fat percentage (0-100).</summary>
    BodyFatPercentage,
    /// <summary>Resting heart rate (beats per minute).</summary>
    RestingHeartRate,
    /// <summary>Blood pressure (systolic/diastolic, mmHg).</summary>
    BloodPressure,
    /// <summary>Blood oxygen saturation (0-100%).</summary>
    OxygenSaturation,
    /// <summary>Sleep duration (hours).</summary>
    SleepDuration,
    /// <summary>Water intake (liters).</summary>
    Hydration,
    /// <summary>Menstruation flow records.</summary>
    MenstruationFlow,

    // Tier 1 - numeric metrics
    /// <summary>Blood glucose (mg/dL).</summary>
    BloodGlucose,
    /// <summary>Body temperature (°C).</summary>
    BodyTemperature,
    /// <summary>Basal (resting) body temperature (°C).</summary>
    BasalBodyTemperature,
    /// <summary>Respiratory rate (breaths per minute).</summary>
    RespiratoryRate,
    /// <summary>Maximal oxygen uptake (mL/kg/min).</summary>
    Vo2Max,
    /// <summary>Heart rate variability (milliseconds).</summary>
    HeartRateVariability,
    /// <summary>Lean body mass (kilograms).</summary>
    LeanBodyMass,
    /// <summary>Basal (resting) energy burned (kilocalories).</summary>
    BasalEnergyBurned,
    /// <summary>Active energy burned (kilocalories).</summary>
    ActiveEnergyBurned,
    /// <summary>Floors / flights of stairs climbed (count).</summary>
    FloorsClimbed,
    /// <summary>Wheelchair pushes (count).</summary>
    WheelchairPushes,

    // Tier 3 - fitness numeric metrics
    /// <summary>Movement speed (m/s); maps to walking speed on iOS.</summary>
    Speed,
    /// <summary>Power output (watts); maps to cycling power on iOS.</summary>
    Power,

    // Tier 2 - reproductive / cycle-tracking (categorical & event based)
    /// <summary>Sexual activity records.</summary>
    SexualActivity,
    /// <summary>Ovulation (luteinizing hormone) test records.</summary>
    OvulationTest,
    /// <summary>Cervical mucus observation records.</summary>
    CervicalMucus,
    /// <summary>Intermenstrual bleeding (spotting) event records.</summary>
    IntermenstrualBleeding,

    // Tier 3 - structured records
    /// <summary>Workout / exercise sessions.</summary>
    Workout,
    /// <summary>Nutrition / food intake records.</summary>
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
    /// <summary>The meal type was not specified.</summary>
    Unknown,
    /// <summary>Breakfast.</summary>
    Breakfast,
    /// <summary>Lunch.</summary>
    Lunch,
    /// <summary>Dinner.</summary>
    Dinner,
    /// <summary>A snack.</summary>
    Snack
}

/// <summary>
/// Cross-platform workout/exercise activity type. Values map to a subset of
/// HealthKit's <c>HKWorkoutActivityType</c> and Health Connect's exercise types
/// that are present on both platforms. Anything unmapped is reported as <see cref="Other"/>.
/// </summary>
public enum WorkoutType
{
    /// <summary>Any activity that does not map to one of the known types.</summary>
    Other,
    /// <summary>Running.</summary>
    Running,
    /// <summary>Walking.</summary>
    Walking,
    /// <summary>Hiking.</summary>
    Hiking,
    /// <summary>Cycling.</summary>
    Cycling,
    /// <summary>Swimming.</summary>
    Swimming,
    /// <summary>Rowing.</summary>
    Rowing,
    /// <summary>Elliptical trainer.</summary>
    Elliptical,
    /// <summary>Stair climbing.</summary>
    StairClimbing,
    /// <summary>Strength / weight training.</summary>
    StrengthTraining,
    /// <summary>High-intensity interval training (HIIT).</summary>
    HighIntensityIntervalTraining,
    /// <summary>Yoga.</summary>
    Yoga,
    /// <summary>Pilates.</summary>
    Pilates,
    /// <summary>Tennis.</summary>
    Tennis,
    /// <summary>Basketball.</summary>
    Basketball,
    /// <summary>Soccer / football.</summary>
    Soccer,
    /// <summary>Baseball.</summary>
    Baseball,
    /// <summary>Golf.</summary>
    Golf,
    /// <summary>Boxing.</summary>
    Boxing,
    /// <summary>Martial arts.</summary>
    MartialArts,
    /// <summary>Dancing.</summary>
    Dancing
}

/// <summary>
/// Base type for all health results, identifying the data type and the time range it covers.
/// </summary>
/// <param name="Type">The data type this result represents.</param>
/// <param name="Start">The start of the result's time range.</param>
/// <param name="End">The end of the result's time range.</param>
public abstract record HealthResult(
    DataType Type,
    DateTimeOffset Start,
    DateTimeOffset End
);

/// <summary>
/// A single numeric health result for an interval bucket (e.g. steps, weight, heart rate).
/// </summary>
/// <param name="DataType">The numeric data type this result represents.</param>
/// <param name="Start">The start of the bucket's time range.</param>
/// <param name="End">The end of the bucket's time range.</param>
/// <param name="Value">The aggregated value for the bucket, in the data type's unit.</param>
public record NumericHealthResult(
    DataType DataType,
    DateTimeOffset Start,
    DateTimeOffset End,
    double Value
) : HealthResult(DataType, Start, End);

/// <summary>
/// A blood pressure result for an interval bucket, with separate systolic and diastolic values (mmHg).
/// </summary>
/// <param name="Start">The start of the bucket's time range.</param>
/// <param name="End">The end of the bucket's time range.</param>
/// <param name="Systolic">The systolic pressure in mmHg.</param>
/// <param name="Diastolic">The diastolic pressure in mmHg.</param>
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
