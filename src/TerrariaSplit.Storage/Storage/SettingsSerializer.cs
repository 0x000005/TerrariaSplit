using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrariaSplit.Storage;

internal static class SettingsSerializer
{
    private static readonly Lazy<JsonObject> EmbeddedDefaults = new(() =>
        JsonNode.Parse(AppSettingsDefaults.TemplateJson) as JsonObject
            ?? throw new InvalidOperationException("Embedded default settings JSON is invalid."));

    public static AppSettings? ReadSettings(string path, string description)
    {
        try
        {
            return ReadSettingsFromJson(File.ReadAllText(path), description);
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to read {description}: {path}");
            return default;
        }
    }

    public static AppSettings? ReadSettingsFromJson(string json, string description)
    {
        try
        {
            return DeserializeSettings(json);
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to read {description}.");
            return default;
        }
    }

    public static OperationResult WriteSettings(string path, AppSettings settings)
    {
        string json = JsonSerializer.Serialize(
            new SettingsDocument(settings),
            SettingsJsonContext.Default.SettingsDocument);
        return JsonFileStore.TryWriteText(path, json, "settings");
    }

    public static AppSettings? ReadSettingsWithDefaults(
        string path,
        AppSettings defaults,
        string description,
        out bool shouldWriteDefaults)
    {
        bool exists = File.Exists(path);
        string? json = exists ? File.ReadAllText(path) : null;
        return ReadSettingsWithDefaults(json, exists, defaults, description, out shouldWriteDefaults);
    }

    public static AppSettings? ReadSettingsWithEmbeddedDefaults(
        string path,
        string description,
        out bool shouldWriteDefaults)
    {
        bool exists = File.Exists(path);
        string? json = exists ? File.ReadAllText(path) : null;
        return ReadSettingsWithEmbeddedDefaults(json, exists, description, out shouldWriteDefaults);
    }

    public static AppSettings? ReadSettingsWithEmbeddedDefaults(
        string? json,
        bool sourceExists,
        string description,
        out bool shouldWriteDefaults)
    {
        shouldWriteDefaults = false;
        try
        {
            JsonObject merged = CreateEmbeddedDefaultsNode();
            if (!sourceExists)
            {
                shouldWriteDefaults = true;
                return DeserializeSettings(merged);
            }

            JsonObject? overrides = ReadJsonObject(json);
            if (overrides is null)
            {
                shouldWriteDefaults = true;
                return DeserializeSettings(merged);
            }

            MergeJsonObject(merged, GetSettingsPayload(overrides));
            AppSettings? settings = DeserializeSettings(merged);
            if (settings is null)
            {
                shouldWriteDefaults = true;
                return DeserializeSettings(CreateEmbeddedDefaultsNode());
            }

            return settings;
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to read {description} with embedded defaults.");
            shouldWriteDefaults = true;
            return DeserializeSettings(CreateEmbeddedDefaultsNode());
        }
    }

    public static AppSettings? ReadSettingsWithDefaults(
        string? json,
        bool sourceExists,
        AppSettings defaults,
        string description,
        out bool shouldWriteDefaults)
    {
        shouldWriteDefaults = false;
        try
        {
            JsonObject? merged = JsonSerializer.SerializeToNode(
                defaults,
                SettingsJsonContext.Default.AppSettings) as JsonObject;
            if (merged is null)
            {
                shouldWriteDefaults = true;
                return AppSettingsCloner.Clone(defaults);
            }

            if (!sourceExists)
            {
                shouldWriteDefaults = true;
                return AppSettingsCloner.Clone(defaults);
            }

            JsonObject? overrides = ReadJsonObject(json);
            if (overrides is null)
            {
                shouldWriteDefaults = true;
                return AppSettingsCloner.Clone(defaults);
            }

            JsonObject overrideSettings = GetSettingsPayload(overrides);
            MergeJsonObject(merged, overrideSettings);
            AppSettings? settings = DeserializeSettings(merged);
            if (settings is null)
            {
                shouldWriteDefaults = true;
                return AppSettingsCloner.Clone(defaults);
            }

            return settings;
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to read {description} with defaults.");
            shouldWriteDefaults = true;
            return AppSettingsCloner.Clone(defaults);
        }
    }

    public static bool IsValidSettingsFile(string path)
    {
        return TryReadValidSettingsFile(path, out _);
    }

    public static bool TryReadValidSettingsFile(string path, out string json)
    {
        try
        {
            json = File.ReadAllText(path);
            using JsonDocument document = JsonDocument.Parse(json);
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
            json = string.Empty;
            FileAppLogger.Instance.Error(ex, $"Ignored invalid settings file: {path}");
            return false;
        }
    }

    private static JsonObject? ReadJsonObject(string? json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, "Failed to read settings JSON object.");
            return null;
        }
    }

    private static AppSettings? DeserializeSettings(string json)
    {
        return JsonNode.Parse(json) is JsonObject root
            ? DeserializeSettings(root)
            : null;
    }

    private static AppSettings? DeserializeSettings(JsonObject root)
    {
        return root[nameof(SettingsDocument.Settings)] is JsonObject
            ? root.Deserialize(SettingsJsonContext.Default.SettingsDocument)?.Settings
            : root.Deserialize(SettingsJsonContext.Default.AppSettings);
    }

    private static JsonObject CreateEmbeddedDefaultsNode()
    {
        return (JsonObject)EmbeddedDefaults.Value.DeepClone();
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
