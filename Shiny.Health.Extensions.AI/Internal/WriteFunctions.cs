using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Shiny.Health.Extensions.AI.Internal;

/// <summary>Writes a numeric metric value (chosen via the <c>metric</c> enum).</summary>
sealed class WriteMetricFunction : HealthAIFunctionBase
{
    readonly IReadOnlyDictionary<string, NumericMetricInfo> bySlug;

    public WriteMetricFunction(IHealthService health, IReadOnlyList<NumericMetricInfo> metrics)
        : base(health, "write_health_metric", BuildDescription(metrics), BuildSchema(metrics))
        => this.bySlug = metrics.ToDictionary(m => m.Slug);

    static string BuildDescription(IReadOnlyList<NumericMetricInfo> metrics)
        => "Record a numeric health metric value. Writable metrics (with units): "
           + string.Join(", ", metrics.Select(m => $"{m.Slug} ({m.Unit})")) + ". "
           + "For ranged metrics (e.g. step_count) provide start and end; otherwise the value is logged at 'start'.";

    static JsonElement BuildSchema(IReadOnlyList<NumericMetricInfo> metrics)
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["metric"] = SchemaJson.String("Which metric to write.", metrics.Select(m => m.Slug)),
                ["value"] = SchemaJson.Number("The value in the metric's unit."),
                ["start"] = SchemaJson.Date("Start (defaults to now)"),
                ["end"] = SchemaJson.Date("End (defaults to start)")
            },
            "metric", "value"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var slug = GetString(arguments, "metric");
        if (slug is null || !this.bySlug.TryGetValue(slug, out var info))
            return new JsonObject { ["error"] = $"Unknown or non-writable metric '{slug}'." };

        var value = GetDouble(arguments, "value");
        if (value is null)
            return new JsonObject { ["error"] = "'value' is required." };

        var start = GetDate(arguments, "start", DateTimeOffset.Now);
        var end = GetDate(arguments, "end", start);

        await this.Health.Write(new NumericHealthResult(info.Type, start, end, value.Value), cancellationToken).ConfigureAwait(false);
        return new JsonObject { ["success"] = true, ["metric"] = info.Slug, ["value"] = value.Value, ["start"] = Iso(start), ["end"] = Iso(end) };
    }
}

/// <summary>Writes a blood pressure reading.</summary>
sealed class WriteBloodPressureFunction : HealthAIFunctionBase
{
    public WriteBloodPressureFunction(IHealthService health)
        : base(health, "write_blood_pressure", "Record a blood pressure reading (mmHg).", BuildSchema())
    { }

    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["systolic"] = SchemaJson.Number("Systolic pressure in mmHg."),
                ["diastolic"] = SchemaJson.Number("Diastolic pressure in mmHg."),
                ["timestamp"] = SchemaJson.Date("Time of the reading (defaults to now)")
            },
            "systolic", "diastolic"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var systolic = GetDouble(arguments, "systolic");
        var diastolic = GetDouble(arguments, "diastolic");
        if (systolic is null || diastolic is null)
            return new JsonObject { ["error"] = "'systolic' and 'diastolic' are required." };

        var t = GetDate(arguments, "timestamp", DateTimeOffset.Now);
        await this.Health.Write(new BloodPressureResult(t, t, systolic.Value, diastolic.Value), cancellationToken).ConfigureAwait(false);
        return new JsonObject { ["success"] = true, ["systolic"] = systolic.Value, ["diastolic"] = diastolic.Value, ["timestamp"] = Iso(t) };
    }
}

/// <summary>Logs a menstruation flow record.</summary>
sealed class WriteMenstruationFlowFunction : HealthAIFunctionBase
{
    public WriteMenstruationFlowFunction(IHealthService health)
        : base(health, "write_menstruation_flow",
            "Log a menstruation flow record for a day. Flow levels: " + string.Join(", ", EnumSlugs.Slugs<MenstrualFlow>()) + ".",
            BuildSchema())
    { }

    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["flow"] = SchemaJson.String("Flow level.", EnumSlugs.Slugs<MenstrualFlow>()),
                ["date"] = SchemaJson.Date("Date (defaults to now)"),
                ["is_cycle_start"] = SchemaJson.Boolean("Whether this marks the first day of the cycle (iOS only).")
            },
            "flow"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!EnumSlugs.TryParse<MenstrualFlow>(GetString(arguments, "flow"), out var flow))
            return new JsonObject { ["error"] = "'flow' is required and must be one of: " + string.Join(", ", EnumSlugs.Slugs<MenstrualFlow>()) + "." };

        var date = GetDate(arguments, "date", DateTimeOffset.Now);
        var isCycleStart = GetBool(arguments, "is_cycle_start");

        await this.Health.Write(new MenstruationFlowResult(date, date, flow, isCycleStart), cancellationToken).ConfigureAwait(false);
        return new JsonObject { ["success"] = true, ["flow"] = EnumSlugs.ToSlug(flow.ToString()), ["date"] = Iso(date), ["isCycleStart"] = isCycleStart };
    }
}

/// <summary>Records a workout / exercise session.</summary>
sealed class WriteWorkoutFunction : HealthAIFunctionBase
{
    public WriteWorkoutFunction(IHealthService health)
        : base(health, "write_workout",
            "Record a workout / exercise session. Activity types: " + string.Join(", ", EnumSlugs.Slugs<WorkoutType>()) + ".",
            BuildSchema())
    { }

    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["workout"] = SchemaJson.String("Activity type.", EnumSlugs.Slugs<WorkoutType>()),
                ["start"] = SchemaJson.Date("Start"),
                ["end"] = SchemaJson.Date("End"),
                ["total_energy_kilocalories"] = SchemaJson.Number("Total energy burned in kcal (optional)."),
                ["total_distance_meters"] = SchemaJson.Number("Total distance in meters (optional)."),
                ["title"] = SchemaJson.String("Optional title/notes.")
            },
            "workout", "start", "end"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!EnumSlugs.TryParse<WorkoutType>(GetString(arguments, "workout"), out var workout))
            return new JsonObject { ["error"] = "'workout' is required and must be a known activity type." };

        var start = GetDate(arguments, "start", DateTimeOffset.MinValue);
        var end = GetDate(arguments, "end", DateTimeOffset.MinValue);
        if (start == DateTimeOffset.MinValue || end == DateTimeOffset.MinValue)
            return new JsonObject { ["error"] = "'start' and 'end' are required." };

        var energy = GetDouble(arguments, "total_energy_kilocalories");
        var distance = GetDouble(arguments, "total_distance_meters");
        var title = GetString(arguments, "title");

        await this.Health.Write(new WorkoutResult(start, end, workout, energy, distance, title), cancellationToken).ConfigureAwait(false);
        return new JsonObject { ["success"] = true, ["workout"] = EnumSlugs.ToSlug(workout.ToString()), ["start"] = Iso(start), ["end"] = Iso(end) };
    }
}

/// <summary>Records a nutrition / food intake entry.</summary>
sealed class WriteNutritionFunction : HealthAIFunctionBase
{
    public WriteNutritionFunction(IHealthService health)
        : base(health, "write_nutrition",
            "Record a nutrition / food intake entry. Only the fields you set are written. Masses are grams, energy is kcal.",
            BuildSchema())
    { }

    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["start"] = SchemaJson.Date("Start"),
                ["end"] = SchemaJson.Date("End"),
                ["meal"] = SchemaJson.String("Meal type.", EnumSlugs.Slugs<MealType>()),
                ["name"] = SchemaJson.String("Food/meal name (optional)."),
                ["energy_kilocalories"] = SchemaJson.Number("Energy in kcal (optional)."),
                ["protein_grams"] = SchemaJson.Number("Protein in grams (optional)."),
                ["carbohydrates_grams"] = SchemaJson.Number("Carbohydrates in grams (optional)."),
                ["total_fat_grams"] = SchemaJson.Number("Total fat in grams (optional)."),
                ["fiber_grams"] = SchemaJson.Number("Fiber in grams (optional)."),
                ["sugar_grams"] = SchemaJson.Number("Sugar in grams (optional)."),
                ["sodium_grams"] = SchemaJson.Number("Sodium in grams (optional)."),
                ["cholesterol_grams"] = SchemaJson.Number("Cholesterol in grams (optional).")
            },
            "start", "end"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var start = GetDate(arguments, "start", DateTimeOffset.MinValue);
        var end = GetDate(arguments, "end", DateTimeOffset.MinValue);
        if (start == DateTimeOffset.MinValue || end == DateTimeOffset.MinValue)
            return new JsonObject { ["error"] = "'start' and 'end' are required." };

        EnumSlugs.TryParse<MealType>(GetString(arguments, "meal"), out var meal); // defaults to Unknown

        await this.Health.Write(new NutritionResult(
            start, end, meal,
            GetString(arguments, "name"),
            GetDouble(arguments, "energy_kilocalories"),
            GetDouble(arguments, "protein_grams"),
            GetDouble(arguments, "carbohydrates_grams"),
            GetDouble(arguments, "total_fat_grams"),
            GetDouble(arguments, "fiber_grams"),
            GetDouble(arguments, "sugar_grams"),
            GetDouble(arguments, "sodium_grams"),
            GetDouble(arguments, "cholesterol_grams")
        ), cancellationToken).ConfigureAwait(false);

        return new JsonObject { ["success"] = true, ["meal"] = EnumSlugs.ToSlug(meal.ToString()), ["start"] = Iso(start), ["end"] = Iso(end) };
    }
}
