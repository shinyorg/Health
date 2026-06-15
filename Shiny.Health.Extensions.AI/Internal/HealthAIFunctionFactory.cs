using Microsoft.Extensions.AI;

namespace Shiny.Health.Extensions.AI.Internal;

static class HealthAIFunctionFactory
{
    public static IReadOnlyList<AITool> Build(IHealthService health, HealthAIToolBuilder builder)
    {
        var tools = new List<AITool>();

        var readMetrics = builder.Metrics
            .Where(kv => kv.Value.HasFlag(HealthAICapabilities.Read))
            .Select(kv => HealthMetricCatalog.Numeric[kv.Key])
            .OrderBy(m => m.Slug)
            .ToList();

        var writeMetrics = builder.Metrics
            .Where(kv => kv.Value.HasFlag(HealthAICapabilities.Write))
            .Select(kv => HealthMetricCatalog.Numeric[kv.Key])
            .OrderBy(m => m.Slug)
            .ToList();

        if (readMetrics.Count > 0)
            tools.Add(new GetMetricFunction(health, readMetrics));
        if (writeMetrics.Count > 0)
            tools.Add(new WriteMetricFunction(health, writeMetrics));

        if (builder.BloodPressure.HasFlag(HealthAICapabilities.Read))
            tools.Add(new GetBloodPressureFunction(health));
        if (builder.BloodPressure.HasFlag(HealthAICapabilities.Write))
            tools.Add(new WriteBloodPressureFunction(health));

        if (builder.Cycle.HasFlag(HealthAICapabilities.Read))
            tools.Add(new GetCycleRecordsFunction(health));
        if (builder.Cycle.HasFlag(HealthAICapabilities.Write))
            tools.Add(new WriteMenstruationFlowFunction(health));

        if (builder.Workouts.HasFlag(HealthAICapabilities.Read))
            tools.Add(new GetWorkoutsFunction(health));
        if (builder.Workouts.HasFlag(HealthAICapabilities.Write))
            tools.Add(new WriteWorkoutFunction(health));

        if (builder.Nutrition.HasFlag(HealthAICapabilities.Read))
            tools.Add(new GetNutritionFunction(health));
        if (builder.Nutrition.HasFlag(HealthAICapabilities.Write))
            tools.Add(new WriteNutritionFunction(health));

        return tools;
    }
}
