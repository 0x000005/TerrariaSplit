namespace TerrariaSplit.Storage;

internal sealed class AppSettingsRepository : ISettingsRepository
{
    private const string DefaultSettingsFileName = "settings.json";
    private const string ActiveSettingsFileName = "active-profile.txt";

    private readonly IRuntimeDataPaths paths;
    private string activeSettingsPath;

    public AppSettingsRepository(IRuntimeDataPaths? paths = null)
    {
        this.paths = paths ?? AppContextRuntimeDataPaths.Default;
        activeSettingsPath = Path.Combine(SettingsDirectory, DefaultSettingsFileName);
    }

    public string SettingsDirectory => paths.SettingsDirectory;

    public string SettingsPath => activeSettingsPath;

    public string SettingsFileName => Path.GetFileName(SettingsPath);

    private string ActiveSettingsPath => Path.Combine(SettingsDirectory, ActiveSettingsFileName);

    public AppSettings Load()
    {
        return Load(SettingsProfileStore.GetRememberedSettingsPath(
            SettingsDirectory,
            ActiveSettingsPath,
            GetFallbackSettingsPath,
            NormalizeSettingsPath,
            SettingsSerializer.IsValidSettingsFile));
    }

    public AppSettings Load(string path)
    {
        activeSettingsPath = NormalizeSettingsPath(path);
        SettingsDocument document = ReadSettingsDocument(SettingsPath);
        AppSettings settings = document.Settings;
        string activeReferenceSplitSet = settings.Comparison.ActiveReferenceSplitSet;
        string activePersonalBestTimeSet = settings.Comparison.ActivePersonalBestTimeSet;
        string activePersonalBestSegmentSet = settings.Comparison.ActivePersonalBestSegmentSet;

        Normalize(settings);
        RestoreActiveSplitSetNames(
            settings,
            activeReferenceSplitSet,
            activePersonalBestTimeSet,
            activePersonalBestSegmentSet);
        SplitSetLoader.LoadInto(settings);

        if (document.ShouldSaveDefaults)
        {
            Save(settings);
        }

        RememberActiveSettingsFile();

        return settings;
    }

    public IReadOnlyList<string> GetSettingsFiles()
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

    public void Save(AppSettings settings)
    {
        AppSettings snapshot = Clone(settings);
        if (!snapshot.Comparison.UsePersonalBestAsReferenceTime)
        {
            SplitTimeSetStore.SaveReferenceSets(snapshot.Comparison.ReferenceSplitSets);
        }

        snapshot.SyncActivePersonalBestTimeSetFromDictionary();
        snapshot.SyncActivePersonalBestSegmentSetFromDictionary();
        SplitTimeSetStore.SavePersonalBestTimeSets(snapshot.Comparison.PersonalBestTimeSets);
        SplitTimeSetStore.SavePersonalBestSegmentSets(snapshot.Comparison.PersonalBestSegmentSets);
        string directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        SettingsSerializer.WriteSettings(SettingsPath, AppSettingsPersistenceProjection.Create(snapshot));
    }

    public AppSettings Clone(AppSettings settings)
    {
        AppSettings clone = SettingsSerializer.Clone(settings);
        Normalize(clone);
        return clone;
    }

    public void Normalize(AppSettings settings)
    {
        SettingsNormalizer.Normalize(settings);
    }

    private SettingsDocument ReadSettingsDocument(string path)
    {
        AppSettings defaults = LoadDefaultSettingsTemplate();
        AppSettings settings = SettingsSerializer.ReadSettingsWithDefaults(
            path,
            defaults,
            "settings",
            out bool shouldSaveDefaults) ?? defaults;
        return new SettingsDocument(settings, path, shouldSaveDefaults);
    }

    private static AppSettings LoadDefaultSettingsTemplate()
    {
        return AppSettingsDefaults.Create();
    }

    private string GetFallbackSettingsPath()
    {
        return GetSettingsFiles().FirstOrDefault()
            ?? Path.Combine(SettingsDirectory, DefaultSettingsFileName);
    }

    private void RememberActiveSettingsFile()
    {
        SettingsProfileStore.RememberActiveSettingsFile(SettingsDirectory, ActiveSettingsPath, SettingsFileName);
    }

    private string NormalizeSettingsPath(string path)
    {
        return SettingsProfileStore.NormalizeSettingsPath(SettingsDirectory, DefaultSettingsFileName, path);
    }

    private static void RestoreActiveSplitSetNames(
        AppSettings settings,
        string? activeReferenceSplitSet,
        string? activePersonalBestTimeSet,
        string? activePersonalBestSegmentSet)
    {
        if (!string.IsNullOrWhiteSpace(activeReferenceSplitSet))
        {
            settings.Comparison.ActiveReferenceSplitSet = activeReferenceSplitSet.Trim();
        }

        if (!string.IsNullOrWhiteSpace(activePersonalBestTimeSet))
        {
            settings.Comparison.ActivePersonalBestTimeSet = activePersonalBestTimeSet.Trim();
        }

        if (!string.IsNullOrWhiteSpace(activePersonalBestSegmentSet))
        {
            settings.Comparison.ActivePersonalBestSegmentSet = activePersonalBestSegmentSet.Trim();
        }
    }
}
