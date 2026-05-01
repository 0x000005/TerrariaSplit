using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class SettingsForm : Form
{
    private static readonly Color WindowColor = UiTheme.Window;
    private static readonly Color SectionColor = UiTheme.Surface;
    private static readonly Color FieldColor = UiTheme.Field;
    private static readonly Color BorderColor = UiTheme.Border;
    private static readonly Color TextColor = UiTheme.Text;
    private static readonly Color MutedTextColor = UiTheme.MutedText;

    private readonly AppSettings settings;
    private readonly HotkeyTextBox pauseKeyBox = new();
    private readonly HotkeyTextBox resetKeyBox = new();
    private readonly HotkeyTextBox mouseClickThroughKeyBox = new();
    private readonly ComboBox languageBox = new();
    private readonly CheckBox alwaysOnTopBox = new();
    private readonly CheckBox practiceModeBox = new();
    private readonly ComboBox referenceSetBox = new();
    private readonly TextBox newReferenceSetNameBox = new();
    private readonly NumericUpDown undefeatedIconGrayscaleBox = new();
    private readonly NumericUpDown undefeatedIconBrightnessBox = new();
    private readonly Dictionary<string, RouteControls> routeControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> bossIconTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> splitTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> personalBestTimeTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> personalBestSegmentTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> colorTextBoxes = new();
    private readonly Dictionary<string, ColumnControls> columnControls = new();
    private readonly Dictionary<string, FontControls> fontControls = new();
    private readonly NumericUpDown timerOffsetXBox = new();
    private readonly NumericUpDown timerOffsetYBox = new();
    private TableLayoutPanel? personalBestTimeGrid;
    private TableLayoutPanel? personalBestSegmentGrid;
    private bool updatingReferenceSetSelection;

    public SettingsForm(AppSettings currentSettings)
    {
        settings = AppSettingsStore.Clone(currentSettings);

        Text = Localizer.Get("TerrariaSplit Settings", settings);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(1100, 840);
        UiTheme.ConfigureForm(this, new Size(940, 720));

        BuildLayout();
    }

    public AppSettings Result => settings;

    public event EventHandler? Applied;

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
            Padding = new Padding(18, 13, 18, 13),
            BackColor = WindowColor
        };
        UiTheme.EnableDoubleBuffering(footer);

        var okButton = CreateButton("OK", accent: true);
        okButton.DialogResult = DialogResult.OK;
        okButton.Click += (_, _) => ApplyToSettings();

        var applyButton = CreateButton("Apply", accent: false);
        applyButton.Click += (_, _) => ApplyAndNotify();

        var cancelButton = CreateButton("Cancel", accent: false);
        cancelButton.DialogResult = DialogResult.Cancel;

        footer.Controls.Add(okButton);
        footer.Controls.Add(applyButton);
        footer.Controls.Add(cancelButton);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            BackColor = WindowColor,
            Padding = new Point(16, 8)
        };
        UiTheme.EnableDoubleBuffering(tabs);
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
        AddPersonalBestDataSection(content);

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

        UiTheme.StyleComboBox(languageBox);
        languageBox.Dock = DockStyle.Fill;
        languageBox.Items.Add("English");
        languageBox.Items.Add("中文");
        languageBox.SelectedItem = settings.Language is "中文" ? "中文" : "English";

        TableLayoutPanel section = CreateSection("General Options");
        TableLayoutPanel grid = CreateGrid(2, 48f, 52f);
        AddSettingRow(grid, "Language", languageBox);
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

    private void AddPersonalBestDataSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Personal Best Data");

        personalBestTimeGrid = CreateGrid(2, 48f, 52f);
        PopulatePersonalBestTimeGrid();
        AddSectionControl(section, CreateSubsectionLabel("Personal best"));
        AddSectionControl(section, personalBestTimeGrid);

        personalBestSegmentGrid = CreateGrid(2, 48f, 52f);
        PopulatePersonalBestSegmentGrid();
        AddSectionControl(section, CreateSubsectionLabel("Personal best segment"));
        AddSectionControl(section, personalBestSegmentGrid);
        AddSection(parent, section);
    }

    private void PopulatePersonalBestTimeGrid()
    {
        if (personalBestTimeGrid is null)
        {
            return;
        }

        ClearGrid(personalBestTimeGrid);
        personalBestTimeTextBoxes.Clear();
        foreach (BossRouteEntry entry in GetRouteOrderedEntries())
        {
            if (!BossSplitDefinitions.TryGetUnit(entry.BossId, out BossUnitDefinition unit))
            {
                continue;
            }

            var textBox = CreateTextBox(settings.GetPersonalBestTimeText(unit.Id));
            textBox.PlaceholderText = "m:ss or h:mm:ss";
            personalBestTimeTextBoxes[unit.Id] = textBox;
            AddSettingRow(personalBestTimeGrid, Localizer.Get(unit.DisplayName, settings), textBox);
        }
    }

    private void PopulatePersonalBestSegmentGrid()
    {
        if (personalBestSegmentGrid is null)
        {
            return;
        }

        ClearGrid(personalBestSegmentGrid);
        personalBestSegmentTextBoxes.Clear();
        foreach (RouteGroup group in BossRouteGroups.Build(settings))
        {
            var textBox = CreateTextBox(settings.GetPersonalBestSegmentText(group.Key));
            textBox.PlaceholderText = "m:ss or h:mm:ss";
            personalBestSegmentTextBoxes[group.Key] = textBox;
            AddSettingRow(personalBestSegmentGrid, BossRouteGroups.GetGroupDisplayName(group, settings), textBox);
        }
    }

    private static void ClearGrid(TableLayoutPanel grid)
    {
        foreach (Control control in grid.Controls.Cast<Control>().ToArray())
        {
            control.Dispose();
        }

        grid.Controls.Clear();
        grid.RowStyles.Clear();
        grid.RowCount = 0;
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
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
        grid.Controls.Add(CreateRowLabel("Part"), 0, headerRow);
        grid.Controls.Add(CreateRowLabel("Show"), 1, headerRow);
        grid.Controls.Add(CreateRowLabel("Font"), 2, headerRow);
        grid.Controls.Add(CreateRowLabel("Bold"), 3, headerRow);

        AddFontSettingsRow(grid, "Main time", "Timer", settings.Columns.Timer);
        AddFontSettingsRow(grid, "Milliseconds", "TimerMilliseconds", settings.Columns.TimerMilliseconds);

        ConfigureNumberBox(timerOffsetXBox, settings.Columns.TimerOffsetX, -2000, 2000, 1);
        ConfigureNumberBox(timerOffsetYBox, settings.Columns.TimerOffsetY, -2000, 2000, 1);

        TableLayoutPanel offsetGrid = CreateGrid(4, 25f, 25f, 25f, 25f);
        int offsetRow = offsetGrid.RowCount++;
        offsetGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
        offsetGrid.Controls.Add(CreateRowLabel("Offset X"), 0, offsetRow);
        offsetGrid.Controls.Add(timerOffsetXBox, 1, offsetRow);
        offsetGrid.Controls.Add(CreateRowLabel("Offset Y"), 2, offsetRow);
        offsetGrid.Controls.Add(timerOffsetYBox, 3, offsetRow);

        AddSectionControl(section, grid);
        AddSectionControl(section, offsetGrid);
        AddSection(parent, section);
    }

    private void AddFontSettingsRow(TableLayoutPanel grid, string label, string key, UiColumnSettings value)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));

        var showBox = new CheckBox
        {
            Checked = value.Show,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(showBox);

        var fontBox = CreateDecimalBox(value.FontSize, 6, 96, 1);
        var boldBox = new CheckBox
        {
            Checked = value.Bold,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(boldBox);

        fontControls[key] = new FontControls(showBox, fontBox, boldBox);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(showBox, 1, row);
        grid.Controls.Add(fontBox, 2, row);
        grid.Controls.Add(boldBox, 3, row);
    }

    private void AddColumnSettingsHeader(TableLayoutPanel grid)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
        grid.Controls.Add(CreateRowLabel("Column"), 0, row);
        grid.Controls.Add(CreateRowLabel("Show"), 1, row);
        grid.Controls.Add(CreateRowLabel("Width"), 2, row);
        grid.Controls.Add(CreateRowLabel("Font"), 3, row);
        grid.Controls.Add(CreateRowLabel("Bold"), 4, row);
    }

    private void AddColumnSettingsRow(TableLayoutPanel grid, string label, string key, UiColumnSettings value)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));

        var showBox = new CheckBox
        {
            Checked = value.Show,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(showBox);

        var widthBox = CreateNumberBox(value.Width, 1, 1000, 5);
        var fontBox = CreateDecimalBox(value.FontSize, 6, 96, 1);
        var boldBox = new CheckBox
        {
            Checked = value.Bold,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(boldBox);

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
        TableLayoutPanel grid = CreateGrid(2, 48f, 52f);
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
            textBox.PlaceholderText = Localizer.Get("empty = bundled icon", settings);
            bossIconTextBoxes[unit.Id] = textBox;

            Button browseButton = CreateButton("Browse", accent: false);
            browseButton.Width = Math.Max(browseButton.Width, 96);
            browseButton.Margin = new Padding(8, 3, 0, 3);
            browseButton.Click += (_, _) => PickBossIcon(textBox);

            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            grid.Controls.Add(CreateRowLabel(Localizer.Get(unit.DisplayName, settings)), 0, row);
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
        newReferenceSetNameBox.PlaceholderText = Localizer.Get("new group name", settings);
        newReferenceSetNameBox.Dock = DockStyle.Fill;
        UiTheme.StyleTextBox(newReferenceSetNameBox);

        Button addButton = CreateButton("Add", accent: false);
        addButton.Width = Math.Max(addButton.Width, 96);
        addButton.Click += (_, _) => AddReferenceSet();

        Button deleteButton = CreateButton("Delete", accent: false);
        deleteButton.Width = Math.Max(deleteButton.Width, 96);
        deleteButton.Click += (_, _) => DeleteReferenceSet();

        int selectorRow = selectorGrid.RowCount++;
        selectorGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
        selectorGrid.Controls.Add(CreateRowLabel("Active group"), 0, selectorRow);
        selectorGrid.Controls.Add(referenceSetBox, 1, selectorRow);
        selectorGrid.Controls.Add(newReferenceSetNameBox, 2, selectorRow);
        selectorGrid.Controls.Add(CreateButtonPanel(addButton, deleteButton), 3, selectorRow);
        AddSectionControl(section, selectorGrid);

        TableLayoutPanel grid = CreateGrid(2, 48f, 52f);

        foreach (BossRouteEntry entry in GetRouteOrderedEntries())
        {
            if (!BossSplitDefinitions.TryGetUnit(entry.BossId, out BossUnitDefinition unit))
            {
                continue;
            }

            var textBox = CreateTextBox(settings.GetReferenceText(unit.Id));
            textBox.PlaceholderText = "m:ss or h:mm:ss";
            splitTextBoxes[unit.Id] = textBox;
            AddSettingRow(grid, Localizer.Get(unit.DisplayName, settings), textBox);
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

    private TableLayoutPanel CreateSection(string title)
    {
        var section = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SectionColor,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 16),
            Padding = new Padding(18, 16, 18, 18)
        };
        UiTheme.EnableDoubleBuffering(section);
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var label = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = UiTheme.FormFont(10.5f, FontStyle.Bold),
            ForeColor = TextColor,
            Margin = new Padding(0, 0, 0, 10),
            Text = Localizer.Get(title, settings)
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
        UiTheme.EnableDoubleBuffering(grid);

        foreach (float columnWidth in columnWidths)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, columnWidth));
        }

        return grid;
    }

    private TabPage CreateTabPage(string text)
    {
        return new TabPage
        {
            BackColor = WindowColor,
            ForeColor = TextColor,
            Text = Localizer.Get(text, settings)
        };
    }

    private static Panel CreateScrollPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = WindowColor,
            Padding = new Padding(20, 18, 20, 6)
        };
        UiTheme.EnableDoubleBuffering(panel);
        return panel;
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
        textBox.Dock = DockStyle.Fill;
        textBox.ReadOnly = true;
        UiTheme.StyleTextBox(textBox);
        textBox.SetHotkey(selected);
    }

    private static void ConfigureCheckBox(CheckBox checkBox, bool selected)
    {
        checkBox.Checked = selected;
        checkBox.Dock = DockStyle.Fill;
        UiTheme.StyleCheckBox(checkBox);
    }

    private void ConfigureReferenceSetBox()
    {
        referenceSetBox.Dock = DockStyle.Fill;
        UiTheme.StyleComboBox(referenceSetBox);

        foreach (ReferenceSplitSet set in settings.ReferenceSplitSets)
        {
            referenceSetBox.Items.Add(set.Name);
        }

        referenceSetBox.SelectedItem = settings.GetActiveReferenceSet().Name;
        referenceSetBox.SelectedIndexChanged += (_, _) => SwitchReferenceSet();
    }

    private static void ConfigurePercentBox(NumericUpDown numericBox, int selected)
    {
        UiTheme.StyleNumericBox(numericBox);
        numericBox.DecimalPlaces = 0;
        numericBox.Dock = DockStyle.Fill;
        numericBox.Increment = 5;
        numericBox.Maximum = 100;
        numericBox.Minimum = 0;
        numericBox.Value = Math.Clamp(selected, 0, 100);
    }

    private static void ConfigureNumberBox(NumericUpDown numericBox, int selected, int minimum, int maximum, int increment)
    {
        UiTheme.StyleNumericBox(numericBox);
        numericBox.DecimalPlaces = 0;
        numericBox.Dock = DockStyle.Fill;
        numericBox.Increment = increment;
        numericBox.Maximum = maximum;
        numericBox.Minimum = minimum;
        numericBox.Value = Math.Clamp(selected, minimum, maximum);
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

    private static NumericUpDown CreateDecimalBoxForSegment(decimal value, decimal minimum, decimal maximum, decimal increment)
    {
        var numericBox = new NumericUpDown();
        ConfigureNumericBox(numericBox, value, minimum, maximum, increment, 1);
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
        UiTheme.StyleNumericBox(numericBox);
        numericBox.DecimalPlaces = decimalPlaces;
        numericBox.Dock = DockStyle.Fill;
        numericBox.Increment = increment;
        numericBox.Maximum = maximum;
        numericBox.Minimum = minimum;
        numericBox.Value = Math.Clamp(value, minimum, maximum);
    }

    private void AddSettingRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));

        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private void AddColorRow(TableLayoutPanel grid, string label, string key, string value)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));

        var textBox = CreateTextBox(value);
        colorTextBoxes[key] = textBox;

        var pickButton = CreateColorButton(textBox);
        textBox.TextChanged += (_, _) => UpdateColorButton(pickButton, textBox.Text);

        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(textBox, 1, row);
        grid.Controls.Add(pickButton, 2, row);
    }

    private Label CreateRowLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = MutedTextColor,
            Margin = new Padding(0, 0, 4, 0),
            Text = Localizer.Get(text, settings),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private Label CreateSubsectionLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = UiTheme.FormFont(9.5f, FontStyle.Bold),
            ForeColor = TextColor,
            Margin = new Padding(0, 6, 0, 6),
            Text = Localizer.Get(text, settings)
        };
    }

    private static TextBox CreateTextBox(string value)
    {
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = value
        };
        UiTheme.StyleTextBox(textBox);
        return textBox;
    }

    private Button CreateButton(string text, bool accent)
    {
        var button = new Button
        {
            Text = Localizer.Get(text, settings),
        };
        UiTheme.StyleButton(button, accent);
        return button;
    }

    private FlowLayoutPanel CreateButtonPanel(params Button[] buttons)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = new Padding(0, 1, 0, 0),
            WrapContents = false
        };
        UiTheme.EnableDoubleBuffering(panel);

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
            Height = 28,
            Margin = new Padding(10, 5, 0, 5),
            Text = string.Empty,
            Width = 42
        };
        UiTheme.StyleButton(button, accent: false, minimumWidth: 42);
        button.MinimumSize = new Size(42, 28);
        button.Padding = Padding.Empty;
        button.Height = 28;
        button.Width = 42;

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
            string text = textBox.Text.Trim();
            activeSet.Splits[name] = TimeText.TryParse(text, out TimeSpan parsed)
                ? TimeText.FormatRecord(parsed)
                : text;
        }
    }

    private void SavePersonalBestTextBoxes()
    {
        foreach ((string name, TextBox textBox) in personalBestTimeTextBoxes)
        {
            settings.SetPersonalBestTimeText(name, NormalizeTimeText(textBox.Text));
        }

        foreach ((string name, TextBox textBox) in personalBestSegmentTextBoxes)
        {
            settings.SetPersonalBestSegmentText(name, NormalizeTimeText(textBox.Text));
        }
    }

    private static string NormalizeTimeText(string text)
    {
        string trimmed = text.Trim();
        return TimeText.TryParse(trimmed, out TimeSpan parsed)
            ? TimeText.FormatRecord(parsed)
            : trimmed;
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
        settings.Language = languageBox.SelectedItem as string ?? "English";
        settings.PauseResumeKey = pauseKeyBox.Hotkey.ToString();
        settings.ResetKey = resetKeyBox.Hotkey.ToString();
        settings.MouseClickThroughKey = mouseClickThroughKeyBox.Hotkey.ToString();
        settings.AlwaysOnTop = alwaysOnTopBox.Checked;
        settings.PracticeMode = practiceModeBox.Checked;
        SaveReferenceTextBoxes();
        SavePersonalBestTextBoxes();
        ApplyRouteSettings();
        AppSettingsStore.Normalize(settings);

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

        settings.Columns.TimerOffsetX = (int)timerOffsetXBox.Value;
        settings.Columns.TimerOffsetY = (int)timerOffsetYBox.Value;

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

    private void ApplyAndNotify()
    {
        ApplyToSettings();
        PopulatePersonalBestTimeGrid();
        PopulatePersonalBestSegmentGrid();
        Applied?.Invoke(this, EventArgs.Empty);
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
                Segment = controls.Segment.Value
            });
        }

        settings.Route = route;
    }

    private IReadOnlyList<BossRouteEntry> GetRouteOrderedEntries()
    {
        return settings.Route
            .Select((entry, index) => new { Entry = entry, Index = index })
            .OrderBy(item => item.Entry.Segment)
            .ThenBy(item => item.Index)
            .Select(item => item.Entry)
            .ToList();
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
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
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
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));

            var enabledBox = new CheckBox
            {
                Checked = entry.Enabled,
                Dock = DockStyle.Fill,
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleCenter
            };
            UiTheme.StyleCheckBox(enabledBox);
            NumericUpDown segmentBox = CreateDecimalBoxForSegment(Math.Clamp(entry.Segment, 1m, 99m), 1m, 99m, 0.1m);

            routeControls[unit.Id] = new RouteControls(enabledBox, segmentBox);
            grid.Controls.Add(CreateRowLabel(Localizer.Get(unit.DisplayName, settings)), 0, row);
            grid.Controls.Add(enabledBox, 1, row);
            grid.Controls.Add(segmentBox, 2, row);
        }

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }
}
