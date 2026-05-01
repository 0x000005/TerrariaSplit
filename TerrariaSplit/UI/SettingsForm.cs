using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class SettingsForm : Form
{
    private const int ResizeBorder = 8;
    private const int RowHeight = 56;
    private const int HeaderRowHeight = 40;

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
    private readonly TextBox undefeatedIconGrayscaleBox = new();
    private readonly TextBox undefeatedIconBrightnessBox = new();
    private readonly Dictionary<string, RouteControls> routeControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> bossIconTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> splitTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> personalBestTimeTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> personalBestSegmentTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> colorTextBoxes = new();
    private readonly Dictionary<string, ColumnControls> columnControls = new();
    private readonly Dictionary<string, FontControls> fontControls = new();
    private readonly TextBox globalScaleBox = new();
    private readonly TextBox timerOffsetXBox = new();
    private readonly TextBox timerOffsetYBox = new();

    private TableLayoutPanel? personalBestTimeGrid;
    private TableLayoutPanel? personalBestSegmentGrid;
    private bool updatingReferenceSetSelection;
    private bool dragging;
    private Point dragStartCursor;
    private Point dragStartLocation;

    public SettingsForm(AppSettings currentSettings)
    {
        settings = AppSettingsStore.Clone(currentSettings);

        Text = Localizer.Get("TerrariaSplit Settings", settings);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(1240, 1040);
        Padding = new Padding(1);
        UiTheme.ConfigureForm(this, new Size(1040, 740));

        BuildLayout();
    }

    public AppSettings Result => settings;

    public event EventHandler? Applied;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(BorderColor);
        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override void WndProc(ref Message m)
    {
        const int wmNcHitTest = 0x84;
        const int htClient = 1;
        const int htLeft = 10;
        const int htRight = 11;
        const int htTop = 12;
        const int htTopLeft = 13;
        const int htTopRight = 14;
        const int htBottom = 15;
        const int htBottomLeft = 16;
        const int htBottomRight = 17;

        base.WndProc(ref m);

        if (m.Msg != wmNcHitTest || m.Result != (IntPtr)htClient)
        {
            return;
        }

        long lParam = m.LParam.ToInt64();
        int x = unchecked((short)(lParam & 0xFFFF));
        int y = unchecked((short)((lParam >> 16) & 0xFFFF));
        Point point = PointToClient(new Point(x, y));

        bool left = point.X <= ResizeBorder;
        bool right = point.X >= ClientSize.Width - ResizeBorder;
        bool top = point.Y <= ResizeBorder;
        bool bottom = point.Y >= ClientSize.Height - ResizeBorder;

        if (left && top)
        {
            m.Result = (IntPtr)htTopLeft;
        }
        else if (right && top)
        {
            m.Result = (IntPtr)htTopRight;
        }
        else if (left && bottom)
        {
            m.Result = (IntPtr)htBottomLeft;
        }
        else if (right && bottom)
        {
            m.Result = (IntPtr)htBottomRight;
        }
        else if (left)
        {
            m.Result = (IntPtr)htLeft;
        }
        else if (right)
        {
            m.Result = (IntPtr)htRight;
        }
        else if (top)
        {
            m.Result = (IntPtr)htTop;
        }
        else if (bottom)
        {
            m.Result = (IntPtr)htBottom;
        }
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowColor,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86f));
        Controls.Add(root);

        root.Controls.Add(CreateTitleBar(), 0, 0);
        root.Controls.Add(CreateBody(), 0, 1);
        root.Controls.Add(CreateFooter(), 0, 2);
    }

    private Control CreateTitleBar()
    {
        var titleBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.SurfaceRaised,
            Padding = new Padding(18, 0, 10, 0)
        };
        UiTheme.EnableDoubleBuffering(titleBar);
        titleBar.MouseDown += (_, e) => BeginDrag(e);
        titleBar.MouseMove += (_, _) => ContinueDrag();
        titleBar.MouseUp += (_, e) => EndDrag(e);

        var title = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            Font = UiTheme.FormFont(11.5f, FontStyle.Bold),
            Text = Localizer.Get("TerrariaSplit Settings", settings),
            TextAlign = ContentAlignment.MiddleLeft
        };
        title.MouseDown += (_, e) => BeginDrag(e);
        title.MouseMove += (_, _) => ContinueDrag();
        title.MouseUp += (_, e) => EndDrag(e);

        var closeButton = new Button
        {
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            ForeColor = TextColor,
            BackColor = UiTheme.SurfaceRaised,
            Text = "X",
            Width = 48
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(78, 57, 57);
        closeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(98, 48, 48);
        closeButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        titleBar.Controls.Add(title);
        titleBar.Controls.Add(closeButton);
        return titleBar;
    }

    private void BeginDrag(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        dragging = true;
        dragStartCursor = Cursor.Position;
        dragStartLocation = Location;
    }

    private void ContinueDrag()
    {
        if (!dragging)
        {
            return;
        }

        Point delta = new(Cursor.Position.X - dragStartCursor.X, Cursor.Position.Y - dragStartCursor.Y);
        Location = new Point(dragStartLocation.X + delta.X, dragStartLocation.Y + delta.Y);
    }

    private void EndDrag(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            dragging = false;
        }
    }

    private Control CreateBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowColor,
            ColumnCount = 2,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(body);
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 172f));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(12, 16, 12, 12),
            WrapContents = false
        };
        UiTheme.EnableDoubleBuffering(nav);

        var pageHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowColor,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(pageHost);

        var pages = new List<(Button Nav, Control Page)>
        {
            (CreateNavButton("General"), CreateGeneralPage()),
            (CreateNavButton("Splits"), CreateSplitsPage()),
            (CreateNavButton("UI"), CreateUiPage()),
            (CreateNavButton("Colors"), CreateColorPage())
        };

        foreach ((Button navButton, Control page) in pages)
        {
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            pageHost.Controls.Add(page);
            nav.Controls.Add(navButton);
        }

        void SelectPage(int index)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                bool selected = i == index;
                pages[i].Page.Visible = selected;
                pages[i].Page.BringToFront();
                pages[i].Nav.BackColor = selected ? UiTheme.Accent : UiTheme.SurfaceRaised;
                pages[i].Nav.FlatAppearance.BorderColor = selected ? UiTheme.Accent : BorderColor;
            }
        }

        for (int i = 0; i < pages.Count; i++)
        {
            int index = i;
            pages[i].Nav.Click += (_, _) => SelectPage(index);
        }

        SelectPage(0);
        body.Controls.Add(nav, 0, 0);
        body.Controls.Add(pageHost, 1, 0);
        return body;
    }

    private Button CreateNavButton(string text)
    {
        var button = new Button
        {
            Text = Localizer.Get(text, settings),
            Width = 148,
            Height = 46,
            Margin = new Padding(0, 0, 0, 8),
            TextAlign = ContentAlignment.MiddleLeft
        };
        UiTheme.StyleButton(button, accent: false, minimumWidth: 148);
        button.Height = 46;
        button.MinimumSize = new Size(148, 46);
        button.Padding = new Padding(14, 0, 14, 2);
        return button;
    }

    private Control CreateFooter()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowColor,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(18, 16, 18, 16),
            WrapContents = false
        };
        UiTheme.EnableDoubleBuffering(footer);

        var okButton = CreateButton("OK", accent: true, minimumWidth: 150);
        okButton.DialogResult = DialogResult.OK;
        okButton.Click += (_, _) => ApplyToSettings();

        var applyButton = CreateButton("Apply", accent: false, minimumWidth: 150);
        applyButton.Click += (_, _) => ApplyAndNotify();

        var cancelButton = CreateButton("Cancel", accent: false, minimumWidth: 150);
        cancelButton.DialogResult = DialogResult.Cancel;

        footer.Controls.Add(okButton);
        footer.Controls.Add(applyButton);
        footer.Controls.Add(cancelButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        return footer;
    }

    private Control CreateGeneralPage()
    {
        TableLayoutPanel content = CreatePageContent();
        AddHotkeySection(content);
        return CreateScrollPage(content);
    }

    private Control CreateSplitsPage()
    {
        TableLayoutPanel content = CreatePageContent();
        AddRouteSection(content);
        AddBossIconSection(content);
        AddReferenceDataSection(content);
        AddPersonalBestDataSection(content);
        return CreateScrollPage(content);
    }

    private Control CreateUiPage()
    {
        TableLayoutPanel content = CreatePageContent();
        AddColumnSettingsSection(content);
        AddTimerSettingsSection(content);
        AddIconStyleSection(content);
        return CreateScrollPage(content);
    }

    private Control CreateColorPage()
    {
        TableLayoutPanel content = CreatePageContent();
        AddColorSection(content);
        return CreateScrollPage(content);
    }

    private static TableLayoutPanel CreatePageContent()
    {
        var content = new TableLayoutPanel
        {
            AutoSize = false,
            BackColor = WindowColor,
            ColumnCount = 1,
            Dock = DockStyle.None,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        UiTheme.EnableDoubleBuffering(content);
        return content;
    }

    private static Control CreateScrollPage(Control content)
    {
        content.Dock = DockStyle.None;
        var panel = new ThemedScrollPanel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowColor,
            Padding = new Padding(22, 18, 20, 12)
        };
        panel.Controls.Add(content);
        UiTheme.EnableDoubleBuffering(panel);
        return panel;
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

        ConfigureNumberBox(globalScaleBox, settings.Columns.ScalePercent, 25, 300);
        ConfigureCheckBox(alwaysOnTopBox, settings.AlwaysOnTop);
        ConfigureCheckBox(practiceModeBox, settings.PracticeMode);

        TableLayoutPanel section = CreateSection("General Options");
        TableLayoutPanel grid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(grid, "Language", languageBox);
        AddSettingRow(grid, "Global scale %", globalScaleBox);
        AddSettingRow(grid, "Pause / Resume", pauseKeyBox);
        AddSettingRow(grid, "Reset at Menu", resetKeyBox);
        AddSettingRow(grid, "Mouse passthrough", mouseClickThroughKeyBox);
        AddSettingRow(grid, "Always on top", alwaysOnTopBox);
        AddSettingRow(grid, "Practice mode", practiceModeBox);
        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private void AddRouteSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Boss Groups");
        TableLayoutPanel grid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(124f),
            ColumnStyleAbsolute(96f));

        AddHeaderRow(grid, "Boss", "Enabled", "Group");

        IReadOnlyDictionary<string, BossRouteEntry> route = settings.Route.ToDictionary(
            entry => entry.BossId,
            StringComparer.OrdinalIgnoreCase);

        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            BossRouteEntry entry = route.TryGetValue(unit.Id, out BossRouteEntry? existing)
                ? existing
                : new BossRouteEntry { BossId = unit.Id };

            var enabledBox = new CheckBox
            {
                Checked = entry.Enabled,
                Dock = DockStyle.Fill,
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleCenter
            };
            UiTheme.StyleCheckBox(enabledBox);

            TextBox groupBox = CreateTextBox(Math.Clamp(entry.Segment, 1m, 99m).ToString("0.#", CultureInfo.InvariantCulture));
            routeControls[unit.Id] = new RouteControls(enabledBox, groupBox);

            int row = AddGridRow(grid);
            grid.Controls.Add(CreateRowLabel(Localizer.Get(unit.DisplayName, settings)), 0, row);
            grid.Controls.Add(enabledBox, 1, row);
            grid.Controls.Add(groupBox, 2, row);
        }

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private void AddBossIconSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Boss Icons");
        TableLayoutPanel grid = CreateGrid(
            ColumnStyleAbsolute(260f),
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(156f));

        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            TextBox textBox = CreateTextBox(settings.GetBossIconPath(unit.Id));
            textBox.PlaceholderText = Localizer.Get("empty = bundled icon", settings);
            bossIconTextBoxes[unit.Id] = textBox;

            Button browseButton = CreateButton("Browse", accent: false, minimumWidth: 140);
            browseButton.Margin = new Padding(8, 2, 0, 2);
            browseButton.Click += (_, _) => PickBossIcon(textBox);

            int row = AddGridRow(grid);
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

        ConfigureReferenceSetBox();
        newReferenceSetNameBox.PlaceholderText = Localizer.Get("new group name", settings);
        newReferenceSetNameBox.Dock = DockStyle.Fill;
        UiTheme.StyleTextBox(newReferenceSetNameBox);

        Button addButton = CreateButton("Add", accent: false, minimumWidth: 120);
        addButton.Click += (_, _) => AddReferenceSet();

        TableLayoutPanel selectorGrid = CreateGrid(
            ColumnStyleAbsolute(220f),
            ColumnStyleAbsolute(260f),
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(240f),
            ColumnStyleAbsolute(136f));
        int selectorRow = AddGridRow(selectorGrid);
        selectorGrid.Controls.Add(CreateRowLabel("Active group"), 0, selectorRow);
        selectorGrid.Controls.Add(referenceSetBox, 1, selectorRow);
        selectorGrid.Controls.Add(newReferenceSetNameBox, 3, selectorRow);
        selectorGrid.Controls.Add(CreateButtonPanel(addButton), 4, selectorRow);
        AddSectionControl(section, selectorGrid);

        TableLayoutPanel grid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        foreach (BossRouteEntry entry in GetRouteOrderedEntries())
        {
            if (!BossSplitDefinitions.TryGetUnit(entry.BossId, out BossUnitDefinition unit))
            {
                continue;
            }

            TextBox textBox = CreateTextBox(settings.GetReferenceText(unit.Id));
            textBox.PlaceholderText = "m:ss or h:mm:ss";
            splitTextBoxes[unit.Id] = textBox;
            AddSettingRow(grid, Localizer.Get(unit.DisplayName, settings), textBox);
        }

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private void AddPersonalBestDataSection(TableLayoutPanel parent)
    {
        TableLayoutPanel personalBestSection = CreateSection("Personal best");
        personalBestTimeGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        PopulatePersonalBestTimeGrid();
        AddSectionControl(personalBestSection, personalBestTimeGrid);
        AddSection(parent, personalBestSection);

        TableLayoutPanel personalBestSegmentSection = CreateSection("Personal segment best");
        personalBestSegmentGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        PopulatePersonalBestSegmentGrid();
        AddSectionControl(personalBestSegmentSection, personalBestSegmentGrid);
        AddSection(parent, personalBestSegmentSection);
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

            TextBox textBox = CreateTextBox(settings.GetPersonalBestTimeText(unit.Id));
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
            TextBox textBox = CreateTextBox(settings.GetPersonalBestSegmentText(group.Key));
            textBox.PlaceholderText = "m:ss or h:mm:ss";
            personalBestSegmentTextBoxes[group.Key] = textBox;
            AddSettingRow(personalBestSegmentGrid, BossRouteGroups.GetGroupDisplayName(group, settings), textBox);
        }
    }

    private void AddColumnSettingsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Columns");
        TableLayoutPanel grid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(92f),
            ColumnStyleAbsolute(118f),
            ColumnStyleAbsolute(132f),
            ColumnStyleAbsolute(92f));

        AddHeaderRow(grid, ContentAlignment.MiddleLeft, "Column", "Show", "Width", "Font", "Bold");
        AddColumnSettingsRow(grid, "Icon", "Icon", settings.Columns.Icon);
        AddColumnSettingsRow(grid, "Time", "Time", settings.Columns.Time);
        AddColumnSettingsRow(grid, "Delta", "Delta", settings.Columns.Delta);

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private void AddColumnSettingsRow(TableLayoutPanel grid, string label, string key, UiColumnSettings value)
    {
        var showBox = new CheckBox
        {
            Checked = value.Show,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(showBox);

        TextBox widthBox = CreateNumberBox(value.Width, 1, 1000);
        TextBox fontBox = CreateDecimalBox(value.FontSize, 6, 96);

        var boldBox = new CheckBox
        {
            Checked = value.Bold,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(boldBox);

        columnControls[key] = new ColumnControls(showBox, widthBox, fontBox, boldBox);

        int row = AddGridRow(grid);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(CreateCenteredCell(showBox, 28), 1, row);
        grid.Controls.Add(CreateCenteredCell(widthBox, 86), 2, row);
        grid.Controls.Add(CreateCenteredCell(fontBox, 92), 3, row);
        grid.Controls.Add(CreateCenteredCell(boldBox, 28), 4, row);
    }

    private void AddTimerSettingsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Timer");
        TableLayoutPanel grid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(92f),
            ColumnStyleAbsolute(132f),
            ColumnStyleAbsolute(92f));

        AddHeaderRow(grid, ContentAlignment.MiddleLeft, "Part", "Show", "Font", "Bold");
        AddFontSettingsRow(grid, "Main time", "Timer", settings.Columns.Timer);
        AddFontSettingsRow(grid, "Milliseconds", "TimerMilliseconds", settings.Columns.TimerMilliseconds);

        ConfigureNumberBox(timerOffsetXBox, settings.Columns.TimerOffsetX, -2000, 2000);
        ConfigureNumberBox(timerOffsetYBox, settings.Columns.TimerOffsetY, -2000, 2000);
        TableLayoutPanel offsetGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(offsetGrid, "Offset X", timerOffsetXBox);
        AddSettingRow(offsetGrid, "Offset Y", timerOffsetYBox);

        AddSectionControl(section, grid);
        AddSectionControl(section, offsetGrid);
        AddSection(parent, section);
    }

    private void AddFontSettingsRow(TableLayoutPanel grid, string label, string key, UiColumnSettings value)
    {
        var showBox = new CheckBox
        {
            Checked = value.Show,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(showBox);

        TextBox fontBox = CreateDecimalBox(value.FontSize, 6, 96);
        var boldBox = new CheckBox
        {
            Checked = value.Bold,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(boldBox);

        fontControls[key] = new FontControls(showBox, fontBox, boldBox);
        int row = AddGridRow(grid);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(CreateCenteredCell(showBox, 28), 1, row);
        grid.Controls.Add(CreateCenteredCell(fontBox, 92), 2, row);
        grid.Controls.Add(CreateCenteredCell(boldBox, 28), 3, row);
    }

    private void AddIconStyleSection(TableLayoutPanel parent)
    {
        ConfigureNumberBox(undefeatedIconGrayscaleBox, settings.UndefeatedIconGrayscalePercent, 0, 100);
        ConfigureNumberBox(undefeatedIconBrightnessBox, settings.UndefeatedIconBrightnessPercent, 0, 100);

        TableLayoutPanel section = CreateSection("Icon Style");
        TableLayoutPanel grid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(grid, "Unlit grayscale %", undefeatedIconGrayscaleBox);
        AddSettingRow(grid, "Unlit brightness %", undefeatedIconBrightnessBox);
        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    private void AddColorSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Text Colors");
        TableLayoutPanel grid = CreateGrid(3, 36f, 50f, 14f);

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
            Margin = new Padding(0, 0, 0, 18),
            Padding = new Padding(22, 18, 22, 20)
        };
        UiTheme.EnableDoubleBuffering(section);
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var label = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = UiTheme.FormFont(13f, FontStyle.Bold),
            ForeColor = TextColor,
            Margin = new Padding(0, 0, 0, 14),
            Text = Localizer.Get(title, settings)
        };
        AddSectionControl(section, label);
        return section;
    }

    private static TableLayoutPanel CreateGrid(int columnCount, params float[] columnWidths)
    {
        return CreateGrid(columnWidths.Select(ColumnStylePercent).ToArray());
    }

    private static ColumnStyle ColumnStylePercent(float width)
    {
        return new ColumnStyle(SizeType.Percent, width);
    }

    private static ColumnStyle ColumnStyleAbsolute(float width)
    {
        return new ColumnStyle(SizeType.Absolute, width);
    }

    private static TableLayoutPanel CreateGrid(params ColumnStyle[] columnStyles)
    {
        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SectionColor,
            ColumnCount = columnStyles.Length,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(grid);

        foreach (ColumnStyle columnStyle in columnStyles)
        {
            grid.ColumnStyles.Add(columnStyle);
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

    private void AddSettingRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = AddGridRow(grid);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private void AddSettingRow(TableLayoutPanel grid, string label, Control control, int controlWidth)
    {
        int row = AddGridRow(grid);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(CreateAlignedCell(control, controlWidth, HorizontalAlignment.Right), 1, row);
    }

    private void AddColorRow(TableLayoutPanel grid, string label, string key, string value)
    {
        TextBox textBox = CreateTextBox(value);
        colorTextBoxes[key] = textBox;

        Button pickButton = CreateColorButton(textBox);
        textBox.TextChanged += (_, _) => UpdateColorButton(pickButton, textBox.Text);

        int row = AddGridRow(grid);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(textBox, 1, row);
        grid.Controls.Add(pickButton, 2, row);
    }

    private int AddGridRow(TableLayoutPanel grid)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));
        return row;
    }

    private void AddHeaderRow(TableLayoutPanel grid, params string[] labels)
    {
        AddHeaderRow(grid, ContentAlignment.MiddleLeft, labels);
    }

    private void AddHeaderRow(TableLayoutPanel grid, ContentAlignment firstColumnAlign, params string[] labels)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderRowHeight));
        for (int i = 0; i < labels.Length; i++)
        {
            ContentAlignment align = i == 0 ? firstColumnAlign : ContentAlignment.MiddleCenter;
            grid.Controls.Add(CreateHeaderLabel(labels[i], align), i, row);
        }
    }

    private Label CreateHeaderLabel(string text, ContentAlignment align = ContentAlignment.MiddleLeft)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = MutedTextColor,
            Font = UiTheme.FormFont(9.5f, FontStyle.Bold),
            Margin = align == ContentAlignment.MiddleLeft ? new Padding(0, 0, 12, 0) : Padding.Empty,
            Text = Localizer.Get(text, settings),
            TextAlign = align
        };
    }

    private Label CreateRowLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = TextColor,
            Margin = new Padding(0, 0, 14, 0),
            Text = Localizer.Get(text, settings),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private Label CreateSubsectionLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = UiTheme.FormFont(11.5f, FontStyle.Bold),
            ForeColor = TextColor,
            Margin = new Padding(0, 16, 0, 10),
            Text = Localizer.Get(text, settings)
        };
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

    private static void ConfigureNumberBox(TextBox textBox, int selected, int minimum, int maximum)
    {
        UiTheme.StyleTextBox(textBox);
        textBox.Dock = DockStyle.Fill;
        textBox.Text = Math.Clamp(selected, minimum, maximum).ToString(CultureInfo.InvariantCulture);
    }

    private static TextBox CreateNumberBox(int value, int minimum, int maximum)
    {
        var textBox = new TextBox();
        ConfigureNumberBox(textBox, value, minimum, maximum);
        return textBox;
    }

    private static TextBox CreateDecimalBox(float value, decimal minimum, decimal maximum)
    {
        var textBox = new TextBox();
        UiTheme.StyleTextBox(textBox);
        textBox.Dock = DockStyle.Fill;
        textBox.Text = Math.Clamp((decimal)value, minimum, maximum).ToString("0.#", CultureInfo.InvariantCulture);
        return textBox;
    }

    private static TextBox CreateTextBox(string value)
    {
        var textBox = new TextBox
        {
            Text = value,
            Dock = DockStyle.Fill
        };
        UiTheme.StyleTextBox(textBox);
        return textBox;
    }

    private static Panel CreateCenteredCell(Control control, int width)
    {
        return CreateAlignedCell(control, width, HorizontalAlignment.Center);
    }

    private static Panel CreateAlignedCell(Control control, int width, HorizontalAlignment alignment)
    {
        control.Dock = DockStyle.None;
        control.Anchor = AnchorStyles.None;
        control.Width = width;

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = SectionColor
        };
        panel.Controls.Add(control);
        panel.Resize += (_, _) => AlignControlInPanel(panel, control, alignment);
        control.SizeChanged += (_, _) => AlignControlInPanel(panel, control, alignment);
        AlignControlInPanel(panel, control, alignment);
        return panel;
    }

    private static void AlignControlInPanel(Panel panel, Control control, HorizontalAlignment alignment)
    {
        control.Left = alignment switch
        {
            HorizontalAlignment.Left => 0,
            HorizontalAlignment.Right => Math.Max(0, panel.ClientSize.Width - control.Width),
            _ => Math.Max(0, (panel.ClientSize.Width - control.Width) / 2)
        };
        control.Top = Math.Max(0, (panel.ClientSize.Height - control.Height) / 2);
    }

    private Button CreateButton(string text, bool accent, int minimumWidth = 128)
    {
        var button = new Button
        {
            Text = Localizer.Get(text, settings)
        };
        UiTheme.StyleButton(button, accent, minimumWidth);
        return button;
    }

    private FlowLayoutPanel CreateButtonPanel(params Button[] buttons)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            WrapContents = false
        };
        UiTheme.EnableDoubleBuffering(panel);

        foreach (Button button in buttons)
        {
            button.Margin = new Padding(6, 2, 0, 2);
            button.Height = 48;
            button.MinimumSize = new Size(Math.Max(72, button.Width), 48);
            panel.Controls.Add(button);
        }

        return panel;
    }

    private Button CreateColorButton(TextBox textBox)
    {
        var button = new Button
        {
            Height = 36,
            Margin = new Padding(10, 8, 0, 8),
            Text = string.Empty,
            Width = 48
        };
        UiTheme.StyleButton(button, accent: false, minimumWidth: 48);
        button.MinimumSize = new Size(48, 36);
        button.Padding = Padding.Empty;
        button.Height = 36;
        button.Width = 48;
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

        settings.Columns.ScalePercent = ParseIntBox(globalScaleBox, 100, 25, 300);
        settings.Columns.TimerOffsetX = ParseIntBox(timerOffsetXBox, 0, -2000, 2000);
        settings.Columns.TimerOffsetY = ParseIntBox(timerOffsetYBox, 0, -2000, 2000);

        settings.UndefeatedIconGrayscalePercent = ParseIntBox(undefeatedIconGrayscaleBox, 80, 0, 100);
        settings.UndefeatedIconBrightnessPercent = ParseIntBox(undefeatedIconBrightnessBox, 40, 0, 100);

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
                Segment = ParseRouteGroup(controls.Group.Text)
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
        target.Width = ParseIntBox(controls.Width, target.Width, 1, 1000);
        target.FontSize = ParseFloatBox(controls.FontSize, target.FontSize, 6f, 96f);
        target.Bold = controls.Bold.Checked;
    }

    private void ApplyFontSettings(string key, UiColumnSettings target)
    {
        if (!fontControls.TryGetValue(key, out FontControls? controls))
        {
            return;
        }

        target.Show = controls.Show.Checked;
        target.FontSize = ParseFloatBox(controls.FontSize, target.FontSize, 6f, 96f);
        target.Bold = controls.Bold.Checked;
    }

    private static int ParseIntBox(TextBox textBox, int fallback, int minimum, int maximum)
    {
        return int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
    }

    private static float ParseFloatBox(TextBox textBox, float fallback, float minimum, float maximum)
    {
        return float.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
    }

    private static decimal ParseRouteGroup(string? text)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? Math.Clamp(value, 1m, 99m)
            : 1m;
    }

    private sealed record ColumnControls(CheckBox Show, TextBox Width, TextBox FontSize, CheckBox Bold);

    private sealed record FontControls(CheckBox Show, TextBox FontSize, CheckBox Bold);

    private sealed record RouteControls(CheckBox Enabled, TextBox Group);

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

    private sealed class ThemedScrollPanel : Panel
    {
        private const int ScrollBarWidth = 12;
        private const int ScrollStep = 42;
        private int scrollOffset;
        private bool draggingThumb;
        private int dragThumbStartY;
        private int dragStartOffset;

        public ThemedScrollPanel()
        {
            AutoScroll = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            MouseWheel += (_, e) => ScrollBy(e.Delta);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            AttachContent(e.Control);
            LayoutContent();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Rectangle thumb = GetThumbBounds();
            if (thumb.Contains(e.Location))
            {
                draggingThumb = true;
                dragThumbStartY = e.Y;
                dragStartOffset = scrollOffset;
                Capture = true;
                return;
            }

            if (GetTrackBounds().Contains(e.Location))
            {
                ScrollToOffset(PointToOffset(e.Y));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!draggingThumb)
            {
                return;
            }

            int maxOffset = GetMaxOffset();
            Rectangle track = GetTrackBounds();
            Rectangle thumb = GetThumbBounds();
            int travel = Math.Max(1, track.Height - thumb.Height);
            int delta = e.Y - dragThumbStartY;
            int offset = dragStartOffset + (int)Math.Round(delta * (maxOffset / (float)travel));
            ScrollToOffset(offset);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            draggingThumb = false;
            Capture = false;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            LayoutContent();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle track = GetTrackBounds();
            if (track.Width <= 0 || track.Height <= 0)
            {
                return;
            }

            using (var trackBrush = new SolidBrush(UiTheme.Field))
            {
                e.Graphics.FillRectangle(trackBrush, track);
            }

            Rectangle thumb = GetThumbBounds();
            if (thumb.Width > 0 && thumb.Height > 0)
            {
                using var thumbBrush = new SolidBrush(UiTheme.SurfaceRaised);
                e.Graphics.FillRectangle(thumbBrush, thumb);
            }
        }

        private void AttachContent(Control control)
        {
            control.SizeChanged += (_, _) => LayoutContent();
            control.MouseWheel += (_, e) => ScrollBy(e.Delta);
            control.ControlAdded += (_, e) => AttachContent(e.Control);

            foreach (Control child in control.Controls)
            {
                AttachContent(child);
            }
        }

        private void ScrollBy(int delta)
        {
            if (delta == 0)
            {
                return;
            }

            ScrollToOffset(scrollOffset - Math.Sign(delta) * ScrollStep);
        }

        private void LayoutContent()
        {
            if (Controls.Count == 0)
            {
                return;
            }

            Control content = Controls[0];
            int availableWidth = Math.Max(0, ClientSize.Width - Padding.Horizontal - ScrollBarWidth - 10);
            Size preferredSize = content.GetPreferredSize(new Size(availableWidth, 0));
            int preferredHeight = Math.Max(0, preferredSize.Height);
            if (content.Width != availableWidth || content.Height != preferredHeight)
            {
                content.Width = availableWidth;
                content.Height = preferredHeight;
            }

            scrollOffset = Math.Clamp(scrollOffset, 0, GetMaxOffset());
            content.Location = new Point(Padding.Left, Padding.Top - scrollOffset);
            Invalidate();
        }

        private int GetMaxOffset()
        {
            if (Controls.Count == 0)
            {
                return 0;
            }

            Control content = Controls[0];
            int visibleHeight = Math.Max(0, ClientSize.Height - Padding.Vertical);
            return Math.Max(0, content.Height - visibleHeight);
        }

        private Rectangle GetTrackBounds()
        {
            return new Rectangle(
                ClientSize.Width - Padding.Right - ScrollBarWidth,
                Padding.Top,
                ScrollBarWidth,
                Math.Max(0, ClientSize.Height - Padding.Vertical));
        }

        private Rectangle GetThumbBounds()
        {
            Rectangle track = GetTrackBounds();
            int maxOffset = GetMaxOffset();
            if (track.Height <= 0)
            {
                return Rectangle.Empty;
            }

            if (maxOffset <= 0 || Controls.Count == 0)
            {
                return new Rectangle(track.X, track.Y, track.Width, track.Height);
            }

            Control content = Controls[0];
            int visibleHeight = Math.Max(1, ClientSize.Height - Padding.Vertical);
            int thumbHeight = Math.Clamp(
                (int)Math.Round(track.Height * (visibleHeight / (float)Math.Max(visibleHeight, content.Height))),
                36,
                track.Height);
            int travel = Math.Max(1, track.Height - thumbHeight);
            int thumbY = track.Y + (int)Math.Round(travel * (scrollOffset / (float)maxOffset));
            return new Rectangle(track.X, thumbY, track.Width, thumbHeight);
        }

        private int PointToOffset(int y)
        {
            int maxOffset = GetMaxOffset();
            Rectangle track = GetTrackBounds();
            Rectangle thumb = GetThumbBounds();
            int travel = Math.Max(1, track.Height - thumb.Height);
            int relativeY = Math.Clamp(y - track.Y - thumb.Height / 2, 0, travel);
            return (int)Math.Round(relativeY * (maxOffset / (float)travel));
        }

        private void ScrollToOffset(int offset)
        {
            scrollOffset = Math.Clamp(offset, 0, GetMaxOffset());
            LayoutContent();
        }
    }
}
