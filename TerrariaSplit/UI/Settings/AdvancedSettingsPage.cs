using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class AdvancedSettingsPage : SettingsPageBase
{
    private readonly CheckBox enableTerrariaUiScalePatchBox = new();

    public override SettingsPageId Id => SettingsPageId.Advanced;

    internal CheckBox EnableTerrariaUiScalePatchBox => enableTerrariaUiScalePatchBox;

    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        settings.Advanced ??= new AdvancedSettings();
        settings.Advanced.EnableTerrariaUiScalePatch = enableTerrariaUiScalePatchBox.Checked;
    }

    private void BuildSections(TableLayoutPanel parent)
    {
        ConfigureCheckBox(enableTerrariaUiScalePatchBox, Draft.Advanced?.EnableTerrariaUiScalePatch == true);

        TableLayoutPanel uiScaleSection = Factory.CreateSection("Terraria UI scale enhancement");
        TableLayoutPanel uiScaleGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(uiScaleGrid, "Enabled", enableTerrariaUiScalePatchBox);
        SettingsUiFactory.AddSectionControl(uiScaleSection, uiScaleGrid);
        SettingsUiFactory.AddSectionControl(
            uiScaleSection,
            Factory.CreateWrappedFieldLabel(
                "Raises Terraria's in-game UI scale slider limit from 200% to 300%.",
                UiTheme.MutedText));
        SettingsUiFactory.AddSectionControl(
            uiScaleSection,
            Factory.CreateWrappedFieldLabel(
                "If Terraria's options menu was already opened before enabling, restart Terraria for the change to take effect.",
                Color.FromArgb(255, 210, 120)));
        SettingsUiFactory.AddSectionControl(
            uiScaleSection,
            Factory.CreateWrappedFieldLabel(
                "This changes the running Terraria process memory; enable with caution.",
                Color.FromArgb(255, 210, 120)));
        SettingsUiFactory.AddSection(parent, uiScaleSection);
    }

    private static void ConfigureCheckBox(CheckBox checkBox, bool selected)
    {
        checkBox.Checked = selected;
        checkBox.Dock = DockStyle.Fill;
        UiTheme.StyleCheckBox(checkBox);
    }
}
