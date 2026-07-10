namespace TerrariaSplit.Storage;

public sealed class AppSettingsRepository : ISettingsRepository
{
    private const string DefaultSettingsFileName = "settings.json";
    private const string ActiveSettingsFileName = "active-profile.txt";

    private readonly IRuntimeDataPaths paths;
    private readonly SplitTimeSetRepository splitTimeSets;
    private readonly SplitSetLoader splitSetLoader;
    private string activeSettingsPath;

    public AppSettingsRepository(IRuntimeDataPaths? paths = null, SplitTimeSetRepository? splitTimeSets = null)
    {
        this.paths = paths ?? AppContextRuntimeDataPaths.Default;
        this.splitTimeSets = splitTimeSets ?? new SplitTimeSetRepository(this.paths);
        splitSetLoader = new SplitSetLoader(this.splitTimeSets);
        activeSettingsPath = Path.Combine(SettingsDirectory, DefaultSettingsFileName);
    }

    public string SettingsDirectory => paths.SettingsDirectory;

    public string SettingsPath => activeSettingsPath;

    public string SettingsFileName => Path.GetFileName(SettingsPath);

    private string ActiveSettingsPath => Path.Combine(SettingsDirectory, ActiveSettingsFileName);

    public AppSettings Load()
    {
        var validatedJson = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool ValidateAndCache(string path)
        {
            if (!SettingsSerializer.TryReadValidSettingsFile(path, out string json))
            {
                return false;
            }

            validatedJson[path] = json;
            return true;
        }

        string path = SettingsProfileStore.GetRememberedSettingsPath(
            SettingsDirectory,
            ActiveSettingsPath,
            () => GetFallbackSettingsPath(ValidateAndCache),
            NormalizeSettingsPath,
            ValidateAndCache);
        return validatedJson.TryGetValue(path, out string? json)
            ? Load(path, json)
            : Load(path);
    }

    public AppSettings Load(string path)
    {
        return Load(path, validatedJson: null);
    }

    private AppSettings Load(string path, string? validatedJson)
    {
        activeSettingsPath = NormalizeSettingsPath(path);
        LoadedSettingsDocument document = validatedJson is null
            ? ReadSettingsDocument(SettingsPath)
            : ReadSettingsDocument(SettingsPath, validatedJson);
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
        splitSetLoader.LoadInto(settings);

        if (document.ShouldSaveDefaults)
        {
            Save(settings);
        }

        RememberActiveSettingsFile();

        return settings;
    }

    public IReadOnlyList<string> GetSettingsFiles()
    {
        return EnumerateOrderedSettingsFiles()
            .Where(SettingsSerializer.IsValidSettingsFile)
            .ToList();
    }

    public OperationResult Save(AppSettings settings)
    {
        AppSettings snapshot = Clone(settings);
        try
        {
            if (!snapshot.Comparison.UsePersonalBestAsReferenceTime)
            {
                splitTimeSets.SaveReferenceSets(snapshot.Comparison.ReferenceSplitSets);
            }

            PersonalBestSetService.SyncActivePersonalBestTimeSetFromDictionary(snapshot);
            PersonalBestSetService.SyncActivePersonalBestSegmentSetFromDictionary(snapshot);
            splitTimeSets.SavePersonalBestTimeSets(snapshot.Comparison.PersonalBestTimeSets);
            splitTimeSets.SavePersonalBestSegmentSets(snapshot.Comparison.PersonalBestSegmentSets);
            string directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            return SettingsSerializer.WriteSettings(SettingsPath, AppSettingsPersistenceProjection.Create(snapshot));
        }
        catch (Exception ex)
        {
            StaticAppLogger.Instance.Error(ex, $"Failed to save settings: {SettingsPath}");
            return OperationResult.Failure("Failed to save settings.", ex);
        }
    }

    public OperationResult TrySave(AppSettings settings)
    {
        return Save(settings);
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

    private LoadedSettingsDocument ReadSettingsDocument(string path)
    {
        AppSettings settings = SettingsSerializer.ReadSettingsWithEmbeddedDefaults(
            path,
            "settings",
            out bool shouldSaveDefaults) ?? AppSettingsDefaults.Create();
        return new LoadedSettingsDocument(settings, path, shouldSaveDefaults);
    }

    private LoadedSettingsDocument ReadSettingsDocument(string path, string validatedJson)
    {
        AppSettings settings = SettingsSerializer.ReadSettingsWithEmbeddedDefaults(
            validatedJson,
            sourceExists: true,
            "settings",
            out bool shouldSaveDefaults) ?? AppSettingsDefaults.Create();
        return new LoadedSettingsDocument(settings, path, shouldSaveDefaults);
    }

    private string GetFallbackSettingsPath(Func<string, bool> isValidSettingsFile)
    {
        return EnumerateOrderedSettingsFiles().FirstOrDefault(isValidSettingsFile)
            ?? Path.Combine(SettingsDirectory, DefaultSettingsFileName);
    }

    private IEnumerable<string> EnumerateOrderedSettingsFiles()
    {
        Directory.CreateDirectory(SettingsDirectory);
        return Directory.EnumerateFiles(SettingsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => string.Equals(
                Path.GetFileName(path),
                DefaultSettingsFileName,
                StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
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
