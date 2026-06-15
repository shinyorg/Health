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
    MenstruationFlow
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
