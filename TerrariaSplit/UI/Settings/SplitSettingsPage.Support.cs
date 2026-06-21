using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class SplitSettingsPage : SettingsPageBase
{
    private void EnsureRouteEntryIds()
    {
        HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < routeEntries.Count; i++)
        {
            SplitRouteEntry entry = routeEntries[i];
            string baseId = string.IsNullOrWhiteSpace(entry.Id)
                ? SplitSettingsRouteIdFactory.CreateSplitId(entry, i + 1)
                : entry.Id.Trim();
            entry.Id = SplitSettingsRouteIdFactory.CreateUniqueSplitId(baseId, seenIds, i + 1);
        }
    }

    private string CreateUniqueSplitId(string preferredId)
    {
        HashSet<string> seenIds = routeEntries
            .Select(entry => entry.Id.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return SplitSettingsRouteIdFactory.CreateUniqueSplitId(preferredId, seenIds, routeEntries.Count + 1);
    }

    private bool TryValidateRoute(out string message)
    {
        return SplitSettingsRouteValidator.TryValidate(routeEntries, Context.Localize, out message);
    }

    private static SplitRouteEntry CloneEntry(SplitRouteEntry entry)
    {
        return new SplitRouteEntry
        {
            Id = entry.Id,
            Enabled = entry.Enabled,
            IsAttached = entry.IsAttached,
            DisplayName = entry.DisplayName,
            Condition = (entry.Condition ?? SplitCondition.All([])).Clone(),
            IconTargetIds = entry.IconTargetIds?.ToList() ?? new List<string>(),
            IconOverride = CloneIconOverride(entry.IconOverride),
            UseAdvancedConditionEditor = entry.UseAdvancedConditionEditor ||
                !CanUseBasicConditionEditor(entry.Condition ?? SplitCondition.All([]))
        };
    }

    private static SplitIconOverride CloneIconOverride(SplitIconOverride? iconOverride)
    {
        return new SplitIconOverride
        {
            Source = SplitIconOverrideSource.Normalize(iconOverride?.Source),
            TargetId = iconOverride?.TargetId ?? string.Empty,
            FilePath = iconOverride?.FilePath ?? string.Empty
        };
    }

    private sealed record TargetListItem(SplitTargetDefinition Target, string DisplayText)
    {
        public override string ToString()
        {
            return DisplayText;
        }
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

    private sealed record TargetKindOption(string Value, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
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
