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
    Hydration
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
