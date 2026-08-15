using System.Globalization;

namespace TerrariaSplit.Storage;

// Persisted, thread-safe pool of generated world files for one WorldPoolSignature.
// The foreground workflow installs the first matching .wld into
// Terraria's Worlds folder instead of replaying a seed through the UI.
public sealed class WorldPoolStore : IWorldPoolStore
{
    private readonly object sync = new();
    private readonly string filePath;
    private readonly string worldDirectory;
    private WorldPoolData data;

    public WorldPoolStore(IRuntimeDataPaths? paths = null)
    {
        paths ??= AppContextRuntimeDataPaths.Default;
        filePath = Path.Combine(paths.WorldPoolDirectory, "world-pool.json");
        worldDirectory = Path.Combine(paths.WorldPoolDirectory, "worlds");
        data = JsonFileStore.Read<WorldPoolData>(filePath, "world pool") ?? new WorldPoolData();
        data.Signature ??= string.Empty;
        data.Worlds ??= new List<PersistedWorldPoolEntry>();
        PruneMissingFiles(persist: true);
    }

    public int Count(string signature)
    {
        lock (sync)
        {
            PruneMissingFiles(persist: true);
            return SignatureMatches(signature) ? data.Worlds.Count : 0;
        }
    }

    public void EnsureSignature(string signature)
    {
        lock (sync)
        {
            if (!SignatureMatches(signature))
            {
                DeletePoolFiles();
                data = new WorldPoolData { Signature = signature };
                Persist();
            }
        }
    }

    public bool TryAdd(
        string signature,
        string sourceWorldPath,
        TerrariaWorldSeedMetadata metadata,
        out WorldPoolItem item)
    {
        item = default;
        if (string.IsNullOrWhiteSpace(sourceWorldPath) || !File.Exists(sourceWorldPath))
        {
            return false;
        }

        lock (sync)
        {
            if (!SignatureMatches(signature))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(worldDirectory);
                string fileName = CreateWorldFileName();
                string targetPath = Path.Combine(worldDirectory, fileName);
                File.Copy(sourceWorldPath, targetPath, overwrite: false);
                CopyBackupIfPresent(sourceWorldPath, targetPath);

                PersistedWorldPoolEntry entry = PersistedWorldPoolEntry.From(fileName, metadata);
                data.Worlds.Add(entry);
                Persist();
                item = entry.ToItem();
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                FileAppLogger.Instance.Error(ex, "World pool failed to bank generated world file.");
                return false;
            }
        }
    }

    public bool TryPeekFirst(string signature, out WorldPoolItem item)
    {
        lock (sync)
        {
            PruneMissingFiles(persist: true);
            if (SignatureMatches(signature) && data.Worlds.Count > 0)
            {
                item = data.Worlds[0].ToItem();
                return true;
            }
        }

        item = default;
        return false;
    }

    public void RemoveFirst(string signature, WorldPoolItem item)
    {
        lock (sync)
        {
            if (SignatureMatches(signature) &&
                data.Worlds.Count > 0 &&
                string.Equals(data.Worlds[0].WorldFileName, item.WorldFileName, StringComparison.OrdinalIgnoreCase))
            {
                DeleteEntryFiles(data.Worlds[0]);
                data.Worlds.RemoveAt(0);
                Persist();
            }
        }
    }

    public bool TryInstallWorld(
        WorldPoolItem item,
        string worldsPath,
        out string installedPath,
        out string message)
    {
        installedPath = string.Empty;
        message = string.Empty;

        lock (sync)
        {
            string? sourcePath = TryGetEntryPath(item.WorldFileName);
            if (sourcePath is null)
            {
                message = "pooled world file is missing";
                return false;
            }

            Directory.CreateDirectory(worldsPath);
            installedPath = Path.Combine(worldsPath, Path.GetFileName(sourcePath));

            try
            {
                File.Copy(sourcePath, installedPath, overwrite: true);
                CopyBackupIfPresent(sourcePath, installedPath);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                message = ex.Message;
                FileAppLogger.Instance.Error(ex, "World pool failed to install pooled world file.");
                return false;
            }
        }
    }

    public bool TryGetWorldPath(WorldPoolItem item, out string worldPath)
    {
        lock (sync)
        {
            worldPath = TryGetEntryPath(item.WorldFileName) ?? string.Empty;
            return worldPath.Length > 0;
        }
    }

    private string? TryGetEntryPath(string? worldFileName)
    {
        if (string.IsNullOrWhiteSpace(worldFileName))
        {
            return null;
        }

        string fileName = Path.GetFileName(worldFileName);
        string path = Path.Combine(worldDirectory, fileName);
        return File.Exists(path) ? path : null;
    }

    private bool SignatureMatches(string signature)
    {
        return string.Equals(data.Signature, signature, StringComparison.Ordinal);
    }

    private string CreateWorldFileName()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        string fileName = $"TerrariaSplit_{timestamp}.wld";
        if (!File.Exists(Path.Combine(worldDirectory, fileName)))
        {
            return fileName;
        }

        for (int suffix = 2; suffix < 1000; suffix++)
        {
            fileName = $"TerrariaSplit_{timestamp}_{suffix.ToString(CultureInfo.InvariantCulture)}.wld";
            if (!File.Exists(Path.Combine(worldDirectory, fileName)))
            {
                return fileName;
            }
        }

        return $"TerrariaSplit_{timestamp}_{Guid.NewGuid():N}.wld";
    }

    private void PruneMissingFiles(bool persist)
    {
        int removed = data.Worlds.RemoveAll(entry => TryGetEntryPath(entry.WorldFileName) is null);
        if (removed > 0 && persist)
        {
            Persist();
        }
    }

    private static void CopyBackupIfPresent(string sourcePath, string targetPath)
    {
        string sourceBackupPath = sourcePath + ".bak";
        if (File.Exists(sourceBackupPath))
        {
            try
            {
                File.Copy(sourceBackupPath, targetPath + ".bak", overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                FileAppLogger.Instance.Error(ex, $"World pool failed to copy optional backup file: {sourceBackupPath}");
            }
        }
    }

    private void DeletePoolFiles()
    {
        if (!Directory.Exists(worldDirectory))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(worldDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            TryDeleteFile(file);
        }
    }

    private void DeleteEntryFiles(PersistedWorldPoolEntry entry)
    {
        if (TryGetEntryPath(entry.WorldFileName) is string worldPath)
        {
            TryDeleteFile(worldPath);
            TryDeleteFile(worldPath + ".bak");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileAppLogger.Instance.Error(ex, $"World pool failed to delete file: {path}");
        }
    }

    private void Persist()
    {
        JsonFileStore.Write(filePath, data, "world pool");
    }

    internal sealed class WorldPoolData
    {
        public string? Signature { get; set; } = string.Empty;

        public List<PersistedWorldPoolEntry> Worlds { get; set; } = new();
    }

    internal sealed class PersistedWorldPoolEntry
    {
        public string WorldFileName { get; set; } = string.Empty;

        public string SeedText { get; set; } = string.Empty;

        public int SizeCode { get; set; }

        public int DifficultyCode { get; set; }

        public bool HasCrimson { get; set; }

        public int SpecialSeedMask { get; set; }

        public static PersistedWorldPoolEntry From(
            string worldFileName,
            TerrariaWorldSeedMetadata metadata)
        {
            return new PersistedWorldPoolEntry
            {
                WorldFileName = worldFileName,
                SeedText = metadata.SeedText,
                SizeCode = metadata.SizeCode,
                DifficultyCode = metadata.DifficultyCode,
                HasCrimson = metadata.HasCrimson,
                SpecialSeedMask = metadata.SpecialSeedMask
            };
        }

        public WorldPoolItem ToItem()
        {
            return new WorldPoolItem(
                WorldFileName,
                new TerrariaWorldSeedMetadata(
                    SeedText,
                    SizeCode,
                    DifficultyCode,
                    HasCrimson,
                    SpecialSeedMask));
        }
    }
}
