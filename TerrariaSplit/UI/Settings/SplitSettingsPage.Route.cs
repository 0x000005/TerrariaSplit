using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class SplitSettingsPage : SettingsPageBase
{
    private void RefreshTargetList()
    {
        if (targetList is null)
        {
            return;
        }

        string query = targetSearchBox?.Text.Trim() ?? string.Empty;
        string targetKind = targetKindBox is null ? SplitTargetKind.Boss : GetSelectedTargetKind(targetKindBox);
        targetList.BeginUpdate();
        try
        {
            targetList.Items.Clear();
            List<SplitTargetDefinition> targets = SplitTargetSearch.QueryTargets(query, targetKind)
                .Take(MaxTargetSearchResults + 1)
                .ToList();
            if (targets.Count > MaxTargetSearchResults)
            {
                targetList.Items.Add(Context.Localize("Too many results"));
                return;
            }

            foreach (SplitTargetDefinition target in targets)
            {
                targetList.Items.Add(new TargetListItem(target, FormatTargetListItem(target)));
            }
        }
        finally
        {
            targetList.EndUpdate();
        }

        LoadSelectedConditionSettings();
    }

    private void RefreshRouteList()
    {
        if (routeList is null)
        {
            return;
        }

        int selected = routeList.SelectedIndex;
        refreshingRouteList = true;
        routeList.BeginUpdate();
        try
        {
            routeList.Items.Clear();
            for (int i = 0; i < routeEntries.Count; i++)
            {
                routeList.Items.Add(new RouteListItem(routeEntries[i]));
            }
        }
        finally
        {
            routeList.EndUpdate();
            refreshingRouteList = false;
        }

        if (routeList.Items.Count == 0)
        {
            ClearSelectedRouteControls();
            return;
        }

        routeList.SelectedIndex = Math.Clamp(selected, 0, routeList.Items.Count - 1);
        if (routeList.SelectedIndex != loadedRouteEntryIndex)
        {
            LoadSelectedRouteEntry();
        }
    }

    private void LoadSelectedRouteEntry()
    {
        if (updatingUi)
        {
            return;
        }

        int newIndex = routeList.SelectedIndex;
        if (newIndex == loadedRouteEntryIndex)
        {
            return;
        }

        if (!SaveSelectedEntryFromControls())
        {
            RevertRouteSelection();
            return;
        }

        if (newIndex < 0 || newIndex >= routeEntries.Count)
        {
            ClearSelectedRouteControls();
            return;
        }

        loadedRouteEntryIndex = newIndex;
        updatingUi = true;
        try
        {
            SplitRouteEntry entry = routeEntries[newIndex];
            splitNameBox.Text = entry.DisplayName;
            splitEnabledBox.Checked = entry.Enabled;
            splitAttachedBox.Checked = entry.IsAttached;
            RenderConditionList(entry.Condition, entry.IconOverride);
            SetAdvancedConditionMode(entry.UseAdvancedConditionEditor, updateEntry: false, updateText: true, markDirty: false);
            UpdateSelectedAttachedAvailability();
        }
        finally
        {
            updatingUi = false;
        }
    }

    private void RevertRouteSelection()
    {
        if (loadedRouteEntryIndex < 0 ||
            loadedRouteEntryIndex >= routeList.Items.Count ||
            routeList.SelectedIndex == loadedRouteEntryIndex)
        {
            return;
        }

        updatingUi = true;
        try
        {
            routeList.SelectedIndex = loadedRouteEntryIndex;
        }
        finally
        {
            updatingUi = false;
        }
    }

    private void ClearSelectedRouteControls()
    {
        loadedRouteEntryIndex = -1;
        updatingUi = true;
        try
        {
            splitNameBox.Text = string.Empty;
            splitEnabledBox.Checked = false;
            splitAttachedBox.Checked = false;
            splitAttachedBox.Enabled = false;
            conditionList.Items.Clear();
            RefreshConditionMatchOptions(1);
            currentCondition = SplitCondition.AtLeast([], 1);
            preserveCurrentCondition = false;
            SetAdvancedConditionMode(false, updateEntry: false, updateText: false, markDirty: false);
            RefreshIconOverrideOptions(new SplitIconOverride());
            LoadSelectedConditionSettings();
        }
        finally
        {
            updatingUi = false;
        }
    }

    private bool SaveSelectedEntryFromControls()
    {
        if (updatingUi ||
            routeList is null ||
            loadedRouteEntryIndex < 0 ||
            loadedRouteEntryIndex >= routeEntries.Count)
        {
            return true;
        }

        if (!TryCommitCurrentEditor())
        {
            return false;
        }

        SplitRouteEntry entry = routeEntries[loadedRouteEntryIndex];
        entry.DisplayName = splitNameBox.Text.Trim();
        entry.Enabled = splitEnabledBox.Checked;
        entry.IsAttached = splitAttachedBox.Enabled && splitAttachedBox.Checked;
        entry.Condition = GetCurrentCondition();
        entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
        entry.IconOverride = GetCurrentIconOverride();
        entry.UseAdvancedConditionEditor = advancedConditionMode;
        return true;
    }

    private void NormalizeAttachedRouteFlags()
    {
        bool hasFollowingEnabledAnchor = false;
        for (int i = routeEntries.Count - 1; i >= 0; i--)
        {
            SplitRouteEntry entry = routeEntries[i];
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

    private void UpdateSelectedAttachedAvailability()
    {
        if (splitAttachedBox is null ||
            loadedRouteEntryIndex < 0 ||
            loadedRouteEntryIndex >= routeEntries.Count)
        {
            return;
        }

        bool canAttach = CanEntryAttachToFollowingAnchor(loadedRouteEntryIndex);
        bool previousUpdating = updatingUi;
        updatingUi = true;
        try
        {
            splitAttachedBox.Enabled = canAttach;
            splitAttachedBox.Checked = canAttach && routeEntries[loadedRouteEntryIndex].IsAttached;
        }
        finally
        {
            updatingUi = previousUpdating;
        }
    }

    private bool CanEntryAttachToFollowingAnchor(int index)
    {
        if (index < 0 || index >= routeEntries.Count || !routeEntries[index].Enabled)
        {
            return false;
        }

        for (int i = index + 1; i < routeEntries.Count; i++)
        {
            if (routeEntries[i].Enabled)
            {
                return true;
            }
        }

        return false;
    }

    private void MarkSelectedEntryDirty()
    {
        if (updatingUi)
        {
            return;
        }

        routeDirty = true;
        if (!SaveSelectedEntryFromControls())
        {
            return;
        }

        NormalizeAttachedRouteFlags();
        UpdateSelectedAttachedAvailability();
        RefreshRouteList();
    }

    private void AddBlankSplit()
    {
        if (!SaveSelectedEntryFromControls())
        {
            return;
        }

        int index = routeEntries.Count + 1;
        routeEntries.Add(new SplitRouteEntry
        {
            Id = CreateUniqueSplitId($"split:custom-{index.ToString(CultureInfo.InvariantCulture)}"),
            DisplayName = $"Custom {index.ToString(CultureInfo.InvariantCulture)}",
            Enabled = true,
            IsAttached = false,
            Condition = SplitCondition.AtLeast([], 1),
            IconTargetIds = [],
            UseAdvancedConditionEditor = false
        });
        NormalizeAttachedRouteFlags();
        routeDirty = true;
        RefreshRouteList();
        routeList.SelectedIndex = routeEntries.Count - 1;
    }

    private void AddTargetToNewGroup()
    {
        if (!TryGetSelectedTarget(out SplitTargetDefinition target))
        {
            statusLabel.Text = Context.Localize("Select a target first.");
            return;
        }

        if (!SaveSelectedEntryFromControls())
        {
            return;
        }

        SplitCondition condition = SplitCondition.AtLeast([CreateFactCondition(target)], 1);
        routeEntries.Add(new SplitRouteEntry
        {
            Id = CreateUniqueSplitId(SplitSettingsRouteIdFactory.CreateSplitId(target)),
            DisplayName = SplitTargetDisplayNames.GetTargetName(target, Draft.General.Language),
            Enabled = true,
            IsAttached = false,
            Condition = condition,
            IconTargetIds = SplitCatalog.InferTargetIds(condition).ToList(),
            UseAdvancedConditionEditor = false
        });

        routeDirty = true;
        statusLabel.Text = string.Empty;
        NormalizeAttachedRouteFlags();
        RefreshRouteList();
        routeList.SelectedIndex = routeEntries.Count - 1;
    }

    private void DeleteSelectedSplit()
    {
        if (routeList.SelectedIndex < 0 || routeList.SelectedIndex >= routeEntries.Count)
        {
            return;
        }

        int index = routeList.SelectedIndex;
        routeEntries.RemoveAt(index);
        loadedRouteEntryIndex = -1;
        routeDirty = true;
        NormalizeAttachedRouteFlags();
        RefreshRouteList();
        if (routeList.Items.Count > 0)
        {
            routeList.SelectedIndex = Math.Min(index, routeList.Items.Count - 1);
        }
    }
}
