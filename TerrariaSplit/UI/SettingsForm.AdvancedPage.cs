using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class SettingsForm : Form
{
    internal void AddAdvancedSection(TableLayoutPanel parent)
    {
        ConfigureCheckBox(enableTerrariaUiScalePatchBox, settings.Advanced?.EnableTerrariaUiScalePatch == true);

        TableLayoutPanel uiScaleSection = CreateSection("Terraria UI scale enhancement");
        TableLayoutPanel uiScaleGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(uiScaleGrid, "Enabled", enableTerrariaUiScalePatchBox);
        AddSectionControl(uiScaleSection, uiScaleGrid);
        AddSectionControl(
            uiScaleSection,
            CreateWrappedFieldLabel(
                "Raises Terraria's in-game UI scale slider limit from 200% to 300%.",
                MutedTextColor));
        AddSectionControl(
            uiScaleSection,
            CreateWrappedFieldLabel(
                "If Terraria's options menu was already opened before enabling, restart Terraria for the change to take effect.",
                Color.FromArgb(255, 210, 120)));
        AddSectionControl(
            uiScaleSection,
            CreateWrappedFieldLabel(
                "This changes the running Terraria process memory; enable with caution.",
                Color.FromArgb(255, 210, 120)));
        AddSection(parent, uiScaleSection);
    }
}
