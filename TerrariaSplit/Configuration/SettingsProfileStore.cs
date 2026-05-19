namespace TerrariaSplit;

internal static class SettingsProfileStore
{
    public static string NormalizeSettingsPath(string settingsDirectory, string defaultSettingsFileName, string path)
    {
        string fileName = Path.GetFileName(string.IsNullOrWhiteSpace(path) ? defaultSettingsFileName : path);
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".json";
        }

        return Path.Combine(settingsDirectory, fileName);
    }

    public static string GetRememberedSettingsPath(
        string settingsDirectory,
        string activeSettingsPath,
        Func<string> getFallbackSettingsPath,
        Func<string, string> normalizeSettingsPath,
        Func<string, bool> isValidSettingsFile)
    {
        Directory.CreateDirectory(settingsDirectory);
        if (!File.Exists(activeSettingsPath))
        {
            return getFallbackSettingsPath();
        }

        try
        {
            string fileName = Path.GetFileName(File.ReadAllText(activeSettingsPath).Trim());
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return getFallbackSettingsPath();
            }

            string path = normalizeSettingsPath(fileName);
            return File.Exists(path) && isValidSettingsFile(path)
                ? path
                : getFallbackSettingsPath();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Failed to read active settings profile: {activeSettingsPath}");
            return getFallbackSettingsPath();
        }
    }

    public static void RememberActiveSettingsFile(
        string settingsDirectory,
        string activeSettingsPath,
        string settingsFileName)
    {
        try
        {
            Directory.CreateDirectory(settingsDirectory);
            JsonFileStore.WriteText(activeSettingsPath, settingsFileName, "active settings profile");
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Failed to write active settings profile: {activeSettingsPath}");
        }
    }
}
