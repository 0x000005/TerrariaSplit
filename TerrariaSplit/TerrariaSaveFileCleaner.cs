using System.Text.Json;

namespace TerrariaSplit;

internal sealed class TerrariaSaveFileCleaner
{
    private const int MaxDeletedBackupFolders = 50;
    private const string FavoritesFileName = "favorites.json";
    private const string DeletedSavesDirectoryName = "TerrariaSplitDeleted";

    public TerrariaSaveCleanupResult MoveNonFavoritesToBackup()
    {
        string root = GetTerrariaSaveRoot();
        string deletedRoot = Path.Combine(root, DeletedSavesDirectoryName);
        string backupRoot = Path.Combine(
            deletedRoot,
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));

        FavoriteSaveFiles favorites = LoadFavorites(Path.Combine(root, FavoritesFileName));
        int favoritePlayers = CountExistingFavoriteFiles(Path.Combine(root, "Players"), "*.plr", favorites.Players);
        int favoriteWorlds = CountExistingFavoriteFiles(Path.Combine(root, "Worlds"), "*.wld", favorites.Worlds);
        int movedPlayers = MoveNonFavoritePlayers(root, backupRoot, favorites.Players);
        int movedWorlds = MoveNonFavoriteWorlds(root, backupRoot, favorites.Worlds);
        PruneDeletedBackupFolders(deletedRoot);
        return new TerrariaSaveCleanupResult(
            root,
            backupRoot,
            favoritePlayers,
            favoriteWorlds,
            movedPlayers,
            movedWorlds);
    }

    public TerrariaSaveInventorySnapshot ReadInventorySnapshot()
    {
        string root = GetTerrariaSaveRoot();
        FavoriteSaveFiles favorites = LoadFavorites(Path.Combine(root, FavoritesFileName));
        return new TerrariaSaveInventorySnapshot(
            CountFiles(Path.Combine(root, "Players"), "*.plr"),
            CountFiles(Path.Combine(root, "Worlds"), "*.wld"),
            CountExistingFavoriteFiles(Path.Combine(root, "Players"), "*.plr", favorites.Players),
            CountExistingFavoriteFiles(Path.Combine(root, "Worlds"), "*.wld", favorites.Worlds));
    }

    private static int MoveNonFavoritePlayers(string root, string backupRoot, HashSet<string> favorites)
    {
        string playersPath = Path.Combine(root, "Players");
        if (!Directory.Exists(playersPath))
        {
            return 0;
        }

        int moved = 0;
        foreach (string playerFile in Directory.EnumerateFiles(playersPath, "*.plr", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(playerFile);
            if (favorites.Contains(fileName))
            {
                continue;
            }

            string stem = Path.GetFileNameWithoutExtension(playerFile);
            MoveFileIfExists(playerFile, Path.Combine(backupRoot, "Players", fileName));
            MoveFileIfExists(playerFile + ".bak", Path.Combine(backupRoot, "Players", fileName + ".bak"));
            MoveDirectoryIfExists(Path.Combine(playersPath, stem), Path.Combine(backupRoot, "Players", stem));
            moved++;
        }

        return moved;
    }

    private static int MoveNonFavoriteWorlds(string root, string backupRoot, HashSet<string> favorites)
    {
        string worldsPath = Path.Combine(root, "Worlds");
        if (!Directory.Exists(worldsPath))
        {
            return 0;
        }

        int moved = 0;
        foreach (string worldFile in Directory.EnumerateFiles(worldsPath, "*.wld", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(worldFile);
            if (favorites.Contains(fileName))
            {
                continue;
            }

            MoveFileIfExists(worldFile, Path.Combine(backupRoot, "Worlds", fileName));
            MoveFileIfExists(worldFile + ".bak", Path.Combine(backupRoot, "Worlds", fileName + ".bak"));
            moved++;
        }

        return moved;
    }

    private static void PruneDeletedBackupFolders(string deletedRoot)
    {
        if (!Directory.Exists(deletedRoot))
        {
            return;
        }

        try
        {
            List<string> batchPaths = Directory.EnumerateDirectories(deletedRoot)
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .ToList();

            int removeCount = batchPaths.Count - MaxDeletedBackupFolders;
            if (removeCount <= 0)
            {
                return;
            }

            for (int i = 0; i < removeCount; i++)
            {
                Directory.Delete(batchPaths[i], recursive: true);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Failed to prune TerrariaSplit deleted backup folders.");
        }
    }

    private static void MoveFileIfExists(string source, string destination)
    {
        if (!File.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(source, GetAvailablePath(destination));
    }

    private static void MoveDirectoryIfExists(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        Directory.Move(source, GetAvailablePath(destination));
    }

    private static string GetAvailablePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return path;
        }

        string directory = Path.GetDirectoryName(path)!;
        string name = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        for (int i = 1; i < 1000; i++)
        {
            string candidate = Path.Combine(directory, $"{name}-{i}{extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{name}-{Guid.NewGuid():N}{extension}");
    }

    private static int CountExistingFavoriteFiles(string directory, string pattern, HashSet<string> favorites)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Count(fileName => fileName is not null && favorites.Contains(fileName));
    }

    private static int CountFiles(string directory, string pattern)
    {
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).Count()
            : 0;
    }

    private static string GetTerrariaSaveRoot()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documents, "My Games", "Terraria");
    }

    private static FavoriteSaveFiles LoadFavorites(string path)
    {
        if (!File.Exists(path))
        {
            return FavoriteSaveFiles.Empty;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            FavoriteJson? json = JsonSerializer.Deserialize<FavoriteJson>(stream);
            return new FavoriteSaveFiles(
                ToFavoriteSet(json?.Player),
                ToFavoriteSet(json?.World));
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Failed to read Terraria favorites file: {path}");
            return FavoriteSaveFiles.Empty;
        }
    }

    private static HashSet<string> ToFavoriteSet(Dictionary<string, bool>? values)
    {
        return values?
            .Where(pair => pair.Value)
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FavoriteJson
    {
        public Dictionary<string, bool>? Player { get; set; }
        public Dictionary<string, bool>? World { get; set; }
    }

    private readonly record struct FavoriteSaveFiles(HashSet<string> Players, HashSet<string> Worlds)
    {
        public static FavoriteSaveFiles Empty => new(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }
}

internal readonly record struct TerrariaSaveCleanupResult(
    string SaveRoot,
    string BackupRoot,
    int FavoritePlayers,
    int FavoriteWorlds,
    int MovedPlayers,
    int MovedWorlds);

internal readonly record struct TerrariaSaveInventorySnapshot(
    int PlayerFiles,
    int WorldFiles,
    int FavoritePlayers,
    int FavoriteWorlds);
