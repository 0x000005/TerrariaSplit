using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class SettingsForm : Form
{
    private static readonly Color WindowColor = Color.FromArgb(20, 20, 23);
    private static readonly Color SectionColor = Color.FromArgb(29, 29, 34);
    private static readonly Color FieldColor = Color.FromArgb(40, 40, 47);
    private static readonly Color BorderColor = Color.FromArgb(74, 74, 84);
    private static readonly Color AccentColor = Color.FromArgb(64, 126, 201);
    private static readonly Color TextColor = Color.FromArgb(235, 235, 238);
    private static readonly Color MutedTextColor = Color.FromArgb(166, 166, 174);

    private static readonly Keys[] AllowedKeys =
    [
        Keys.A, Keys.B, Keys.C, Keys.D, Keys.E, Keys.F, Keys.G, Keys.H, Keys.I, Keys.J, Keys.K, Keys.L, Keys.M,
        Keys.N, Keys.O, Keys.P, Keys.Q, Keys.R, Keys.S, Keys.T, Keys.U, Keys.V, Keys.W, Keys.X, Keys.Y, Keys.Z,
        Keys.D0, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9,
        Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12
    ];

    private readonly AppSettings settings;
    private readonly ComboBox pauseKeyBox = new();
    private readonly ComboBox resetKeyBox = new();
    private readonly Dictionary<BossSplitName, TextBox> splitTextBoxes = new();
    private readonly Dictionary<string, TextBox> colorTextBoxes = new();

    public SettingsForm(AppSettings currentSettings)
    {
        settings = AppSettingsStore.Clone(currentSettings);

        Text = "TerrariaSplit Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        MaximizeBox = false;
        MinimumSize = new Size(560, 560);
        Size = new Size(660, 760);
        BackColor = WindowColor;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 10f, FontStyle.Regular);

        BuildLayout();
    }

    public AppSettings Result => settings;

    private void BuildLayout()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(16, 10, 16, 10),
            BackColor = WindowColor
        };

        var okButton = CreateButton("OK", accent: true);
        okButton.DialogResult = DialogResult.OK;
        okButton.Click += (_, _) => ApplyToSettings();

        var cancelButton = CreateButton("Cancel", accent: false);
        cancelButton.DialogResult = DialogResult.Cancel;

        footer.Controls.Add(okButton);
        footer.Controls.Add(cancelButton);
        Controls.Add(footer);

        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = WindowColor,
            Padding = new Padding(18, 18, 18, 4)
        };

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = WindowColor,
            Margin = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        AddHotkeySection(content);
        AddWorldRecordSection(content);
        AddColorSection(content);

        scrollPanel.Controls.Add(content);
        Controls.Add(scrollPanel);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void AddHotkeySection(TableLayoutPanel parent)
    {
        ConfigureKeyBox(pauseKeyBox, settings.PauseResumeKeys);
        ConfigureKeyBox(resetKeyBox, settings.ResetKeys);

        TableLayoutPanel section = CreateSection("Hotkeys");
        TableLayoutPanel grid = CreateGrid(2, 42f, 58f);
        AddSettingRow(grid, "Pause / Resume", pauseKeyBox);
        AddSettingRow(grid, "Reset at Menu", resetKeyBox);
        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private void AddWorldRecordSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Best Splits");
        TableLayoutPanel grid = CreateGrid(2, 42f, 58f);

        foreach (BossSplitDefinition definition in BossSplitDefinitions.All)
        {
            var textBox = CreateTextBox(settings.GetWorldRecordText(definition.Name));
            textBox.PlaceholderText = "m:ss or h:mm:ss";
            splitTextBoxes[definition.Name] = textBox;
            AddSettingRow(grid, definition.DisplayName, textBox);
        }

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private void AddColorSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Text Colors");
        TableLayoutPanel grid = CreateGrid(3, 42f, 42f, 16f);

        AddColorRow(grid, "Boss name text", nameof(settings.Colors.BossNameText), settings.Colors.BossNameText);
        AddColorRow(grid, "World record text", nameof(settings.Colors.WorldRecordText), settings.Colors.WorldRecordText);
        AddColorRow(grid, "Current split text", nameof(settings.Colors.CurrentText), settings.Colors.CurrentText);
        AddColorRow(grid, "Completed split text", nameof(settings.Colors.CompletedText), settings.Colors.CompletedText);
        AddColorRow(grid, "Skipped split text", nameof(settings.Colors.SkippedText), settings.Colors.SkippedText);
        AddColorRow(grid, "Delta ahead text", nameof(settings.Colors.DeltaAheadText), settings.Colors.DeltaAheadText);
        AddColorRow(grid, "Delta behind text", nameof(settings.Colors.DeltaBehindText), settings.Colors.DeltaBehindText);
        AddColorRow(grid, "Delta even text", nameof(settings.Colors.DeltaEvenText), settings.Colors.DeltaEvenText);
        AddColorRow(grid, "Timer text", nameof(settings.Colors.TimerText), settings.Colors.TimerText);

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private static TableLayoutPanel CreateSection(string title)
    {
        var section = new TableLayoutPanel
        {
            AutoSize = true,
            BackColor = SectionColor,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(16, 14, 16, 16)
        };
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var label = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = TextColor,
            Margin = new Padding(0, 0, 0, 10),
            Text = title
        };
        AddSectionControl(section, label);
        return section;
    }

    private static TableLayoutPanel CreateGrid(int columnCount, params float[] columnWidths)
    {
        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            BackColor = SectionColor,
            ColumnCount = columnCount,
            Dock = DockStyle.Top,
            Margin = Padding.Empty
        };

        foreach (float columnWidth in columnWidths)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, columnWidth));
        }

        return grid;
    }

    private static void AddSection(TableLayoutPanel parent, Control section)
    {
        int row = parent.RowCount++;
        parent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        parent.Controls.Add(section, 0, row);
    }

    private static void AddSectionControl(TableLayoutPanel section, Control control)
    {
        int row = section.RowCount++;
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        section.Controls.Add(control, 0, row);
    }

    private static void ConfigureKeyBox(ComboBox comboBox, Keys selected)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.BackColor = FieldColor;
        comboBox.ForeColor = TextColor;
        comboBox.Height = 34;
        comboBox.Dock = DockStyle.Fill;
        comboBox.Margin = new Padding(0, 3, 0, 3);

        foreach (Keys key in AllowedKeys)
        {
            comboBox.Items.Add(key);
        }

        comboBox.SelectedItem = AllowedKeys.Contains(selected) ? selected : Keys.R;
    }

    private static void AddSettingRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));

        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private void AddColorRow(TableLayoutPanel grid, string label, string key, string value)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));

        var textBox = CreateTextBox(value);
        colorTextBoxes[key] = textBox;

        var pickButton = CreateColorButton(textBox);
        textBox.TextChanged += (_, _) => UpdateColorButton(pickButton, textBox.Text);

        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(textBox, 1, row);
        grid.Controls.Add(pickButton, 2, row);
    }

    private static Label CreateRowLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = MutedTextColor,
            Margin = new Padding(0, 0, 12, 0),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static TextBox CreateTextBox(string value)
    {
        return new TextBox
        {
            BackColor = FieldColor,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            Margin = new Padding(0, 4, 0, 4),
            Text = value
        };
    }

    private static Button CreateButton(string text, bool accent)
    {
        var button = new Button
        {
            BackColor = accent ? AccentColor : FieldColor,
            FlatStyle = FlatStyle.Flat,
            ForeColor = TextColor,
            Height = 34,
            Margin = new Padding(8, 0, 0, 0),
            Text = text,
            UseVisualStyleBackColor = false,
            Width = 94
        };

        button.FlatAppearance.BorderColor = accent ? AccentColor : BorderColor;
        button.FlatAppearance.MouseDownBackColor = accent ? Color.FromArgb(49, 101, 166) : Color.FromArgb(52, 52, 60);
        button.FlatAppearance.MouseOverBackColor = accent ? Color.FromArgb(77, 144, 223) : Color.FromArgb(48, 48, 56);
        return button;
    }

    private Button CreateColorButton(TextBox textBox)
    {
        var button = new Button
        {
            FlatStyle = FlatStyle.Flat,
            Height = 28,
            Margin = new Padding(10, 5, 0, 5),
            Text = string.Empty,
            UseVisualStyleBackColor = false,
            Width = 42
        };

        button.FlatAppearance.BorderColor = BorderColor;
        button.Click += (_, _) => PickColor(textBox);
        UpdateColorButton(button, textBox.Text);
        return button;
    }

    private static void UpdateColorButton(Button button, string colorText)
    {
        Color color = ColorText.Parse(colorText, TextColor);
        button.BackColor = color;
        button.FlatAppearance.MouseDownBackColor = color;
        button.FlatAppearance.MouseOverBackColor = color;
    }

    private void PickColor(TextBox textBox)
    {
        using var dialog = new ColorDialog
        {
            Color = ColorText.Parse(textBox.Text, Color.White),
            FullOpen = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            textBox.Text = ColorText.Format(dialog.Color);
        }
    }

    private void ApplyToSettings()
    {
        settings.PauseResumeKey = pauseKeyBox.SelectedItem is Keys pauseKey
            ? pauseKey.ToString()
            : Keys.R.ToString();
        settings.ResetKey = resetKeyBox.SelectedItem is Keys resetKey
            ? resetKey.ToString()
            : Keys.T.ToString();

        foreach ((BossSplitName name, TextBox textBox) in splitTextBoxes)
        {
            settings.SetWorldRecordText(name, textBox.Text.Trim());
        }

        SetColor(nameof(settings.Colors.BossNameText), value => settings.Colors.BossNameText = value);
        SetColor(nameof(settings.Colors.WorldRecordText), value => settings.Colors.WorldRecordText = value);
        SetColor(nameof(settings.Colors.CurrentText), value => settings.Colors.CurrentText = value);
        SetColor(nameof(settings.Colors.CompletedText), value => settings.Colors.CompletedText = value);
        SetColor(nameof(settings.Colors.SkippedText), value => settings.Colors.SkippedText = value);
        SetColor(nameof(settings.Colors.DeltaAheadText), value => settings.Colors.DeltaAheadText = value);
        SetColor(nameof(settings.Colors.DeltaBehindText), value => settings.Colors.DeltaBehindText = value);
        SetColor(nameof(settings.Colors.DeltaEvenText), value => settings.Colors.DeltaEvenText = value);
        SetColor(nameof(settings.Colors.TimerText), value => settings.Colors.TimerText = value);
    }

    private void SetColor(string key, Action<string> setter)
    {
        if (colorTextBoxes.TryGetValue(key, out TextBox? textBox))
        {
            setter(ColorText.Format(ColorText.Parse(textBox.Text, Color.White)));
        }
    }
}
