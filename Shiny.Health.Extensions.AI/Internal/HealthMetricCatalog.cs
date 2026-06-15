namespace Shiny.Health.Extensions.AI.Internal;

/// <summary>Describes one numeric metric: its tool slug, unit, aggregation hint, and read dispatch.</summary>
sealed record NumericMetricInfo(
    DataType Type,
    string Slug,
    string Unit,
    string Aggregation,
    Func<IHealthService, DateTimeOffset, DateTimeOffset, Interval, CancellationToken, Task<IList<NumericHealthResult>>> Read
);

/// <summary>
/// The fixed set of numeric metrics the AI tools can read/write, mapping each <see cref="DataType"/>
/// to its <c>IHealthService</c> getter, a stable snake_case slug, unit, and how it aggregates.
/// </summary>
static class HealthMetricCatalog
{
    public static readonly IReadOnlyDictionary<DataType, NumericMetricInfo> Numeric = new[]
    {
        new NumericMetricInfo(DataType.StepCount, "step_count", "count", "sum", (h, s, e, i, c) => h.GetStepCounts(s, e, i, c)),
        new NumericMetricInfo(DataType.HeartRate, "heart_rate", "bpm", "average", (h, s, e, i, c) => h.GetAverageHeartRate(s, e, i, c)),
        new NumericMetricInfo(DataType.Calories, "calories", "kcal", "sum", (h, s, e, i, c) => h.GetCalories(s, e, i, c)),
        new NumericMetricInfo(DataType.Distance, "distance", "meters", "sum", (h, s, e, i, c) => h.GetDistances(s, e, i, c)),
        new NumericMetricInfo(DataType.Weight, "weight", "kg", "average", (h, s, e, i, c) => h.GetWeight(s, e, i, c)),
        new NumericMetricInfo(DataType.Height, "height", "meters", "average", (h, s, e, i, c) => h.GetHeight(s, e, i, c)),
        new NumericMetricInfo(DataType.BodyFatPercentage, "body_fat_percentage", "%", "average", (h, s, e, i, c) => h.GetBodyFatPercentage(s, e, i, c)),
        new NumericMetricInfo(DataType.RestingHeartRate, "resting_heart_rate", "bpm", "average", (h, s, e, i, c) => h.GetRestingHeartRate(s, e, i, c)),
        new NumericMetricInfo(DataType.OxygenSaturation, "oxygen_saturation", "%", "average", (h, s, e, i, c) => h.GetOxygenSaturation(s, e, i, c)),
        new NumericMetricInfo(DataType.SleepDuration, "sleep_duration", "hours", "sum", (h, s, e, i, c) => h.GetSleepDuration(s, e, i, c)),
        new NumericMetricInfo(DataType.Hydration, "hydration", "liters", "sum", (h, s, e, i, c) => h.GetHydration(s, e, i, c)),
        new NumericMetricInfo(DataType.BloodGlucose, "blood_glucose", "mg/dL", "average", (h, s, e, i, c) => h.GetBloodGlucose(s, e, i, c)),
        new NumericMetricInfo(DataType.BodyTemperature, "body_temperature", "°C", "average", (h, s, e, i, c) => h.GetBodyTemperature(s, e, i, c)),
        new NumericMetricInfo(DataType.BasalBodyTemperature, "basal_body_temperature", "°C", "average", (h, s, e, i, c) => h.GetBasalBodyTemperature(s, e, i, c)),
        new NumericMetricInfo(DataType.RespiratoryRate, "respiratory_rate", "breaths/min", "average", (h, s, e, i, c) => h.GetRespiratoryRate(s, e, i, c)),
        new NumericMetricInfo(DataType.Vo2Max, "vo2_max", "mL/kg/min", "average", (h, s, e, i, c) => h.GetVo2Max(s, e, i, c)),
        new NumericMetricInfo(DataType.HeartRateVariability, "heart_rate_variability", "ms", "average", (h, s, e, i, c) => h.GetHeartRateVariability(s, e, i, c)),
        new NumericMetricInfo(DataType.LeanBodyMass, "lean_body_mass", "kg", "average", (h, s, e, i, c) => h.GetLeanBodyMass(s, e, i, c)),
        new NumericMetricInfo(DataType.BasalEnergyBurned, "basal_energy_burned", "kcal", "sum", (h, s, e, i, c) => h.GetBasalEnergyBurned(s, e, i, c)),
        new NumericMetricInfo(DataType.ActiveEnergyBurned, "active_energy_burned", "kcal", "sum", (h, s, e, i, c) => h.GetActiveEnergyBurned(s, e, i, c)),
        new NumericMetricInfo(DataType.FloorsClimbed, "floors_climbed", "count", "sum", (h, s, e, i, c) => h.GetFloorsClimbed(s, e, i, c)),
        new NumericMetricInfo(DataType.WheelchairPushes, "wheelchair_pushes", "count", "sum", (h, s, e, i, c) => h.GetWheelchairPushes(s, e, i, c)),
        new NumericMetricInfo(DataType.Speed, "speed", "m/s", "average", (h, s, e, i, c) => h.GetSpeed(s, e, i, c)),
        new NumericMetricInfo(DataType.Power, "power", "watts", "average", (h, s, e, i, c) => h.GetPower(s, e, i, c)),
    }.ToDictionary(m => m.Type);
}
