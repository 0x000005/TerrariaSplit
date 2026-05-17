using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class SettingsForm : Form
{
    internal void AddPracticeWorldSection(TableLayoutPanel parent)
    {
        practiceSlotControls.Clear();

        TableLayoutPanel slotsSection = CreateSection("Enter World");
        AddSectionControl(
            slotsSection,
            CreateWrappedFieldLabel(
                "Do not choose players or worlds in the default save location.",
                Color.FromArgb(255, 210, 120)));
        AddSectionControl(
            slotsSection,
            CreateWrappedFieldLabel(
                "Do not choose favorite players or worlds.",
                Color.FromArgb(255, 210, 120)));

        TableLayoutPanel slotsGrid = CreateGrid(
            ColumnStyleAbsolute(48f),
            ColumnStyleAbsolute(180f),
            ColumnStylePercent(50f),
            ColumnStyleAbsolute(152f),
            ColumnStylePercent(50f),
            ColumnStyleAbsolute(152f));
        AddHeaderRow(slotsGrid, string.Empty, "Name", "Player file", string.Empty, "World file", string.Empty);

        IReadOnlyList<PracticeWorldSlot> slots = settings.PracticeWorlds.Slots;
        for (int index = 0; index < PracticeWorldSettings.SlotCount; index++)
        {
            PracticeWorldSlot slot = index < slots.Count ? slots[index] : new PracticeWorldSlot();
            AddPracticeWorldSlotRow(slotsGrid, index, slot);
        }

        AddSectionControl(slotsSection, slotsGrid);
        AddSection(parent, slotsSection);
    }

    private void AddPracticeWorldSlotRow(TableLayoutPanel grid, int index, PracticeWorldSlot slot)
    {
        TextBox nameBox = CreateTextBox(slot.Name);
        TextBox playerPathBox = CreateTextBox(slot.PlayerFilePath);
        TextBox worldPathBox = CreateTextBox(slot.WorldFilePath);

        Button playerBrowseButton = CreatePracticeBrowseButton(
            "Choose player file",
            "Terraria player|*.plr|All files|*.*",
            playerPathBox);
        Button worldBrowseButton = CreatePracticeBrowseButton(
            "Choose world file",
            "Terraria world|*.wld|All files|*.*",
            worldPathBox);

        int row = AddPracticeSlotGridRow(grid);
        grid.Controls.Add(CreatePracticeSlotKeyLabel(index), 0, row);
        grid.Controls.Add(nameBox, 1, row);
        grid.Controls.Add(playerPathBox, 2, row);
        grid.Controls.Add(playerBrowseButton, 3, row);
        grid.Controls.Add(worldPathBox, 4, row);
        grid.Controls.Add(worldBrowseButton, 5, row);

        practiceSlotControls.Add(new PracticeSlotControls(nameBox, playerPathBox, worldPathBox));
    }

    private Button CreatePracticeBrowseButton(string title, string filter, TextBox target)
    {
        Button button = CreateSmallButton("Browse");
        button.Click += (_, _) => PickPracticeSaveFile(target, title, filter);
        return button;
    }

    private static int AddPracticeSlotGridRow(TableLayoutPanel grid)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 64f));
        return row;
    }

    private Label CreatePracticeSlotKeyLabel(int index)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            Font = UiTheme.FormFont(10f, FontStyle.Bold),
            Margin = new Padding(0, 0, 10, 0),
            Text = GetPracticeSlotKeyText(index),
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private void PickPracticeSaveFile(TextBox target, string title, string filter)
    {
        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = filter,
            Title = Localizer.Get(title, settings)
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.FileName;
        }
    }

    private static string GetPracticeSlotKeyText(int index)
    {
        return index == 9 ? "0" : (index + 1).ToString(CultureInfo.InvariantCulture);
    }
}
