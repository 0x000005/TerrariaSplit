using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class SoundSettingsPage : SettingsPageBase
{
    private readonly Dictionary<string, TextBox> soundTextBoxes = new();

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

    private void BuildSections(TableLayoutPanel parent)
    {
        Draft.Sounds ??= new UiSoundSettings();

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
                descriptor.Label,
                descriptor.Key,
                descriptor.GetValue(Draft.Sounds));
        }

        SettingsUiFactory.AddSectionControl(section, grid);
        SettingsUiFactory.AddSection(parent, section);
    }

    private void AddSoundRow(TableLayoutPanel grid, string label, string key, string value)
    {
        TextBox textBox = Factory.CreateTextBox(value);
        soundTextBoxes[key] = textBox;

        Button browseButton = Factory.CreateSmallButton("Browse");
        browseButton.Click += (_, _) => Dialogs.PickSound(textBox);

        Button clearButton = Factory.CreateSmallButton("Clear");
        clearButton.Click += (_, _) => textBox.Text = string.Empty;

        int row = Factory.AddGridRow(grid);
        grid.Controls.Add(Factory.CreateRowLabel(label), 0, row);
        grid.Controls.Add(textBox, 1, row);
        grid.Controls.Add(browseButton, 2, row);
        grid.Controls.Add(clearButton, 3, row);
    }
}
