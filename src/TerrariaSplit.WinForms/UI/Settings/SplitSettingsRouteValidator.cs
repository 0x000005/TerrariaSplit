using System.Globalization;

namespace TerrariaSplit.UI.Settings;

internal static class SplitSettingsRouteValidator
{
    public static bool TryValidate(
        IReadOnlyList<SplitRouteEntry> routeEntries,
        Func<string, string> localize,
        out string message)
    {
        message = string.Empty;
        if (routeEntries.Count == 0)
        {
            message = localize("Route must contain at least one split.");
            return false;
        }

        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        foreach (SplitRouteEntry entry in routeEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                message = localize("Every split needs an id.");
                return false;
            }

            if (!ids.Add(entry.Id.Trim()))
            {
                message = string.Format(CultureInfo.InvariantCulture, localize("Duplicate split id: {0}"), entry.Id);
                return false;
            }

            if (!ValidateCondition(entry.Condition, localize, out string conditionMessage))
            {
                message = $"{entry.DisplayName}: {conditionMessage}";
                return false;
            }

            if (!ValidateIconOverride(entry, localize, out string iconMessage))
            {
                message = $"{entry.DisplayName}: {iconMessage}";
                return false;
            }
        }

        return true;
    }

    private static bool ValidateCondition(SplitCondition condition, Func<string, string> localize, out string message)
    {
        message = string.Empty;
        SplitCondition normalized = (condition ?? SplitCondition.All([])).Clone();
        normalized.Normalize();
        if (!normalized.GetFactConditions().Any())
        {
            message = localize("Condition group cannot be empty.");
            return false;
        }

        return ValidateConditionNode(normalized, localize, out message);
    }

    private static bool ValidateConditionNode(SplitCondition condition, Func<string, string> localize, out string message)
    {
        message = string.Empty;
        string kind = SplitConditionKind.Normalize(condition.Kind);
        if (kind == SplitConditionKind.Fact)
        {
            if (!SplitCatalog.TryGetTargetByFactKey(condition.FactKey, out _))
            {
                message = localize("Unknown fact.");
                return false;
            }

            string comparison = SplitFactComparison.Normalize(condition.Comparison);
            if ((comparison == SplitFactComparison.AtLeast || comparison == SplitFactComparison.Equal) &&
                condition.Value < 1)
            {
                message = localize("Item quantity must be at least 1.");
                return false;
            }

            return true;
        }

        if (kind != SplitConditionKind.All &&
            kind != SplitConditionKind.Any &&
            kind != SplitConditionKind.AtLeast)
        {
            message = localize("Unknown condition group.");
            return false;
        }

        if (condition.Children.Count == 0)
        {
            message = localize("Condition group cannot be empty.");
            return false;
        }

        int requiredCount = kind == SplitConditionKind.All
            ? condition.Children.Count
            : Math.Max(1, condition.Value);
        if (requiredCount < 1)
        {
            message = localize("Match count must be at least 1.");
            return false;
        }

        if (requiredCount > condition.Children.Count)
        {
            message = localize("Match count cannot exceed condition count.");
            return false;
        }

        foreach (SplitCondition child in condition.Children)
        {
            if (!ValidateConditionNode(child, localize, out message))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateIconOverride(SplitRouteEntry entry, Func<string, string> localize, out string message)
    {
        message = string.Empty;
        SplitIconOverride iconOverride = entry.IconOverride ?? new SplitIconOverride();
        string source = SplitIconOverrideSource.Normalize(iconOverride.Source);
        if (source == SplitIconOverrideSource.Target)
        {
            HashSet<string> conditionTargetIds = SplitCatalog.InferTargetIds(entry.Condition)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!conditionTargetIds.Contains(iconOverride.TargetId?.Trim() ?? string.Empty))
            {
                message = localize("Icon target must be in condition.");
                return false;
            }
        }

        if (source == SplitIconOverrideSource.CustomFile &&
            string.IsNullOrWhiteSpace(iconOverride.FilePath))
        {
            message = localize("Custom icon file is required.");
            return false;
        }

        return true;
    }
}
