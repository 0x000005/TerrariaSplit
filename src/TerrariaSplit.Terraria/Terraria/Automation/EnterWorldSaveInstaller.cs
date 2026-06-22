namespace TerrariaSplit.Terraria.Automation;

public static class EnterWorldSaveInstaller
{
    public static bool TryValidate(PracticeWorldSlot slot, out string message)
    {
        OperationResult result = Validate(slot);
        message = result.Message;
        return result.Succeeded;
    }

    public static OperationResult Validate(PracticeWorldSlot slot)
    {
        if (string.IsNullOrWhiteSpace(slot.Name))
        {
            return OperationResult.Failure("Load world slot name is missing.");
        }

        if (!IsValidSaveFile(slot.PlayerFilePath, ".plr") &&
            !IsValidSaveFile(slot.WorldFilePath, ".wld"))
        {
            return OperationResult.Failure($"Load world slot has no valid player or world file: {slot.Name}");
        }

        return OperationResult.Success();
    }

    public static bool TryInstall(PracticeWorldSlot slot, out string message)
    {
        OperationResult result = Install(slot);
        message = result.Message;
        return result.Succeeded;
    }

    public static OperationResult Install(PracticeWorldSlot slot)
    {
        try
        {
            string playersPath = Path.Combine(TerrariaSavePaths.SaveRoot(), "Players");
            string worldsPath = Path.Combine(TerrariaSavePaths.SaveRoot(), "Worlds");
            Directory.CreateDirectory(playersPath);
            Directory.CreateDirectory(worldsPath);

            bool copiedAny = false;
            if (IsValidSaveFile(slot.PlayerFilePath, ".plr"))
            {
                CopyPlayer(slot.PlayerFilePath, playersPath);
                copiedAny = true;
            }

            if (IsValidSaveFile(slot.WorldFilePath, ".wld"))
            {
                CopyWorld(slot.WorldFilePath, worldsPath);
                copiedAny = true;
            }

            if (!copiedAny)
            {
                return OperationResult.Failure($"Load world slot has no valid player or world file: {slot.Name}");
            }

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            StaticAppLogger.Instance.Error(ex, "Failed to install practice world save files.");
            return OperationResult.Failure(ex.Message, ex);
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
        CopyDirectory(sourceDirectory, targetDirectory);
    }

    private static void CopyWorld(string sourcePath, string worldsPath)
    {
        CopySaveFile(sourcePath, worldsPath);
    }

    private static string CopySaveFile(string sourcePath, string targetDirectory)
    {
        string targetPath = Path.Combine(targetDirectory, Path.GetFileName(sourcePath));
        CopyFile(sourcePath, targetPath);

        string backupPath = sourcePath + ".bak";
        if (File.Exists(backupPath))
        {
            CopyFile(backupPath, targetPath + ".bak");
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
            CopyFile(file, targetPath);
        }
    }

    private static void CopyFile(string sourcePath, string targetPath)
    {
        if (IsSamePath(sourcePath, targetPath))
        {
            return;
        }

        File.Copy(sourcePath, targetPath, overwrite: true);
    }

    private static bool IsSamePath(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
