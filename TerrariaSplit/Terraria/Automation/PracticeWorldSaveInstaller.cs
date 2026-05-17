namespace TerrariaSplit;

internal static class PracticeWorldSaveInstaller
{
    public static bool TryValidate(PracticeWorldSlot slot, out string message)
    {
        if (!IsValidSaveFile(slot.PlayerFilePath, ".plr"))
        {
            message = $"Practice player file is missing or invalid: {slot.PlayerFilePath}";
            return false;
        }

        if (!IsValidSaveFile(slot.WorldFilePath, ".wld"))
        {
            message = $"Practice world file is missing or invalid: {slot.WorldFilePath}";
            return false;
        }

        message = string.Empty;
        return true;
    }

    public static bool TryInstall(PracticeWorldSlot slot, out string message)
    {
        try
        {
            string playersPath = Path.Combine(TerrariaSavePaths.SaveRoot(), "Players");
            string worldsPath = Path.Combine(TerrariaSavePaths.SaveRoot(), "Worlds");
            Directory.CreateDirectory(playersPath);
            Directory.CreateDirectory(worldsPath);

            CopyPlayer(slot.PlayerFilePath, playersPath);
            CopyWorld(slot.WorldFilePath, worldsPath);

            message = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            AppLogger.Error(ex, "Failed to install practice world save files.");
            return false;
        }
    }

    private static bool IsValidSaveFile(string path, string extension)
    {
        return !string.IsNullOrWhiteSpace(path) &&
            string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(path) &&
            new FileInfo(path).Length > 0;
    }

    private static void CopyPlayer(string sourcePath, string playersPath)
    {
        string targetPath = CopySaveFile(sourcePath, playersPath);
        string sourceDirectory = Path.Combine(
            Path.GetDirectoryName(sourcePath)!,
            Path.GetFileNameWithoutExtension(sourcePath));
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        string targetDirectory = Path.Combine(
            playersPath,
            Path.GetFileNameWithoutExtension(targetPath));
        CopyDirectory(sourceDirectory, GetAvailablePath(targetDirectory));
    }

    private static void CopyWorld(string sourcePath, string worldsPath)
    {
        CopySaveFile(sourcePath, worldsPath);
    }

    private static string CopySaveFile(string sourcePath, string targetDirectory)
    {
        string targetPath = GetAvailablePath(Path.Combine(targetDirectory, Path.GetFileName(sourcePath)));
        File.Copy(sourcePath, targetPath);

        string backupPath = sourcePath + ".bak";
        if (File.Exists(backupPath))
        {
            File.Copy(backupPath, targetPath + ".bak");
        }

        return targetPath;
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (string directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, file);
            string targetPath = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file, targetPath);
        }
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
}
