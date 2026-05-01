namespace TerrariaSplit;

internal static class SplitTimeSetStore
{
    public static string ReferenceDirectory => Path.Combine(AppContext.BaseDirectory, "reference-times");

    public static string LastRunDirectory => Path.Combine(AppContext.BaseDirectory, "last-times");

    public static List<ReferenceSplitSet> LoadReferenceSets()
    {
        List<ReferenceSplitSet> sets = LoadSets(ReferenceDirectory);
        return sets.Count == 0
            ? new List<ReferenceSplitSet> { AppSettings.CreateReferenceSet("WR") }
            : sets;
    }

    public static void SaveReferenceSets(IEnumerable<ReferenceSplitSet> sets)
    {
        SaveNamedSets(ReferenceDirectory, sets);
    }

    public static Dictionary<string, string> LoadLatestLastRun()
    {
        ReferenceSplitSet? latest = LoadLatestSet(LastRunDirectory);
        return latest?.Splits ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static List<ReferenceSplitSet> LoadLastRunSets()
    {
        return LoadSets(LastRunDirectory)
            .OrderByDescending(set => set.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void SaveLastRun(Dictionary<string, string> splits)
    {
        string name = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        SaveSet(LastRunDirectory, $"{name}.json", new ReferenceSplitSet
        {
            Name = name,
            Splits = new Dictionary<string, string>(splits, StringComparer.OrdinalIgnoreCase)
        });
    }

    private static List<ReferenceSplitSet> LoadSets(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return new List<ReferenceSplitSet>();
        }

        var sets = new List<ReferenceSplitSet>();
        foreach (string path in Directory.EnumerateFiles(directory, "*.json").OrderBy(path => path))
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

    private static void SaveSet(string directory, string fileName, ReferenceSplitSet set)
    {
        JsonFileStore.Write(Path.Combine(directory, fileName), set, "split time set");
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
