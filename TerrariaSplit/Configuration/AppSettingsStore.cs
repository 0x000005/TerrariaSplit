namespace TerrariaSplit;

internal static class AppSettingsStore
{
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
        settings = JsonFileStore.Read<AppSettings>(SettingsPath, "settings") ?? AppSettings.CreateDefault();
        shouldSave = !File.Exists(SettingsPath);

        Normalize(settings);
        LoadExternalReferenceSets(settings);

        if (shouldSave)
        {
            Save(settings);
        }

        return settings;
    }

    public static void Save(AppSettings settings)
    {
        Normalize(settings);
        SplitTimeSetStore.SaveReferenceSets(settings.ReferenceSplitSets);
        List<ReferenceSplitSet> referenceSets = settings.ReferenceSplitSets;
        settings.ReferenceSplitSets = new List<ReferenceSplitSet>();
        string directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        try
        {
            JsonFileStore.Write(SettingsPath, settings, "settings");
        }
        finally
        {
            settings.ReferenceSplitSets = referenceSets;
        }
    }

    public static AppSettings Clone(AppSettings settings)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(settings, JsonFileStore.JsonOptions);
        AppSettings clone = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, JsonFileStore.JsonOptions) ?? AppSettings.CreateDefault();
        Normalize(clone);
        return clone;
    }

    public static void Normalize(AppSettings settings)
    {
        settings.Route ??= new List<BossRouteEntry>();
        settings.BossIconPaths ??= new Dictionary<string, string>();
        settings.ReferenceSplitSets ??= new List<ReferenceSplitSet>();
        settings.PersonalBestTimes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.PersonalBestSegmentTimes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.Colors ??= new UiColorSettings();
        settings.Columns ??= new UiColumnLayoutSettings();
        NormalizeRoute(settings);
        NormalizeColumnSettings(settings.Columns);

        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            settings.BossIconPaths.TryAdd(unit.Id, string.Empty);
            settings.PersonalBestTimes.TryAdd(unit.Id, string.Empty);
        }

        foreach (RouteGroup group in BossRouteGroups.Build(settings))
        {
            settings.PersonalBestSegmentTimes.TryAdd(group.Key, string.Empty);
        }

        NormalizeReferenceSets(settings);
    }

    private static void LoadExternalReferenceSets(AppSettings settings)
    {
        List<ReferenceSplitSet> externalSets = SplitTimeSetStore.LoadReferenceSets();
        bool hasOldSettingsSets = settings.ReferenceSplitSets.Count > 0;
        bool externalOnlyDefault = externalSets.Count == 1 &&
            string.Equals(externalSets[0].Name, "WR", StringComparison.OrdinalIgnoreCase) &&
            externalSets[0].Splits.Values.All(string.IsNullOrWhiteSpace);

        if (hasOldSettingsSets && externalOnlyDefault)
        {
            SplitTimeSetStore.SaveReferenceSets(settings.ReferenceSplitSets);
            return;
        }

        settings.ReferenceSplitSets = externalSets;
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

            foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
            {
                set.Splits.TryAdd(unit.Id, string.Empty);
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
