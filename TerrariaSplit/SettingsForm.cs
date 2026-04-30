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

    private readonly AppSettings settings;
    private readonly HotkeyTextBox pauseKeyBox = new();
    private readonly HotkeyTextBox resetKeyBox = new();
    private readonly HotkeyTextBox mouseClickThroughKeyBox = new();
    private readonly CheckBox alwaysOnTopBox = new();
    private readonly CheckBox practiceModeBox = new();
    private readonly ComboBox referenceSetBox = new();
    private readonly TextBox newReferenceSetNameBox = new();
    private readonly NumericUpDown undefeatedIconGrayscaleBox = new();
    private readonly NumericUpDown undefeatedIconBrightnessBox = new();
    private readonly Dictionary<string, RouteControls> routeControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> bossIconTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> splitTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> colorTextBoxes = new();
    private readonly Dictionary<string, ColumnControls> columnControls = new();
    private readonly Dictionary<string, FontControls> fontControls = new();
    private bool updatingReferenceSetSelection;

    public SettingsForm(AppSettings currentSettings)
    {
        settings = AppSettingsStore.Clone(currentSettings);

        Text = "TerrariaSplit Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(900, 700);
        ClientSize = new Size(1060, 820);
        BackColor = WindowColor;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 10f, FontStyle.Regular);

        BuildLayout();
    }

    public AppSettings Result => settings;

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowColor,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66f));
        Controls.Add(root);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(16, 12, 16, 12),
            BackColor = WindowColor
        };

        var okButton = CreateButton("OK", accent: true);
        okButton.DialogResult = DialogResult.OK;
        okButton.Click += (_, _) => ApplyToSettings();

        var cancelButton = CreateButton("Cancel", accent: false);
        cancelButton.DialogResult = DialogResult.Cancel;

        footer.Controls.Add(okButton);
        footer.Controls.Add(cancelButton);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            BackColor = WindowColor,
            Padding = new Point(12, 6)
        };
        tabs.TabPages.Add(CreateGeneralPage());
        tabs.TabPages.Add(CreateSplitsPage());
        tabs.TabPages.Add(CreateUiPage());
        tabs.TabPages.Add(CreateColorPage());
        root.Controls.Add(tabs, 0, 0);
        root.Controls.Add(footer, 0, 1);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private TabPage CreateGeneralPage()
    {
        TabPage page = CreateTabPage("General");
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

        var scrollPanel = CreateScrollPanel();
        scrollPanel.Controls.Add(content);
        page.Controls.Add(scrollPanel);
        return page;
    }

    private TabPage CreateSplitsPage()
    {
        TabPage page = CreateTabPage("Splits");
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = WindowColor,
            Margin = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        AddRouteSection(content);
        AddBossIconSection(content);
        AddReferenceDataSection(content);

        var scrollPanel = CreateScrollPanel();
        scrollPanel.Controls.Add(content);
        page.Controls.Add(scrollPanel);
        return page;
    }

    private TabPage CreateUiPage()
    {
        TabPage page = CreateTabPage("UI");
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = WindowColor,
            Margin = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        AddColumnSettingsSection(content);
        AddTimerSettingsSection(content);
        AddIconStyleSection(content);

        var scrollPanel = CreateScrollPanel();
        scrollPanel.Controls.Add(content);
        page.Controls.Add(scrollPanel);
        return page;
    }

    private TabPage CreateColorPage()
    {
        TabPage page = CreateTabPage("Colors");
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = WindowColor,
            Margin = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        AddColorSection(content);

        var scrollPanel = CreateScrollPanel();
        scrollPanel.Controls.Add(content);
        page.Controls.Add(scrollPanel);
        return page;
    }

    private void AddHotkeySection(TableLayoutPanel parent)
    {
        ConfigureKeyBox(pauseKeyBox, settings.PauseResumeKeys);
        ConfigureKeyBox(resetKeyBox, settings.ResetKeys);
        ConfigureKeyBox(mouseClickThroughKeyBox, settings.MouseClickThroughKeys);

        TableLayoutPanel section = CreateSection("Hotkeys");
        TableLayoutPanel grid = CreateGrid(2, 42f, 58f);
        AddSettingRow(grid, "Pause / Resume", pauseKeyBox);
        AddSettingRow(grid, "Reset at Menu", resetKeyBox);
        AddSettingRow(grid, "Mouse passthrough", mouseClickThroughKeyBox);
        ConfigureCheckBox(alwaysOnTopBox, settings.AlwaysOnTop);
        AddSettingRow(grid, "Always on top", alwaysOnTopBox);
        ConfigureCheckBox(practiceModeBox, settings.PracticeMode);
        AddSettingRow(grid, "Practice mode", practiceModeBox);
        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private void AddColumnSettingsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Columns");
        TableLayoutPanel grid = CreateGrid(5, 32f, 14f, 20f, 20f, 14f);

        AddColumnSettingsHeader(grid);
        AddColumnSettingsRow(grid, "Icon", "Icon", settings.Columns.Icon);
        AddColumnSettingsRow(grid, "Time", "Time", settings.Columns.Time);
        AddColumnSettingsRow(grid, "Delta", "Delta", settings.Columns.Delta);

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private void AddTimerSettingsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Timer");
        TableLayoutPanel grid = CreateGrid(4, 42f, 18f, 24f, 16f);

        int headerRow = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
        grid.Controls.Add(CreateRowLabel("Part"), 0, headerRow);
        grid.Controls.Add(CreateRowLabel("Show"), 1, headerRow);
        grid.Controls.Add(CreateRowLabel("Font"), 2, headerRow);
        grid.Controls.Add(CreateRowLabel("Bold"), 3, headerRow);

        AddFontSettingsRow(grid, "Main time", "Timer", settings.Columns.Timer);
        AddFontSettingsRow(grid, "Milliseconds", "TimerMilliseconds", settings.Columns.TimerMilliseconds);

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private void AddFontSettingsRow(TableLayoutPanel grid, string label, string key, UiColumnSettings value)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));

        var showBox = new CheckBox
        {
            Checked = value.Show,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            ForeColor = TextColor,
            Margin = new Padding(0, 6, 0, 6),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var fontBox = CreateDecimalBox(value.FontSize, 6, 96, 1);
        var boldBox = new CheckBox
        {
            Checked = value.Bold,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            ForeColor = TextColor,
            Margin = new Padding(0, 6, 0, 6),
            TextAlign = ContentAlignment.MiddleCenter
        };

        fontControls[key] = new FontControls(showBox, fontBox, boldBox);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(showBox, 1, row);
        grid.Controls.Add(fontBox, 2, row);
        grid.Controls.Add(boldBox, 3, row);
    }

    private static void AddColumnSettingsHeader(TableLayoutPanel grid)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
        grid.Controls.Add(CreateRowLabel("Column"), 0, row);
        grid.Controls.Add(CreateRowLabel("Show"), 1, row);
        grid.Controls.Add(CreateRowLabel("Width"), 2, row);
        grid.Controls.Add(CreateRowLabel("Font"), 3, row);
        grid.Controls.Add(CreateRowLabel("Bold"), 4, row);
    }

    private void AddColumnSettingsRow(TableLayoutPanel grid, string label, string key, UiColumnSettings value)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));

        var showBox = new CheckBox
        {
            Checked = value.Show,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            ForeColor = TextColor,
            Margin = new Padding(0, 6, 0, 6),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var widthBox = CreateNumberBox(value.Width, 1, 1000, 5);
        var fontBox = CreateDecimalBox(value.FontSize, 6, 96, 1);
        var boldBox = new CheckBox
        {
            Checked = value.Bold,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            ForeColor = TextColor,
            Margin = new Padding(0, 6, 0, 6),
            TextAlign = ContentAlignment.MiddleCenter
        };

        columnControls[key] = new ColumnControls(showBox, widthBox, fontBox, boldBox);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(showBox, 1, row);
        grid.Controls.Add(widthBox, 2, row);
        grid.Controls.Add(fontBox, 3, row);
        grid.Controls.Add(boldBox, 4, row);
    }

    private void AddIconStyleSection(TableLayoutPanel parent)
    {
        ConfigurePercentBox(undefeatedIconGrayscaleBox, settings.UndefeatedIconGrayscalePercent);
        ConfigurePercentBox(undefeatedIconBrightnessBox, settings.UndefeatedIconBrightnessPercent);

        TableLayoutPanel section = CreateSection("Icon Style");
        TableLayoutPanel grid = CreateGrid(2, 42f, 58f);
        AddSettingRow(grid, "Unlit grayscale %", undefeatedIconGrayscaleBox);
        AddSettingRow(grid, "Unlit brightness %", undefeatedIconBrightnessBox);
        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private void AddBossIconSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Boss Icons");
        TableLayoutPanel grid = CreateGrid(3, 30f, 56f, 14f);

        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            var textBox = CreateTextBox(settings.GetBossIconPath(unit.Id));
            textBox.PlaceholderText = "empty = bundled icon";
            bossIconTextBoxes[unit.Id] = textBox;

            Button browseButton = CreateButton("Browse", accent: false);
            browseButton.Width = 76;
            browseButton.Margin = new Padding(8, 3, 0, 3);
            browseButton.Click += (_, _) => PickBossIcon(textBox);

            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            grid.Controls.Add(CreateRowLabel(unit.DisplayName), 0, row);
            grid.Controls.Add(textBox, 1, row);
            grid.Controls.Add(browseButton, 2, row);
        }

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private void AddReferenceDataSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Reference Data");

        TableLayoutPanel selectorGrid = CreateGrid(4, 20f, 38f, 22f, 20f);
        ConfigureReferenceSetBox();
        newReferenceSetNameBox.PlaceholderText = "new group name";
        newReferenceSetNameBox.BackColor = FieldColor;
        newReferenceSetNameBox.BorderStyle = BorderStyle.FixedSingle;
        newReferenceSetNameBox.Dock = DockStyle.Fill;
        newReferenceSetNameBox.ForeColor = TextColor;
        newReferenceSetNameBox.Margin = new Padding(0, 4, 8, 4);

        Button addButton = CreateButton("Add", accent: false);
        addButton.Width = 80;
        addButton.Click += (_, _) => AddReferenceSet();

        Button deleteButton = CreateButton("Delete", accent: false);
        deleteButton.Width = 80;
        deleteButton.Click += (_, _) => DeleteReferenceSet();

        int selectorRow = selectorGrid.RowCount++;
        selectorGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
        selectorGrid.Controls.Add(CreateRowLabel("Active group"), 0, selectorRow);
        selectorGrid.Controls.Add(referenceSetBox, 1, selectorRow);
        selectorGrid.Controls.Add(newReferenceSetNameBox, 2, selectorRow);
        selectorGrid.Controls.Add(CreateButtonPanel(addButton, deleteButton), 3, selectorRow);
        AddSectionControl(section, selectorGrid);

        TableLayoutPanel grid = CreateGrid(2, 42f, 58f);

        foreach (BossSplitDefinition definition in BossSplitDefinitions.Build(settings))
        {
            var textBox = CreateTextBox(settings.GetReferenceText(definition.Name));
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

        AddColorRow(grid, "Reference text", nameof(settings.Colors.ReferenceText), settings.Colors.ReferenceText);
        AddColorRow(grid, "Active reference text", nameof(settings.Colors.ActiveReferenceText), settings.Colors.ActiveReferenceText);
        AddColorRow(grid, "Completed split text", nameof(settings.Colors.SplitText), settings.Colors.SplitText);
        AddColorRow(grid, "Delta ahead text", nameof(settings.Colors.DeltaAheadText), settings.Colors.DeltaAheadText);
        AddColorRow(grid, "Delta behind text", nameof(settings.Colors.DeltaBehindText), settings.Colors.DeltaBehindText);
        AddColorRow(grid, "Delta even text", nameof(settings.Colors.DeltaEvenText), settings.Colors.DeltaEvenText);
        AddColorRow(grid, "Timer text", nameof(settings.Colors.TimerText), settings.Colors.TimerText);
        AddColorRow(grid, "Timer ahead text", nameof(settings.Colors.TimerAheadText), settings.Colors.TimerAheadText);
        AddColorRow(grid, "Timer behind text", nameof(settings.Colors.TimerBehindText), settings.Colors.TimerBehindText);
        AddColorRow(grid, "Timer record text", nameof(settings.Colors.TimerRecordText), settings.Colors.TimerRecordText);

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private static TableLayoutPanel CreateSection(string title)
    {
        var section = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SectionColor,
            ColumnCount = 1,
            Dock = DockStyle.Top,
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
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
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

    private static TabPage CreateTabPage(string text)
    {
        return new TabPage
        {
            BackColor = WindowColor,
            ForeColor = TextColor,
            Text = text
        };
    }

    private static Panel CreateScrollPanel()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = WindowColor,
            Padding = new Padding(18, 18, 18, 4)
        };
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

    private static void ConfigureKeyBox(HotkeyTextBox textBox, Keys selected)
    {
        textBox.BackColor = FieldColor;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Dock = DockStyle.Fill;
        textBox.ForeColor = TextColor;
        textBox.Margin = new Padding(0, 4, 0, 4);
        textBox.ReadOnly = true;
        textBox.SetHotkey(selected);
    }

    private static void ConfigureCheckBox(CheckBox checkBox, bool selected)
    {
        checkBox.Checked = selected;
        checkBox.Dock = DockStyle.Fill;
        checkBox.FlatStyle = FlatStyle.Flat;
        checkBox.ForeColor = TextColor;
        checkBox.Margin = new Padding(0, 6, 0, 6);
    }

    private void ConfigureReferenceSetBox()
    {
        referenceSetBox.DropDownStyle = ComboBoxStyle.DropDownList;
        referenceSetBox.FlatStyle = FlatStyle.Flat;
        referenceSetBox.BackColor = FieldColor;
        referenceSetBox.ForeColor = TextColor;
        referenceSetBox.Dock = DockStyle.Fill;
        referenceSetBox.Margin = new Padding(0, 4, 8, 4);

        foreach (ReferenceSplitSet set in settings.ReferenceSplitSets)
        {
            referenceSetBox.Items.Add(set.Name);
        }

        referenceSetBox.SelectedItem = settings.GetActiveReferenceSet().Name;
        referenceSetBox.SelectedIndexChanged += (_, _) => SwitchReferenceSet();
    }

    private static void ConfigurePercentBox(NumericUpDown numericBox, int selected)
    {
        numericBox.BackColor = FieldColor;
        numericBox.BorderStyle = BorderStyle.FixedSingle;
        numericBox.DecimalPlaces = 0;
        numericBox.Dock = DockStyle.Fill;
        numericBox.ForeColor = TextColor;
        numericBox.Increment = 5;
        numericBox.Margin = new Padding(0, 4, 0, 4);
        numericBox.Maximum = 100;
        numericBox.Minimum = 0;
        numericBox.TextAlign = HorizontalAlignment.Right;
        numericBox.Value = Math.Clamp(selected, 0, 100);
    }

    private static NumericUpDown CreateNumberBox(int value, int minimum, int maximum, int increment)
    {
        var numericBox = new NumericUpDown();
        ConfigureNumericBox(numericBox, value, minimum, maximum, increment, 0);
        return numericBox;
    }

    private static NumericUpDown CreateDecimalBox(float value, decimal minimum, decimal maximum, decimal increment)
    {
        var numericBox = new NumericUpDown();
        ConfigureNumericBox(numericBox, (decimal)value, minimum, maximum, increment, 1);
        return numericBox;
    }

    private static void ConfigureNumericBox(
        NumericUpDown numericBox,
        decimal value,
        decimal minimum,
        decimal maximum,
        decimal increment,
        int decimalPlaces)
    {
        numericBox.BackColor = FieldColor;
        numericBox.BorderStyle = BorderStyle.FixedSingle;
        numericBox.DecimalPlaces = decimalPlaces;
        numericBox.Dock = DockStyle.Fill;
        numericBox.ForeColor = TextColor;
        numericBox.Increment = increment;
        numericBox.Margin = new Padding(0, 4, 8, 4);
        numericBox.Maximum = maximum;
        numericBox.Minimum = minimum;
        numericBox.TextAlign = HorizontalAlignment.Right;
        numericBox.Value = Math.Clamp(value, minimum, maximum);
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

    private static FlowLayoutPanel CreateButtonPanel(params Button[] buttons)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            WrapContents = false
        };

        foreach (Button button in buttons)
        {
            button.Margin = new Padding(0, 3, 8, 3);
            panel.Controls.Add(button);
        }

        return panel;
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

    private void PickBossIcon(TextBox textBox)
    {
        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
            Title = "Choose Boss Icon"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            textBox.Text = dialog.FileName;
        }
    }

    private void SwitchReferenceSet()
    {
        if (updatingReferenceSetSelection)
        {
            return;
        }

        SaveReferenceTextBoxes();
        if (referenceSetBox.SelectedItem is string selectedName)
        {
            settings.ActiveReferenceSplitSet = selectedName;
        }

        LoadReferenceTextBoxes();
    }

    private void AddReferenceSet()
    {
        SaveReferenceTextBoxes();
        string name = newReferenceSetNameBox.Text.Trim();
        if (name.Length == 0 ||
            settings.ReferenceSplitSets.Any(set => string.Equals(set.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        settings.ReferenceSplitSets.Add(AppSettings.CreateReferenceSet(name));
        referenceSetBox.Items.Add(name);
        referenceSetBox.SelectedItem = name;
        newReferenceSetNameBox.Clear();
    }

    private void DeleteReferenceSet()
    {
        if (settings.ReferenceSplitSets.Count <= 1 ||
            referenceSetBox.SelectedItem is not string selectedName)
        {
            return;
        }

        ReferenceSplitSet? selectedSet = settings.ReferenceSplitSets.FirstOrDefault(
            set => string.Equals(set.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        if (selectedSet is null)
        {
            return;
        }

        settings.ReferenceSplitSets.Remove(selectedSet);
        updatingReferenceSetSelection = true;
        referenceSetBox.Items.Remove(selectedName);
        settings.ActiveReferenceSplitSet = settings.ReferenceSplitSets[0].Name;
        referenceSetBox.SelectedItem = settings.ActiveReferenceSplitSet;
        updatingReferenceSetSelection = false;
        LoadReferenceTextBoxes();
    }

    private void SaveReferenceTextBoxes()
    {
        ReferenceSplitSet activeSet = settings.GetActiveReferenceSet();
        foreach ((string name, TextBox textBox) in splitTextBoxes)
        {
            activeSet.Splits[name] = textBox.Text.Trim();
        }
    }

    private void LoadReferenceTextBoxes()
    {
        ReferenceSplitSet activeSet = settings.GetActiveReferenceSet();
        foreach ((string name, TextBox textBox) in splitTextBoxes)
        {
            textBox.Text = activeSet.Splits.TryGetValue(name, out string? value)
                ? value
                : string.Empty;
        }
    }

    private void ApplyToSettings()
    {
        settings.PauseResumeKey = pauseKeyBox.Hotkey.ToString();
        settings.ResetKey = resetKeyBox.Hotkey.ToString();
        settings.MouseClickThroughKey = mouseClickThroughKeyBox.Hotkey.ToString();
        settings.AlwaysOnTop = alwaysOnTopBox.Checked;
        settings.PracticeMode = practiceModeBox.Checked;
        ApplyRouteSettings();
        AppSettingsStore.Normalize(settings);

        SaveReferenceTextBoxes();
        settings.ActiveReferenceSplitSet = referenceSetBox.SelectedItem is string selectedReferenceSet
            ? selectedReferenceSet
            : settings.GetActiveReferenceSet().Name;

        foreach ((string name, TextBox textBox) in bossIconTextBoxes)
        {
            settings.SetBossIconPath(name, textBox.Text.Trim());
        }

        ApplyColumnSettings("Icon", settings.Columns.Icon);
        ApplyColumnSettings("Time", settings.Columns.Time);
        ApplyColumnSettings("Delta", settings.Columns.Delta);
        ApplyFontSettings("Timer", settings.Columns.Timer);
        ApplyFontSettings("TimerMilliseconds", settings.Columns.TimerMilliseconds);

        settings.UndefeatedIconGrayscalePercent = (int)undefeatedIconGrayscaleBox.Value;
        settings.UndefeatedIconBrightnessPercent = (int)undefeatedIconBrightnessBox.Value;

        SetColor(nameof(settings.Colors.ReferenceText), value => settings.Colors.ReferenceText = value);
        SetColor(nameof(settings.Colors.ActiveReferenceText), value => settings.Colors.ActiveReferenceText = value);
        SetColor(nameof(settings.Colors.SplitText), value => settings.Colors.SplitText = value);
        SetColor(nameof(settings.Colors.DeltaAheadText), value => settings.Colors.DeltaAheadText = value);
        SetColor(nameof(settings.Colors.DeltaBehindText), value => settings.Colors.DeltaBehindText = value);
        SetColor(nameof(settings.Colors.DeltaEvenText), value => settings.Colors.DeltaEvenText = value);
        SetColor(nameof(settings.Colors.TimerText), value => settings.Colors.TimerText = value);
        SetColor(nameof(settings.Colors.TimerAheadText), value => settings.Colors.TimerAheadText = value);
        SetColor(nameof(settings.Colors.TimerBehindText), value => settings.Colors.TimerBehindText = value);
        SetColor(nameof(settings.Colors.TimerRecordText), value => settings.Colors.TimerRecordText = value);
    }

    private void ApplyRouteSettings()
    {
        var route = new List<BossRouteEntry>();
        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            if (!routeControls.TryGetValue(unit.Id, out RouteControls? controls))
            {
                continue;
            }

            route.Add(new BossRouteEntry
            {
                BossId = unit.Id,
                Enabled = controls.Enabled.Checked,
                Segment = (int)controls.Segment.Value
            });
        }

        settings.Route = route;
    }

    private void SetColor(string key, Action<string> setter)
    {
        if (colorTextBoxes.TryGetValue(key, out TextBox? textBox))
        {
            setter(ColorText.Format(ColorText.Parse(textBox.Text, Color.White)));
        }
    }

    private void ApplyColumnSettings(string key, UiColumnSettings target)
    {
        if (!columnControls.TryGetValue(key, out ColumnControls? controls))
        {
            return;
        }

        target.Show = controls.Show.Checked;
        target.Width = (int)controls.Width.Value;
        target.FontSize = (float)controls.FontSize.Value;
        target.Bold = controls.Bold.Checked;
    }

    private void ApplyFontSettings(string key, UiColumnSettings target)
    {
        if (!fontControls.TryGetValue(key, out FontControls? controls))
        {
            return;
        }

        target.Show = controls.Show.Checked;
        target.FontSize = (float)controls.FontSize.Value;
        target.Bold = controls.Bold.Checked;
    }

    private sealed record ColumnControls(CheckBox Show, NumericUpDown Width, NumericUpDown FontSize, CheckBox Bold);

    private sealed record FontControls(CheckBox Show, NumericUpDown FontSize, CheckBox Bold);

    private sealed record RouteControls(CheckBox Enabled, NumericUpDown Segment);

    private sealed class HotkeyTextBox : TextBox
    {
        public Keys Hotkey { get; private set; } = Keys.None;

        public void SetHotkey(Keys hotkey)
        {
            Hotkey = hotkey == Keys.None ? Keys.I : hotkey;
            Text = Hotkey.ToString();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            Keys key = e.KeyCode;
            if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu)
            {
                key = e.KeyData & Keys.KeyCode;
            }

            if (key != Keys.None)
            {
                SetHotkey(key);
            }

            e.SuppressKeyPress = true;
        }
    }

    private void AddRouteSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Boss Route");
        TableLayoutPanel grid = CreateGrid(3, 54f, 18f, 28f);

        int headerRow = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
        grid.Controls.Add(CreateRowLabel("Boss"), 0, headerRow);
        grid.Controls.Add(CreateRowLabel("Enabled"), 1, headerRow);
        grid.Controls.Add(CreateRowLabel("Segment"), 2, headerRow);

        IReadOnlyDictionary<string, BossRouteEntry> route = settings.Route.ToDictionary(
            entry => entry.BossId,
            StringComparer.OrdinalIgnoreCase);
        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            BossRouteEntry entry = route.TryGetValue(unit.Id, out BossRouteEntry? existing)
                ? existing
                : new BossRouteEntry { BossId = unit.Id };

            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));

            var enabledBox = new CheckBox
            {
                Checked = entry.Enabled,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                ForeColor = TextColor,
                Margin = new Padding(0, 6, 0, 6),
                TextAlign = ContentAlignment.MiddleCenter
            };
            NumericUpDown segmentBox = CreateNumberBox(Math.Clamp(entry.Segment, 1, 99), 1, 99, 1);

            routeControls[unit.Id] = new RouteControls(enabledBox, segmentBox);
            grid.Controls.Add(CreateRowLabel(unit.DisplayName), 0, row);
            grid.Controls.Add(enabledBox, 1, row);
            grid.Controls.Add(segmentBox, 2, row);
        }

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }
}
