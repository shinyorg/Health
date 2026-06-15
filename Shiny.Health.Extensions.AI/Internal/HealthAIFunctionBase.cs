using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Shiny.Health.Extensions.AI.Internal;

/// <summary>
/// Base for the health <see cref="AIFunction"/> tools. Holds the name/description/schema and
/// provides reflection-free argument extraction from the LLM-supplied <see cref="AIFunctionArguments"/>.
/// </summary>
abstract class HealthAIFunctionBase : AIFunction
{
    protected IHealthService Health { get; }
    readonly string name;
    readonly string description;
    readonly JsonElement schema;

    protected HealthAIFunctionBase(IHealthService health, string name, string description, JsonElement schema)
    {
        this.Health = health;
        this.name = name;
        this.description = description;
        this.schema = schema;
    }

    public override string Name => this.name;
    public override string Description => this.description;
    public override JsonElement JsonSchema => this.schema;

    protected static string? GetString(AIFunctionArguments args, string key)
    {
        if (!args.TryGetValue(key, out var raw) || raw is null)
            return null;
        if (raw is string s)
            return s;
        if (raw is JsonElement el)
            return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
        return raw.ToString();
    }

    protected static double? GetDouble(AIFunctionArguments args, string key)
    {
        if (!args.TryGetValue(key, out var raw) || raw is null)
            return null;
        switch (raw)
        {
            case double d: return d;
            case float f: return f;
            case int i: return i;
            case long l: return l;
            case JsonElement el when el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var v): return v;
            case string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var ps): return ps;
            default: return null;
        }
    }

    protected static bool GetBool(AIFunctionArguments args, string key, bool fallback = false)
    {
        if (!args.TryGetValue(key, out var raw) || raw is null)
            return fallback;
        return raw switch
        {
            bool b => b,
            JsonElement el when el.ValueKind is JsonValueKind.True or JsonValueKind.False => el.GetBoolean(),
            string s when bool.TryParse(s, out var pb) => pb,
            _ => fallback
        };
    }

    protected static DateTimeOffset GetDate(AIFunctionArguments args, string key, DateTimeOffset fallback)
    {
        var s = GetString(args, key);
        if (string.IsNullOrWhiteSpace(s))
            return fallback;
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)
            ? dto
            : fallback;
    }

    protected static Interval GetInterval(AIFunctionArguments args, string key, Interval fallback)
    {
        var s = GetString(args, key)?.ToLowerInvariant();
        return s switch
        {
            "minute" or "minutes" => Interval.Minutes,
            "hour" or "hours" => Interval.Hours,
            "day" or "days" => Interval.Days,
            _ => fallback
        };
    }

    protected static string Iso(DateTimeOffset dto) => dto.ToString("o", CultureInfo.InvariantCulture);
}
