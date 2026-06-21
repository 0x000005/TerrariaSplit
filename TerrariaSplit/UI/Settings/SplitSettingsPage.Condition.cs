using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class SplitSettingsPage : SettingsPageBase
{
    private void AddFactToCurrentSplit()
    {
        if (advancedConditionMode)
        {
            CopySelectedTargetReferenceId();
            return;
        }

        if (!TryGetSelectedTarget(out SplitTargetDefinition target))
        {
            statusLabel.Text = Context.Localize("Select a target first.");
            return;
        }

        if (!TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            statusLabel.Text = Context.Localize("Select a split first.");
            return;
        }

        SplitCondition fact = CreateFactCondition(target);
        bool matchAll = IsAllMatchModeSelected();
        int selectedRequiredCount = GetConditionMatchCountFromSelection();
        int index = conditionList.Items.Add(CreateConditionListItem(fact));
        RefreshConditionMatchOptions(matchAll ? conditionList.Items.Count : selectedRequiredCount);
        conditionList.SelectedIndex = index;
        UseBasicConditionFromList();
        entry.Condition = GetCurrentCondition();
        entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
        SplitIconOverride previousOverride = GetCurrentIconOverride();
        RefreshIconOverrideOptions(previousOverride);
        entry.IconOverride = GetCurrentIconOverride();
        routeDirty = true;
        statusLabel.Text = string.Empty;
    }

    private void CopySelectedTargetReferenceId()
    {
        if (!TryGetSelectedTarget(out SplitTargetDefinition target))
        {
            statusLabel.Text = Context.Localize("Select a target first.");
            return;
        }

        string targetId = SplitTargetTokenFormatter.Format(target);
        Clipboard.SetText(targetId);
        statusLabel.Text = string.Format(
            CultureInfo.InvariantCulture,
            Context.Localize("Copied target ID: {0}"),
            targetId);
    }

    private void RemoveSelectedFact()
    {
        if (advancedConditionMode)
        {
            return;
        }

        if (!TryGetSelectedRouteEntry(out SplitRouteEntry entry) ||
            conditionList.SelectedIndex < 0 ||
            conditionList.SelectedIndex >= conditionList.Items.Count)
        {
            return;
        }

        int selected = conditionList.SelectedIndex;
        bool matchAll = IsAllMatchModeSelected();
        int selectedRequiredCount = GetConditionMatchCountFromSelection();
        conditionList.Items.RemoveAt(selected);
        int remainingConditionCount = conditionList.Items.Count;
        int requiredCountAfterRemoval = matchAll
            ? remainingConditionCount
            : Math.Max(1, selectedRequiredCount - 1);
        RefreshConditionMatchOptions(requiredCountAfterRemoval);
        if (conditionList.Items.Count > 0)
        {
            conditionList.SelectedIndex = Math.Min(selected, conditionList.Items.Count - 1);
        }

        UseBasicConditionFromList();
        entry.Condition = GetCurrentCondition();
        entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
        SplitIconOverride previousOverride = GetCurrentIconOverride();
        RefreshIconOverrideOptions(previousOverride);
        entry.IconOverride = GetCurrentIconOverride();
        routeDirty = true;
    }

    private void ToggleAdvancedConditionMode()
    {
        if (!advancedConditionMode)
        {
            SetAdvancedConditionMode(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(advancedConditionBox.Text))
        {
            currentCondition = SplitCondition.AtLeast([], 1);
            preserveCurrentCondition = true;
            advancedConditionError = string.Empty;
            SplitIconOverride emptyOverride = GetCurrentIconOverride();
            RenderConditionList(currentCondition, emptyOverride);
            SetAdvancedConditionMode(false, updateText: false);
            return;
        }

        if (!TryCommitAdvancedConditionText())
        {
            ShowAdvancedConditionWarning(advancedConditionError);
            return;
        }

        if (!SplitConditionEditorMode.CanUseBasicEditor(GetCurrentCondition()))
        {
            advancedConditionError = Context.Localize("Advanced condition cannot be converted to basic editor without losing structure.");
            ShowAdvancedConditionWarning(advancedConditionError);
            return;
        }

        SplitIconOverride previousOverride = GetCurrentIconOverride();
        RenderConditionList(GetCurrentCondition(), previousOverride);
        SetAdvancedConditionMode(false, updateText: false);
    }

    private void ShowAdvancedConditionWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            message = Context.Localize("Invalid advanced condition.");
        }

        advancedConditionError = message;
        statusLabel.Text = message;
        Context.Dialogs.ShowWarning(message, Context.Localize("TerrariaSplit Settings"));
    }

    private void SetAdvancedConditionMode(
        bool enabled,
        bool updateEntry = true,
        bool updateText = true,
        bool markDirty = true)
    {
        bool changed = advancedConditionMode != enabled;
        advancedConditionMode = enabled;
        if (updateEntry && TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            if (entry.UseAdvancedConditionEditor != enabled)
            {
                entry.UseAdvancedConditionEditor = enabled;
                if (markDirty)
                {
                    routeDirty = true;
                }
            }
        }

        if (enabled && updateText)
        {
            bool previousUpdating = updatingConditionSettings;
            updatingConditionSettings = true;
            try
            {
                advancedConditionBox.Text = SplitConditionText.Format(GetCurrentCondition(), Draft.General.Language);
            }
            finally
            {
                updatingConditionSettings = previousUpdating;
            }
        }

        advancedConditionBox.Visible = enabled;
        conditionList.Visible = !enabled;
        advancedConditionButton.Text = Context.Localize(enabled ? "Switch to basic" : "Switch to advanced");
        UpdateConditionEditorAvailability();
        if (changed)
        {
            statusLabel.Text = string.Empty;
        }
    }

    private void UpdateAdvancedConditionFromText()
    {
        if (updatingUi || updatingConditionSettings || !advancedConditionMode)
        {
            return;
        }

        if (!TryCommitAdvancedConditionText(updateStatusOnFailure: true))
        {
            return;
        }

        routeDirty = true;
        statusLabel.Text = string.Empty;
    }

    private bool TryCommitCurrentEditor()
    {
        if (!advancedConditionMode)
        {
            advancedConditionError = string.Empty;
            return true;
        }

        if (TryCommitAdvancedConditionText(updateStatusOnFailure: true))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(advancedConditionError))
        {
            advancedConditionError = Context.Localize("Invalid advanced condition.");
        }

        return false;
    }

    private bool TryCommitAdvancedConditionText(bool updateStatusOnFailure = true)
    {
        if (!advancedConditionMode)
        {
            return true;
        }

        if (!SplitConditionText.TryParse(advancedConditionBox.Text, Draft.General.Language, out SplitCondition condition, out string errorMessage))
        {
            advancedConditionError = errorMessage;
            if (updateStatusOnFailure)
            {
                statusLabel.Text = errorMessage;
            }

            return false;
        }

        advancedConditionError = string.Empty;
        currentCondition = condition;
        preserveCurrentCondition = true;
        if (TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            entry.Condition = GetCurrentCondition();
            entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
            entry.UseAdvancedConditionEditor = advancedConditionMode;
            SplitIconOverride previousOverride = GetCurrentIconOverride();
            RefreshIconOverrideOptions(previousOverride);
            entry.IconOverride = GetCurrentIconOverride();
        }

        return true;
    }

    private void UpdateConditionEditorAvailability()
    {
        bool basic = !advancedConditionMode;
        conditionMatchModeBox.Enabled = basic;
        conditionList.Enabled = basic;
        removeConditionButton.Enabled = basic;
        addTargetToSelectedGroupButton.Enabled = true;
        addTargetToSelectedGroupButton.Text = Context.Localize(basic ? "Add to selected group" : "Copy ID");
        addTargetToNewGroupButton.Enabled = true;
        targetKindBox.Enabled = true;
        targetSearchBox.Enabled = true;
        targetList.Enabled = true;
        LoadSelectedConditionSettings();
    }

    private void LoadSelectedConditionSettings()
    {
        if (itemQuantityBox is null)
        {
            return;
        }

        updatingConditionSettings = true;
        try
        {
            if (!advancedConditionMode &&
                TryGetSelectedConditionItem(out ConditionListItem item) &&
                IsItemCondition(item.Condition))
            {
                itemQuantityBox.Enabled = true;
                itemQuantityBox.Text = Math.Max(1, item.Condition.Value).ToString(CultureInfo.InvariantCulture);
                return;
            }

            itemQuantityBox.Enabled = false;
            itemQuantityBox.Text = string.Empty;
        }
        finally
        {
            updatingConditionSettings = false;
        }
    }

    private void UpdateSelectedConditionQuantity()
    {
        if (updatingUi || updatingConditionSettings || advancedConditionMode)
        {
            return;
        }

        if (!TryGetSelectedConditionItem(out ConditionListItem item) ||
            !IsItemCondition(item.Condition) ||
            !int.TryParse(itemQuantityBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantity) ||
            quantity < 1)
        {
            return;
        }

        item.Condition.Comparison = SplitFactComparison.AtLeast;
        item.Condition.Value = quantity;
        int selected = conditionList.SelectedIndex;
        conditionList.Items[selected] = CreateConditionListItem(item.Condition);
        conditionList.SelectedIndex = selected;

        if (TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            UseBasicConditionFromList();
            entry.Condition = GetCurrentCondition();
            routeDirty = true;
            statusLabel.Text = string.Empty;
        }
    }

    private void UpdateSelectedConditionMatchCount()
    {
        if (updatingUi || updatingConditionSettings || advancedConditionMode)
        {
            return;
        }

        if (TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            UseBasicConditionFromList();
            entry.Condition = GetCurrentCondition();
            routeDirty = true;
            statusLabel.Text = string.Empty;
        }
    }

    private SplitCondition GetCurrentCondition()
    {
        return preserveCurrentCondition
            ? currentCondition.Clone()
            : BuildConditionFromList();
    }

    private SplitCondition BuildConditionFromList()
    {
        IEnumerable<SplitCondition> facts = conditionList.Items
            .Cast<ConditionListItem>()
            .Select(item => item.Condition);
        return SplitCondition.AtLeast(facts, GetConditionMatchCountFromSelection());
    }

    private void UseBasicConditionFromList()
    {
        preserveCurrentCondition = false;
        currentCondition = BuildConditionFromList();
    }

    private int GetConditionMatchCountFromSelection()
    {
        int conditionCount = GetCurrentConditionCount();
        if (conditionCount <= 0)
        {
            return 1;
        }

        return conditionMatchModeBox.SelectedItem is MatchModeOption option
            ? Math.Clamp(option.RequiredCount, 1, conditionCount)
            : conditionCount;
    }

    private bool IsAllMatchModeSelected()
    {
        int conditionCount = Math.Max(1, GetCurrentConditionCount());
        return conditionMatchModeBox?.SelectedItem is not MatchModeOption option ||
            option.RequiredCount >= conditionCount;
    }

    private int GetCurrentConditionCount()
    {
        return conditionList?.Items.Count ?? 0;
    }

    private void RefreshConditionMatchOptions(int selectedRequiredCount)
    {
        if (conditionMatchModeBox is null)
        {
            return;
        }

        int conditionCount = GetCurrentConditionCount();
        int normalizedRequiredCount = conditionCount <= 0
            ? 1
            : Math.Clamp(selectedRequiredCount, 1, conditionCount);
        bool previousUpdating = updatingConditionSettings;
        updatingConditionSettings = true;
        try
        {
            conditionMatchModeBox.Items.Clear();
            if (conditionCount <= 0)
            {
                string allText = Context.Localize("All");
                conditionMatchModeBox.Items.Add(new MatchModeOption(1, allText, allText));
            }
            else
            {
                string allText = Context.Localize("All");
                conditionMatchModeBox.Items.Add(new MatchModeOption(conditionCount, allText, allText));
                for (int count = 1; count < conditionCount; count++)
                {
                    conditionMatchModeBox.Items.Add(new MatchModeOption(
                        count,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            Context.Localize("At least {0}"),
                            count),
                        count.ToString(CultureInfo.InvariantCulture)));
                }
            }

            for (int i = 0; i < conditionMatchModeBox.Items.Count; i++)
            {
                if (conditionMatchModeBox.Items[i] is MatchModeOption option &&
                    option.RequiredCount == normalizedRequiredCount)
                {
                    conditionMatchModeBox.SelectedIndex = i;
                    return;
                }
            }

            if (conditionMatchModeBox.Items.Count > 0)
            {
                conditionMatchModeBox.SelectedIndex = 0;
            }
        }
        finally
        {
            updatingConditionSettings = previousUpdating;
        }
    }

    private SplitIconOverride GetCurrentIconOverride()
    {
        if (iconOverrideBox?.SelectedItem is not IconOverrideOption option)
        {
            return new SplitIconOverride();
        }

        return option.Source switch
        {
            SplitIconOverrideSource.Target => new SplitIconOverride
            {
                Source = SplitIconOverrideSource.Target,
                TargetId = option.TargetId,
                FilePath = string.Empty
            },
            SplitIconOverrideSource.CustomFile => new SplitIconOverride
            {
                Source = SplitIconOverrideSource.CustomFile,
                TargetId = string.Empty,
                FilePath = iconOverrideFileBox?.Text.Trim() ?? string.Empty
            },
            _ => new SplitIconOverride()
        };
    }

    private IReadOnlyList<SplitTargetDefinition> GetCurrentConditionTargets()
    {
        var targets = new List<SplitTargetDefinition>();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string factKey in GetCurrentCondition().GetFactKeys())
        {
            if (SplitCatalog.TryGetTargetByFactKey(factKey, out SplitTargetDefinition target) &&
                seen.Add(target.Id))
            {
                targets.Add(target);
            }
        }

        return targets;
    }

    private void RenderConditionList(SplitCondition condition, SplitIconOverride? selectedOverride = null)
    {
        currentCondition = (condition ?? SplitCondition.All([])).Clone();
        currentCondition.Normalize();
        preserveCurrentCondition = true;
        SplitCondition flat = currentCondition.ToFlatGroup();
        bool previousUpdating = updatingConditionSettings;
        updatingConditionSettings = true;
        conditionList.BeginUpdate();
        try
        {
            conditionList.Items.Clear();
            foreach (SplitCondition fact in flat.GetFactConditions())
            {
                conditionList.Items.Add(CreateConditionListItem(fact));
            }

            RefreshConditionMatchOptions(Math.Max(1, flat.GetRequiredCount()));
            if (conditionList.Items.Count > 0)
            {
                conditionList.SelectedIndex = 0;
            }
            else
            {
                LoadSelectedConditionSettings();
            }
        }
        finally
        {
            conditionList.EndUpdate();
            updatingConditionSettings = previousUpdating;
        }

        RefreshIconOverrideOptions(selectedOverride);
    }

    private string FormatFact(SplitCondition condition)
    {
        if (!SplitCatalog.TryGetTargetByFactKey(condition.FactKey, out SplitTargetDefinition target))
        {
            return $"Fact: {condition.FactKey}";
        }

        return SplitTargetDisplayNames.FormatFact(condition, Draft.General.Language);
    }

    private string FormatTargetListItem(SplitTargetDefinition target)
    {
        return SplitTargetListController.FormatTargetListItem(target, Draft.General.Language);
    }

    private ConditionListItem CreateConditionListItem(SplitCondition condition)
    {
        return new ConditionListItem(condition, FormatFact(condition));
    }

    private bool TryGetSelectedRouteEntry(out SplitRouteEntry entry)
    {
        entry = null!;
        if (loadedRouteEntryIndex < 0 || loadedRouteEntryIndex >= routeEntries.Count)
        {
            return false;
        }

        entry = routeEntries[loadedRouteEntryIndex];
        return true;
    }

    private bool TryGetSelectedTarget(out SplitTargetDefinition target)
    {
        return targetController.TryGetSelectedTarget(out target);
    }

    private SplitCondition CreateFactCondition(SplitTargetDefinition target)
    {
        if (target.Kind != SplitTargetKind.Item)
        {
            if (target.Kind == SplitTargetKind.Npc &&
                SplitCatalog.TryParseNpcTargetId(target.Id, out int npcId))
            {
                return SplitCatalog.CreateNpcPresentCondition(npcId);
            }

            if (target.Kind == SplitTargetKind.Biome &&
                SplitCatalog.TryParseBiomeTargetId(target.Id, out string? biomeId))
            {
                return SplitCatalog.CreateBiomeActiveCondition(biomeId);
            }

            return SplitCondition.Fact(target.FactKey);
        }

        if (!SplitCatalog.TryParseItemTargetId(target.Id, out int itemId))
        {
            return SplitCondition.Fact(target.FactKey, SplitFactComparison.AtLeast, 1);
        }

        return SplitCatalog.CreateItemEverOwnedCondition(itemId, 1);
    }

    private bool TryGetSelectedConditionItem(out ConditionListItem item)
    {
        item = null!;
        if (conditionList is null ||
            conditionList.SelectedIndex < 0 ||
            conditionList.SelectedIndex >= conditionList.Items.Count ||
            conditionList.Items[conditionList.SelectedIndex] is not ConditionListItem selected)
        {
            return false;
        }

        item = selected;
        return true;
    }

    private static bool IsItemCondition(SplitCondition condition)
    {
        return SplitCatalog.TryParseItemFactKey(condition.FactKey, out _);
    }
}
