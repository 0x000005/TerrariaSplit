namespace TerrariaSplit.Storage;

public sealed class SplitTimeSetRepository
{
    private const int MaxLastRunSetsToLoad = 100;
    private readonly IRuntimeDataPaths paths;

    public SplitTimeSetRepository(IRuntimeDataPaths? paths = null)
    {
        this.paths = paths ?? AppContextRuntimeDataPaths.Default;
    }

    public string ReferenceDirectory => paths.ReferenceTimesDirectory;

    public string LastRunDirectory => paths.LastRunTimesDirectory;

    public string PersonalBestTimeDirectory => paths.PersonalBestTimesDirectory;

    public string PersonalBestSegmentDirectory => paths.PersonalBestSegmentsDirectory;

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(ReferenceDirectory);
        Directory.CreateDirectory(LastRunDirectory);
        Directory.CreateDirectory(PersonalBestTimeDirectory);
        Directory.CreateDirectory(PersonalBestSegmentDirectory);
    }

    public List<ReferenceSplitSet> LoadReferenceSets()
    {
        List<ReferenceSplitSet> sets = LoadSets(ReferenceDirectory);
        return sets.Count == 0 || AreReferenceSetsEmpty(sets)
            ? LoadAndSaveDefaultReferenceSets()
            : sets;
    }

    public OperationResult SaveReferenceSets(IEnumerable<ReferenceSplitSet> sets)
    {
        return SaveNamedSets(
            ReferenceDirectory,
            sets,
            "reference split time set");
    }

    public List<ReferenceSplitSet> LoadPersonalBestTimeSets()
    {
        return LoadSets(PersonalBestTimeDirectory, newestFirst: true, maxCount: null);
    }

    public List<ReferenceSplitSet> LoadPersonalBestSegmentSets()
    {
        return LoadSets(PersonalBestSegmentDirectory, newestFirst: true, maxCount: null);
    }

    public OperationResult SavePersonalBestTimeSets(IEnumerable<ReferenceSplitSet> sets)
    {
        return SaveSets(
            PersonalBestTimeDirectory,
            sets,
            "personal best time set");
    }

    public OperationResult SavePersonalBestSegmentSets(IEnumerable<ReferenceSplitSet> sets)
    {
        return SaveSets(
            PersonalBestSegmentDirectory,
            sets,
            "personal best segment set");
    }

    public OperationResult TrySavePersonalBestTimeSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime,
        out ReferenceSplitSet? snapshot)
    {
        return TrySavePersonalBestSnapshot(
            PersonalBestTimeDirectory,
            "personal best time set",
            splits,
            bossName,
            previousTime,
            newTime,
            out snapshot);
    }

    public OperationResult TrySavePersonalBestSegmentSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime,
        out ReferenceSplitSet? snapshot)
    {
        return TrySavePersonalBestSnapshot(
            PersonalBestSegmentDirectory,
            "personal best segment set",
            splits,
            bossName,
            previousTime,
            newTime,
            out snapshot);
    }

    public Dictionary<string, string> LoadLatestLastRun()
    {
        ReferenceSplitSet? latest = LoadLatestSet(LastRunDirectory);
        return latest?.Splits ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public List<ReferenceSplitSet> LoadLastRunSets()
    {
        return LoadSets(LastRunDirectory, newestFirst: true, MaxLastRunSetsToLoad);
    }

    public void SaveLastRun(Dictionary<string, string> splits, string? lastBossName = null, TimeSpan? runDuration = null)
    {
        string name = BuildLastRunName(lastBossName, runDuration);
        SaveSet(LastRunDirectory, $"{name}.json", new ReferenceSplitSet
        {
            Name = name,
            Splits = new Dictionary<string, string>(splits, StringComparer.OrdinalIgnoreCase)
        });
    }

    private static List<ReferenceSplitSet> LoadSets(string directory)
    {
        return LoadSets(directory, newestFirst: false, maxCount: null);
    }

    private List<ReferenceSplitSet> LoadAndSaveDefaultReferenceSets()
    {
        ReferenceSplitSet set = LoadEmbeddedDefaultReferenceSet();
        _ = TrySaveSet(
            ReferenceDirectory,
            $"{SanitizeFileName(set.Name)}.json",
            set,
            "default reference split time set");
        return new List<ReferenceSplitSet> { set };
    }

    private static ReferenceSplitSet LoadEmbeddedDefaultReferenceSet()
    {
        try
        {
            ReferenceSplitSet? set = System.Text.Json.JsonSerializer.Deserialize<ReferenceSplitSet>(
                EmbeddedDefaults.ReferenceTimesWrJson,
                JsonFileStore.JsonOptions);
            if (set is not null)
            {
                set.Name = string.IsNullOrWhiteSpace(set.Name) ? "WR" : set.Name.Trim();
                set.Splits = new Dictionary<string, string>(
                    set.Splits ?? new Dictionary<string, string>(),
                    StringComparer.OrdinalIgnoreCase);
                return set;
            }
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, "Failed to load embedded default WR reference times.");
        }

        return ReferenceSplitSetService.CreateReferenceSet("WR");
    }

    private static bool AreReferenceSetsEmpty(IEnumerable<ReferenceSplitSet> sets)
    {
        return sets.All(set => set.Splits.Values.All(string.IsNullOrWhiteSpace));
    }

    private static List<ReferenceSplitSet> LoadSets(string directory, bool newestFirst, int? maxCount)
    {
        if (!Directory.Exists(directory))
        {
            return new List<ReferenceSplitSet>();
        }

        IEnumerable<string> paths = Directory.EnumerateFiles(directory, "*.json");
        paths = newestFirst
            ? paths.OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            : paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        if (maxCount is int limit)
        {
            paths = paths.Take(limit);
        }

        var sets = new List<ReferenceSplitSet>();
        foreach (string path in paths)
        {
            ReferenceSplitSet? set = LoadSet(path);
            if (set is not null)
            {
                sets.Add(set);
            }
        }

        return sets;
    }

    private static ReferenceSplitSet? LoadLatestSet(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.EnumerateFiles(directory, "*.json")
            .OrderByDescending(path => path)
            .Select(LoadSet)
            .FirstOrDefault(set => set is not null);
    }

    private static ReferenceSplitSet? LoadSet(string path)
    {
        try
        {
            ReferenceSplitSet? set = JsonFileStore.Read<ReferenceSplitSet>(path, "split time set");
            if (set is null)
            {
                return null;
            }

            set.Name = string.IsNullOrWhiteSpace(set.Name)
                ? Path.GetFileNameWithoutExtension(path)
                : set.Name.Trim();
            set.Splits ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return set;
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to load split time set: {path}");
            return null;
        }
    }

    private static OperationResult SaveNamedSets(
        string directory,
        IEnumerable<ReferenceSplitSet> sets,
        string description)
    {
        try
        {
            Directory.CreateDirectory(directory);
            HashSet<string> expectedFiles = new(StringComparer.OrdinalIgnoreCase);

            foreach (ReferenceSplitSet set in sets)
            {
                string fileName = $"{SanitizeFileName(set.Name)}.json";
                expectedFiles.Add(Path.Combine(directory, fileName));
                OperationResult saveResult = TrySaveSet(
                    directory,
                    fileName,
                    set,
                    description);
                if (saveResult.Failed)
                {
                    return OperationResult.Failure(
                        $"Failed to save {description} '{set.Name}' in '{directory}'.",
                        saveResult.Exception);
                }
            }

            foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
            {
                if (expectedFiles.Contains(path))
                {
                    continue;
                }

                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    FileAppLogger.Instance.Error(ex, $"Failed to delete old split time set: {path}");
                    return OperationResult.Failure(
                        $"Failed to remove obsolete {description} '{path}'.",
                        ex);
                }
            }

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to save {description} collection: {directory}");
            return OperationResult.Failure(
                $"Failed to save {description} collection in '{directory}'.",
                ex);
        }
    }

    private static OperationResult SaveSets(
        string directory,
        IEnumerable<ReferenceSplitSet> sets,
        string description)
    {
        try
        {
            Directory.CreateDirectory(directory);
            foreach (ReferenceSplitSet set in sets)
            {
                string fileName = $"{SanitizeFileName(set.Name)}.json";
                OperationResult saveResult = TrySaveSet(
                    directory,
                    fileName,
                    set,
                    description);
                if (saveResult.Failed)
                {
                    return OperationResult.Failure(
                        $"Failed to save {description} '{set.Name}' in '{directory}'.",
                        saveResult.Exception);
                }
            }

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to save {description} collection: {directory}");
            return OperationResult.Failure(
                $"Failed to save {description} collection in '{directory}'.",
                ex);
        }
    }

    private static ReferenceSplitSet SavePersonalBestSnapshot(
        string directory,
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime)
    {
        Directory.CreateDirectory(directory);
        string name = BuildPersonalBestSnapshotName(bossName, previousTime, newTime);
        var set = new ReferenceSplitSet
        {
            Name = name,
            Splits = new Dictionary<string, string>(splits, StringComparer.OrdinalIgnoreCase)
        };
        SaveSet(directory, $"{name}.json", set);
        return set;
    }

    private static OperationResult TrySavePersonalBestSnapshot(
        string directory,
        string description,
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime,
        out ReferenceSplitSet? snapshot)
    {
        snapshot = null;
        try
        {
            Directory.CreateDirectory(directory);
            string name = BuildPersonalBestSnapshotName(bossName, previousTime, newTime);
            snapshot = new ReferenceSplitSet
            {
                Name = name,
                Splits = new Dictionary<string, string>(splits, StringComparer.OrdinalIgnoreCase)
            };
            return TrySaveSet(directory, $"{name}.json", snapshot, description);
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to save {description}: {directory}");
            snapshot = null;
            return OperationResult.Failure($"Failed to save {description}.", ex);
        }
    }

    private static void SaveSet(string directory, string fileName, ReferenceSplitSet set)
    {
        OperationResult result = TrySaveSet(
            directory,
            fileName,
            set,
            "split time set");
        if (result.Failed)
        {
            throw new InvalidOperationException(result.Message, result.Exception);
        }
    }

    private static OperationResult TrySaveSet(
        string directory,
        string fileName,
        ReferenceSplitSet set,
        string description)
    {
        return JsonFileStore.TryWrite(Path.Combine(directory, fileName), set, description);
    }

    private static string BuildLastRunName(string? lastBossName, TimeSpan? runDuration)
    {
        string dateTime = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        string bossName = string.IsNullOrWhiteSpace(lastBossName) ? "Unknown" : lastBossName.Trim();
        string duration = runDuration is TimeSpan value ? FormatFileNameDuration(value) : "Unknown";
        return SanitizeFileName($"{dateTime}-{bossName}-{duration}");
    }

    private static string BuildPersonalBestSnapshotName(string bossName, string? previousTime, string newTime)
    {
        string dateTime = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        string normalizedBossName = string.IsNullOrWhiteSpace(bossName) ? "Unknown" : bossName.Trim();
        string oldTime = string.IsNullOrWhiteSpace(previousTime) ? "None" : previousTime.Trim();
        string normalizedNewTime = string.IsNullOrWhiteSpace(newTime) ? "Unknown" : newTime.Trim();
        return SanitizeFileName($"{dateTime}_{normalizedBossName}_{oldTime}-{normalizedNewTime}");
    }

    private static string FormatFileNameDuration(TimeSpan duration)
    {
        int hours = (int)duration.TotalHours;
        string milliseconds = (duration.Milliseconds / 10).ToString("00");
        return hours > 0
            ? $"{hours}h{duration.Minutes:00}m{duration.Seconds:00}.{milliseconds}s"
            : $"{duration.Minutes}m{duration.Seconds:00}.{milliseconds}s";
    }

    private static string SanitizeFileName(string name)
    {
        string trimmed = string.IsNullOrWhiteSpace(name) ? "Reference" : name.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(invalid, '_');
        }

        return trimmed.Length == 0 ? "Reference" : trimmed;
    }
}
