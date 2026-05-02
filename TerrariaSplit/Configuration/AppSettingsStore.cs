using System.Text.Json;

namespace TerrariaSplit;

internal static class AppSettingsStore
{
    private const string DefaultSettingsFileName = "settings.json";
    private const string ActiveSettingsFileName = "active-profile.txt";
    private static string activeSettingsPath = Path.Combine(SettingsDirectory, DefaultSettingsFileName);

    public static string SettingsDirectory => Path.Combine(AppContext.BaseDirectory, "settings");

    private static string DefaultSettingsTemplatePath => Path.Combine(AppContext.BaseDirectory, "Assets", "DefaultSettings", DefaultSettingsFileName);

    private static string ActiveSettingsPath => Path.Combine(SettingsDirectory, ActiveSettingsFileName);

    public static string SettingsPath
    {
        get => activeSettingsPath;
    }

    public static string SettingsFileName => Path.GetFileName(SettingsPath);

    public static AppSettings Load()
    {
        return Load(GetRememberedSettingsPath());
    }

    public static AppSettings Load(string path)
    {
        AppSettings settings;
        bool shouldSave = false;
        activeSettingsPath = NormalizeSettingsPath(path);
        settings = JsonFileStore.Read<AppSettings>(SettingsPath, "settings") ?? LoadDefaultSettingsTemplate();
        shouldSave = !File.Exists(SettingsPath);

        Normalize(settings);
        LoadExternalReferenceSets(settings);

        if (shouldSave)
        {
            Save(settings);
        }

        RememberActiveSettingsFile();

        return settings;
    }

    public static IReadOnlyList<string> GetSettingsFiles()
    {
        Directory.CreateDirectory(SettingsDirectory);
        return Directory.EnumerateFiles(SettingsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(IsValidSettingsFile)
            .OrderBy(path => string.Equals(
                Path.GetFileName(path),
                DefaultSettingsFileName,
                StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
        AppSettings clone = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, JsonFileStore.JsonOptions) ?? new AppSettings();
        Normalize(clone);
        return clone;
    }

    private static AppSettings LoadDefaultSettingsTemplate()
    {
        return JsonFileStore.Read<AppSettings>(DefaultSettingsTemplatePath, "default settings template")
            ?? throw new InvalidOperationException($"Default settings template is missing or invalid: {DefaultSettingsTemplatePath}");
    }

    private static string GetRememberedSettingsPath()
    {
        Directory.CreateDirectory(SettingsDirectory);
        if (!File.Exists(ActiveSettingsPath))
        {
            return GetFallbackSettingsPath();
        }

        try
        {
            string fileName = Path.GetFileName(File.ReadAllText(ActiveSettingsPath).Trim());
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return GetFallbackSettingsPath();
            }

            string path = NormalizeSettingsPath(fileName);
            return File.Exists(path) && IsValidSettingsFile(path)
                ? path
                : GetFallbackSettingsPath();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Failed to read active settings profile: {ActiveSettingsPath}");
            return GetFallbackSettingsPath();
        }
    }

    private static string GetFallbackSettingsPath()
    {
        return GetSettingsFiles().FirstOrDefault()
            ?? Path.Combine(SettingsDirectory, DefaultSettingsFileName);
    }

    private static void RememberActiveSettingsFile()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(ActiveSettingsPath, SettingsFileName);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Failed to write active settings profile: {ActiveSettingsPath}");
        }
    }

    private static bool IsValidSettingsFile(string path)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            JsonElement root = document.RootElement;
            return root.TryGetProperty(nameof(AppSettings.Route), out _) ||
                root.TryGetProperty(nameof(AppSettings.Columns), out _) ||
                root.TryGetProperty(nameof(AppSettings.Colors), out _) ||
                root.TryGetProperty(nameof(AppSettings.ReferenceSplitSets), out _) ||
                root.TryGetProperty(nameof(AppSettings.BossIconPaths), out _) ||
                root.TryGetProperty(nameof(AppSettings.ShowSplitCompletionAnimation), out _);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Ignored invalid settings file: {path}");
            return false;
        }
    }

    private static string NormalizeSettingsPath(string path)
    {
        string fileName = Path.GetFileName(string.IsNullOrWhiteSpace(path) ? DefaultSettingsFileName : path);
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".json";
        }

        return Path.Combine(SettingsDirectory, fileName);
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
        settings.SplitCompletionOutlineSplitStyles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.SplitCompletionOutlineSegmentStyles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.SegmentBestDeltaHighlightStyles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.Colors ??= new UiColorSettings();
        settings.Sounds ??= new UiSoundSettings();
        settings.Columns ??= new UiColumnLayoutSettings();
        settings.SplitCompletionAnimationDurationSeconds = Math.Clamp(settings.SplitCompletionAnimationDurationSeconds, 1f, 20f);
        settings.SplitCompletionOutlineThicknessPercent = Math.Clamp(settings.SplitCompletionOutlineThicknessPercent, 0, 100);
        settings.CurrentSplitHighlightScalePercent = Math.Clamp(settings.CurrentSplitHighlightScalePercent, 100, 140);
        settings.CurrentSplitDepthStrengthPercent = Math.Clamp(settings.CurrentSplitDepthStrengthPercent, 0, 100);
        settings.EarlyDeltaTimeSeconds = Math.Clamp(settings.EarlyDeltaTimeSeconds, 0, 3600);
        settings.CurrentBossIconGrayscaleWeakenPercent = Math.Clamp(settings.CurrentBossIconGrayscaleWeakenPercent, 0, 100);
        settings.CurrentBossIconBrightnessBoostPercent = Math.Clamp(settings.CurrentBossIconBrightnessBoostPercent, 0, 100);
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
            settings.SplitCompletionOutlineSplitStyles[group.Key] = GetNormalizedOutlineStyle(settings.SplitCompletionOutlineSplitStyles, group.Key);
            settings.SplitCompletionOutlineSegmentStyles[group.Key] = GetNormalizedOutlineStyle(settings.SplitCompletionOutlineSegmentStyles, group.Key);
            settings.SegmentBestDeltaHighlightStyles[group.Key] = GetNormalizedDeltaHighlightStyle(settings.SegmentBestDeltaHighlightStyles, group.Key);
        }

        RemoveUnknownRouteGroupKeys(settings);

        NormalizeReferenceSets(settings);
    }

    private static void LoadExternalReferenceSets(AppSettings settings)
    {
        settings.ReferenceSplitSets = SplitTimeSetStore.LoadReferenceSets();
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
        RemoveKeysExcept(settings.SplitCompletionOutlineSplitStyles, validGroupKeys);
        RemoveKeysExcept(settings.SplitCompletionOutlineSegmentStyles, validGroupKeys);
        RemoveKeysExcept(settings.SegmentBestDeltaHighlightStyles, validGroupKeys);
    }

    private static string GetNormalizedDeltaHighlightStyle(Dictionary<string, string> styles, string key)
    {
        return styles.TryGetValue(key, out string? existing)
            ? SegmentBestDeltaHighlightStyles.Normalize(existing)
            : SegmentBestDeltaHighlightStyles.Aurora;
    }

    private static string GetNormalizedOutlineStyle(Dictionary<string, string> styles, string key)
    {
        return styles.TryGetValue(key, out string? existing)
            ? SplitCompletionOutlineStyles.Normalize(existing)
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
