namespace Shiny.Health.Extensions.AI.Internal;

sealed class HealthAIToolBuilder : IHealthAIToolBuilder
{
    public Dictionary<DataType, HealthAICapabilities> Metrics { get; } = new();
    public HealthAICapabilities BloodPressure { get; private set; }
    public HealthAICapabilities Cycle { get; private set; }
    public HealthAICapabilities Workouts { get; private set; }
    public HealthAICapabilities Nutrition { get; private set; }

    public bool IsEmpty =>
        this.Metrics.Count == 0 &&
        this.BloodPressure == HealthAICapabilities.None &&
        this.Cycle == HealthAICapabilities.None &&
        this.Workouts == HealthAICapabilities.None &&
        this.Nutrition == HealthAICapabilities.None;

    public IHealthAIToolBuilder AddMetric(DataType metric, HealthAICapabilities capabilities = HealthAICapabilities.Read)
    {
        if (!HealthMetricCatalog.Numeric.ContainsKey(metric))
            throw new ArgumentException(
                $"{metric} is not a numeric metric. Use AddBloodPressure, AddCycleTracking, AddWorkouts, or AddNutrition for non-numeric data.",
                nameof(metric));

        this.Metrics[metric] = this.Metrics.TryGetValue(metric, out var existing)
            ? existing | capabilities
            : capabilities;
        return this;
    }

    public IHealthAIToolBuilder AddAllMetrics(HealthAICapabilities capabilities = HealthAICapabilities.Read)
    {
        foreach (var dt in HealthMetricCatalog.Numeric.Keys)
            this.AddMetric(dt, capabilities);
        return this;
    }

    public IHealthAIToolBuilder AddBloodPressure(HealthAICapabilities capabilities = HealthAICapabilities.Read)
    {
        this.BloodPressure |= capabilities;
        return this;
    }

    public IHealthAIToolBuilder AddCycleTracking(HealthAICapabilities capabilities = HealthAICapabilities.Read)
    {
        this.Cycle |= capabilities;
        return this;
    }

    public IHealthAIToolBuilder AddWorkouts(HealthAICapabilities capabilities = HealthAICapabilities.Read)
    {
        this.Workouts |= capabilities;
        return this;
    }

    public IHealthAIToolBuilder AddNutrition(HealthAICapabilities capabilities = HealthAICapabilities.Read)
    {
        this.Nutrition |= capabilities;
        return this;
    }
}
