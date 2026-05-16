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
        settings = SettingsSerializer.ReadSettings(SettingsPath, "settings") ?? LoadDefaultSettingsTemplate();
        shouldSave = !File.Exists(SettingsPath);

        Normalize(settings);
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
        return SettingsSerializer.ReadSettings(DefaultSettingsTemplatePath, "default settings template")
            ?? throw new InvalidOperationException($"Default settings template is missing or invalid: {DefaultSettingsTemplatePath}");
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
}
