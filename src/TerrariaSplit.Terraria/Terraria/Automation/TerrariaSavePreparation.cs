using System.Text.Json;

namespace TerrariaSplit.Terraria.Automation;

public sealed class TerrariaSavePreparation
{
    private const string FavoritesFileName = "favorites.json";

    private readonly TerrariaSaveFileCleaner saveCleaner = new();

    public TerrariaSaveCleanupResult MoveNonFavoritesToBackup()
    {
        return saveCleaner.MoveNonFavoritesToBackup();
    }

    public TerrariaWorldCleanupResult MoveNonFavoriteWorldsToBackup()
    {
        return saveCleaner.MoveNonFavoriteWorldsToBackup();
    }

    public TerrariaSaveInventorySnapshot ReadInventorySnapshot()
    {
        return saveCleaner.ReadInventorySnapshot();
    }

    public Dictionary<string, DateTime> SnapshotSaveFiles(string directoryName, string pattern)
    {
        string directory = Path.Combine(TerrariaSavePaths.SaveRoot(), directoryName);
        if (!Directory.Exists(directory))
        {
            return new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        }

        return Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .ToDictionary(
                path => Path.GetFileName(path),
                File.GetLastWriteTimeUtc,
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<TerrariaPlayerSelectionEntry> ReadPlayerSelectionEntries(
        string? createdFileName,
        string createdPlayerName)
    {
        string root = TerrariaSavePaths.SaveRoot();
        string directory = Path.Combine(root, "Players");
        if (!Directory.Exists(directory))
        {
            return [];
        }

        HashSet<string> favoritePlayers = ReadFavoritePlayers(Path.Combine(root, FavoritesFileName));
        return Directory.EnumerateFiles(directory, "*.plr", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                string fileName = Path.GetFileName(path);
                string displayName = GetPlayerDisplayName(fileName, createdFileName, createdPlayerName);
                return new TerrariaPlayerSelectionEntry(
                    fileName,
                    displayName,
                    favoritePlayers.Contains(fileName),
                    File.GetLastWriteTimeUtc(path));
            })
            .ToArray();
    }

    public static string? FindNewOrChangedSaveFile(
        IReadOnlyDictionary<string, DateTime> before,
        IReadOnlyDictionary<string, DateTime> after)
    {
        return after
            .Where(pair => !before.TryGetValue(pair.Key, out DateTime previousWriteTime) || pair.Value > previousWriteTime)
            .OrderByDescending(static pair => pair.Value)
            .Select(static pair => pair.Key)
            .FirstOrDefault();
    }

    private static string GetPlayerDisplayName(
        string fileName,
        string? createdFileName,
        string createdPlayerName)
    {
        if (!string.IsNullOrWhiteSpace(createdFileName) &&
            string.Equals(fileName, createdFileName, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(createdPlayerName))
        {
            return createdPlayerName;
        }

        return Path.GetFileNameWithoutExtension(fileName);
    }

    private static HashSet<string> ReadFavoritePlayers(string path)
    {
        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            FavoriteJson? json = JsonSerializer.Deserialize<FavoriteJson>(stream);
            return json?.Player?
                .Where(static pair => pair.Value)
                .Select(static pair => pair.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ??
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to read Terraria favorites file: {path}");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private sealed class FavoriteJson
    {
        public Dictionary<string, bool>? Player { get; set; }
    }
}
