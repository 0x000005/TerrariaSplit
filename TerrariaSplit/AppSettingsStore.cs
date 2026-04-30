using System.Text.Json;

namespace TerrariaSplit;

internal static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string SettingsPath
    {
        get
        {
            return Path.Combine(AppContext.BaseDirectory, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        AppSettings settings;
        bool shouldSave = false;
        try
        {
            settings = File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions)
                    ?? AppSettings.CreateDefault()
                : AppSettings.CreateDefault();
            shouldSave = !File.Exists(SettingsPath);
        }
        catch (Exception)
        {
            settings = AppSettings.CreateDefault();
            shouldSave = true;
        }

        Normalize(settings);

        if (shouldSave)
        {
            Save(settings);
        }

        return settings;
    }

    public static void Save(AppSettings settings)
    {
        Normalize(settings);
        string directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static AppSettings Clone(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        AppSettings clone = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.CreateDefault();
        Normalize(clone);
        return clone;
    }

    public static void Normalize(AppSettings settings)
    {
        settings.Route ??= new List<BossRouteEntry>();
        settings.BossIconPaths ??= new Dictionary<string, string>();
        settings.ReferenceSplitSets ??= new List<ReferenceSplitSet>();
        settings.Colors ??= new UiColorSettings();
        settings.Columns ??= new UiColumnLayoutSettings();
        NormalizeRoute(settings);
        NormalizeColumnSettings(settings.Columns);

        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            settings.BossIconPaths.TryAdd(unit.Id, string.Empty);
        }

        NormalizeReferenceSets(settings);
    }

    private static void NormalizeColumnSettings(UiColumnLayoutSettings columns)
    {
        var defaults = new UiColumnLayoutSettings();
        columns.Icon ??= defaults.Icon;
        columns.Time ??= defaults.Time;
        columns.Delta ??= defaults.Delta;
        columns.Timer ??= defaults.Timer;
        columns.TimerMilliseconds ??= defaults.TimerMilliseconds;

        NormalizeColumn(columns.Icon, defaults.Icon);
        NormalizeColumn(columns.Time, defaults.Time);
        NormalizeColumn(columns.Delta, defaults.Delta);
        NormalizeColumn(columns.Timer, defaults.Timer);
        NormalizeColumn(columns.TimerMilliseconds, defaults.TimerMilliseconds);
    }

    private static void NormalizeColumn(UiColumnSettings column, UiColumnSettings defaults)
    {
        if (column.Width <= 0)
        {
            column.Width = defaults.Width;
        }

        if (column.FontSize <= 0)
        {
            column.FontSize = defaults.FontSize;
        }
    }

    private static void NormalizeReferenceSets(AppSettings settings)
    {
        if (settings.ReferenceSplitSets.Count == 0)
        {
            settings.ReferenceSplitSets.Add(AppSettings.CreateReferenceSet("WR"));
        }

        foreach (ReferenceSplitSet set in settings.ReferenceSplitSets)
        {
            set.Name = string.IsNullOrWhiteSpace(set.Name) ? "Reference" : set.Name.Trim();
            set.Splits ??= new Dictionary<string, string>();

            foreach (BossSplitDefinition definition in BossSplitDefinitions.Build(settings))
            {
                set.Splits.TryAdd(definition.Name, string.Empty);
            }
        }

        if (string.IsNullOrWhiteSpace(settings.ActiveReferenceSplitSet) ||
            !settings.ReferenceSplitSets.Any(set => string.Equals(
                set.Name,
                settings.ActiveReferenceSplitSet,
                StringComparison.OrdinalIgnoreCase)))
        {
            settings.ActiveReferenceSplitSet = settings.ReferenceSplitSets[0].Name;
        }
    }

    private static void NormalizeRoute(AppSettings settings)
    {
        List<BossRouteEntry> defaults = BossSplitDefinitions.CreateDefaultRoute();
        if (settings.Route.Count == 0)
        {
            settings.Route = defaults;
            return;
        }

        Dictionary<string, BossRouteEntry> existing = settings.Route
            .Where(entry => !string.IsNullOrWhiteSpace(entry.BossId))
            .GroupBy(entry => entry.BossId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var normalized = new List<BossRouteEntry>();
        foreach (BossRouteEntry defaultEntry in defaults)
        {
            if (!existing.TryGetValue(defaultEntry.BossId, out BossRouteEntry? entry))
            {
                normalized.Add(defaultEntry);
                continue;
            }

            normalized.Add(new BossRouteEntry
            {
                BossId = defaultEntry.BossId,
                Enabled = entry.Enabled,
                Segment = Math.Clamp(entry.Segment, 1m, 99m)
            });
        }

        settings.Route = normalized;
    }
}
