using System;

namespace Shiny.Health;


public record HealthResult<T>(
    DateTimeOffset Start,
    DateTimeOffset End,
    T Value
);


public enum PermissionType
{
    Read,
    Write,
    Both
}


public record Permission(
    HealthMetric Metric,
    PermissionType Type
);


public enum Interval
{
    Minutes,
    Hours,
    Days
}