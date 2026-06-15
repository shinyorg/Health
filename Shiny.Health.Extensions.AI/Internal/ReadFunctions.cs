using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Shiny.Health.Extensions.AI.Internal;

/// <summary>Reads a numeric metric (chosen via the <c>metric</c> enum), aggregated into interval buckets.</summary>
sealed class GetMetricFunction : HealthAIFunctionBase
{
    readonly IReadOnlyDictionary<string, NumericMetricInfo> bySlug;

    public GetMetricFunction(IHealthService health, IReadOnlyList<NumericMetricInfo> metrics)
        : base(health, "get_health_metric", BuildDescription(metrics), BuildSchema(metrics))
        => this.bySlug = metrics.ToDictionary(m => m.Slug);

    static string BuildDescription(IReadOnlyList<NumericMetricInfo> metrics)
        => "Read a numeric health metric aggregated into time buckets. Available metrics (with units): "
           + string.Join(", ", metrics.Select(m => $"{m.Slug} ({m.Unit}, {m.Aggregation})")) + ".";

    static JsonElement BuildSchema(IReadOnlyList<NumericMetricInfo> metrics)
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["metric"] = SchemaJson.String("Which metric to read.", metrics.Select(m => m.Slug)),
                ["start"] = SchemaJson.Date("Start"),
                ["end"] = SchemaJson.Date("End"),
                ["interval"] = SchemaJson.Interval()
            },
            "metric"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var slug = GetString(arguments, "metric");
        if (slug is null || !this.bySlug.TryGetValue(slug, out var info))
            return new JsonObject { ["error"] = $"Unknown or unavailable metric '{slug}'." };

        var end = GetDate(arguments, "end", DateTimeOffset.Now);
        var start = GetDate(arguments, "start", end.AddDays(-1));
        var interval = GetInterval(arguments, "interval", Interval.Days);

        var results = await info.Read(this.Health, start, end, interval, cancellationToken).ConfigureAwait(false);

        var buckets = new JsonArray();
        foreach (var r in results)
            buckets.Add((JsonNode)new JsonObject { ["start"] = Iso(r.Start), ["end"] = Iso(r.End), ["value"] = r.Value });

        return new JsonObject
        {
            ["metric"] = info.Slug,
            ["unit"] = info.Unit,
            ["aggregation"] = info.Aggregation,
            ["interval"] = interval.ToString().ToLowerInvariant(),
            ["buckets"] = buckets
        };
    }
}

/// <summary>Reads blood pressure (systolic/diastolic) into interval buckets.</summary>
sealed class GetBloodPressureFunction : HealthAIFunctionBase
{
    public GetBloodPressureFunction(IHealthService health)
        : base(health, "get_blood_pressure",
            "Read blood pressure (systolic/diastolic, mmHg), averaged into time buckets.",
            BuildSchema())
    { }

    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(new JsonObject
        {
            ["start"] = SchemaJson.Date("Start"),
            ["end"] = SchemaJson.Date("End"),
            ["interval"] = SchemaJson.Interval()
        }));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var end = GetDate(arguments, "end", DateTimeOffset.Now);
        var start = GetDate(arguments, "start", end.AddDays(-1));
        var interval = GetInterval(arguments, "interval", Interval.Days);

        var results = await this.Health.GetBloodPressure(start, end, interval, cancellationToken).ConfigureAwait(false);

        var buckets = new JsonArray();
        foreach (var r in results)
            buckets.Add((JsonNode)new JsonObject
            {
                ["start"] = Iso(r.Start),
                ["end"] = Iso(r.End),
                ["systolic"] = r.Systolic,
                ["diastolic"] = r.Diastolic
            });

        return new JsonObject { ["unit"] = "mmHg", ["buckets"] = buckets };
    }
}

/// <summary>Reads reproductive/cycle records of the kind chosen via the <c>kind</c> enum.</summary>
sealed class GetCycleRecordsFunction : HealthAIFunctionBase
{
    static readonly string[] Kinds =
    [
        "menstruation_flow", "sexual_activity", "ovulation_test", "cervical_mucus", "intermenstrual_bleeding"
    ];

    public GetCycleRecordsFunction(IHealthService health)
        : base(health, "get_cycle_records",
            "Read reproductive/cycle records over a date range. Choose a 'kind': "
            + string.Join(", ", Kinds) + ".",
            BuildSchema())
    { }

    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["kind"] = SchemaJson.String("Which cycle record type to read.", Kinds),
                ["start"] = SchemaJson.Date("Start"),
                ["end"] = SchemaJson.Date("End")
            },
            "kind"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var kind = GetString(arguments, "kind");
        var end = GetDate(arguments, "end", DateTimeOffset.Now);
        var start = GetDate(arguments, "start", end.AddMonths(-1));

        var records = new JsonArray();
        switch (kind)
        {
            case "menstruation_flow":
                foreach (var r in await this.Health.GetMenstruationFlow(start, end, cancellationToken).ConfigureAwait(false))
                    records.Add((JsonNode)new JsonObject { ["start"] = Iso(r.Start), ["end"] = Iso(r.End), ["flow"] = EnumSlugs.ToSlug(r.Flow.ToString()), ["isCycleStart"] = r.IsCycleStart });
                break;

            case "sexual_activity":
                foreach (var r in await this.Health.GetSexualActivity(start, end, cancellationToken).ConfigureAwait(false))
                    records.Add((JsonNode)new JsonObject { ["start"] = Iso(r.Start), ["end"] = Iso(r.End), ["protection"] = EnumSlugs.ToSlug(r.Protection.ToString()) });
                break;

            case "ovulation_test":
                foreach (var r in await this.Health.GetOvulationTests(start, end, cancellationToken).ConfigureAwait(false))
                    records.Add((JsonNode)new JsonObject { ["start"] = Iso(r.Start), ["end"] = Iso(r.End), ["outcome"] = EnumSlugs.ToSlug(r.Outcome.ToString()) });
                break;

            case "cervical_mucus":
                foreach (var r in await this.Health.GetCervicalMucus(start, end, cancellationToken).ConfigureAwait(false))
                    records.Add((JsonNode)new JsonObject { ["start"] = Iso(r.Start), ["end"] = Iso(r.End), ["appearance"] = EnumSlugs.ToSlug(r.Appearance.ToString()) });
                break;

            case "intermenstrual_bleeding":
                foreach (var r in await this.Health.GetIntermenstrualBleeding(start, end, cancellationToken).ConfigureAwait(false))
                    records.Add((JsonNode)new JsonObject { ["start"] = Iso(r.Start), ["end"] = Iso(r.End) });
                break;

            default:
                return new JsonObject { ["error"] = $"Unknown cycle record kind '{kind}'." };
        }

        return new JsonObject { ["kind"] = kind, ["records"] = records };
    }
}

/// <summary>Reads workout / exercise sessions over a date range.</summary>
sealed class GetWorkoutsFunction : HealthAIFunctionBase
{
    public GetWorkoutsFunction(IHealthService health)
        : base(health, "get_workouts",
            "Read workout / exercise sessions over a date range.",
            BuildSchema())
    { }

    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(new JsonObject
        {
            ["start"] = SchemaJson.Date("Start"),
            ["end"] = SchemaJson.Date("End")
        }));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var end = GetDate(arguments, "end", DateTimeOffset.Now);
        var start = GetDate(arguments, "start", end.AddDays(-7));

        var list = new JsonArray();
        foreach (var w in await this.Health.GetWorkouts(start, end, cancellationToken).ConfigureAwait(false))
            list.Add((JsonNode)new JsonObject
            {
                ["start"] = Iso(w.Start),
                ["end"] = Iso(w.End),
                ["workout"] = EnumSlugs.ToSlug(w.Workout.ToString()),
                ["totalEnergyKilocalories"] = w.TotalEnergyKilocalories,
                ["totalDistanceMeters"] = w.TotalDistanceMeters,
                ["title"] = w.Title
            });

        return new JsonObject { ["workouts"] = list };
    }
}

/// <summary>Reads nutrition / food intake records over a date range.</summary>
sealed class GetNutritionFunction : HealthAIFunctionBase
{
    public GetNutritionFunction(IHealthService health)
        : base(health, "get_nutrition",
            "Read nutrition / food intake records (energy + macros) over a date range.",
            BuildSchema())
    { }

    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(new JsonObject
        {
            ["start"] = SchemaJson.Date("Start"),
            ["end"] = SchemaJson.Date("End")
        }));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var end = GetDate(arguments, "end", DateTimeOffset.Now);
        var start = GetDate(arguments, "start", end.AddDays(-1));

        var list = new JsonArray();
        foreach (var n in await this.Health.GetNutrition(start, end, cancellationToken).ConfigureAwait(false))
            list.Add((JsonNode)new JsonObject
            {
                ["start"] = Iso(n.Start),
                ["end"] = Iso(n.End),
                ["meal"] = EnumSlugs.ToSlug(n.Meal.ToString()),
                ["name"] = n.Name,
                ["energyKilocalories"] = n.EnergyKilocalories,
                ["proteinGrams"] = n.ProteinGrams,
                ["carbohydratesGrams"] = n.CarbohydratesGrams,
                ["totalFatGrams"] = n.TotalFatGrams,
                ["fiberGrams"] = n.FiberGrams,
                ["sugarGrams"] = n.SugarGrams,
                ["sodiumGrams"] = n.SodiumGrams,
                ["cholesterolGrams"] = n.CholesterolGrams
            });

        return new JsonObject { ["nutrition"] = list };
    }
}
