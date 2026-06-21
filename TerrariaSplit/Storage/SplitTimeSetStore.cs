namespace TerrariaSplit.Storage;

internal static class SplitTimeSetStore
{
    private const int MaxLastRunSetsToLoad = 100;

    public static string ReferenceDirectory => RuntimeDataPaths.ReferenceTimesDirectory;

    public static string LastRunDirectory => RuntimeDataPaths.LastRunTimesDirectory;

    public static string PersonalBestTimeDirectory => RuntimeDataPaths.PersonalBestTimesDirectory;

    public static string PersonalBestSegmentDirectory => RuntimeDataPaths.PersonalBestSegmentsDirectory;

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(ReferenceDirectory);
        Directory.CreateDirectory(LastRunDirectory);
        Directory.CreateDirectory(PersonalBestTimeDirectory);
        Directory.CreateDirectory(PersonalBestSegmentDirectory);
    }

    public static List<ReferenceSplitSet> LoadReferenceSets()
    {
        List<ReferenceSplitSet> sets = LoadSets(ReferenceDirectory);
        return sets.Count == 0 || AreReferenceSetsEmpty(sets)
            ? LoadAndSaveDefaultReferenceSets()
            : sets;
    }

    public static void SaveReferenceSets(IEnumerable<ReferenceSplitSet> sets)
    {
        SaveNamedSets(ReferenceDirectory, sets);
    }

    public static List<ReferenceSplitSet> LoadPersonalBestTimeSets()
    {
        return LoadSets(PersonalBestTimeDirectory, newestFirst: true, maxCount: null);
    }

    public static List<ReferenceSplitSet> LoadPersonalBestSegmentSets()
    {
        return LoadSets(PersonalBestSegmentDirectory, newestFirst: true, maxCount: null);
    }

    public static void SavePersonalBestTimeSets(IEnumerable<ReferenceSplitSet> sets)
    {
        SaveSets(PersonalBestTimeDirectory, sets);
    }

    public static void SavePersonalBestSegmentSets(IEnumerable<ReferenceSplitSet> sets)
    {
        SaveSets(PersonalBestSegmentDirectory, sets);
    }

    public static ReferenceSplitSet SavePersonalBestTimeSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime)
    {
        return SavePersonalBestSnapshot(PersonalBestTimeDirectory, splits, bossName, previousTime, newTime);
    }

    public static ReferenceSplitSet SavePersonalBestSegmentSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime)
    {
        return SavePersonalBestSnapshot(PersonalBestSegmentDirectory, splits, bossName, previousTime, newTime);
    }

    public static Dictionary<string, string> LoadLatestLastRun()
    {
        ReferenceSplitSet? latest = LoadLatestSet(LastRunDirectory);
        return latest?.Splits ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static List<ReferenceSplitSet> LoadLastRunSets()
    {
        return LoadSets(LastRunDirectory, newestFirst: true, MaxLastRunSetsToLoad);
    }

    public static void SaveLastRun(Dictionary<string, string> splits, string? lastBossName = null, TimeSpan? runDuration = null)
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

    private static List<ReferenceSplitSet> LoadAndSaveDefaultReferenceSets()
    {
        ReferenceSplitSet set = LoadEmbeddedDefaultReferenceSet();
        SaveSet(ReferenceDirectory, $"{SanitizeFileName(set.Name)}.json", set);
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
            AppLogger.Error(ex, "Failed to load embedded default WR reference times.");
        }

        return AppSettings.CreateReferenceSet("WR");
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
            AppLogger.Error(ex, $"Failed to load split time set: {path}");
            return null;
        }
    }

    private static void SaveNamedSets(string directory, IEnumerable<ReferenceSplitSet> sets)
    {
        Directory.CreateDirectory(directory);
        HashSet<string> expectedFiles = new(StringComparer.OrdinalIgnoreCase);

        foreach (ReferenceSplitSet set in sets)
        {
            string fileName = $"{SanitizeFileName(set.Name)}.json";
            expectedFiles.Add(Path.Combine(directory, fileName));
            SaveSet(directory, fileName, set);
        }

        foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
        {
            if (!expectedFiles.Contains(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, $"Failed to delete old split time set: {path}");
                }
            }
        }
    }

    private static void SaveSets(string directory, IEnumerable<ReferenceSplitSet> sets)
    {
        Directory.CreateDirectory(directory);
        foreach (ReferenceSplitSet set in sets)
        {
            string fileName = $"{SanitizeFileName(set.Name)}.json";
            SaveSet(directory, fileName, set);
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

    private static void SaveSet(string directory, string fileName, ReferenceSplitSet set)
    {
        JsonFileStore.Write(Path.Combine(directory, fileName), set, "split time set");
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
