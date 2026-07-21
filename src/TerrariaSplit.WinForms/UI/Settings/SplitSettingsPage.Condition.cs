using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class SplitSettingsPage : SettingsPageBase
{
    private void AddFactToCurrentSplit()
    {
        if (conditionController.AdvancedMode)
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
        routeController.MarkDirty();
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
        if (conditionController.AdvancedMode)
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
        routeController.MarkDirty();
    }

    private void ToggleAdvancedConditionMode()
    {
        if (!conditionController.AdvancedMode)
        {
            SetAdvancedConditionMode(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(advancedConditionBox.Text))
        {
            conditionController.CurrentCondition = SplitCondition.AtLeast([], 1);
            conditionController.PreserveCurrentCondition = true;
            conditionController.AdvancedError = string.Empty;
            SplitIconOverride emptyOverride = GetCurrentIconOverride();
            RenderConditionList(conditionController.CurrentCondition, emptyOverride);
            SetAdvancedConditionMode(false, updateText: false);
            return;
        }

        if (!TryCommitAdvancedConditionText())
        {
            ShowAdvancedConditionWarning(conditionController.AdvancedError);
            return;
        }

        if (!SplitConditionEditorMode.CanUseBasicEditor(GetCurrentCondition()))
        {
            conditionController.AdvancedError = Context.Localize("Advanced condition cannot be converted to basic editor without losing structure.");
            ShowAdvancedConditionWarning(conditionController.AdvancedError);
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

        conditionController.AdvancedError = message;
        statusLabel.Text = message;
        Context.Dialogs.ShowWarning(message, Context.Localize("TerrariaSplit Settings"));
    }

    private void SetAdvancedConditionMode(
        bool enabled,
        bool updateEntry = true,
        bool updateText = true,
        bool markDirty = true)
    {
        bool changed = conditionController.AdvancedMode != enabled;
        conditionController.AdvancedMode = enabled;
        if (updateEntry && TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            if (entry.UseAdvancedConditionEditor != enabled)
            {
                entry.UseAdvancedConditionEditor = enabled;
                if (markDirty)
                {
                    routeController.MarkDirty();
                }
            }
        }

        if (enabled && updateText)
        {
            bool previousUpdating = conditionController.UpdatingSettings;
            conditionController.UpdatingSettings = true;
            try
            {
                advancedConditionBox.Text = SplitConditionText.Format(GetCurrentCondition(), Draft.General.Language);
            }
            finally
            {
                conditionController.UpdatingSettings = previousUpdating;
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
        if (updatingUi || conditionController.UpdatingSettings || !conditionController.AdvancedMode)
        {
            return;
        }

        if (!TryCommitAdvancedConditionText(updateStatusOnFailure: true))
        {
            return;
        }

        routeController.MarkDirty();
        statusLabel.Text = string.Empty;
    }

    private bool TryCommitCurrentEditor()
    {
        if (!conditionController.AdvancedMode)
        {
            conditionController.AdvancedError = string.Empty;
            return true;
        }

        if (TryCommitAdvancedConditionText(updateStatusOnFailure: true))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(conditionController.AdvancedError))
        {
            conditionController.AdvancedError = Context.Localize("Invalid advanced condition.");
        }

        return false;
    }

    private bool TryCommitAdvancedConditionText(bool updateStatusOnFailure = true)
    {
        if (!conditionController.AdvancedMode)
        {
            return true;
        }

        if (!SplitConditionText.TryParse(advancedConditionBox.Text, Draft.General.Language, out SplitCondition condition, out string errorMessage))
        {
            conditionController.AdvancedError = errorMessage;
            if (updateStatusOnFailure)
            {
                statusLabel.Text = errorMessage;
            }

            return false;
        }

        conditionController.AdvancedError = string.Empty;
        conditionController.CurrentCondition = condition;
        conditionController.PreserveCurrentCondition = true;
        if (TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            entry.Condition = GetCurrentCondition();
            entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
            entry.UseAdvancedConditionEditor = conditionController.AdvancedMode;
            SplitIconOverride previousOverride = GetCurrentIconOverride();
            RefreshIconOverrideOptions(previousOverride);
            entry.IconOverride = GetCurrentIconOverride();
        }

        return true;
    }

    private void UpdateConditionEditorAvailability()
    {
        bool basic = !conditionController.AdvancedMode;
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

        conditionController.UpdatingSettings = true;
        try
        {
            if (!conditionController.AdvancedMode &&
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
            conditionController.UpdatingSettings = false;
        }
    }

    private void UpdateSelectedConditionQuantity()
    {
        if (updatingUi || conditionController.UpdatingSettings || conditionController.AdvancedMode)
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
            routeController.MarkDirty();
            statusLabel.Text = string.Empty;
        }
    }

    private void UpdateSelectedConditionMatchCount()
    {
        if (updatingUi || conditionController.UpdatingSettings || conditionController.AdvancedMode)
        {
            return;
        }

        if (TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            UseBasicConditionFromList();
            entry.Condition = GetCurrentCondition();
            routeController.MarkDirty();
            statusLabel.Text = string.Empty;
        }
    }

    private SplitCondition GetCurrentCondition()
    {
        return conditionController.PreserveCurrentCondition
            ? conditionController.CurrentCondition.Clone()
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
        conditionController.PreserveCurrentCondition = false;
        conditionController.CurrentCondition = BuildConditionFromList();
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
        bool previousUpdating = conditionController.UpdatingSettings;
        conditionController.UpdatingSettings = true;
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
            conditionController.UpdatingSettings = previousUpdating;
        }
    }

    private SplitIconOverride GetCurrentIconOverride()
    {
        Dictionary<string, string> allIconFilePaths = allIconFileBoxes
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value.Text))
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.Text.Trim(),
                StringComparer.OrdinalIgnoreCase);
        if (iconOverrideBox?.SelectedItem is not IconOverrideOption option)
        {
            return new SplitIconOverride { AllIconFilePaths = allIconFilePaths };
        }

        return option.Source switch
        {
            SplitIconOverrideSource.Target => new SplitIconOverride
            {
                Source = SplitIconOverrideSource.Target,
                TargetId = option.TargetId,
                FilePath = string.Empty,
                AllIconFilePaths = allIconFilePaths
            },
            SplitIconOverrideSource.CustomFile => new SplitIconOverride
            {
                Source = SplitIconOverrideSource.CustomFile,
                TargetId = string.Empty,
                FilePath = iconOverrideFileBox?.Text.Trim() ?? string.Empty,
                AllIconFilePaths = allIconFilePaths
            },
            _ => new SplitIconOverride { AllIconFilePaths = allIconFilePaths }
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
        conditionController.CurrentCondition = (condition ?? SplitCondition.All([])).Clone();
        conditionController.CurrentCondition.Normalize();
        conditionController.PreserveCurrentCondition = true;
        SplitCondition flat = conditionController.CurrentCondition.ToFlatGroup();
        bool previousUpdating = conditionController.UpdatingSettings;
        conditionController.UpdatingSettings = true;
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
            conditionController.UpdatingSettings = previousUpdating;
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
        if (routeController.LoadedEntryIndex < 0 || routeController.LoadedEntryIndex >= routeEntries.Count)
        {
            return false;
        }

        entry = routeEntries[routeController.LoadedEntryIndex];
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
