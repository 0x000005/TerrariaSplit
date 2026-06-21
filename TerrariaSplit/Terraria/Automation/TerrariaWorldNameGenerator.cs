using System.Text.Json;

namespace TerrariaSplit.Terraria.Automation;

internal static class TerrariaWorldNameGenerator
{
    private const int MaxWorldNameLength = 27;
    private const int MaxAttempts = 4096;
    private static readonly object CacheSync = new();
    private static readonly Dictionary<string, TerrariaWorldNameData> DataCache = new(StringComparer.OrdinalIgnoreCase);

    public static string Create(string? appLanguage)
    {
        TerrariaWorldNameData data = GetData(TerrariaLanguageCodes.FromAppLanguage(appLanguage));
        return TerrariaSeedRandom.WithShared(random => Create(data, random.Next));
    }

    internal static string Create(TerrariaWorldNameData data, Func<int, int> next)
    {
        string lastName = string.Empty;
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            string composition = SelectRandomLikeTerraria(data.Composition, next);
            string adjective = SelectRandomLikeTerraria(data.Adjective, next);
            string location = SelectRandomLikeTerraria(data.Location, next);
            string noun = SelectRandomLikeTerraria(data.Noun, next);

            lastName = composition
                .Replace("{Adjective}", adjective, StringComparison.Ordinal)
                .Replace("{Location}", location, StringComparison.Ordinal)
                .Replace("{Noun}", noun, StringComparison.Ordinal);

            if (lastName.Length <= MaxWorldNameLength)
            {
                return string.IsNullOrWhiteSpace(lastName) ? "World" : lastName;
            }
        }

        return lastName.Length == 0 ? "World" : lastName[..Math.Min(lastName.Length, MaxWorldNameLength)];
    }

    private static TerrariaWorldNameData GetData(string culture)
    {
        lock (CacheSync)
        {
            if (!DataCache.TryGetValue(culture, out TerrariaWorldNameData? data))
            {
                data = LoadData(culture);
                DataCache[culture] = data;
            }

            return data;
        }
    }

    private static TerrariaWorldNameData LoadData(string culture)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "TerrariaWorldNames", culture + ".json");
        try
        {
            if (File.Exists(path))
            {
                TerrariaWorldNameData? data = JsonSerializer.Deserialize<TerrariaWorldNameData>(File.ReadAllText(path));
                if (data is not null && data.IsUsable)
                {
                    return data;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            AppLogger.Error(ex, $"Failed to load Terraria world name data: {path}");
        }

        return LanguageNames.IsChinese(culture) || string.Equals(culture, TerrariaLanguageCodes.ChineseSimplified, StringComparison.OrdinalIgnoreCase)
            ? TerrariaWorldNameData.ChineseFallback
            : TerrariaWorldNameData.EnglishFallback;
    }

    private static string SelectRandomLikeTerraria(IReadOnlyList<string> values, Func<int, int> next)
    {
        if (values.Count == 0)
        {
            return string.Empty;
        }

        int indexFromEnd = Math.Clamp(next(values.Count), 0, values.Count - 1);
        return values[values.Count - 1 - indexFromEnd];
    }
}

internal sealed class TerrariaWorldNameData
{
    public string[] Composition { get; set; } = [];

    public string[] Adjective { get; set; } = [];

    public string[] Location { get; set; } = [];

    public string[] Noun { get; set; } = [];

    public bool IsUsable =>
        Composition.Length > 0 &&
        Adjective.Length > 0 &&
        Location.Length > 0 &&
        Noun.Length > 0;

    public static TerrariaWorldNameData EnglishFallback { get; } = new()
    {
        Composition = ["The {Adjective} {Location}", "{Location} of {Noun}"],
        Adjective = ["Ancient", "Hidden", "Brave"],
        Location = ["Forest", "Realm", "World"],
        Noun = ["Dawn", "Stars", "Night"]
    };

    public static TerrariaWorldNameData ChineseFallback { get; } = new()
    {
        Composition = ["{Adjective}的{Location}", "{Noun}{Location}"],
        Adjective = ["古老", "隐秘", "勇敢"],
        Location = ["森林", "王国", "世界"],
        Noun = ["黎明", "星辰", "夜晚"]
    };
}
