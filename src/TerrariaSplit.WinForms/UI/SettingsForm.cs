using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed partial class SettingsForm : Form
{
    private const int ResizeBorder = 8;

    private enum TitleBarButtonIcon
    {
        Minimize,
        Maximize,
        Restore,
        Close
    }

    private readonly AppSettings settings;
    private readonly Func<RuntimePerformanceDiagnostics>? runtimeDiagnosticsProvider;
    private readonly Func<RuntimeDebugSnapshot>? runtimeDebugSnapshotProvider;
    private readonly Func<AppSettings, int>? worldPoolCountProvider;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly SettingsUiFactory uiFactory;
    private readonly SettingsDialogService dialogService;
    private readonly ToolTip titleBarToolTip = new();
    private SettingsPageHost? pageHost;
    private Button maximizeButton = null!;
    private bool dragging;
    private Point dragStartCursor;
    private Point dragStartLocation;

    public SettingsForm(
        AppSettings currentSettings,
        Func<RuntimePerformanceDiagnostics>? runtimeDiagnosticsProvider = null,
        Func<RuntimeDebugSnapshot>? runtimeDebugSnapshotProvider = null,
        Func<AppSettings, int>? worldPoolCountProvider = null,
        SettingsMessageBoxPresenter? messageBoxPresenter = null,
        Action<IntPtr>? modalHandleChanged = null,
        ISettingsSnapshotFactory? settingsSnapshots = null)
    {
        this.settingsSnapshots = settingsSnapshots ?? new StoredSettingsSnapshotFactory();
        settings = this.settingsSnapshots.CreateSnapshot(currentSettings);
        this.runtimeDiagnosticsProvider = runtimeDiagnosticsProvider;
        this.runtimeDebugSnapshotProvider = runtimeDebugSnapshotProvider;
        this.worldPoolCountProvider = worldPoolCountProvider;
        uiFactory = new SettingsUiFactory(Localize);
        dialogService = new SettingsDialogService(this, Localize, messageBoxPresenter, modalHandleChanged);

        Text = Localizer.Get("TerrariaSplit Settings", settings);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        MinimizeBox = true;
        MaximizeBox = true;
        ClientSize = new Size(1500, 1000);
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

    internal int GetWorldPoolCount()
    {
        try
        {
            return worldPoolCountProvider?.Invoke(PageHost.CreateAppliedSnapshot()) ?? 0;
        }
        catch (Exception ex)
        {
            StaticAppLogger.Instance.Error(ex, "Settings debug page failed to read world pool count.");
            return 0;
        }
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            titleBarToolTip.Dispose();
        }

        base.Dispose(disposing);
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

        if (m.Msg != wmNcHitTest ||
            m.Result != (IntPtr)htClient ||
            WindowState == FormWindowState.Maximized)
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
        if (!TryApplyToSettings(showError: false, out string message))
        {
            throw new SettingsApplyFailedException(message);
        }
    }

    internal bool TryApplyForTests(out string message)
    {
        return TryApplyToSettings(showError: false, out message);
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
        titleBar.DoubleClick += (_, _) => ToggleMaximized();

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
        title.DoubleClick += (_, _) => ToggleMaximized();

        Button closeButton = CreateTitleBarButton(TitleBarButtonIcon.Close, "Close", danger: true);
        closeButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        maximizeButton = CreateTitleBarButton(TitleBarButtonIcon.Maximize, "Maximize");
        maximizeButton.Click += (_, _) => ToggleMaximized();
        SizeChanged += (_, _) => UpdateMaximizeButtonText();
        UpdateMaximizeButtonText();

        Button minimizeButton = CreateTitleBarButton(TitleBarButtonIcon.Minimize, "Minimize");
        minimizeButton.Click += (_, _) => WindowState = FormWindowState.Minimized;

        var windowButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 144,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        windowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48f));
        windowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48f));
        windowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48f));
        windowButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        windowButtons.Controls.Add(minimizeButton, 0, 0);
        windowButtons.Controls.Add(maximizeButton, 1, 0);
        windowButtons.Controls.Add(closeButton, 2, 0);

        titleBar.Controls.Add(title);
        titleBar.Controls.Add(windowButtons);
        return titleBar;
    }

    private Button CreateTitleBarButton(TitleBarButtonIcon icon, string accessibleName, bool danger = false)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            ForeColor = UiTheme.Text,
            BackColor = UiTheme.SurfaceRaised,
            Text = string.Empty,
            AccessibleName = accessibleName,
            Margin = Padding.Empty
        };
        button.Tag = icon;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = danger
            ? Color.FromArgb(78, 57, 57)
            : Color.FromArgb(47, 58, 64);
        button.FlatAppearance.MouseDownBackColor = danger
            ? Color.FromArgb(98, 48, 48)
            : Color.FromArgb(41, 50, 56);
        button.Paint += DrawTitleBarButtonIcon;
        titleBarToolTip.SetToolTip(button, Localize(accessibleName));
        return button;
    }

    private static void DrawTitleBarButtonIcon(object? sender, PaintEventArgs e)
    {
        if (sender is not Button button || button.Tag is not TitleBarButtonIcon icon)
        {
            return;
        }

        Color color = button.Enabled ? button.ForeColor : UiTheme.MutedText;
        int centerX = button.ClientSize.Width / 2;
        int centerY = button.ClientSize.Height / 2;
        using var pen = new Pen(color, 1.8f);
        switch (icon)
        {
            case TitleBarButtonIcon.Minimize:
                e.Graphics.DrawLine(pen, centerX - 7, centerY + 6, centerX + 7, centerY + 6);
                break;
            case TitleBarButtonIcon.Maximize:
                e.Graphics.DrawRectangle(pen, centerX - 7, centerY - 7, 14, 14);
                break;
            case TitleBarButtonIcon.Restore:
                e.Graphics.DrawRectangle(pen, centerX - 8, centerY - 3, 12, 12);
                e.Graphics.DrawRectangle(pen, centerX - 4, centerY - 7, 12, 12);
                break;
            case TitleBarButtonIcon.Close:
                e.Graphics.DrawLine(pen, centerX - 6, centerY - 6, centerX + 6, centerY + 6);
                e.Graphics.DrawLine(pen, centerX + 6, centerY - 6, centerX - 6, centerY + 6);
                break;
        }
    }

    private void ToggleMaximized()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
        UpdateMaximizeButtonText();
    }

    private void UpdateMaximizeButtonText()
    {
        if (maximizeButton is null)
        {
            return;
        }

        bool maximized = WindowState == FormWindowState.Maximized;
        maximizeButton.Tag = maximized ? TitleBarButtonIcon.Restore : TitleBarButtonIcon.Maximize;
        maximizeButton.AccessibleName = maximized ? "Restore" : "Maximize";
        titleBarToolTip.SetToolTip(maximizeButton, Localize(maximized ? "Restore" : "Maximize"));
        maximizeButton.Invalidate();
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
            settingsSnapshots,
            GetRuntimeDiagnostics,
            GetRuntimeDebugSnapshot,
            pagePanel);
        pageHost.Register("General", new GeneralSettingsPage());
        pageHost.Register("Route", new SplitSettingsPage());
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
        okButton.Click += (_, _) =>
        {
            if (!TryApplyToSettings(showError: true, out _))
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        Button applyButton = uiFactory.CreateButton("Apply", accent: false, minimumWidth: 150);
        applyButton.Click += (_, _) => ApplyAndNotify();

        Button cancelButton = uiFactory.CreateButton("Cancel", accent: false, minimumWidth: 150);
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        footer.Controls.Add(okButton);
        footer.Controls.Add(applyButton);
        footer.Controls.Add(cancelButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        return footer;
    }

    private bool TryApplyToSettings(bool showError, out string message)
    {
        message = string.Empty;
        try
        {
            PageHost.ApplyToSettings();
            return true;
        }
        catch (SettingsApplyFailedException ex)
        {
            message = ex.Message;
            if (showError)
            {
                dialogService.ShowWarning(ex.Message, Localize("TerrariaSplit Settings"));
            }

            return false;
        }
    }

    private void ApplyAndNotify()
    {
        if (!TryApplyToSettings(showError: true, out _))
        {
            return;
        }

        Applied?.Invoke(this, EventArgs.Empty);
    }

    private void BeginDrag(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || WindowState == FormWindowState.Maximized)
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
