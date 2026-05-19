using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class SettingsForm : Form
{
    private const int ResizeBorder = 8;

    private readonly AppSettings settings;
    private readonly Func<RuntimePerformanceDiagnostics>? runtimeDiagnosticsProvider;
    private readonly Func<RuntimeDebugSnapshot>? runtimeDebugSnapshotProvider;
    private readonly SettingsUiFactory uiFactory;
    private readonly SettingsDialogService dialogService;
    private SettingsPageHost? pageHost;
    private bool dragging;
    private Point dragStartCursor;
    private Point dragStartLocation;

    public SettingsForm(
        AppSettings currentSettings,
        Func<RuntimePerformanceDiagnostics>? runtimeDiagnosticsProvider = null,
        Func<RuntimeDebugSnapshot>? runtimeDebugSnapshotProvider = null)
    {
        settings = AppSettingsStore.Clone(currentSettings);
        this.runtimeDiagnosticsProvider = runtimeDiagnosticsProvider;
        this.runtimeDebugSnapshotProvider = runtimeDebugSnapshotProvider;
        uiFactory = new SettingsUiFactory(Localize);
        dialogService = new SettingsDialogService(this, Localize);

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

    internal SettingsPageHost PageHost => pageHost
        ?? throw new InvalidOperationException("Settings page host has not been created.");

    public event EventHandler? Applied;

    internal RuntimePerformanceDiagnostics GetRuntimeDiagnostics()
    {
        return runtimeDiagnosticsProvider?.Invoke() ?? RuntimePerformanceDiagnostics.Empty;
    }

    internal RuntimeDebugSnapshot GetRuntimeDebugSnapshot()
    {
        return runtimeDebugSnapshotProvider?.Invoke() ?? RuntimeDebugSnapshot.Empty;
    }

    internal string Localize(string key)
    {
        return Localizer.Get(key, settings);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(UiTheme.Border);
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

    internal void ApplyForTests()
    {
        ApplyToSettings();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
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
            ForeColor = UiTheme.Text,
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
            ForeColor = UiTheme.Text,
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

    private Control CreateBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
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

        var pagePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(pagePanel);

        pageHost = new SettingsPageHost(
            this,
            settings,
            uiFactory,
            dialogService,
            GetRuntimeDiagnostics,
            GetRuntimeDebugSnapshot,
            pagePanel);
        pageHost.Register("General", new GeneralSettingsPage());
        pageHost.Register("BOSS", new BossSettingsPage());
        pageHost.Register("Data", new DataSettingsPage());
        pageHost.Register("UI", new UiSettingsPage());
        pageHost.Register("Effects", new AnimationSettingsPage());
        pageHost.Register("Automation", new AutomationSettingsPage());
        pageHost.Register("Sounds", new SoundSettingsPage());
        pageHost.Register("Colors", new ColorSettingsPage());
        pageHost.Register("Advanced", new AdvancedSettingsPage());
        pageHost.Register("Debug", new DebugSettingsPage());
        pageHost.AttachNavigation(nav);
        pageHost.Select(SettingsPageId.General);

        body.Controls.Add(nav, 0, 0);
        body.Controls.Add(pagePanel, 1, 0);
        return body;
    }

    private Control CreateFooter()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(18, 16, 18, 16),
            WrapContents = false
        };
        UiTheme.EnableDoubleBuffering(footer);

        Button okButton = uiFactory.CreateButton("OK", accent: true, minimumWidth: 150);
        okButton.DialogResult = DialogResult.OK;
        okButton.Click += (_, _) => ApplyToSettings();

        Button applyButton = uiFactory.CreateButton("Apply", accent: false, minimumWidth: 150);
        applyButton.Click += (_, _) => ApplyAndNotify();

        Button cancelButton = uiFactory.CreateButton("Cancel", accent: false, minimumWidth: 150);
        cancelButton.DialogResult = DialogResult.Cancel;

        footer.Controls.Add(okButton);
        footer.Controls.Add(applyButton);
        footer.Controls.Add(cancelButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        return footer;
    }

    private void ApplyToSettings()
    {
        PageHost.ApplyToSettings();
    }

    private void ApplyAndNotify()
    {
        ApplyToSettings();
        Applied?.Invoke(this, EventArgs.Empty);
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
}
