using System.Drawing;
using System.Drawing.Drawing2D;
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
    private readonly CheckBox autoUpdatePersonalBestDataBox = new();
    private readonly CheckBox showSplitCompletionAnimationBox = new();
    private readonly CheckBox showCurrentSplitHighlightBox = new();
    private readonly TextBox currentSplitHighlightScaleBox = new();
    private readonly TextBox currentSplitDepthStrengthBox = new();
    private readonly CheckBox showSegmentBestDeltaHighlightBox = new();
    private readonly CheckBox enableDefeatedBossIconLightingBox = new();
    private readonly TextBox splitCompletionAnimationDurationBox = new();
    private readonly TextBox splitCompletionOutlineThicknessBox = new();
    private readonly TextBox undefeatedIconGrayscaleBox = new();
    private readonly TextBox undefeatedIconBrightnessBox = new();
    private readonly TextBox currentBossIconGrayscaleWeakenBox = new();
    private readonly TextBox currentBossIconBrightnessBoostBox = new();
    private readonly Dictionary<string, RouteControls> routeControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> bossIconTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> splitTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> personalBestTimeTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> personalBestSegmentTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> colorTextBoxes = new();
    private readonly Dictionary<string, ColumnControls> columnControls = new();
    private readonly Dictionary<string, FontControls> fontControls = new();
    private readonly Dictionary<string, AnimationOutlineControls> animationOutlineControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SegmentBestDeltaHighlightControls> segmentBestDeltaHighlightControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Panel outlineStylePreview = new();
    private readonly Panel segmentBestDeltaHighlightPreview = new();
    private readonly System.Windows.Forms.Timer outlineStylePreviewTimer = new();
    private readonly TextBox globalScaleBox = new();
    private readonly TextBox timerOffsetXBox = new();
    private readonly TextBox timerOffsetYBox = new();

    private TableLayoutPanel? personalBestTimeGrid;
    private TableLayoutPanel? personalBestSegmentGrid;
    private TableLayoutPanel? animationComparisonGrid;
    private TableLayoutPanel? animationOutlineGrid;
    private TableLayoutPanel? segmentBestDeltaHighlightGrid;
    private string? personalBestTimeGridSignature;
    private string? personalBestSegmentGridSignature;
    private string? animationGridSignature;
    private string previewOutlineStyle = SplitCompletionOutlineStyles.Rainbow;
    private string previewSegmentBestDeltaHighlightStyle = SegmentBestDeltaHighlightStyles.Aurora;
    private readonly List<SettingsPageDescriptor> pages = new();
    private Panel? pageHost;
    private bool updatingReferenceSetSelection;
    private int selectedPageIndex = -1;
    private bool bossRouteDirty;
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

        pageHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowColor,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(pageHost);

        pages.Clear();
        pages.Add(new SettingsPageDescriptor(CreateNavButton("General"), () => GeneralSettingsPage.Build(this)));
        pages.Add(new SettingsPageDescriptor(CreateNavButton("BOSS"), () => BossSettingsPage.Build(this)));
        pages.Add(new SettingsPageDescriptor(CreateNavButton("Data"), () => DataSettingsPage.Build(this)));
        pages.Add(new SettingsPageDescriptor(CreateNavButton("UI"), () => UiSettingsPage.Build(this)));
        pages.Add(new SettingsPageDescriptor(CreateNavButton("Effects"), () => AnimationSettingsPage.Build(this)));
        pages.Add(new SettingsPageDescriptor(CreateNavButton("Colors"), () => ColorSettingsPage.Build(this)));

        foreach (SettingsPageDescriptor page in pages)
        {
            nav.Controls.Add(page.Nav);
        }

        void SelectPage(int index)
        {
            if (index == selectedPageIndex)
            {
                return;
            }

            bool refreshedAnimation = false;
            if (selectedPageIndex == 1 && index != selectedPageIndex)
            {
                refreshedAnimation = ApplyBossPageRouteChanges();
            }

            Control selectedPage = EnsurePageCreated(index);

            if (index == 4 && !refreshedAnimation)
            {
                RefreshAnimationOutlineGrid();
            }

            for (int i = 0; i < pages.Count; i++)
            {
                bool selected = i == index;
                Control? page = pages[i].Page;
                if (page is not null)
                {
                    page.Visible = selected;
                }

                pages[i].Nav.BackColor = selected ? UiTheme.Accent : UiTheme.SurfaceRaised;
                pages[i].Nav.FlatAppearance.BorderColor = selected ? UiTheme.Accent : BorderColor;
            }

            selectedPage.Visible = true;
            selectedPage.BringToFront();
            selectedPageIndex = index;
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

    private Control EnsurePageCreated(int index)
    {
        SettingsPageDescriptor descriptor = pages[index];
        if (descriptor.Page is not null)
        {
            return descriptor.Page;
        }

        if (pageHost is null)
        {
            throw new InvalidOperationException("Settings page host has not been created.");
        }

        pageHost.SuspendLayout();
        try
        {
            Control page = descriptor.Create();
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            descriptor.Page = page;
            pageHost.Controls.Add(page);
            return page;
        }
        finally
        {
            pageHost.ResumeLayout(true);
        }
    }

    private void EnsureAllPagesCreated()
    {
        for (int i = 0; i < pages.Count; i++)
        {
            EnsurePageCreated(i);
        }
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

    internal Control BuildScrollPage(Action<TableLayoutPanel> populate)
    {
        TableLayoutPanel content = CreatePageContent();
        content.SuspendLayout();
        try
        {
            populate(content);
        }
        finally
        {
            content.ResumeLayout(false);
        }

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
        panel.BeginContentUpdate();
        try
        {
            panel.Controls.Add(content);
        }
        finally
        {
            panel.EndContentUpdate();
        }

        UiTheme.EnableDoubleBuffering(panel);
        return panel;
    }

    internal void AddHotkeySection(TableLayoutPanel parent)
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

    internal void AddRouteSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("BOSS Groups");
        TableLayoutPanel grid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(124f),
            ColumnStyleAbsolute(96f));

        AddHeaderRow(grid, "BOSS", "Enabled", "Group");

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
            enabledBox.CheckedChanged += (_, _) => bossRouteDirty = true;
            groupBox.TextChanged += (_, _) => bossRouteDirty = true;
            routeControls[unit.Id] = new RouteControls(enabledBox, groupBox);

            int row = AddGridRow(grid);
            grid.Controls.Add(CreateRowLabel(Localizer.Get(unit.DisplayName, settings)), 0, row);
            grid.Controls.Add(enabledBox, 1, row);
            grid.Controls.Add(groupBox, 2, row);
        }

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }

    internal void AddBossIconSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("BOSS Icons");
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

    internal void AddReferenceDataSection(TableLayoutPanel parent)
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

    internal void AddPersonalBestDataSection(TableLayoutPanel parent)
    {
        ConfigureCheckBox(autoUpdatePersonalBestDataBox, settings.AutoUpdatePersonalBestData);
        TableLayoutPanel autoUpdateSection = CreateSection("Personal Data");
        TableLayoutPanel autoUpdateGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(autoUpdateGrid, "Auto update personal data", autoUpdatePersonalBestDataBox);
        AddSectionControl(autoUpdateSection, autoUpdateGrid);
        AddSection(parent, autoUpdateSection);

        TableLayoutPanel personalBestSection = CreateSection("Personal Cumulative Best");
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

        List<BossRouteEntry> entries = GetRouteOrderedEntries().ToList();
        string signature = string.Join('\u001F', entries.Select(entry => entry.BossId));
        if (personalBestTimeGridSignature == signature && personalBestTimeTextBoxes.Count > 0)
        {
            foreach (BossRouteEntry entry in entries)
            {
                if (personalBestTimeTextBoxes.TryGetValue(entry.BossId, out TextBox? textBox))
                {
                    textBox.Text = settings.GetPersonalBestTimeText(entry.BossId);
                }
            }

            return;
        }

        personalBestTimeGrid.SuspendLayout();
        try
        {
            ClearGrid(personalBestTimeGrid);
            personalBestTimeTextBoxes.Clear();
            foreach (BossRouteEntry entry in entries)
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

            personalBestTimeGridSignature = signature;
        }
        finally
        {
            personalBestTimeGrid.ResumeLayout(true);
        }
    }

    private void PopulatePersonalBestSegmentGrid()
    {
        if (personalBestSegmentGrid is null)
        {
            return;
        }

        List<RouteGroup> groups = BossRouteGroups.Build(settings).ToList();
        string signature = string.Join('\u001F', groups.Select(group => group.Key));
        if (personalBestSegmentGridSignature == signature && personalBestSegmentTextBoxes.Count > 0)
        {
            foreach (RouteGroup group in groups)
            {
                if (personalBestSegmentTextBoxes.TryGetValue(group.Key, out TextBox? textBox))
                {
                    textBox.Text = settings.GetPersonalBestSegmentText(group.Key);
                }
            }

            return;
        }

        personalBestSegmentGrid.SuspendLayout();
        try
        {
            ClearGrid(personalBestSegmentGrid);
            personalBestSegmentTextBoxes.Clear();
            foreach (RouteGroup group in groups)
            {
                TextBox textBox = CreateTextBox(settings.GetPersonalBestSegmentText(group.Key));
                textBox.PlaceholderText = "m:ss or h:mm:ss";
                personalBestSegmentTextBoxes[group.Key] = textBox;
                AddSettingRow(personalBestSegmentGrid, BossRouteGroups.GetGroupDisplayName(group, settings), textBox);
            }

            personalBestSegmentGridSignature = signature;
        }
        finally
        {
            personalBestSegmentGrid.ResumeLayout(true);
        }
    }

    internal void AddColumnSettingsSection(TableLayoutPanel parent)
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

    internal void AddTimerSettingsSection(TableLayoutPanel parent)
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

    internal void AddAnimationSection(TableLayoutPanel parent)
    {
        ConfigureCheckBox(enableDefeatedBossIconLightingBox, settings.EnableDefeatedBossIconLighting);
        ConfigureNumberBox(undefeatedIconGrayscaleBox, settings.UndefeatedIconGrayscalePercent, 0, 100);
        ConfigureNumberBox(undefeatedIconBrightnessBox, settings.UndefeatedIconBrightnessPercent, 0, 100);
        ConfigureNumberBox(currentBossIconGrayscaleWeakenBox, settings.CurrentBossIconGrayscaleWeakenPercent, 0, 100);
        ConfigureNumberBox(currentBossIconBrightnessBoostBox, settings.CurrentBossIconBrightnessBoostPercent, 0, 100);
        ConfigureCheckBox(showCurrentSplitHighlightBox, settings.ShowCurrentSplitHighlight);
        ConfigureNumberBox(currentSplitHighlightScaleBox, settings.CurrentSplitHighlightScalePercent, 100, 140);
        ConfigureNumberBox(currentSplitDepthStrengthBox, settings.CurrentSplitDepthStrengthPercent, 0, 100);
        ConfigureCheckBox(showSplitCompletionAnimationBox, settings.ShowSplitCompletionAnimation);
        ConfigureDecimalBox(splitCompletionAnimationDurationBox, settings.SplitCompletionAnimationDurationSeconds, 1m, 20m);
        ConfigureNumberBox(splitCompletionOutlineThicknessBox, settings.SplitCompletionOutlineThicknessPercent, 0, 100);
        splitCompletionOutlineThicknessBox.TextChanged += (_, _) => outlineStylePreview.Invalidate();

        TableLayoutPanel iconSection = CreateSection("Icon Style");
        TableLayoutPanel iconGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(iconGrid, "Enable defeated icon lighting", enableDefeatedBossIconLightingBox);
        AddSettingRow(iconGrid, "Unlit grayscale %", undefeatedIconGrayscaleBox);
        AddSettingRow(iconGrid, "Unlit brightness %", undefeatedIconBrightnessBox);
        AddSettingRow(iconGrid, "Current boss grayscale weaken %", currentBossIconGrayscaleWeakenBox);
        AddSettingRow(iconGrid, "Current boss brightness boost %", currentBossIconBrightnessBoostBox);
        AddSectionControl(iconSection, iconGrid);
        AddSection(parent, iconSection);

        TableLayoutPanel currentSection = CreateSection("Current Split Highlight");
        TableLayoutPanel currentGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(currentGrid, "Enable current split highlight", showCurrentSplitHighlightBox);
        AddSettingRow(currentGrid, "Current split scale %", currentSplitHighlightScaleBox);
        AddSettingRow(currentGrid, "Depth strength %", currentSplitDepthStrengthBox);
        AddSectionControl(currentSection, currentGrid);
        AddSection(parent, currentSection);

        TableLayoutPanel section = CreateSection("BOSS Defeat");
        TableLayoutPanel optionGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(optionGrid, "Enable animation", showSplitCompletionAnimationBox);
        AddSettingRow(optionGrid, "Animation duration seconds", splitCompletionAnimationDurationBox);
        AddSectionControl(section, optionGrid);

        AddSectionControl(section, CreateSubsectionLabel("Show comparison"));
        animationComparisonGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(180f),
            ColumnStyleAbsolute(180f));
        AddSectionControl(section, animationComparisonGrid);

        AddSectionControl(section, CreateSubsectionLabel("Rainbow outline"));
        TableLayoutPanel outlineOptionGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(outlineOptionGrid, "Outline thickness %", splitCompletionOutlineThicknessBox);
        AddSectionControl(section, outlineOptionGrid);

        animationOutlineGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(180f),
            ColumnStyleAbsolute(180f));
        AddSectionControl(section, animationOutlineGrid);
        AddSectionControl(section, CreateOutlineStylePreview());
        PopulateAnimationOutlineGrid();
        AddSection(parent, section);

        AddSegmentBestDeltaHighlightSection(parent);
    }

    private void AddSegmentBestDeltaHighlightSection(TableLayoutPanel parent)
    {
        ConfigureCheckBox(showSegmentBestDeltaHighlightBox, settings.ShowSegmentBestDeltaHighlight);
        TableLayoutPanel section = CreateSection("Segment Best Highlight");
        TableLayoutPanel optionGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(optionGrid, "Enable highlight", showSegmentBestDeltaHighlightBox);
        AddSectionControl(section, optionGrid);

        segmentBestDeltaHighlightGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(220f));
        PopulateSegmentBestDeltaHighlightGrid();
        AddSectionControl(section, segmentBestDeltaHighlightGrid);
        AddSectionControl(section, CreateSegmentBestDeltaHighlightPreview());
        AddSection(parent, section);
    }

    private void PopulateAnimationOutlineGrid()
    {
        if (animationComparisonGrid is null || animationOutlineGrid is null)
        {
            return;
        }

        List<RouteGroup> groups = BossRouteGroups.Build(settings).ToList();
        string signature = string.Join('\u001F', groups.Select(group => group.Key));
        if (animationGridSignature == signature && animationOutlineControls.Count > 0)
        {
            foreach (RouteGroup group in groups)
            {
                if (!animationOutlineControls.TryGetValue(group.Key, out AnimationOutlineControls? controls))
                {
                    continue;
                }

                controls.SplitComparison.Checked = GetAnimationOutlineSetting(settings.SplitCompletionSplitComparisons, group.Key);
                controls.SegmentComparison.Checked = GetAnimationOutlineSetting(settings.SplitCompletionSegmentComparisons, group.Key);
                SetOutlineStyle(controls.SplitTime, GetAnimationOutlineStyle(settings.SplitCompletionOutlineSplitStyles, settings.SplitCompletionOutlineSplitTimes, group.Key));
                SetOutlineStyle(controls.SegmentTime, GetAnimationOutlineStyle(settings.SplitCompletionOutlineSegmentStyles, settings.SplitCompletionOutlineSegmentTimes, group.Key));
            }

            return;
        }

        animationComparisonGrid.SuspendLayout();
        animationOutlineGrid.SuspendLayout();
        try
        {
            ClearGrid(animationComparisonGrid);
            ClearGrid(animationOutlineGrid);
            animationOutlineControls.Clear();
            AddHeaderRow(animationComparisonGrid, "BOSS Group", "Cumulative time", "Segment time");
            AddHeaderRow(animationOutlineGrid, "BOSS Group", "Cumulative time", "Segment time");
            foreach (RouteGroup group in groups)
            {
                var splitComparisonBox = new CheckBox
                {
                    Checked = GetAnimationOutlineSetting(settings.SplitCompletionSplitComparisons, group.Key),
                    Dock = DockStyle.Fill,
                    ForeColor = TextColor,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                UiTheme.StyleCheckBox(splitComparisonBox);

                var segmentComparisonBox = new CheckBox
                {
                    Checked = GetAnimationOutlineSetting(settings.SplitCompletionSegmentComparisons, group.Key),
                    Dock = DockStyle.Fill,
                    ForeColor = TextColor,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                UiTheme.StyleCheckBox(segmentComparisonBox);

                ComboBox splitTimeBox = CreateOutlineStyleBox(GetAnimationOutlineStyle(
                    settings.SplitCompletionOutlineSplitStyles,
                    settings.SplitCompletionOutlineSplitTimes,
                    group.Key));
                ComboBox segmentTimeBox = CreateOutlineStyleBox(GetAnimationOutlineStyle(
                    settings.SplitCompletionOutlineSegmentStyles,
                    settings.SplitCompletionOutlineSegmentTimes,
                    group.Key));

                animationOutlineControls[group.Key] = new AnimationOutlineControls(splitComparisonBox, segmentComparisonBox, splitTimeBox, segmentTimeBox);

                int comparisonRow = AddGridRow(animationComparisonGrid);
                animationComparisonGrid.Controls.Add(CreateRowLabel(BossRouteGroups.GetGroupDisplayName(group, settings)), 0, comparisonRow);
                animationComparisonGrid.Controls.Add(splitComparisonBox, 1, comparisonRow);
                animationComparisonGrid.Controls.Add(segmentComparisonBox, 2, comparisonRow);

                int outlineRow = AddGridRow(animationOutlineGrid);
                animationOutlineGrid.Controls.Add(CreateRowLabel(BossRouteGroups.GetGroupDisplayName(group, settings)), 0, outlineRow);
                animationOutlineGrid.Controls.Add(splitTimeBox, 1, outlineRow);
                animationOutlineGrid.Controls.Add(segmentTimeBox, 2, outlineRow);
            }

            animationGridSignature = signature;
        }
        finally
        {
            animationOutlineGrid.ResumeLayout(true);
            animationComparisonGrid.ResumeLayout(true);
        }
    }

    private static bool GetAnimationOutlineSetting(Dictionary<string, bool> values, string key)
    {
        return !values.TryGetValue(key, out bool enabled) || enabled;
    }

    private static string GetAnimationOutlineStyle(
        Dictionary<string, string> values,
        Dictionary<string, bool> legacyEnabled,
        string key)
    {
        if (values.TryGetValue(key, out string? style))
        {
            return SplitCompletionOutlineStyles.Normalize(style);
        }

        return legacyEnabled.TryGetValue(key, out bool enabled) && !enabled
            ? SplitCompletionOutlineStyles.None
            : SplitCompletionOutlineStyles.Rainbow;
    }

    private ComboBox CreateOutlineStyleBox(string selectedStyle)
    {
        var comboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        UiTheme.StyleComboBox(comboBox);

        foreach (string style in SplitCompletionOutlineStyles.Ids)
        {
            comboBox.Items.Add(new OutlineStyleOption(style, Localizer.Get(SplitCompletionOutlineStyles.GetDisplayName(style), settings)));
        }

        SetOutlineStyle(comboBox, selectedStyle);
        comboBox.SelectedIndexChanged += (_, _) =>
        {
            previewOutlineStyle = GetSelectedOutlineStyle(comboBox);
            outlineStylePreview.Invalidate();
        };
        return comboBox;
    }

    private static string GetSelectedOutlineStyle(ComboBox comboBox)
    {
        return comboBox.SelectedItem is OutlineStyleOption option
            ? option.Id
            : SplitCompletionOutlineStyles.None;
    }

    private static void SetOutlineStyle(ComboBox comboBox, string style)
    {
        string normalized = SplitCompletionOutlineStyles.Normalize(style);
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is OutlineStyleOption option &&
                string.Equals(option.Id, normalized, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private Control CreateOutlineStylePreview()
    {
        outlineStylePreview.Dock = DockStyle.Fill;
        outlineStylePreview.Height = 96;
        outlineStylePreview.BackColor = FieldColor;
        outlineStylePreview.Margin = new Padding(0, 10, 0, 2);
        outlineStylePreview.Paint += (_, e) => PaintOutlineStylePreview(e.Graphics, outlineStylePreview.ClientRectangle);
        UiTheme.EnableDoubleBuffering(outlineStylePreview);
        outlineStylePreviewTimer.Interval = 120;
        outlineStylePreviewTimer.Tick += (_, _) => outlineStylePreview.Invalidate();
        outlineStylePreviewTimer.Start();
        outlineStylePreview.Disposed += (_, _) => outlineStylePreviewTimer.Stop();
        return outlineStylePreview;
    }

    private Control CreateSegmentBestDeltaHighlightPreview()
    {
        segmentBestDeltaHighlightPreview.Dock = DockStyle.Fill;
        segmentBestDeltaHighlightPreview.Height = 96;
        segmentBestDeltaHighlightPreview.BackColor = FieldColor;
        segmentBestDeltaHighlightPreview.Margin = new Padding(0, 10, 0, 2);
        segmentBestDeltaHighlightPreview.Paint += (_, e) => PaintSegmentBestDeltaHighlightPreview(e.Graphics, segmentBestDeltaHighlightPreview.ClientRectangle);
        UiTheme.EnableDoubleBuffering(segmentBestDeltaHighlightPreview);
        outlineStylePreviewTimer.Tick += (_, _) => segmentBestDeltaHighlightPreview.Invalidate();
        return segmentBestDeltaHighlightPreview;
    }

    private void PaintSegmentBestDeltaHighlightPreview(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var backgroundBrush = new SolidBrush(FieldColor);
        graphics.FillRectangle(backgroundBrush, bounds);
        using var borderPen = new Pen(BorderColor);
        graphics.DrawRectangle(borderPen, 0, 0, Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1));

        using var font = UiTheme.FormFont(16f, FontStyle.Bold);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        double seconds = Environment.TickCount64 / 1000.0;
        Color[] baseColors =
        {
            ColorText.Parse(settings.Colors.DeltaAheadText, Color.FromArgb(114, 213, 114)),
            ColorText.Parse(settings.Colors.DeltaEvenText, Color.FromArgb(216, 216, 216)),
            ColorText.Parse(settings.Colors.DeltaBehindText, Color.FromArgb(240, 112, 112))
        };
        string[] texts = { "-0:01.23", "+0:00.00", "+0:01.23" };
        int columns = texts.Length;
        for (int i = 0; i < columns; i++)
        {
            var rect = new Rectangle(bounds.Left + i * bounds.Width / columns, bounds.Top, bounds.Width / columns, bounds.Height);
            Color color = SegmentBestDeltaHighlightStyles.Apply(baseColors[i], previewSegmentBestDeltaHighlightStyle, seconds);
            using var brush = new SolidBrush(color);
            graphics.DrawString(texts[i], font, brush, rect, format);
        }
    }

    private void PaintOutlineStylePreview(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var backgroundBrush = new SolidBrush(FieldColor);
        graphics.FillRectangle(backgroundBrush, bounds);
        using var borderPen = new Pen(BorderColor);
        graphics.DrawRectangle(borderPen, 0, 0, Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1));

        using var font = UiTheme.FormFont(18f, FontStyle.Bold);
        string text = "XX:XX.XX";
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near
        };
        DrawPreviewOutlinedString(
            graphics,
            text,
            font,
            Color.White,
            bounds.Left + bounds.Width / 2f,
            bounds.Top + bounds.Height / 2f,
            format,
            previewOutlineStyle,
            ParseIntBox(splitCompletionOutlineThicknessBox, 30, 0, 100));
    }

    private static void DrawPreviewOutlinedString(
        Graphics graphics,
        string text,
        Font font,
        Color fillColor,
        float centerX,
        float centerY,
        StringFormat format,
        string style,
        int thicknessPercent)
    {
        string normalized = SplitCompletionOutlineStyles.Normalize(style);
        if (normalized == SplitCompletionOutlineStyles.None)
        {
            using var textBrush = new SolidBrush(fillColor);
            SizeF size = graphics.MeasureString(text, font, Size.Empty, format);
            graphics.DrawString(text, font, textBrush, centerX - size.Width / 2f, centerY - size.Height / 2f, format);
            return;
        }

        using GraphicsPath path = CreatePreviewTextPath(graphics, text, font, 0f, 0f, format);
        CenterPath(path, centerX, centerY);
        RectangleF pathBounds = path.GetBounds();
        RectangleF gradientBounds = InflateBounds(pathBounds, Math.Max(4f, font.Size * 0.35f));
        using var outlineBrush = new LinearGradientBrush(gradientBounds, Color.White, Color.White, LinearGradientMode.Horizontal);
        Color[] colors = SplitCompletionOutlineStyles.GetColors(normalized, Environment.TickCount64 / 1000.0);
        outlineBrush.InterpolationColors = new ColorBlend
        {
            Positions = CreateColorPositions(colors.Length),
            Colors = colors
        };

        float thickness = font.Size * Math.Clamp(thicknessPercent, 0, 100) / 100f;
        using var outlinePen = new Pen(outlineBrush, Math.Max(1f, thickness))
        {
            LineJoin = LineJoin.Round
        };
        graphics.DrawPath(outlinePen, path);

        using var fillBrush = new SolidBrush(fillColor);
        graphics.FillPath(fillBrush, path);
    }

    private static GraphicsPath CreatePreviewTextPath(Graphics graphics, string text, Font font, float x, float y, StringFormat format)
    {
        var path = new GraphicsPath();
        using StringFormat pathFormat = (StringFormat)format.Clone();
        path.AddString(
            text,
            font.FontFamily,
            (int)font.Style,
            emSize: font.SizeInPoints * graphics.DpiY / 72f,
            origin: new PointF(x, y),
            format: pathFormat);
        return path;
    }

    private static void CenterPath(GraphicsPath path, float centerX, float centerY)
    {
        RectangleF bounds = path.GetBounds();
        using var matrix = new Matrix();
        matrix.Translate(centerX - (bounds.Left + bounds.Width / 2f), centerY - (bounds.Top + bounds.Height / 2f));
        path.Transform(matrix);
    }

    private static RectangleF InflateBounds(RectangleF bounds, float amount)
    {
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return new RectangleF(bounds.X - amount, bounds.Y - amount, amount * 2f + 1f, amount * 2f + 1f);
        }

        bounds.Inflate(amount, amount);
        return bounds;
    }

    private static float[] CreateColorPositions(int count)
    {
        if (count <= 1)
        {
            return new[] { 0f };
        }

        var positions = new float[count];
        for (int i = 0; i < count; i++)
        {
            positions[i] = i / (float)(count - 1);
        }

        return positions;
    }

    private void RefreshAnimationOutlineGrid()
    {
        if (animationComparisonGrid is null || animationOutlineGrid is null)
        {
            return;
        }

        SaveAnimationOutlineControls();
        PopulateAnimationOutlineGrid();
        animationComparisonGrid.PerformLayout();
        animationOutlineGrid.PerformLayout();
    }

    private void PopulateSegmentBestDeltaHighlightGrid()
    {
        if (segmentBestDeltaHighlightGrid is null)
        {
            return;
        }

        List<RouteGroup> groups = BossRouteGroups.Build(settings).ToList();
        segmentBestDeltaHighlightGrid.SuspendLayout();
        try
        {
            ClearGrid(segmentBestDeltaHighlightGrid);
            segmentBestDeltaHighlightControls.Clear();
            AddHeaderRow(segmentBestDeltaHighlightGrid, "BOSS Group", "Effect");
            foreach (RouteGroup group in groups)
            {
                ComboBox styleBox = CreateSegmentBestDeltaHighlightStyleBox(GetSegmentBestDeltaHighlightStyle(group.Key));
                segmentBestDeltaHighlightControls[group.Key] = new SegmentBestDeltaHighlightControls(styleBox);
                int row = AddGridRow(segmentBestDeltaHighlightGrid);
                segmentBestDeltaHighlightGrid.Controls.Add(CreateRowLabel(BossRouteGroups.GetGroupDisplayName(group, settings)), 0, row);
                segmentBestDeltaHighlightGrid.Controls.Add(styleBox, 1, row);
            }
        }
        finally
        {
            segmentBestDeltaHighlightGrid.ResumeLayout(true);
        }
    }

    private string GetSegmentBestDeltaHighlightStyle(string key)
    {
        return settings.SegmentBestDeltaHighlightStyles.TryGetValue(key, out string? style)
            ? SegmentBestDeltaHighlightStyles.Normalize(style)
            : SegmentBestDeltaHighlightStyles.Aurora;
    }

    private ComboBox CreateSegmentBestDeltaHighlightStyleBox(string selectedStyle)
    {
        var comboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        UiTheme.StyleComboBox(comboBox);

        foreach (string style in SegmentBestDeltaHighlightStyles.Ids)
        {
            comboBox.Items.Add(new EffectStyleOption(style, Localizer.Get(SegmentBestDeltaHighlightStyles.GetDisplayName(style), settings)));
        }

        SetEffectStyle(comboBox, selectedStyle);
        comboBox.SelectedIndexChanged += (_, _) =>
        {
            previewSegmentBestDeltaHighlightStyle = GetSelectedEffectStyle(comboBox);
            segmentBestDeltaHighlightPreview.Invalidate();
        };
        return comboBox;
    }

    private static string GetSelectedEffectStyle(ComboBox comboBox)
    {
        return comboBox.SelectedItem is EffectStyleOption option
            ? option.Id
            : SegmentBestDeltaHighlightStyles.None;
    }

    private static void SetEffectStyle(ComboBox comboBox, string style)
    {
        string normalized = SegmentBestDeltaHighlightStyles.Normalize(style);
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is EffectStyleOption option &&
                string.Equals(option.Id, normalized, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    internal void AddColorSection(TableLayoutPanel parent)
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
        ConfigureDecimalBox(textBox, value, minimum, maximum);
        return textBox;
    }

    private static void ConfigureDecimalBox(TextBox textBox, float value, decimal minimum, decimal maximum)
    {
        UiTheme.StyleTextBox(textBox);
        textBox.Dock = DockStyle.Fill;
        textBox.Text = Math.Clamp((decimal)value, minimum, maximum).ToString("0.#", CultureInfo.InvariantCulture);
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
            Title = Localizer.Get("Choose BOSS Icon", settings)
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
        EnsurePageCreated(1);
        ApplyBossPageRouteChanges();
        EnsureAllPagesCreated();

        settings.Language = languageBox.SelectedItem as string ?? "English";
        settings.PauseResumeKey = pauseKeyBox.Hotkey.ToString();
        settings.ResetKey = resetKeyBox.Hotkey.ToString();
        settings.MouseClickThroughKey = mouseClickThroughKeyBox.Hotkey.ToString();
        settings.AlwaysOnTop = alwaysOnTopBox.Checked;
        settings.PracticeMode = practiceModeBox.Checked;
        settings.AutoUpdatePersonalBestData = autoUpdatePersonalBestDataBox.Checked;
        settings.ShowSplitCompletionAnimation = showSplitCompletionAnimationBox.Checked;
        settings.ShowCurrentSplitHighlight = showCurrentSplitHighlightBox.Checked;
        settings.CurrentSplitHighlightScalePercent = ParseIntBox(currentSplitHighlightScaleBox, 112, 100, 140);
        settings.CurrentSplitDepthStrengthPercent = ParseIntBox(currentSplitDepthStrengthBox, 45, 0, 100);
        settings.ShowSegmentBestDeltaHighlight = showSegmentBestDeltaHighlightBox.Checked;
        settings.EnableDefeatedBossIconLighting = enableDefeatedBossIconLightingBox.Checked;
        settings.SplitCompletionAnimationDurationSeconds = ParseFloatBox(splitCompletionAnimationDurationBox, 4.2f, 1f, 20f);
        settings.SplitCompletionOutlineThicknessPercent = ParseIntBox(splitCompletionOutlineThicknessBox, 30, 0, 100);
        SaveReferenceTextBoxes();
        SavePersonalBestTextBoxes();
        ApplyRouteSettings();
        AppSettingsStore.Normalize(settings);
        SaveAnimationOutlineControls();
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
        settings.CurrentBossIconGrayscaleWeakenPercent = ParseIntBox(currentBossIconGrayscaleWeakenBox, 40, 0, 100);
        settings.CurrentBossIconBrightnessBoostPercent = ParseIntBox(currentBossIconBrightnessBoostBox, 35, 0, 100);

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

    private void SaveAnimationOutlineControls()
    {
        foreach ((string key, AnimationOutlineControls controls) in animationOutlineControls)
        {
            settings.SplitCompletionSplitComparisons[key] = controls.SplitComparison.Checked;
            settings.SplitCompletionSegmentComparisons[key] = controls.SegmentComparison.Checked;
            string splitStyle = GetSelectedOutlineStyle(controls.SplitTime);
            string segmentStyle = GetSelectedOutlineStyle(controls.SegmentTime);
            settings.SplitCompletionOutlineSplitStyles[key] = splitStyle;
            settings.SplitCompletionOutlineSegmentStyles[key] = segmentStyle;
            settings.SplitCompletionOutlineSplitTimes[key] = splitStyle != SplitCompletionOutlineStyles.None;
            settings.SplitCompletionOutlineSegmentTimes[key] = segmentStyle != SplitCompletionOutlineStyles.None;
        }

        foreach ((string key, SegmentBestDeltaHighlightControls controls) in segmentBestDeltaHighlightControls)
        {
            settings.SegmentBestDeltaHighlightStyles[key] = GetSelectedEffectStyle(controls.Style);
        }
    }

    private void ApplyAndNotify()
    {
        ApplyToSettings();
        PopulatePersonalBestTimeGrid();
        PopulatePersonalBestSegmentGrid();
        Applied?.Invoke(this, EventArgs.Empty);
    }

    private bool ApplyBossPageRouteChanges()
    {
        if (!bossRouteDirty)
        {
            return false;
        }

        ApplyRouteSettings();
        AppSettingsStore.Normalize(settings);
        bossRouteDirty = false;
        PopulatePersonalBestTimeGrid();
        PopulatePersonalBestSegmentGrid();
        RefreshAnimationOutlineGrid();
        PopulateSegmentBestDeltaHighlightGrid();
        return true;
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

    private sealed record AnimationOutlineControls(
        CheckBox SplitComparison,
        CheckBox SegmentComparison,
        ComboBox SplitTime,
        ComboBox SegmentTime);

    private sealed record SegmentBestDeltaHighlightControls(ComboBox Style);

    private sealed record OutlineStyleOption(string Id, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private sealed record EffectStyleOption(string Id, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private sealed class SettingsPageDescriptor
    {
        public SettingsPageDescriptor(Button nav, Func<Control> create)
        {
            Nav = nav;
            Create = create;
        }

        public Button Nav { get; }

        public Func<Control> Create { get; }

        public Control? Page { get; set; }
    }

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
        private int contentUpdateDepth;
        private bool layoutContentPending;
        private readonly Dictionary<Control, AttachedContentHandlers> attachedContentHandlers = new();

        public ThemedScrollPanel()
        {
            AutoScroll = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            MouseWheel += (_, e) => ScrollBy(e.Delta);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control is not null)
            {
                AttachContent(e.Control);
            }

            RequestLayoutContent();
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            if (e.Control is not null)
            {
                DetachContent(e.Control);
            }

            base.OnControlRemoved(e);
            RequestLayoutContent();
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
            RequestLayoutContent();
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

        public void BeginContentUpdate()
        {
            contentUpdateDepth++;
            SuspendLayout();
        }

        public void EndContentUpdate()
        {
            if (contentUpdateDepth > 0)
            {
                contentUpdateDepth--;
            }

            ResumeLayout(false);
            if (contentUpdateDepth == 0 && layoutContentPending)
            {
                layoutContentPending = false;
                LayoutContent();
            }
        }

        private void AttachContent(Control control)
        {
            if (attachedContentHandlers.ContainsKey(control))
            {
                return;
            }

            EventHandler sizeChanged = (_, _) => RequestLayoutContent();
            MouseEventHandler mouseWheel = (_, e) => ScrollBy(e.Delta);
            ControlEventHandler controlAdded = (_, e) =>
            {
                if (e.Control is not null)
                {
                    AttachContent(e.Control);
                }

                RequestLayoutContent();
            };
            ControlEventHandler controlRemoved = (_, e) =>
            {
                if (e.Control is not null)
                {
                    DetachContent(e.Control);
                }

                RequestLayoutContent();
            };

            attachedContentHandlers[control] = new AttachedContentHandlers(
                sizeChanged,
                mouseWheel,
                controlAdded,
                controlRemoved);

            control.SizeChanged += sizeChanged;
            control.MouseWheel += mouseWheel;
            control.ControlAdded += controlAdded;
            control.ControlRemoved += controlRemoved;

            foreach (Control child in control.Controls)
            {
                AttachContent(child);
            }
        }

        private void DetachContent(Control control)
        {
            foreach (Control child in control.Controls.Cast<Control>().ToArray())
            {
                DetachContent(child);
            }

            if (!attachedContentHandlers.Remove(control, out AttachedContentHandlers? handlers))
            {
                return;
            }

            control.SizeChanged -= handlers.SizeChanged;
            control.MouseWheel -= handlers.MouseWheel;
            control.ControlAdded -= handlers.ControlAdded;
            control.ControlRemoved -= handlers.ControlRemoved;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Control control in attachedContentHandlers.Keys.ToArray())
                {
                    DetachContent(control);
                }
            }

            base.Dispose(disposing);
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
            if (contentUpdateDepth > 0)
            {
                layoutContentPending = true;
                return;
            }

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

        private void RequestLayoutContent()
        {
            if (contentUpdateDepth > 0)
            {
                layoutContentPending = true;
                return;
            }

            LayoutContent();
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
            if (Controls.Count > 0)
            {
                Control content = Controls[0];
                content.Location = new Point(Padding.Left, Padding.Top - scrollOffset);
            }

            Invalidate();
        }

        private sealed record AttachedContentHandlers(
            EventHandler SizeChanged,
            MouseEventHandler MouseWheel,
            ControlEventHandler ControlAdded,
            ControlEventHandler ControlRemoved);
    }
}
