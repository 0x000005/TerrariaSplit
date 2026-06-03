namespace TerrariaSplit;

// Persisted, thread-safe pool of generated world files known to contain a pyramid for
// one WorldGenSignature. The foreground workflow installs the first matching .wld into
// Terraria's Worlds folder instead of replaying a seed through the UI.
internal sealed class SeedPoolStore
{
    private static readonly string RootDirectory = Path.Combine(AppContext.BaseDirectory, "seed-pool");
    private static readonly string FilePath = Path.Combine(RootDirectory, "seed-pool.json");
    private static readonly string WorldDirectory = Path.Combine(RootDirectory, "worlds");

    private readonly object sync = new();
    private SeedPoolData data;

    public SeedPoolStore()
    {
        data = JsonFileStore.Read<SeedPoolData>(FilePath, "seed pool") ?? new SeedPoolData();
        data.Signature ??= string.Empty;
        data.Worlds ??= new List<SeedPoolWorldEntry>();
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
                data = new SeedPoolData { Signature = signature };
                Persist();
            }
        }
    }

    public bool TryAdd(
        string signature,
        string sourceWorldPath,
        TerrariaWorldSeedMetadata metadata,
        out SeedPoolWorldEntry entry)
    {
        entry = new SeedPoolWorldEntry();
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
                Directory.CreateDirectory(WorldDirectory);
                string fileName = CreateWorldFileName(sourceWorldPath);
                string targetPath = Path.Combine(WorldDirectory, fileName);
                File.Copy(sourceWorldPath, targetPath, overwrite: false);
                CopyBackupIfPresent(sourceWorldPath, targetPath);

                entry = SeedPoolWorldEntry.From(fileName, metadata);
                data.Worlds.Add(entry);
                Persist();
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppLogger.Error(ex, "Seed pool failed to bank generated world file.");
                return false;
            }
        }
    }

    public bool TryPeekFirst(string signature, out SeedPoolWorldEntry entry)
    {
        lock (sync)
        {
            PruneMissingFiles(persist: true);
            if (SignatureMatches(signature) && data.Worlds.Count > 0)
            {
                entry = data.Worlds[0];
                return true;
            }
        }

        entry = new SeedPoolWorldEntry();
        return false;
    }

    public void RemoveFirst(string signature, SeedPoolWorldEntry entry)
    {
        lock (sync)
        {
            if (SignatureMatches(signature) &&
                data.Worlds.Count > 0 &&
                string.Equals(data.Worlds[0].WorldFileName, entry.WorldFileName, StringComparison.OrdinalIgnoreCase))
            {
                DeleteEntryFiles(data.Worlds[0]);
                data.Worlds.RemoveAt(0);
                Persist();
            }
        }
    }

    public bool TryInstallWorld(SeedPoolWorldEntry entry, out string installedPath, out string message)
    {
        installedPath = string.Empty;
        message = string.Empty;

        lock (sync)
        {
            string? sourcePath = TryGetEntryPath(entry);
            if (sourcePath is null)
            {
                message = "pooled world file is missing";
                return false;
            }

            string worldsPath = Path.Combine(TerrariaSavePaths.SaveRoot(), "Worlds");
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
                AppLogger.Error(ex, "Seed pool failed to install pooled world file.");
                return false;
            }
        }
    }

    public bool TryGetWorldPath(SeedPoolWorldEntry entry, out string worldPath)
    {
        lock (sync)
        {
            worldPath = TryGetEntryPath(entry) ?? string.Empty;
            return worldPath.Length > 0;
        }
    }

    private string? TryGetEntryPath(SeedPoolWorldEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.WorldFileName))
        {
            return null;
        }

        string fileName = Path.GetFileName(entry.WorldFileName);
        string path = Path.Combine(WorldDirectory, fileName);
        return File.Exists(path) ? path : null;
    }

    private bool SignatureMatches(string signature)
    {
        return string.Equals(data.Signature, signature, StringComparison.Ordinal);
    }

    private static string CreateWorldFileName(string sourceWorldPath)
    {
        string stem = Path.GetFileNameWithoutExtension(sourceWorldPath);
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = "pooled-world";
        }

        return $"{stem}-{Guid.NewGuid():N}.wld";
    }

    private void PruneMissingFiles(bool persist)
    {
        int removed = data.Worlds.RemoveAll(entry => TryGetEntryPath(entry) is null);
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
                AppLogger.Error(ex, $"Seed pool failed to copy optional backup file: {sourceBackupPath}");
            }
        }
    }

    private void DeletePoolFiles()
    {
        if (!Directory.Exists(WorldDirectory))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(WorldDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            TryDeleteFile(file);
        }
    }

    private void DeleteEntryFiles(SeedPoolWorldEntry entry)
    {
        if (TryGetEntryPath(entry) is string worldPath)
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
            AppLogger.Error(ex, $"Seed pool failed to delete file: {path}");
        }
    }

    private void Persist()
    {
        JsonFileStore.Write(FilePath, data, "seed pool");
    }

    internal sealed class SeedPoolData
    {
        public string? Signature { get; set; } = string.Empty;

        public List<SeedPoolWorldEntry> Worlds { get; set; } = new();
    }
}

internal sealed class SeedPoolWorldEntry
{
    public string WorldFileName { get; set; } = string.Empty;

    public string SeedText { get; set; } = string.Empty;

    public int SizeCode { get; set; }

    public int DifficultyCode { get; set; }

    public bool HasCrimson { get; set; }

    public int SpecialSeedMask { get; set; }

    public static SeedPoolWorldEntry From(string worldFileName, TerrariaWorldSeedMetadata metadata)
    {
        return new SeedPoolWorldEntry
        {
            WorldFileName = worldFileName,
            SeedText = metadata.SeedText,
            SizeCode = metadata.SizeCode,
            DifficultyCode = metadata.DifficultyCode,
            HasCrimson = metadata.HasCrimson,
            SpecialSeedMask = metadata.SpecialSeedMask
        };
    }

    public TerrariaWorldSeedMetadata ToMetadata()
    {
        return new TerrariaWorldSeedMetadata(SeedText, SizeCode, DifficultyCode, HasCrimson, SpecialSeedMask);
    }
}
