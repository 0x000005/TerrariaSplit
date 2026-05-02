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
        settings.SplitCompletionSplitComparisons ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        settings.SplitCompletionSegmentComparisons ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        settings.SplitCompletionOutlineSplitTimes ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        settings.SplitCompletionOutlineSegmentTimes ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        settings.SplitCompletionOutlineSplitStyles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.SplitCompletionOutlineSegmentStyles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.Colors ??= new UiColorSettings();
        settings.Columns ??= new UiColumnLayoutSettings();
        settings.SplitCompletionOutlineThicknessPercent = Math.Clamp(settings.SplitCompletionOutlineThicknessPercent, 0, 100);
        NormalizeRoute(settings);
        NormalizeColumnSettings(settings.Columns);
        RemoveUnknownBossUnitKeys(settings);

        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            settings.BossIconPaths.TryAdd(unit.Id, string.Empty);
            settings.PersonalBestTimes.TryAdd(unit.Id, string.Empty);
        }

        foreach (RouteGroup group in BossRouteGroups.Build(settings))
        {
            settings.PersonalBestSegmentTimes.TryAdd(group.Key, string.Empty);
            settings.SplitCompletionSplitComparisons.TryAdd(group.Key, true);
            settings.SplitCompletionSegmentComparisons.TryAdd(group.Key, true);
            settings.SplitCompletionOutlineSplitTimes.TryAdd(group.Key, true);
            settings.SplitCompletionOutlineSegmentTimes.TryAdd(group.Key, true);
            settings.SplitCompletionOutlineSplitStyles[group.Key] = GetNormalizedOutlineStyle(
                settings.SplitCompletionOutlineSplitStyles,
                settings.SplitCompletionOutlineSplitTimes,
                group.Key);
            settings.SplitCompletionOutlineSegmentStyles[group.Key] = GetNormalizedOutlineStyle(
                settings.SplitCompletionOutlineSegmentStyles,
                settings.SplitCompletionOutlineSegmentTimes,
                group.Key);
        }

        RemoveUnknownRouteGroupKeys(settings);

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
        columns.ScalePercent = Math.Clamp(columns.ScalePercent, 25, 300);

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

        bool hadRemovedSegmentSevenEntry = settings.Route.Any(entry =>
            Math.Truncate(entry.Segment) == 7m &&
            defaults.All(defaultEntry => !string.Equals(defaultEntry.BossId, entry.BossId, StringComparison.OrdinalIgnoreCase)));
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

        if (hadRemovedSegmentSevenEntry &&
            normalized.All(entry => Math.Truncate(entry.Segment) != 7m) &&
            normalized.FirstOrDefault(entry => string.Equals(entry.BossId, BossSplitDefinitions.MoonLord, StringComparison.OrdinalIgnoreCase)) is BossRouteEntry moonLordEntry &&
            moonLordEntry.Segment >= 8m)
        {
            moonLordEntry.Segment = 7m;
        }

        settings.Route = normalized;
    }

    private static void RemoveUnknownBossUnitKeys(AppSettings settings)
    {
        HashSet<string> validBossIds = BossSplitDefinitions.Units
            .Select(unit => unit.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        RemoveKeysExcept(settings.BossIconPaths, validBossIds);
        RemoveKeysExcept(settings.PersonalBestTimes, validBossIds);

        foreach (ReferenceSplitSet set in settings.ReferenceSplitSets)
        {
            RemoveKeysExcept(set.Splits, validBossIds);
        }
    }

    private static void RemoveUnknownRouteGroupKeys(AppSettings settings)
    {
        HashSet<string> validGroupKeys = BossRouteGroups.Build(settings)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        RemoveKeysExcept(settings.PersonalBestSegmentTimes, validGroupKeys);
        RemoveKeysExcept(settings.SplitCompletionSplitComparisons, validGroupKeys);
        RemoveKeysExcept(settings.SplitCompletionSegmentComparisons, validGroupKeys);
        RemoveKeysExcept(settings.SplitCompletionOutlineSplitTimes, validGroupKeys);
        RemoveKeysExcept(settings.SplitCompletionOutlineSegmentTimes, validGroupKeys);
        RemoveKeysExcept(settings.SplitCompletionOutlineSplitStyles, validGroupKeys);
        RemoveKeysExcept(settings.SplitCompletionOutlineSegmentStyles, validGroupKeys);
    }

    private static string GetNormalizedOutlineStyle(
        Dictionary<string, string> styles,
        Dictionary<string, bool> legacyEnabled,
        string key)
    {
        if (styles.TryGetValue(key, out string? existing))
        {
            return SplitCompletionOutlineStyles.Normalize(existing);
        }

        return legacyEnabled.TryGetValue(key, out bool enabled) && !enabled
            ? SplitCompletionOutlineStyles.None
            : SplitCompletionOutlineStyles.Rainbow;
    }

    private static void RemoveKeysExcept<TValue>(Dictionary<string, TValue> values, HashSet<string> allowedKeys)
    {
        foreach (string key in values.Keys.ToArray())
        {
            if (!allowedKeys.Contains(key))
            {
                values.Remove(key);
            }
        }
    }
}
