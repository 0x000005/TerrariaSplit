using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrariaSplit.Storage;

internal static class SettingsSerializer
{
    public static AppSettings? ReadSettings(string path, string description)
    {
        try
        {
            return ReadSettingsFromJson(File.ReadAllText(path), description);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Failed to read {description}: {path}");
            return default;
        }
    }

    public static AppSettings? ReadSettingsFromJson(string json, string description)
    {
        try
        {
            return SettingsJsonSectionMigrator.Deserialize(json, JsonFileStore.JsonOptions);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Failed to read {description}.");
            return default;
        }
    }

    public static OperationResult WriteSettings(string path, AppSettings settings)
    {
        return JsonFileStore.TryWrite(path, new SettingsDocument(settings), "settings");
    }

    public static AppSettings? ReadSettingsWithDefaults(
        string path,
        AppSettings defaults,
        string description,
        out bool shouldWriteDefaults)
    {
        shouldWriteDefaults = false;
        try
        {
            JsonObject? merged = JsonSerializer.SerializeToNode(defaults, JsonFileStore.JsonOptions) as JsonObject;
            if (merged is null)
            {
                shouldWriteDefaults = true;
                return Clone(defaults);
            }

            if (!File.Exists(path))
            {
                shouldWriteDefaults = true;
                return SettingsJsonSectionMigrator.Deserialize(merged, JsonFileStore.JsonOptions);
            }

            JsonObject? overrides = ReadJsonObject(path);
            if (overrides is null)
            {
                shouldWriteDefaults = true;
                return SettingsJsonSectionMigrator.Deserialize(merged, JsonFileStore.JsonOptions);
            }

            SettingsJsonSectionMigrator.Migrate(overrides);
            JsonObject overrideSettings = GetSettingsPayload(overrides);
            MergeJsonObject(merged, overrideSettings);
            AppSettings? settings = SettingsJsonSectionMigrator.Deserialize(merged, JsonFileStore.JsonOptions);
            if (settings is null)
            {
                shouldWriteDefaults = true;
                return Clone(defaults);
            }

            return settings;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Failed to read {description} with defaults: {path}");
            shouldWriteDefaults = true;
            return Clone(defaults);
        }
    }

    public static AppSettings Clone(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonFileStore.JsonOptions);
        return SettingsJsonSectionMigrator.Deserialize(json, JsonFileStore.JsonOptions) ?? new AppSettings();
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
            JsonElement settingsRoot = root.TryGetProperty(nameof(SettingsDocument.Settings), out JsonElement settings) &&
                settings.ValueKind == JsonValueKind.Object
                ? settings
                : root;
            return settingsRoot.TryGetProperty(nameof(AppSettings.Route), out _) ||
                settingsRoot.TryGetProperty(nameof(AppSettings.Overlay), out _) ||
                settingsRoot.TryGetProperty(nameof(AppSettings.Comparison), out _) ||
                settingsRoot.TryGetProperty(nameof(RouteSettings.SplitRoute), out _) ||
                settingsRoot.TryGetProperty(nameof(OverlaySettings.Columns), out _) ||
                settingsRoot.TryGetProperty(nameof(OverlaySettings.Colors), out _) ||
                settingsRoot.TryGetProperty(nameof(ComparisonSettings.ReferenceSplitSets), out _) ||
                settingsRoot.TryGetProperty(nameof(OverlaySettings.ShowSplitCompletionAnimation), out _);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Ignored invalid settings file: {path}");
            return false;
        }
    }

    private static JsonObject? ReadJsonObject(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Failed to read settings JSON object: {path}");
            return null;
        }
    }

    private static JsonObject GetSettingsPayload(JsonObject root)
    {
        return root[nameof(SettingsDocument.Settings)] is JsonObject settings
            ? settings
            : root;
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
