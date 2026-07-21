namespace TerrariaSplit.UI.Settings;

internal sealed class SplitRouteDraft
{
    public List<SplitRouteEntry> Entries { get; } = new();

    public void LoadFrom(RouteSettings route)
    {
        Entries.Clear();
        Entries.AddRange(route.SplitRoute.Select(CloneEntry));
        if (Entries.Count == 0)
        {
            Entries.AddRange(SplitCatalog.CreateDefaultRoute().Select(CloneEntry));
        }
    }

    public List<SplitRouteEntry> CreateSnapshot()
    {
        return Entries.Select(CloneEntry).ToList();
    }

    public void EnsureEntryIds()
    {
        HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Entries.Count; i++)
        {
            SplitRouteEntry entry = Entries[i];
            string baseId = string.IsNullOrWhiteSpace(entry.Id)
                ? SplitSettingsRouteIdFactory.CreateSplitId(entry, i + 1)
                : entry.Id.Trim();
            entry.Id = SplitSettingsRouteIdFactory.CreateUniqueSplitId(baseId, seenIds, i + 1);
        }
    }

    public string CreateUniqueSplitId(string preferredId)
    {
        HashSet<string> seenIds = Entries
            .Select(entry => entry.Id.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return SplitSettingsRouteIdFactory.CreateUniqueSplitId(preferredId, seenIds, Entries.Count + 1);
    }

    public void NormalizeAttachedRouteFlags()
    {
        bool hasFollowingEnabledAnchor = false;
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            SplitRouteEntry entry = Entries[i];
            if (!entry.Enabled)
            {
                continue;
            }

            if (entry.IsAttached && !hasFollowingEnabledAnchor)
            {
                entry.IsAttached = false;
            }

            if (!entry.IsAttached)
            {
                hasFollowingEnabledAnchor = true;
            }
        }
    }

    public bool CanEntryAttachToFollowingAnchor(int index)
    {
        if (index < 0 || index >= Entries.Count || !Entries[index].Enabled)
        {
            return false;
        }

        for (int i = index + 1; i < Entries.Count; i++)
        {
            if (Entries[i].Enabled)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryValidate(Func<string, string> localize, out string message)
    {
        return SplitSettingsRouteValidator.TryValidate(Entries, localize, out message);
    }

    public static SplitRouteEntry CloneEntry(SplitRouteEntry entry)
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
                !SplitConditionEditorMode.CanUseBasicEditor(entry.Condition ?? SplitCondition.All([]))
        };
    }

    private static SplitIconOverride CloneIconOverride(SplitIconOverride? iconOverride)
    {
        return new SplitIconOverride
        {
            Source = SplitIconOverrideSource.Normalize(iconOverride?.Source),
            TargetId = iconOverride?.TargetId ?? string.Empty,
            FilePath = iconOverride?.FilePath ?? string.Empty,
            AllIconFilePaths = new Dictionary<string, string>(
                iconOverride?.AllIconFilePaths ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase)
        };
    }
}
