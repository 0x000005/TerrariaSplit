using System.Text.Json;
using System.Text.Json.Nodes;

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

    public static AppSettings? ReadSettingsWithDefaults(string path, string defaultsPath, string description)
    {
        try
        {
            JsonObject? merged = ReadJsonObject(defaultsPath);
            if (merged is null)
            {
                return ReadSettings(path, description);
            }

            if (File.Exists(path) && ReadJsonObject(path) is JsonObject overrides)
            {
                MergeJsonObject(merged, overrides);
            }

            return merged.Deserialize<AppSettings>(JsonFileStore.JsonOptions);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Failed to read {description} with defaults: {path}");
            return default;
        }
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

    private static JsonObject? ReadJsonObject(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
    }

    private static void MergeJsonObject(JsonObject target, JsonObject overrides)
    {
        foreach ((string key, JsonNode? overrideValue) in overrides.ToList())
        {
            if (overrideValue is JsonObject overrideObject &&
                target[key] is JsonObject targetObject)
            {
                MergeJsonObject(targetObject, overrideObject);
                continue;
            }

            target[key] = overrideValue?.DeepClone();
        }
    }
}
