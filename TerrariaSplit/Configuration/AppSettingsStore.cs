namespace TerrariaSplit.Configuration;

internal static class AppSettingsStore
{
    private const string DefaultSettingsFileName = "settings.json";
    private const string ActiveSettingsFileName = "active-profile.txt";
    private static string activeSettingsPath = Path.Combine(SettingsDirectory, DefaultSettingsFileName);

    public static string SettingsDirectory => RuntimeDataPaths.SettingsDirectory;

    private static string ActiveSettingsPath => Path.Combine(SettingsDirectory, ActiveSettingsFileName);

    public static string SettingsPath
    {
        get => activeSettingsPath;
    }

    public static string SettingsFileName => Path.GetFileName(SettingsPath);

    public static AppSettings Load()
    {
        return Load(SettingsProfileStore.GetRememberedSettingsPath(
            SettingsDirectory,
            ActiveSettingsPath,
            GetFallbackSettingsPath,
            NormalizeSettingsPath,
            SettingsSerializer.IsValidSettingsFile));
    }

    public static AppSettings Load(string path)
    {
        AppSettings settings;
        bool shouldSave = false;
        activeSettingsPath = NormalizeSettingsPath(path);
        AppSettings defaults = LoadDefaultSettingsTemplate();
        settings = SettingsSerializer.ReadSettingsWithDefaults(
            SettingsPath,
            defaults,
            "settings",
            out shouldSave) ?? defaults;
        string activeReferenceSplitSet = settings.ActiveReferenceSplitSet;
        string activePersonalBestTimeSet = settings.ActivePersonalBestTimeSet;
        string activePersonalBestSegmentSet = settings.ActivePersonalBestSegmentSet;

        Normalize(settings);
        RestoreActiveSplitSetNames(
            settings,
            activeReferenceSplitSet,
            activePersonalBestTimeSet,
            activePersonalBestSegmentSet);
        SplitSetLoader.LoadInto(settings);

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
            .Where(SettingsSerializer.IsValidSettingsFile)
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
        if (!settings.UsePersonalBestAsReferenceTime)
        {
            SplitTimeSetStore.SaveReferenceSets(settings.ReferenceSplitSets);
        }

        settings.SyncActivePersonalBestTimeSetFromDictionary();
        settings.SyncActivePersonalBestSegmentSetFromDictionary();
        SplitTimeSetStore.SavePersonalBestTimeSets(settings.PersonalBestTimeSets);
        SplitTimeSetStore.SavePersonalBestSegmentSets(settings.PersonalBestSegmentSets);
        List<ReferenceSplitSet> referenceSets = settings.ReferenceSplitSets;
        List<ReferenceSplitSet> personalBestTimeSets = settings.PersonalBestTimeSets;
        List<ReferenceSplitSet> personalBestSegmentSets = settings.PersonalBestSegmentSets;
        settings.ReferenceSplitSets = new List<ReferenceSplitSet>();
        settings.PersonalBestTimeSets = new List<ReferenceSplitSet>();
        settings.PersonalBestSegmentSets = new List<ReferenceSplitSet>();
        string directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        try
        {
            SettingsSerializer.WriteSettings(SettingsPath, settings);
        }
        finally
        {
            settings.ReferenceSplitSets = referenceSets;
            settings.PersonalBestTimeSets = personalBestTimeSets;
            settings.PersonalBestSegmentSets = personalBestSegmentSets;
        }
    }

    public static AppSettings Clone(AppSettings settings)
    {
        AppSettings clone = SettingsSerializer.Clone(settings);
        Normalize(clone);
        return clone;
    }

    private static AppSettings LoadDefaultSettingsTemplate()
    {
        return AppSettingsDefaults.Create();
    }

    private static string GetFallbackSettingsPath()
    {
        return GetSettingsFiles().FirstOrDefault()
            ?? Path.Combine(SettingsDirectory, DefaultSettingsFileName);
    }

    private static void RememberActiveSettingsFile()
    {
        SettingsProfileStore.RememberActiveSettingsFile(SettingsDirectory, ActiveSettingsPath, SettingsFileName);
    }

    private static string NormalizeSettingsPath(string path)
    {
        return SettingsProfileStore.NormalizeSettingsPath(SettingsDirectory, DefaultSettingsFileName, path);
    }

    public static void Normalize(AppSettings settings)
    {
        SettingsNormalizer.Normalize(settings);
    }

    private static void RestoreActiveSplitSetNames(
        AppSettings settings,
        string? activeReferenceSplitSet,
        string? activePersonalBestTimeSet,
        string? activePersonalBestSegmentSet)
    {
        if (!string.IsNullOrWhiteSpace(activeReferenceSplitSet))
        {
            settings.ActiveReferenceSplitSet = activeReferenceSplitSet.Trim();
        }

        if (!string.IsNullOrWhiteSpace(activePersonalBestTimeSet))
        {
            settings.ActivePersonalBestTimeSet = activePersonalBestTimeSet.Trim();
        }

        if (!string.IsNullOrWhiteSpace(activePersonalBestSegmentSet))
        {
            settings.ActivePersonalBestSegmentSet = activePersonalBestSegmentSet.Trim();
        }
    }
}
