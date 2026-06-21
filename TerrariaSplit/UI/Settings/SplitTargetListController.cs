using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class SplitTargetListController
{
    private const int MaxTargetSearchResults = 500;

    private readonly ListBox targetList;
    private readonly TextBox targetSearchBox;
    private readonly ThemedDropDownList targetKindBox;
    private readonly Func<string> languageProvider;
    private readonly Func<string, string> localize;

    public SplitTargetListController(
        ListBox targetList,
        TextBox targetSearchBox,
        ThemedDropDownList targetKindBox,
        Func<string> languageProvider,
        Func<string, string> localize)
    {
        this.targetList = targetList;
        this.targetSearchBox = targetSearchBox;
        this.targetKindBox = targetKindBox;
        this.languageProvider = languageProvider;
        this.localize = localize;
    }

    public static ThemedDropDownList CreateTargetKindBox(
        SettingsUiFactory factory,
        Func<string, string> localize,
        string selectedKind)
    {
        ThemedDropDownList comboBox = factory.CreateDropDownList();
        comboBox.Items.Add(new SplitTargetKindOption(SplitTargetKind.Boss, localize("Boss")));
        comboBox.Items.Add(new SplitTargetKindOption(SplitTargetKind.Item, localize("Item")));
        comboBox.Items.Add(new SplitTargetKindOption(SplitTargetKind.Npc, localize("NPC")));
        comboBox.Items.Add(new SplitTargetKindOption(SplitTargetKind.Biome, localize("Biome")));
        SetTargetKind(comboBox, selectedKind);
        return comboBox;
    }

    public void Refresh()
    {
        string query = targetSearchBox.Text.Trim();
        string targetKind = GetSelectedTargetKind(targetKindBox);
        targetList.BeginUpdate();
        try
        {
            targetList.Items.Clear();
            List<SplitTargetDefinition> targets = SplitTargetSearch.QueryTargets(query, targetKind)
                .Take(MaxTargetSearchResults + 1)
                .ToList();
            if (targets.Count > MaxTargetSearchResults)
            {
                targetList.Items.Add(localize("Too many results"));
                return;
            }

            string language = languageProvider();
            foreach (SplitTargetDefinition target in targets)
            {
                targetList.Items.Add(new SplitTargetListItem(target, FormatTargetListItem(target, language)));
            }
        }
        finally
        {
            targetList.EndUpdate();
        }
    }

    public bool TryGetSelectedTarget(out SplitTargetDefinition target)
    {
        if (targetList.SelectedItem is SplitTargetListItem selected)
        {
            target = selected.Target;
            return true;
        }

        target = null!;
        return false;
    }

    public static string FormatTargetListItem(SplitTargetDefinition target, string language)
    {
        return $"{SplitTargetDisplayNames.GetTargetName(target, language)} ({SplitTargetTokenFormatter.Format(target)})";
    }

    private static void SetTargetKind(ThemedDropDownList comboBox, string selectedKind)
    {
        string normalized = NormalizeTargetKind(selectedKind);
        comboBox.SelectedItem = comboBox.Items
            .Cast<SplitTargetKindOption>()
            .FirstOrDefault(option => string.Equals(option.Value, normalized, StringComparison.OrdinalIgnoreCase));
        if (comboBox.SelectedIndex < 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private static string GetSelectedTargetKind(ThemedDropDownList comboBox)
    {
        return comboBox.SelectedItem is SplitTargetKindOption option
            ? NormalizeTargetKind(option.Value)
            : SplitTargetKind.Boss;
    }

    private static string NormalizeTargetKind(string? value)
    {
        if (string.Equals(value, SplitTargetKind.Item, StringComparison.OrdinalIgnoreCase))
        {
            return SplitTargetKind.Item;
        }

        if (string.Equals(value, SplitTargetKind.Npc, StringComparison.OrdinalIgnoreCase))
        {
            return SplitTargetKind.Npc;
        }

        return string.Equals(value, SplitTargetKind.Biome, StringComparison.OrdinalIgnoreCase)
            ? SplitTargetKind.Biome
            : SplitTargetKind.Boss;
    }
}

internal sealed record SplitTargetListItem(SplitTargetDefinition Target, string DisplayText)
{
    public override string ToString()
    {
        return DisplayText;
    }
}

internal sealed record SplitTargetKindOption(string Value, string DisplayName)
{
    public override string ToString()
    {
        return DisplayName;
    }
}
