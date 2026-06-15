namespace Shiny.Health.Extensions.AI;

/// <summary>
/// Opt-in builder for the health areas an AI agent is allowed to access. Anything not added
/// here is invisible to the LLM. All areas default to <see cref="HealthAICapabilities.Read"/>;
/// pass <see cref="HealthAICapabilities.ReadWrite"/> (or <c>Write</c>) to also expose write tools.
/// </summary>
public interface IHealthAIToolBuilder
{
    /// <summary>
    /// Allows a single numeric metric (e.g. <see cref="DataType.StepCount"/>, <see cref="DataType.HeartRate"/>).
    /// Throws if <paramref name="metric"/> is not numeric — use <see cref="AddBloodPressure"/>,
    /// <see cref="AddCycleTracking"/>, <see cref="AddWorkouts"/>, or <see cref="AddNutrition"/> for those.
    /// </summary>
    IHealthAIToolBuilder AddMetric(DataType metric, HealthAICapabilities capabilities = HealthAICapabilities.Read);

    /// <summary>Allows every numeric metric in one call.</summary>
    IHealthAIToolBuilder AddAllMetrics(HealthAICapabilities capabilities = HealthAICapabilities.Read);

    /// <summary>Allows blood pressure (systolic/diastolic).</summary>
    IHealthAIToolBuilder AddBloodPressure(HealthAICapabilities capabilities = HealthAICapabilities.Read);

    /// <summary>
    /// Allows reproductive/cycle records (menstruation flow, sexual activity, ovulation tests,
    /// cervical mucus, intermenstrual bleeding) via a single parameterized read tool. When
    /// <c>Write</c> is included, a menstruation-flow logging tool is also exposed.
    /// </summary>
    IHealthAIToolBuilder AddCycleTracking(HealthAICapabilities capabilities = HealthAICapabilities.Read);

    /// <summary>Allows workouts / exercise sessions.</summary>
    IHealthAIToolBuilder AddWorkouts(HealthAICapabilities capabilities = HealthAICapabilities.Read);

    /// <summary>Allows nutrition / food intake records.</summary>
    IHealthAIToolBuilder AddNutrition(HealthAICapabilities capabilities = HealthAICapabilities.Read);
}
