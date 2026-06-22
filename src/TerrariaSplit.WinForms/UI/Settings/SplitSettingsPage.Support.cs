using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class SplitSettingsPage : SettingsPageBase
{
    private string CreateUniqueSplitId(string preferredId)
    {
        return routeDraft.CreateUniqueSplitId(preferredId);
    }

    private sealed record RouteListItem(SplitRouteEntry Entry)
    {
        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Entry.DisplayName) ? "Unnamed split" : Entry.DisplayName;
        }
    }

    private sealed record ConditionListItem(SplitCondition Condition, string DisplayText)
    {
        public override string ToString()
        {
            return DisplayText;
        }
    }

    private sealed record MatchModeOption(int RequiredCount, string DisplayName, string CollapsedDisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private static string FormatCollapsedMatchModeOption(object? item)
    {
        return item is MatchModeOption option
            ? option.CollapsedDisplayName
            : item?.ToString() ?? string.Empty;
    }

    private sealed record IconOverrideOption(string Source, string TargetId, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private readonly record struct RouteDragItem(int Index);

    private readonly record struct ConditionDragItem(int Index);
}
