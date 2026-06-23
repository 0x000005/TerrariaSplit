using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrariaSplit.Configuration;

public static class SettingsJsonSectionMigrator
{
    private const string LegacyChinese = "\u6D93\uE15F\u6783";

    private static readonly string[] GeneralKeys =
    [
        nameof(GeneralSettings.ShowMouseClickThroughIndicator),
        nameof(GeneralSettings.Language),
        nameof(GeneralSettings.AlwaysOnTop),
        nameof(GeneralSettings.PracticeMode)
    ];

    private static readonly string[] HotkeyKeys =
    [
        nameof(HotkeySettings.PauseResumeKey),
        nameof(HotkeySettings.ResetKey),
        nameof(HotkeySettings.MouseClickThroughKey),
        nameof(HotkeySettings.CreateWorldKey),
        nameof(HotkeySettings.PracticeWorldKey)
    ];

    private static readonly string[] RouteKeys =
    [
        nameof(RouteSettings.SplitRoute),
        nameof(RouteSettings.ExpandSplitDetails),
        nameof(RouteSettings.CollapseSplitDetailsOnCompletion),
        nameof(RouteSettings.EnableVisibleGroupCountLimit),
        nameof(RouteSettings.VisibleGroupCountLimit),
        nameof(RouteSettings.CurrentGroupPosition),
        nameof(RouteSettings.ShowFinalGroup),
        nameof(RouteSettings.ShowAllVisibleGroupsAfterFinalGroup),
        nameof(RouteSettings.ShowAllAttachedGroupsAfterFinalGroup),
        nameof(RouteSettings.ShowAllMultiConditionMainGroupsAfterFinalGroup),
        nameof(RouteSettings.AutoHideAttachedGroups)
    ];

    private static readonly string[] ComparisonKeys =
    [
        nameof(ComparisonSettings.ReferenceSplitSets),
        nameof(ComparisonSettings.ActiveReferenceSplitSet),
        nameof(ComparisonSettings.UsePersonalBestAsReferenceTime),
        nameof(ComparisonSettings.PersonalBestTimeSets),
        nameof(ComparisonSettings.ActivePersonalBestTimeSet),
        nameof(ComparisonSettings.PersonalBestSegmentSets),
        nameof(ComparisonSettings.ActivePersonalBestSegmentSet),
        nameof(ComparisonSettings.PersonalBestTimes),
        nameof(ComparisonSettings.PersonalBestSegmentTimes),
        nameof(ComparisonSettings.AutoUpdatePersonalBestData),
        nameof(ComparisonSettings.AskBeforeUpdatingPersonalBestData)
    ];

    private static readonly string[] OverlayKeys =
    [
        nameof(OverlaySettings.ShowSplitCompletionAnimation),
        nameof(OverlaySettings.SplitCompletionAnimationDurationSeconds),
        nameof(OverlaySettings.SplitCompletionOutlineThicknessPercent),
        nameof(OverlaySettings.SplitCompletionSplitComparisons),
        nameof(OverlaySettings.SplitCompletionSegmentComparisons),
        nameof(OverlaySettings.SplitCompletionOutlineSplitStyles),
        nameof(OverlaySettings.SplitCompletionOutlineSegmentStyles),
        nameof(OverlaySettings.ShowCurrentSplitHighlight),
        nameof(OverlaySettings.CurrentSplitHighlightScalePercent),
        nameof(OverlaySettings.CurrentSplitDepthStrengthPercent),
        nameof(OverlaySettings.ShowEarlyDeltaTime),
        nameof(OverlaySettings.EarlyDeltaTimeSeconds),
        nameof(OverlaySettings.EnableDynamicDeltaTimeUnits),
        nameof(OverlaySettings.EnableDeltaGradientColor),
        nameof(OverlaySettings.EnableCurrentDeltaGradientColor),
        nameof(OverlaySettings.EnableTimerGradientColor),
        nameof(OverlaySettings.DeltaGradientThresholdSeconds),
        nameof(OverlaySettings.DeltaGradientCurve),
        nameof(OverlaySettings.ShowSegmentBestDeltaHighlight),
        nameof(OverlaySettings.SegmentBestDeltaHighlightStyles),
        nameof(OverlaySettings.Colors),
        nameof(OverlaySettings.Sounds),
        nameof(OverlaySettings.Columns),
        nameof(OverlaySettings.TextEffects),
        nameof(OverlaySettings.EnableDefeatedBossIconLighting),
        nameof(OverlaySettings.UndefeatedIconGrayscalePercent),
        nameof(OverlaySettings.UndefeatedIconBrightnessPercent),
        nameof(OverlaySettings.CurrentBossIconGrayscaleWeakenPercent),
        nameof(OverlaySettings.CurrentBossIconBrightnessBoostPercent)
    ];

    public static AppSettings? Deserialize(string json, JsonSerializerOptions options)
    {
        JsonNode? node = JsonNode.Parse(json);
        if (node is not JsonObject root)
        {
            return null;
        }

        return Deserialize(root, options);
    }

    public static AppSettings? Deserialize(JsonObject root, JsonSerializerOptions options)
    {
        return DeserializeDocument(root, options)?.Settings;
    }

    public static SettingsDocument? DeserializeDocument(string json, JsonSerializerOptions options)
    {
        JsonNode? node = JsonNode.Parse(json);
        if (node is not JsonObject root)
        {
            return null;
        }

        return DeserializeDocument(root, options);
    }

    public static SettingsDocument? DeserializeDocument(JsonObject root, JsonSerializerOptions options)
    {
        Migrate(root);
        return root.Deserialize<SettingsDocument>(options);
    }

    public static void Migrate(JsonObject root)
    {
        JsonObject settings = GetSettingsPayload(root);
        MigrateSettings(settings);

        JsonObject current = new()
        {
            [nameof(SettingsDocument.SchemaVersion)] = SettingsSchemaVersion.Current,
            [nameof(SettingsDocument.Settings)] = settings.DeepClone()
        };
        root.Clear();
        foreach ((string key, JsonNode? value) in current)
        {
            root[key] = value?.DeepClone();
        }
    }

    private static JsonObject GetSettingsPayload(JsonObject root)
    {
        string? settingsKey = FindKey(root, nameof(SettingsDocument.Settings));
        if (settingsKey is not null && root[settingsKey] is JsonObject settings)
        {
            return settings;
        }

        return root;
    }

    private static void MigrateSettings(JsonObject root)
    {
        MoveKeys(root, nameof(AppSettings.General), GeneralKeys);
        MoveKeys(root, nameof(AppSettings.Hotkeys), HotkeyKeys);
        MoveKeys(root, nameof(AppSettings.Route), RouteKeys);
        MigrateVisibleGroupCountLimit(root);
        MigrateExpandedSplitDetails(root);
        MigrateLegacyLanguage(root);
        MoveKeys(root, nameof(AppSettings.Comparison), ComparisonKeys);
        MoveKeys(root, nameof(AppSettings.Overlay), OverlayKeys);
        MoveKey(root, nameof(AppSettings.Automation), nameof(AutomationSettings.AutoCreate));
    }

    private static void MigrateVisibleGroupCountLimit(JsonObject root)
    {
        string? routeKey = FindKey(root, nameof(AppSettings.Route));
        if (routeKey is null ||
            root[routeKey] is not JsonObject route ||
            ContainsKey(route, nameof(RouteSettings.EnableVisibleGroupCountLimit)))
        {
            return;
        }

        if (TryGetInt(route, nameof(RouteSettings.VisibleGroupCountLimit), out int limit) && limit > 0)
        {
            route[nameof(RouteSettings.EnableVisibleGroupCountLimit)] = true;
        }
    }

    private static void MigrateExpandedSplitDetails(JsonObject root)
    {
        string? routeKey = FindKey(root, nameof(AppSettings.Route));
        JsonObject? route = routeKey is not null && root[routeKey] is JsonObject existingRoute
            ? existingRoute
            : null;
        string? splitRouteKey = route is not null
            ? FindKey(route, nameof(RouteSettings.SplitRoute))
            : null;
        if (route is null ||
            splitRouteKey is null ||
            route[splitRouteKey] is not JsonArray splitRoute)
        {
            return;
        }

        bool shouldExpandSplitDetails = false;
        foreach (JsonNode? node in splitRoute)
        {
            if (node is not JsonObject entry)
            {
                continue;
            }

            if (TryGetBool(entry, nameof(SplitRouteEntry.ExpandDetails), out bool expandDetails) && expandDetails)
            {
                shouldExpandSplitDetails = true;
            }

            RemoveKey(entry, nameof(SplitRouteEntry.ExpandDetails));
        }

        if (shouldExpandSplitDetails)
        {
            route[nameof(RouteSettings.ExpandSplitDetails)] = true;
        }
    }

    private static void MigrateLegacyLanguage(JsonObject root)
    {
        string? generalKey = FindKey(root, nameof(AppSettings.General));
        if (generalKey is null ||
            root[generalKey] is not JsonObject general ||
            FindKey(general, nameof(GeneralSettings.Language)) is not string languageKey ||
            general[languageKey] is not JsonValue language ||
            !language.TryGetValue(out string? value))
        {
            return;
        }

        if (string.Equals(value, LegacyChinese, StringComparison.Ordinal))
        {
            general[languageKey] = LanguageNames.Chinese;
        }
    }

    private static void MoveKeys(JsonObject root, string sectionName, IEnumerable<string> keys)
    {
        foreach (string key in keys)
        {
            MoveKey(root, sectionName, key);
        }
    }

    private static void MoveKey(JsonObject root, string sectionName, string key)
    {
        if (!TryRemove(root, key, out JsonNode? value))
        {
            return;
        }

        JsonObject section = GetOrCreateSection(root, sectionName);
        if (!ContainsKey(section, key))
        {
            section[key] = value;
        }
    }

    private static JsonObject GetOrCreateSection(JsonObject root, string sectionName)
    {
        string? existingKey = FindKey(root, sectionName);
        if (existingKey is not null &&
            root[existingKey] is JsonObject section)
        {
            return section;
        }

        section = new JsonObject();
        root[sectionName] = section;
        return section;
    }

    private static bool TryRemove(JsonObject root, string key, out JsonNode? value)
    {
        string? existingKey = FindKey(root, key);
        if (existingKey is null)
        {
            value = null;
            return false;
        }

        value = root[existingKey];
        root.Remove(existingKey);
        return true;
    }

    private static void RemoveKey(JsonObject root, string key)
    {
        string? existingKey = FindKey(root, key);
        if (existingKey is not null)
        {
            root.Remove(existingKey);
        }
    }

    private static bool ContainsKey(JsonObject root, string key)
    {
        return FindKey(root, key) is not null;
    }

    private static string? FindKey(JsonObject root, string key)
    {
        foreach ((string existingKey, _) in root)
        {
            if (string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
            {
                return existingKey;
            }
        }

        return null;
    }

    private static bool TryGetInt(JsonObject root, string key, out int value)
    {
        string? existingKey = FindKey(root, key);
        if (existingKey is not null &&
            root[existingKey] is JsonValue node &&
            node.TryGetValue(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetBool(JsonObject root, string key, out bool value)
    {
        string? existingKey = FindKey(root, key);
        if (existingKey is not null &&
            root[existingKey] is JsonValue node &&
            node.TryGetValue(out value))
        {
            return true;
        }

        value = false;
        return false;
    }
}
