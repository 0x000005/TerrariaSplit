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

        settings.BossIconPaths ??= new Dictionary<string, string>();
        settings.ReferenceSplitSets ??= new List<ReferenceSplitSet>();
        settings.Colors ??= new UiColorSettings();
        settings.Columns ??= new UiColumnLayoutSettings();
        NormalizeColumnSettings(settings.Columns);

        foreach (BossSplitDefinition definition in BossSplitDefinitions.All)
        {
            settings.BossIconPaths.TryAdd(definition.Name.ToString(), string.Empty);
        }

        NormalizeReferenceSets(settings);

        if (shouldSave)
        {
            Save(settings);
        }

        return settings;
    }

    public static void Save(AppSettings settings)
    {
        string directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static AppSettings Clone(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.CreateDefault();
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

            foreach (BossSplitDefinition definition in BossSplitDefinitions.All)
            {
                set.Splits.TryAdd(definition.Name.ToString(), string.Empty);
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
}
