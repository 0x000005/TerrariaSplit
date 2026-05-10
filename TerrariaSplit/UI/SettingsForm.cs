using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class SettingsForm : Form
{
    private const int ResizeBorder = 8;
    private const int RowHeight = 56;
    private const int HeaderRowHeight = 40;
    private const int GeneralPageIndex = 0;
    private const int AutoCreatePageIndex = 1;
    private const int BossPageIndex = 2;
    private const int DataPageIndex = 3;
    private const int UiPageIndex = 4;
    private const int AnimationPageIndex = 5;
    private const int SoundPageIndex = 6;
    private const int ColorPageIndex = 7;

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
    private readonly HotkeyTextBox createWorldKeyBox = new();
    private readonly CheckBox showMouseClickThroughIndicatorBox = new();
    private readonly ComboBox languageBox = new();
    private readonly CheckBox alwaysOnTopBox = new();
    private readonly CheckBox practiceModeBox = new();
    private readonly TextBox autoCreatePlayerNameBox = new();
    private readonly TextBox autoCreatePlayerTemplateCodeBox = new();
    private readonly ComboBox autoCreatePlayerDifficultyBox = new();
    private readonly ComboBox autoCreateWorldSizeBox = new();
    private readonly ComboBox autoCreateWorldDifficultyBox = new();
    private readonly ComboBox autoCreateWorldEvilBox = new();
    private readonly TextBox autoCreateShortActionDelayBox = new();
    private readonly TextBox autoCreateMenuActionDelayBox = new();
    private readonly TextBox autoCreateWindowActivationDelayBox = new();
    private readonly TextBox autoCreateClickFocusDelayBox = new();
    private readonly TextBox autoCreateInputPressDurationBox = new();
    private readonly ComboBox referenceSetBox = new();
    private readonly TextBox newReferenceSetNameBox = new();
    private readonly ComboBox personalBestTimeSetBox = new();
    private readonly ComboBox personalBestSegmentSetBox = new();
    private readonly CheckBox autoUpdatePersonalBestDataBox = new();
    private readonly CheckBox askBeforeUpdatingPersonalBestDataBox = new();
    private readonly CheckBox showSplitCompletionAnimationBox = new();
    private readonly CheckBox showCurrentSplitHighlightBox = new();
    private readonly TextBox currentSplitHighlightScaleBox = new();
    private readonly TextBox currentSplitDepthStrengthBox = new();
    private readonly CheckBox showEarlyDeltaTimeBox = new();
    private readonly TextBox earlyDeltaTimeSecondsBox = new();
    private readonly CheckBox enableDynamicDeltaTimeUnitsBox = new();
    private readonly CheckBox enableDeltaGradientColorBox = new();
    private readonly CheckBox enableTimerGradientColorBox = new();
    private readonly TextBox deltaGradientThresholdBox = new();
    private readonly ComboBox deltaGradientCurveBox = new();
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
    private readonly Dictionary<string, TextBox> soundTextBoxes = new();
    private readonly Dictionary<string, ColumnControls> columnControls = new();
    private readonly Dictionary<string, FontControls> fontControls = new();
    private readonly Dictionary<string, AnimationOutlineControls> animationOutlineControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SegmentBestDeltaHighlightControls> segmentBestDeltaHighlightControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Panel outlineStylePreview = new();
    private readonly Panel segmentBestDeltaHighlightPreview = new();
    private readonly Panel deltaGradientPreview = new();
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

    internal string Localize(string key)
    {
        return Localizer.Get(key, settings);
    }

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
        AddSettingsPage("General", new GeneralSettingsPage());
        AddSettingsPage("Create World", new AutoCreateSettingsPage());
        AddSettingsPage("BOSS", new BossSettingsPage());
        AddSettingsPage("Data", new DataSettingsPage());
        AddSettingsPage("UI", new UiSettingsPage());
        AddSettingsPage("Effects", new AnimationSettingsPage());
        AddSettingsPage("Sounds", new SoundSettingsPage());
        AddSettingsPage("Colors", new ColorSettingsPage());
        AddSettingsPage("Debug", new DelegateSettingsPage(context => DebugSettingsPage.Build(context.Owner)));

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
            if (selectedPageIndex == 2 && index != selectedPageIndex)
            {
                refreshedAnimation = ApplyBossPageRouteChanges();
            }

            Control selectedPage = EnsurePageCreated(index);

            if (index == 5 && !refreshedAnimation)
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
            Control page = descriptor.PageDefinition.Build(new SettingsPageContext(this));
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

    private void ApplyPage(int index)
    {
        pages[index].PageDefinition.Apply(settings);
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

    private void AddSettingsPage(string title, ISettingsPage pageDefinition)
    {
        pages.Add(new SettingsPageDescriptor(
            CreateNavButton(title),
            pageDefinition));
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
        textBox.TextChanged += (_, _) =>
        {
            UpdateColorButton(pickButton, textBox.Text);
            InvalidateDeltaGradientPreview();
        };

        int row = AddGridRow(grid);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(textBox, 1, row);
        grid.Controls.Add(pickButton, 2, row);
    }

    private void AddSoundRow(TableLayoutPanel grid, string label, string key, string value)
    {
        TextBox textBox = CreateTextBox(value);
        soundTextBoxes[key] = textBox;

        Button browseButton = CreateSmallButton("Browse");
        browseButton.Click += (_, _) => PickSound(textBox);

        Button clearButton = CreateSmallButton("Clear");
        clearButton.Click += (_, _) => textBox.Text = string.Empty;

        int row = AddGridRow(grid);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(textBox, 1, row);
        grid.Controls.Add(browseButton, 2, row);
        grid.Controls.Add(clearButton, 3, row);
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
            Font = UiTheme.FormFont(10f, FontStyle.Bold),
            ForeColor = TextColor,
            Margin = new Padding(0, 14, 0, 8),
            Text = Localizer.Get(text, settings)
        };
    }

    private Label CreateFieldLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = UiTheme.FormFont(),
            ForeColor = TextColor,
            Margin = new Padding(0, 14, 0, 8),
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
  private static void ConfigurePersonalSetBox(
        ComboBox comboBox,
        IEnumerable<ReferenceSplitSet> sets,
        string activeName,
        EventHandler selectionChanged)
    {
        comboBox.Dock = DockStyle.Fill;
        UiTheme.StyleComboBox(comboBox);
        comboBox.Items.Clear();

        foreach (ReferenceSplitSet set in sets)
        {
            comboBox.Items.Add(set.Name);
        }

        comboBox.SelectedItem = activeName;
        comboBox.SelectedIndexChanged += selectionChanged;
    }

    private void ConfigureOptionBox(ComboBox comboBox, IEnumerable<string> options, string selected)
    {
        comboBox.Dock = DockStyle.Fill;
        UiTheme.StyleComboBox(comboBox);
        comboBox.Items.Clear();

        foreach (string option in options)
        {
            comboBox.Items.Add(new LocalizedOption(option, Localizer.Get(option, settings)));
        }

        comboBox.SelectedItem = comboBox.Items
            .Cast<LocalizedOption>()
            .FirstOrDefault(option => string.Equals(option.Value, selected, StringComparison.OrdinalIgnoreCase));
        if (comboBox.SelectedIndex < 0 && comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private static string GetSelectedOption(ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem switch
        {
            LocalizedOption option => option.Value,
            string value => value,
            _ => fallback
        };
    }

    private static void ConfigureNumberBox(TextBox textBox, int selected, int minimum, int maximum)
    {
        UiTheme.StyleTextBox(textBox);
        textBox.Dock = DockStyle.Fill;
        textBox.Text = Math.Clamp(selected, minimum, maximum).ToString(CultureInfo.InvariantCulture);
    }

    private static void ConfigureTimeBox(TextBox textBox, int selectedSeconds, int minimumSeconds, int maximumSeconds)
    {
        UiTheme.StyleTextBox(textBox);
        textBox.Dock = DockStyle.Fill;
        textBox.Text = TimeText.FormatSplit(TimeSpan.FromSeconds(Math.Clamp(selectedSeconds, minimumSeconds, maximumSeconds)));
        textBox.PlaceholderText = "m:ss or h:mm:ss";
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

    private Button CreateSmallButton(string text)
    {
        var button = new Button
        {
            Height = 36,
            Margin = new Padding(8, 8, 0, 8),
            Text = Localizer.Get(text, settings),
            Width = 136
        };
        UiTheme.StyleButton(button, accent: false, minimumWidth: 132);
        button.MinimumSize = new Size(132, 36);
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
        bool transparent = color.A == 0;
        Color previewColor = transparent ? FieldColor : Color.FromArgb(color.R, color.G, color.B);
        button.Text = transparent ? "T" : string.Empty;
        button.ForeColor = TextColor;
        button.BackColor = previewColor;
        button.FlatAppearance.MouseDownBackColor = previewColor;
        button.FlatAppearance.MouseOverBackColor = previewColor;
    }

    private void PickColor(TextBox textBox)
    {
        Color currentColor = ColorText.Parse(textBox.Text, Color.White);
        if (currentColor.A == 0)
        {
            currentColor = Color.White;
        }

        using var dialog = new ColorDialog
        {
            Color = currentColor,
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

    private void PickSound(TextBox textBox)
    {
        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "Wave audio|*.wav|All files|*.*",
            Title = Localizer.Get("Choose sound", settings)
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            textBox.Text = dialog.FileName;
        }
    }
void ApplyToSettings()
    {
        EnsurePageCreated(BossPageIndex);
        ApplyBossPageRouteChanges();
        EnsureAllPagesCreated();

        ApplyPage(GeneralPageIndex);
        ApplyPage(AutoCreatePageIndex);
        ApplyPage(DataPageIndex);
        ApplyPage(BossPageIndex);
        AppSettingsStore.Normalize(settings);
        ApplyPage(AnimationPageIndex);
        AppSettingsStore.Normalize(settings);
        ApplyPage(UiPageIndex);
        ApplyPage(ColorPageIndex);
        ApplyPage(SoundPageIndex);
    }

    internal void ApplyGeneralSettings(AppSettings targetSettings)
    {
        targetSettings.Language = languageBox.SelectedItem as string ?? LanguageNames.English;
        targetSettings.PauseResumeKey = pauseKeyBox.Hotkey.ToString();
        targetSettings.ResetKey = resetKeyBox.Hotkey.ToString();
        targetSettings.MouseClickThroughKey = mouseClickThroughKeyBox.Hotkey.ToString();
        targetSettings.CreateWorldKey = createWorldKeyBox.Hotkey.ToString();
        targetSettings.ShowMouseClickThroughIndicator = showMouseClickThroughIndicatorBox.Checked;
        targetSettings.AlwaysOnTop = alwaysOnTopBox.Checked;
        targetSettings.PracticeMode = practiceModeBox.Checked;
    }

    internal void ApplyAutoCreateSettings(AppSettings targetSettings)
    {
        targetSettings.AutoCreate.PlayerName = autoCreatePlayerNameBox.Text.Trim();
        targetSettings.AutoCreate.PlayerTemplateCode = autoCreatePlayerTemplateCodeBox.Text.Trim();
        targetSettings.AutoCreate.PlayerDifficulty = AutoCreatePlayerDifficulty.Normalize(
            GetSelectedOption(autoCreatePlayerDifficultyBox, AutoCreatePlayerDifficulty.Softcore));
        targetSettings.AutoCreate.WorldSize = AutoCreateWorldSize.Normalize(
            GetSelectedOption(autoCreateWorldSizeBox, AutoCreateWorldSize.Medium));
        targetSettings.AutoCreate.WorldDifficulty = AutoCreateWorldDifficulty.Normalize(
            GetSelectedOption(autoCreateWorldDifficultyBox, AutoCreateWorldDifficulty.Classic));
        targetSettings.AutoCreate.WorldEvil = AutoCreateWorldEvil.Normalize(
            GetSelectedOption(autoCreateWorldEvilBox, AutoCreateWorldEvil.Random));
        targetSettings.AutoCreate.ShortActionDelayMilliseconds = ParseIntBox(
            autoCreateShortActionDelayBox,
            AutoCreateWorldSettings.DefaultShortActionDelayMilliseconds,
            0,
            5000);
        targetSettings.AutoCreate.MenuActionDelayMilliseconds = ParseIntBox(
            autoCreateMenuActionDelayBox,
            AutoCreateWorldSettings.DefaultMenuActionDelayMilliseconds,
            0,
            5000);
        targetSettings.AutoCreate.WindowActivationDelayMilliseconds = ParseIntBox(
            autoCreateWindowActivationDelayBox,
            AutoCreateWorldSettings.DefaultWindowActivationDelayMilliseconds,
            0,
            5000);
        targetSettings.AutoCreate.ClickFocusDelayMilliseconds = ParseIntBox(
            autoCreateClickFocusDelayBox,
            AutoCreateWorldSettings.DefaultClickFocusDelayMilliseconds,
            0,
            5000);
        targetSettings.AutoCreate.InputPressDurationMilliseconds = ParseIntBox(
            autoCreateInputPressDurationBox,
            AutoCreateWorldSettings.DefaultInputPressDurationMilliseconds,
            1,
            5000);
    }

    internal void ApplyBossSettings(AppSettings targetSettings)
    {
        ApplyRouteSettings();

        foreach ((string name, TextBox textBox) in bossIconTextBoxes)
        {
            targetSettings.SetBossIconPath(name, textBox.Text.Trim());
        }
    }

    internal void ApplyDataSettings(AppSettings targetSettings)
    {
        targetSettings.AutoUpdatePersonalBestData = autoUpdatePersonalBestDataBox.Checked;
        targetSettings.AskBeforeUpdatingPersonalBestData = askBeforeUpdatingPersonalBestDataBox.Checked;
        SaveReferenceTextBoxes();
        SavePersonalBestTextBoxes();

        targetSettings.ActiveReferenceSplitSet = referenceSetBox.SelectedItem is string selectedReferenceSet
            ? selectedReferenceSet
            : targetSettings.GetActiveReferenceSet().Name;
        targetSettings.ActivePersonalBestTimeSet = personalBestTimeSetBox.SelectedItem is string selectedPersonalBestTimeSet
            ? selectedPersonalBestTimeSet
            : targetSettings.GetActivePersonalBestTimeSet().Name;
        targetSettings.ActivePersonalBestSegmentSet = personalBestSegmentSetBox.SelectedItem is string selectedPersonalBestSegmentSet
            ? selectedPersonalBestSegmentSet
            : targetSettings.GetActivePersonalBestSegmentSet().Name;
    }

    internal void ApplyUiSettings(AppSettings targetSettings)
    {
        ApplyColumnSettings("Icon", targetSettings.Columns.Icon);
        ApplyColumnSettings("Time", targetSettings.Columns.Time);
        ApplyColumnSettings("Delta", targetSettings.Columns.Delta);
        ApplyFontSettings("Timer", targetSettings.Columns.Timer);
        ApplyFontSettings("TimerMilliseconds", targetSettings.Columns.TimerMilliseconds);

        targetSettings.Columns.ScalePercent = ParseIntBox(globalScaleBox, 100, 25, 300);
        targetSettings.Columns.TimerOffsetX = ParseIntBox(timerOffsetXBox, 0, -2000, 2000);
        targetSettings.Columns.TimerOffsetY = ParseIntBox(timerOffsetYBox, 0, -2000, 2000);

        targetSettings.UndefeatedIconGrayscalePercent = ParseIntBox(undefeatedIconGrayscaleBox, 80, 0, 100);
        targetSettings.UndefeatedIconBrightnessPercent = ParseIntBox(undefeatedIconBrightnessBox, 40, 0, 100);
        targetSettings.CurrentBossIconGrayscaleWeakenPercent = ParseIntBox(currentBossIconGrayscaleWeakenBox, 40, 0, 100);
        targetSettings.CurrentBossIconBrightnessBoostPercent = ParseIntBox(currentBossIconBrightnessBoostBox, 35, 0, 100);
    }

    internal void ApplyAnimationSettings(AppSettings targetSettings)
    {
        targetSettings.ShowSplitCompletionAnimation = showSplitCompletionAnimationBox.Checked;
        targetSettings.ShowCurrentSplitHighlight = showCurrentSplitHighlightBox.Checked;
        targetSettings.CurrentSplitHighlightScalePercent = ParseIntBox(currentSplitHighlightScaleBox, 112, 100, 140);
        targetSettings.CurrentSplitDepthStrengthPercent = ParseIntBox(currentSplitDepthStrengthBox, 45, 0, 100);
        targetSettings.ShowEarlyDeltaTime = showEarlyDeltaTimeBox.Checked;
        targetSettings.EarlyDeltaTimeSeconds = ParseIntBox(earlyDeltaTimeSecondsBox, 60, 0, 3600);
        targetSettings.EnableDynamicDeltaTimeUnits = enableDynamicDeltaTimeUnitsBox.Checked;
        targetSettings.EnableDeltaGradientColor = enableDeltaGradientColorBox.Checked;
        targetSettings.EnableTimerGradientColor = enableTimerGradientColorBox.Checked;
        targetSettings.DeltaGradientThresholdSeconds = ParseTimeBox(deltaGradientThresholdBox, 120, 1, 3600);
        targetSettings.DeltaGradientCurve = GetSelectedDeltaGradientCurve(deltaGradientCurveBox);
        targetSettings.ShowSegmentBestDeltaHighlight = showSegmentBestDeltaHighlightBox.Checked;
        targetSettings.EnableDefeatedBossIconLighting = enableDefeatedBossIconLightingBox.Checked;
        targetSettings.SplitCompletionAnimationDurationSeconds = ParseFloatBox(splitCompletionAnimationDurationBox, 4.2f, 2f, 20f);
        targetSettings.SplitCompletionOutlineThicknessPercent = ParseIntBox(splitCompletionOutlineThicknessBox, 30, 0, 100);
        SaveAnimationOutlineControls();
    }

    internal void ApplyColorSettings(AppSettings targetSettings)
    {
        SetColor(nameof(targetSettings.Colors.ReferenceText), value => targetSettings.Colors.ReferenceText = value);
        SetColor(nameof(targetSettings.Colors.ActiveReferenceText), value => targetSettings.Colors.ActiveReferenceText = value);
        SetColor(nameof(targetSettings.Colors.SplitText), value => targetSettings.Colors.SplitText = value);
        SetColor(nameof(targetSettings.Colors.DeltaAheadText), value => targetSettings.Colors.DeltaAheadText = value);
        SetColor(nameof(targetSettings.Colors.DeltaBehindText), value => targetSettings.Colors.DeltaBehindText = value);
        SetColor(nameof(targetSettings.Colors.TimerText), value => targetSettings.Colors.TimerText = value);
        SetColor(nameof(targetSettings.Colors.TimerAheadText), value => targetSettings.Colors.TimerAheadText = value);
        SetColor(nameof(targetSettings.Colors.TimerBehindText), value => targetSettings.Colors.TimerBehindText = value);
        SetColor(nameof(targetSettings.Colors.TimerRecordText), value => targetSettings.Colors.TimerRecordText = value);
        SetColor(nameof(targetSettings.Colors.TimerNoRecordText), value => targetSettings.Colors.TimerNoRecordText = value);
        SetColor(nameof(targetSettings.Colors.TimerPausedText), value => targetSettings.Colors.TimerPausedText = value);
    }

    internal void ApplySoundSettings(AppSettings targetSettings)
    {
        SetSound(nameof(targetSettings.Sounds.Pause), value => targetSettings.Sounds.Pause = value);
        SetSound(nameof(targetSettings.Sounds.Reset), value => targetSettings.Sounds.Reset = value);
        SetSound(nameof(targetSettings.Sounds.SplitBehindReferenceBehindSegment), value => targetSettings.Sounds.SplitBehindReferenceBehindSegment = value);
        SetSound(nameof(targetSettings.Sounds.SplitBehindReferenceAheadSegment), value => targetSettings.Sounds.SplitBehindReferenceAheadSegment = value);
        SetSound(nameof(targetSettings.Sounds.SplitAheadReferenceBehindSegment), value => targetSettings.Sounds.SplitAheadReferenceBehindSegment = value);
        SetSound(nameof(targetSettings.Sounds.SplitAheadReferenceAheadSegment), value => targetSettings.Sounds.SplitAheadReferenceAheadSegment = value);
    }
    private void ApplyAndNotify()
    {
        ApplyToSettings();
        PopulatePersonalBestTimeGrid();
        PopulatePersonalBestSegmentGrid();
        Applied?.Invoke(this, EventArgs.Empty);
    }
  private void SetColor(string key, Action<string> setter)
    {
        if (colorTextBoxes.TryGetValue(key, out TextBox? textBox))
        {
            setter(ColorText.Format(ColorText.Parse(textBox.Text, Color.White)));
        }
    }

    private void SetSound(string key, Action<string> setter)
    {
        if (soundTextBoxes.TryGetValue(key, out TextBox? textBox))
        {
            setter(textBox.Text.Trim());
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

    private static int ParseTimeBox(TextBox textBox, int fallbackSeconds, int minimumSeconds, int maximumSeconds)
    {
        return TimeText.TryParse(textBox.Text, out TimeSpan value)
            ? Math.Clamp((int)Math.Round(value.TotalSeconds), minimumSeconds, maximumSeconds)
            : fallbackSeconds;
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

    private sealed record LocalizedOption(string Value, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

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
        public SettingsPageDescriptor(Button nav, ISettingsPage pageDefinition)
        {
            Nav = nav;
            PageDefinition = pageDefinition;
        }

        public Button Nav { get; }

        public ISettingsPage PageDefinition { get; }

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
            MouseEventHandler mouseWheel = (_, e) =>
            {
                if (ShouldChildHandleMouseWheel(control))
                {
                    return;
                }

                ScrollBy(e.Delta);
            };
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

        private static bool ShouldChildHandleMouseWheel(Control control)
        {
            return control is TextBox textBox &&
                textBox.Multiline &&
                textBox.ScrollBars != ScrollBars.None;
        }

        private sealed record AttachedContentHandlers(
            EventHandler SizeChanged,
            MouseEventHandler MouseWheel,
            ControlEventHandler ControlAdded,
            ControlEventHandler ControlRemoved);
    }
}

