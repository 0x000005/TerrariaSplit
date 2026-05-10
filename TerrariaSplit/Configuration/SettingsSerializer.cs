using System.Text.Json;

namespace TerrariaSplit;

internal static class SettingsSerializer
{
    public static AppSettings? ReadSettings(string path, string description)
    {
        return JsonFileStore.Read<AppSettings>(path, description);
    }

    public static void WriteSettings(string path, AppSettings settings)
    {
        JsonFileStore.Write(path, settings, "settings");
    }

    public static AppSettings Clone(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonFileStore.JsonOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonFileStore.JsonOptions) ?? new AppSettings();
    }

    public static bool IsValidSettingsFile(string path)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            JsonElement root = document.RootElement;
            return root.TryGetProperty(nameof(AppSettings.Route), out _) ||
                root.TryGetProperty(nameof(AppSettings.Columns), out _) ||
                root.TryGetProperty(nameof(AppSettings.Colors), out _) ||
                root.TryGetProperty(nameof(AppSettings.ReferenceSplitSets), out _) ||
                root.TryGetProperty(nameof(AppSettings.BossIconPaths), out _) ||
                root.TryGetProperty(nameof(AppSettings.ShowSplitCompletionAnimation), out _);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Ignored invalid settings file: {path}");
            return false;
        }
    }
}
