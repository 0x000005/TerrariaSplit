namespace TerrariaSplit.Configuration;

public sealed class SettingsRouteOverridePackage
{
    public string Key { get; init; } = string.Empty;

    public List<SplitRouteEntry> SplitRoute { get; init; } = new();

    public ReferenceSplitSet? ReferenceSet { get; init; }
}

public static class SettingsRouteOverrideService
{
    public static SettingsRouteOverridePackage Clone(SettingsRouteOverridePackage package)
    {
        return new SettingsRouteOverridePackage
        {
            Key = package.Key ?? string.Empty,
            SplitRoute = CloneRoute(package.SplitRoute),
            ReferenceSet = CloneReferenceSet(package.ReferenceSet)
        };
    }

    public static AppSettings Apply(
        AppSettings baseSettings,
        SettingsRouteOverridePackage package,
        ISettingsSnapshotFactory settingsSnapshots)
    {
        AppSettings nextSettings = settingsSnapshots.CreateSnapshot(baseSettings);
        nextSettings.Route.SplitRoute = CloneRoute(package.SplitRoute);
        if (package.ReferenceSet is ReferenceSplitSet referenceSet)
        {
            ReferenceSplitSet clonedReference = CloneReferenceSet(referenceSet)!;
            nextSettings.Comparison.UsePersonalBestAsReferenceTime = false;
            nextSettings.Comparison.ReferenceSplitSets =
            [
                clonedReference
            ];
            nextSettings.Comparison.ActiveReferenceSplitSet = clonedReference.Name;
        }

        SettingsNormalizer.Normalize(nextSettings);
        return nextSettings;
    }

    public static List<SplitRouteEntry> CloneRoute(IReadOnlyList<SplitRouteEntry> route)
    {
        return route.Select(CloneEntry).ToList();
    }

    public static SplitRouteEntry CloneEntry(SplitRouteEntry entry)
    {
        return new SplitRouteEntry
        {
            Id = entry.Id,
            Enabled = entry.Enabled,
            DisplayName = entry.DisplayName,
            Condition = (entry.Condition ?? SplitCondition.Fact(string.Empty)).Clone(),
            IconTargetIds = entry.IconTargetIds?.ToList() ?? new List<string>(),
            IconOverride = new SplitIconOverride
            {
                Source = SplitIconOverrideSource.Normalize(entry.IconOverride?.Source),
                TargetId = entry.IconOverride?.TargetId ?? string.Empty,
                FilePath = entry.IconOverride?.FilePath ?? string.Empty
            },
            IsAttached = entry.IsAttached,
            UseAdvancedConditionEditor = entry.UseAdvancedConditionEditor,
            ExpandDetails = entry.ExpandDetails
        };
    }

    public static ReferenceSplitSet? CloneReferenceSet(ReferenceSplitSet? source)
    {
        if (source is null)
        {
            return null;
        }

        return new ReferenceSplitSet
        {
            Name = source.Name,
            Splits = new Dictionary<string, string>(source.Splits, StringComparer.OrdinalIgnoreCase)
        };
    }
}
