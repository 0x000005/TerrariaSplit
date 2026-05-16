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
        AddSettingRow(uiScaleGrid, "Enable UI scale enhancement", enableTerrariaUiScalePatchBox);
        AddSectionControl(uiScaleSection, uiScaleGrid);
        AddSectionControl(
            uiScaleSection,
            CreateWrappedFieldLabel(
                "Raises Terraria's in-game UI scale slider limit from 200% to 300% by patching the running Terraria process memory.",
                TextColor));
        AddSectionControl(
            uiScaleSection,
            CreateWrappedFieldLabel(
                "Warning: restart Terraria after enabling if the options menu was already opened.",
                Color.FromArgb(255, 210, 120)));
        AddSectionControl(
            uiScaleSection,
            CreateWrappedFieldLabel(
                "Modifying memory is risky; enable with caution.",
                MutedTextColor));
        AddSection(parent, uiScaleSection);
    }
}
