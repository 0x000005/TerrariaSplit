namespace TerrariaSplit;

internal sealed class TerrariaSavePreparation
{
    private readonly TerrariaSaveFileCleaner saveCleaner = new();

    public TerrariaSaveCleanupResult MoveNonFavoritesToBackup()
    {
        return saveCleaner.MoveNonFavoritesToBackup();
    }

    public TerrariaSaveInventorySnapshot ReadInventorySnapshot()
    {
        return saveCleaner.ReadInventorySnapshot();
    }

    public Dictionary<string, DateTime> SnapshotSaveFiles(string directoryName, string pattern)
    {
        string directory = Path.Combine(GetTerrariaSaveRoot(), directoryName);
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

    private static string GetTerrariaSaveRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "Terraria");
    }
}
