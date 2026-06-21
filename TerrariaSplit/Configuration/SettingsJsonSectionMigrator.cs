using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrariaSplit.Configuration;

internal static class SettingsJsonSectionMigrator
{
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
        nameof(RouteSettings.AutoHideAttachedGroups),
        nameof(RouteSettings.AttachedGroupsAffectTimerComparison)
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
        Migrate(root);
        return root.Deserialize<AppSettings>(options);
    }

    public static void Migrate(JsonObject root)
    {
        MoveKeys(root, nameof(AppSettings.General), GeneralKeys);
        MoveKeys(root, nameof(AppSettings.Hotkeys), HotkeyKeys);
        MoveKeys(root, nameof(AppSettings.Route), RouteKeys);
        MoveKeys(root, nameof(AppSettings.Comparison), ComparisonKeys);
        MoveKeys(root, nameof(AppSettings.Overlay), OverlayKeys);
        MoveKey(root, nameof(AppSettings.Automation), nameof(AutomationSettings.AutoCreate));
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
}
