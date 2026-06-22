using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class SplitSettingsPage : SettingsPageBase
{
    private void RefreshTargetList()
    {
        targetController.Refresh();
        LoadSelectedConditionSettings();
    }

    private void RefreshRouteList()
    {
        if (routeList is null)
        {
            return;
        }

        int selected = routeList.SelectedIndex;
        routeController.Refreshing = true;
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
            routeController.Refreshing = false;
        }

        if (routeList.Items.Count == 0)
        {
            ClearSelectedRouteControls();
            return;
        }

        routeList.SelectedIndex = Math.Clamp(selected, 0, routeList.Items.Count - 1);
        if (routeList.SelectedIndex != routeController.LoadedEntryIndex)
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
        if (newIndex == routeController.LoadedEntryIndex)
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

        routeController.LoadedEntryIndex = newIndex;
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
        if (routeController.LoadedEntryIndex < 0 ||
            routeController.LoadedEntryIndex >= routeList.Items.Count ||
            routeList.SelectedIndex == routeController.LoadedEntryIndex)
        {
            return;
        }

        updatingUi = true;
        try
        {
            routeList.SelectedIndex = routeController.LoadedEntryIndex;
        }
        finally
        {
            updatingUi = false;
        }
    }

    private void ClearSelectedRouteControls()
    {
        routeController.ClearLoadedEntry();
        updatingUi = true;
        try
        {
            splitNameBox.Text = string.Empty;
            splitEnabledBox.Checked = false;
            splitAttachedBox.Checked = false;
            splitAttachedBox.Enabled = false;
            conditionList.Items.Clear();
            RefreshConditionMatchOptions(1);
            conditionController.CurrentCondition = SplitCondition.AtLeast([], 1);
            conditionController.PreserveCurrentCondition = false;
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
            routeController.LoadedEntryIndex < 0 ||
            routeController.LoadedEntryIndex >= routeEntries.Count)
        {
            return true;
        }

        if (!TryCommitCurrentEditor())
        {
            return false;
        }

        SplitRouteEntry entry = routeEntries[routeController.LoadedEntryIndex];
        entry.DisplayName = splitNameBox.Text.Trim();
        entry.Enabled = splitEnabledBox.Checked;
        entry.IsAttached = splitAttachedBox.Enabled && splitAttachedBox.Checked;
        entry.Condition = GetCurrentCondition();
        entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
        entry.IconOverride = GetCurrentIconOverride();
        entry.UseAdvancedConditionEditor = conditionController.AdvancedMode;
        return true;
    }

    private void UpdateSelectedAttachedAvailability()
    {
        if (splitAttachedBox is null ||
            routeController.LoadedEntryIndex < 0 ||
            routeController.LoadedEntryIndex >= routeEntries.Count)
        {
            return;
        }

        bool canAttach = routeDraft.CanEntryAttachToFollowingAnchor(routeController.LoadedEntryIndex);
        bool previousUpdating = updatingUi;
        updatingUi = true;
        try
        {
            splitAttachedBox.Enabled = canAttach;
            splitAttachedBox.Checked = canAttach && routeEntries[routeController.LoadedEntryIndex].IsAttached;
        }
        finally
        {
            updatingUi = previousUpdating;
        }
    }

    private void MarkSelectedEntryDirty()
    {
        if (updatingUi)
        {
            return;
        }

        routeController.MarkDirty();
        if (!SaveSelectedEntryFromControls())
        {
            return;
        }

        routeDraft.NormalizeAttachedRouteFlags();
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
        routeDraft.NormalizeAttachedRouteFlags();
        routeController.MarkDirty();
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

        routeController.MarkDirty();
        statusLabel.Text = string.Empty;
        routeDraft.NormalizeAttachedRouteFlags();
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
        routeController.ClearLoadedEntry();
        routeController.MarkDirty();
        routeDraft.NormalizeAttachedRouteFlags();
        RefreshRouteList();
        if (routeList.Items.Count > 0)
        {
            routeList.SelectedIndex = Math.Min(index, routeList.Items.Count - 1);
        }
    }
}
