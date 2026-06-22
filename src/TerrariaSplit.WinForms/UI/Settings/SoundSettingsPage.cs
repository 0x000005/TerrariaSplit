using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class SoundSettingsPage : SettingsPageBase
{
    private readonly Dictionary<string, TextBox> soundTextBoxes = new();
    private readonly Dictionary<string, Label> soundLabels = new();

    public override SettingsPageId Id => SettingsPageId.Sounds;

    internal IReadOnlyDictionary<string, TextBox> SoundTextBoxes => soundTextBoxes;

    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        SettingsBinder.ApplySounds(settings, soundTextBoxes);
    }

    public override void OnModelChanged(SettingsModelChange change)
    {
        if (change == SettingsModelChange.RouteChanged)
        {
            UpdateFinalGroupLabels();
        }
    }

    private void BuildSections(TableLayoutPanel parent)
    {
        Draft.Overlay.Sounds ??= new UiSoundSettings();

        TableLayoutPanel section = Factory.CreateSection("Sounds");
        TableLayoutPanel grid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStyleAbsolute(420f),
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(152f),
            SettingsUiFactory.ColumnStyleAbsolute(144f));

        foreach (SoundDescriptor descriptor in SettingsDescriptors.Sounds)
        {
            AddSoundRow(
                grid,
                GetSoundLabel(descriptor),
                descriptor.PrefixWithFinalGroupName,
                descriptor.Key,
                descriptor.GetValue(Draft.Overlay.Sounds));
        }

        SettingsUiFactory.AddSectionControl(section, grid);
        SettingsUiFactory.AddSection(parent, section);
    }

    private string GetSoundLabel(SoundDescriptor descriptor)
    {
        if (!descriptor.PrefixWithFinalGroupName)
        {
            return descriptor.Label;
        }

        string finalGroupName = SplitRouteGroups.Build(Draft).LastOrDefault() is RouteGroup finalGroup
            ? SplitRouteGroups.GetGroupDisplayName(finalGroup, Draft)
            : Context.Localize("Final group");
        return $"{finalGroupName}{Context.Localize(": ")}{Context.Localize(descriptor.Label)}";
    }

    private void AddSoundRow(TableLayoutPanel grid, string label, bool labelIsLocalized, string key, string value)
    {
        TextBox textBox = Factory.CreateTextBox(value);
        soundTextBoxes[key] = textBox;

        Button browseButton = Factory.CreateSmallButton("Browse");
        browseButton.Click += (_, _) => Dialogs.PickSound(textBox);

        Button clearButton = Factory.CreateSmallButton("Clear");
        clearButton.Click += (_, _) => textBox.Text = string.Empty;

        int row = Factory.AddGridRow(grid);
        Label rowLabel = labelIsLocalized
            ? Factory.CreateRawRowLabel(label)
            : Factory.CreateRowLabel(label);
        soundLabels[key] = rowLabel;
        grid.Controls.Add(rowLabel, 0, row);
        grid.Controls.Add(textBox, 1, row);
        grid.Controls.Add(browseButton, 2, row);
        grid.Controls.Add(clearButton, 3, row);
    }

    private void UpdateFinalGroupLabels()
    {
        foreach (SoundDescriptor descriptor in SettingsDescriptors.Sounds.Where(sound => sound.PrefixWithFinalGroupName))
        {
            if (soundLabels.TryGetValue(descriptor.Key, out Label? label))
            {
                label.Text = GetSoundLabel(descriptor);
            }
        }
    }
}
