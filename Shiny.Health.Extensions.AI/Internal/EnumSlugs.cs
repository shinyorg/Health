using System.Text;

namespace Shiny.Health.Extensions.AI.Internal;

/// <summary>Converts enum names to/from stable snake_case slugs for tool schemas.</summary>
static class EnumSlugs
{
    public static string ToSlug(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (char.IsUpper(ch) && i > 0)
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    public static IReadOnlyList<string> Slugs<TEnum>() where TEnum : struct, Enum
        => Enum.GetNames<TEnum>().Select(ToSlug).ToList();

    public static bool TryParse<TEnum>(string? slug, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(slug))
            return false;

        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (ToSlug(name).Equals(slug, StringComparison.OrdinalIgnoreCase))
            {
                value = Enum.Parse<TEnum>(name);
                return true;
            }
        }
        return false;
    }
}
