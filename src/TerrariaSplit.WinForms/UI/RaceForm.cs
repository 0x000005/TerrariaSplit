using TerrariaSplit.Configuration;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TerrariaSplit.Race.Client;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.UI.Rendering;
using TerrariaSplit.UI.Settings;

namespace TerrariaSplit.UI;

internal sealed class RaceForm : Form
{
    private const int ResizeBorder = 8;
    private const float LeaderboardVisualColumnWidthScale = 1.5f;
    private const float RaceSettingsLabelColumnWidth = 280f;
    private const float RaceSettingsCompactLabelColumnWidth = 220f;
    private const float RaceSettingsValueColumnWidth = 720f;
    private const float RaceWorldFileBrowseColumnWidth = 304f;
    private const float StatusPlayerListHeaderHeight = 64f;
    private const float StatusPlayerListRowHeight = 64f;
    private const float CheatActivationButtonPercent = 20f;
    private const float CheatActivationSpacerPercent = 10f;
    private const float CheatOptionButtonsPercent = 70f;
    private const float CheatSelectorRowHeight = 54f;
    private const int CheatSelectorHorizontalGap = 8;
    private const int CheatSelectorGap = 10;
    private static readonly Color SelectorButtonHover = Color.FromArgb(40, 48, 53);
    private static readonly Color SelectorButtonSelectedHover = Color.FromArgb(58, 93, 88);
    private static readonly Color SelectorButtonDown = Color.FromArgb(34, 41, 46);
    private static readonly Color SelectorButtonSelectedDown = Color.FromArgb(46, 76, 71);

    private enum TitleBarButtonIcon
    {
        Minimize,
        Maximize,
        Restore,
        Close
    }

    private enum RaceSettingsPage
    {
        Connection,
        Interface,
        Colors
    }

    private enum HostWorldActionButtonState
    {
        Idle,
        Running,
        Cancelling
    }

    private readonly IRacePanelShell shell;
    private readonly SettingsUiFactory uiFactory;
    private readonly SettingsDialogService dialogs;
    private readonly TextBox serverBox;
    private readonly TextBox nicknameBox;
    private readonly TextBox roomCodeBox;
    private readonly TextBox seedBox;
    private readonly TextBox randomSecretSeedsBox;
    private readonly TextBox worldPathBox;
    private readonly ThemedDropDownList sizeBox;
    private readonly ThemedDropDownList difficultyBox;
    private readonly ThemedDropDownList evilBox;
    private readonly CheckBox hostRoleButton;
    private readonly CheckBox memberRoleButton;
    private readonly CheckBox randomWorldSourceButton;
    private readonly CheckBox customSeedWorldSourceButton;
    private readonly CheckBox existingWorldFileSourceButton;
    private readonly CheckBox cheatsEnabledBox;
    private readonly CheckBox pyramidEnabledBox;
    private readonly CheckBox crimsonEnabledBox;
    private readonly Dictionary<string, CheckBox> specialSeedButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CheckBox> pyramidItemButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CheckBox> crimsonDistanceButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CheckBox> resourceItemButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, CheckBox> lifeCrystalMinimumButtons = new();
    private readonly Dictionary<string, CheckBox> hookMinimumButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, CheckBox> spelunkerMinimumButtons = new();
    private readonly Dictionary<int, CheckBox> featherfallMinimumButtons = new();
    private readonly ToolTip titleBarToolTip = new();
    private readonly RaceLeaderboardSettings leaderboardSettings;
    private readonly Dictionary<string, LeaderboardColumnControls> leaderboardColumnControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LeaderboardColorControls> leaderboardColorControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<RaceSettingsPage, Button> raceSettingsNavButtons = new();
    private readonly Dictionary<RaceSettingsPage, Control> raceSettingsPages = new();
    private CheckBox? useRankColorForMainTimerBox;
    private LeaderboardRankGradientControls? leaderboardRankGradientControls;
    private ThemedScrollPanel? settingsScrollPanel;
    private Panel raceSettingsPagePanel = null!;
    private Control hostSection = null!;
    private Control raceStatusSection = null!;
    private Control memberSection = null!;
    private Control randomWorldConfig = null!;
    private Control customSeedWorldConfig = null!;
    private Control existingWorldFileConfig = null!;
    private Button maximizeButton = null!;
    private Button? hostWorldActionButton;
    private Button? roomLifecycleButton;
    private RaceActionProgressBar? hostWorldProgressBar;
    private Label? statusRouteOverrideHint;
    private Label? statusWorldNameValue;
    private TableLayoutPanel? statusPlayerList;
    private CancellationTokenSource? hostWorldActionCancellation;
    private RacePanelWorldSource hostWorldActionSource;
    private string? hostWorldActionWorldPath;
    private bool hostWorldActionCancelRequested;
    private bool hostWorldUploaded;
    private int hostWorldProgressVersion;
    private RacePanelRole selectedRole;
    private RacePanelWorldSource selectedWorldSource = RacePanelWorldSource.Random;
    private int programmaticUpdateDepth;
    private bool updatingCrimsonDistanceSelection;
    private bool updatingResourceMinimumSelection;
    private bool dragging;
    private Point dragStartCursor;
    private Point dragStartLocation;

    public RaceForm(IRacePanelShell shell)
    {
        this.shell = shell;
        RacePanelDraftState draftState = shell.DraftState;
        leaderboardSettings = CloneLeaderboardSettings(shell.LeaderboardSettings);
        uiFactory = new SettingsUiFactory(shell.Localize);
        dialogs = new SettingsDialogService(this, shell.Localize);
        selectedRole = draftState.Role;
        selectedWorldSource = draftState.WorldSource;
        serverBox = CreateTextBox(draftState.ServerUrl);
        nicknameBox = CreateTextBox(draftState.Nickname);
        roomCodeBox = CreateTextBox(draftState.RoomCode);
        seedBox = CreateTextBox(draftState.SeedText);
        randomSecretSeedsBox = CreateTextBox(string.Empty);
        worldPathBox = CreateTextBox(draftState.LocalWorldPath);
        worldPathBox.ReadOnly = true;
        hostRoleButton = CreateRoleButton("Host", RacePanelRole.Host, selected: selectedRole == RacePanelRole.Host);
        memberRoleButton = CreateRoleButton("Member", RacePanelRole.Member, selected: selectedRole == RacePanelRole.Member);
        sizeBox = CreateDropDown(
            ("Small", 1),
            ("Medium", 2),
            ("Large", 3));
        difficultyBox = CreateDropDown(
            ("Journey", 4),
            ("Classic", 1),
            ("Expert", 2),
            ("Master", 3));
        difficultyBox.SelectedIndex = 1;
        evilBox = CreateDropDown(
            ("Corruption", 1),
            ("Crimson", 2));
        evilBox.SelectedIndex = 1;
        randomWorldSourceButton = CreateWorldSourceButton(
            "Generate random world",
            RacePanelWorldSource.Random,
            selected: selectedWorldSource == RacePanelWorldSource.Random);
        customSeedWorldSourceButton = CreateWorldSourceButton(
            "Generate custom seed world",
            RacePanelWorldSource.CustomSeed,
            selected: selectedWorldSource == RacePanelWorldSource.CustomSeed);
        existingWorldFileSourceButton = CreateWorldSourceButton(
            "Directly use world file",
            RacePanelWorldSource.ExistingFile,
            selected: selectedWorldSource == RacePanelWorldSource.ExistingFile);
        cheatsEnabledBox = uiFactory.CreateCheckBox(selected: false);
        cheatsEnabledBox.CheckedChanged += (_, _) => UpdateCheatAvailability();
        pyramidEnabledBox = CreateSelectorButton("Pyramid", selected: false);
        pyramidEnabledBox.CheckedChanged += (_, _) => UpdateCheatAvailability();
        crimsonEnabledBox = CreateSelectorButton("Crimson", selected: false);
        crimsonEnabledBox.CheckedChanged += (_, _) => UpdateCheatAvailability();
        foreach (string item in AutoCreatePyramidFilterItem.All)
        {
            bool selected = (AutoCreatePyramidFilterItem.Mask(item) &
                (AutoCreatePyramidFilterItem.SandstormInABottleMask | AutoCreatePyramidFilterItem.FlyingCarpetMask)) != 0;
            CheckBox button = CreatePyramidItemButton(item, selected);
            pyramidItemButtons[item] = button;
        }

        foreach (string distance in AutoCreateCrimsonDistance.All)
        {
            CheckBox button = CreateSelectorButton(
                distance,
                AutoCreateCrimsonDistance.Includes(AutoCreateCrimsonDistance.Default, distance));
            button.CheckedChanged += (_, _) => SelectCrimsonDistance(distance);
            crimsonDistanceButtons[distance] = button;
        }

        foreach (string item in AutoCreateResourceFilterItem.All)
        {
            CheckBox button = CreatePyramidItemButton(item, selected: false);
            resourceItemButtons[item] = button;
        }

        InitializeMinimumButtons(AutoCreateResourceMinimum.LifeCrystals, lifeCrystalMinimumButtons, "Life Crystal");
        InitializeHookMinimumButtons();
        InitializeMinimumButtons(AutoCreateResourceMinimum.Potions, spelunkerMinimumButtons, "Spelunker Potion");
        InitializeMinimumButtons(AutoCreateResourceMinimum.Potions, featherfallMinimumButtons, "Featherfall Potion");
        sizeBox.SelectedIndexChanged += (_, _) => UpdateCheatAvailability();
        evilBox.SelectedIndexChanged += (_, _) => UpdateCheatAvailability();

        foreach (string seed in AutoCreateSpecialWorldSeed.All)
        {
            CheckBox button = CreateSpecialSeedButton(seed, selected: false);
            specialSeedButtons[seed] = button;
        }

        ApplyWorldSettings(shell.State?.WorldSettings);

        Text = Localize("Race");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        MinimizeBox = true;
        MaximizeBox = true;
        Padding = new Padding(1);
        UiTheme.ConfigureForm(this, new Size(760, 520));

        BuildUi();
        UiDpiScale.ApplyBase200ClientLayout(this, new Size(1800, 1000), new Size(760, 520));
        UpdateRaceState(shell.State);
    }

    public void UpdateRaceState(RaceRoomState? state)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateRaceState(state)));
            return;
        }

        RunProgrammaticUpdate(() =>
        {
            if (state?.WorldSettings is RaceWorldSettings worldSettings)
            {
                ApplyWorldSettings(worldSettings);
            }

            if (state?.Seed is RaceSeedAssignment seed && !seedBox.Focused)
            {
                seedBox.Text = seed.SeedText;
            }

            if (state is not null && !roomCodeBox.Focused)
            {
                roomCodeBox.Text = state.RoomCode;
            }

            if (!worldPathBox.Focused && !string.IsNullOrWhiteSpace(shell.LocalWorldPath))
            {
                worldPathBox.Text = shell.LocalWorldPath;
            }

            hostWorldUploaded = state?.WorldFile is not null && state.Status != RaceRoomStatus.Closed;
            RefreshStatusWorldName(state);
            RefreshStatusRouteOverrideHint(state);
            RefreshStatusPlayerList(state);
            UpdateRoleVisibility(persist: false);
            UpdateRoomLifecycleButton();
            UpdateConnectionInputLockState();
            UpdateHostWorldActionButton();
        });
    }

    private bool IsProgrammaticUpdate => programmaticUpdateDepth > 0;

    private void RunProgrammaticUpdate(Action action)
    {
        programmaticUpdateDepth++;
        try
        {
            RunSettingsLayoutBatch(action);
        }
        finally
        {
            programmaticUpdateDepth--;
        }
    }

    private void RunSettingsLayoutBatch(Action action)
    {
        settingsScrollPanel?.BeginContentUpdate();
        SuspendLayout();
        try
        {
            action();
        }
        finally
        {
            ResumeLayout(false);
            settingsScrollPanel?.EndContentUpdate();
        }
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
            hostWorldActionCancellation?.Cancel();
            _ = shell.CancelWorldGenerationAsync();
            hostWorldActionCancellation?.Dispose();
            titleBarToolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        PersistDraftState();
        base.OnFormClosing(e);
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

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        Controls.Add(root);

        root.Controls.Add(CreateTitleBar(), 0, 0);
        root.Controls.Add(CreateBody(), 0, 1);
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
            AutoEllipsis = true,
            ForeColor = UiTheme.Text,
            Font = UiTheme.FormFont(11.5f, FontStyle.Bold),
            Text = Localize("Race"),
            TextAlign = ContentAlignment.MiddleLeft
        };
        title.MouseDown += (_, e) => BeginDrag(e);
        title.MouseMove += (_, _) => ContinueDrag();
        title.MouseUp += (_, e) => EndDrag(e);
        title.DoubleClick += (_, _) => ToggleMaximized();

        Button closeButton = CreateTitleBarButton(TitleBarButtonIcon.Close, "Close", danger: true);
        closeButton.Click += (_, _) => Close();

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
            Margin = Padding.Empty,
            Tag = icon
        };
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

    private Control CreateBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
            ColumnCount = 1,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(body);
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        Control settingsBody = CreateSettingsBody();
        body.Controls.Add(settingsBody, 0, 0);
        return body;
    }

    private Control CreateSettingsBody()
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

        raceSettingsPagePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(raceSettingsPagePanel);

        AddRaceSettingsPage(nav, RaceSettingsPage.Connection, "Connection", CreateConnectionPage());
        AddRaceSettingsPage(nav, RaceSettingsPage.Interface, "UI", CreateInterfacePage());
        AddRaceSettingsPage(nav, RaceSettingsPage.Colors, "Colors", CreateColorsPage());
        SelectRaceSettingsPage(RaceSettingsPage.Connection);

        body.Controls.Add(nav, 0, 0);
        body.Controls.Add(raceSettingsPagePanel, 1, 0);
        RunSettingsLayoutBatch(() => UpdateRoleVisibility());
        return body;
    }

    private Control CreateConnectionPage()
    {
        Control page = uiFactory.BuildScrollPage(content =>
        {
            SettingsUiFactory.AddSection(content, CreateConnectionSection());
            hostSection = CreateHostSection();
            raceStatusSection = CreateRaceStatusSection();
            memberSection = CreateMemberSection();
            SettingsUiFactory.AddSection(content, hostSection);
            SettingsUiFactory.AddSection(content, raceStatusSection);
            SettingsUiFactory.AddSection(content, memberSection);
        });
        settingsScrollPanel ??= page as ThemedScrollPanel;
        return page;
    }

    private Control CreateInterfacePage()
    {
        return uiFactory.BuildScrollPage(content =>
        {
            SettingsUiFactory.AddSection(content, CreateInterfaceSection());
        });
    }

    private Control CreateColorsPage()
    {
        return uiFactory.BuildScrollPage(content =>
        {
            SettingsUiFactory.AddSection(content, CreateColorsSection());
        });
    }

    private void AddRaceSettingsPage(FlowLayoutPanel nav, RaceSettingsPage page, string title, Control content)
    {
        content.Dock = DockStyle.Fill;
        content.Visible = false;
        raceSettingsPagePanel.Controls.Add(content);
        raceSettingsPages[page] = content;

        Button button = uiFactory.CreateNavigationButton(title);
        button.Click += (_, _) => SelectRaceSettingsPage(page);
        raceSettingsNavButtons[page] = button;
        nav.Controls.Add(button);
    }

    private void SelectRaceSettingsPage(RaceSettingsPage selectedPage)
    {
        foreach ((RaceSettingsPage page, Control content) in raceSettingsPages)
        {
            bool selected = page == selectedPage;
            content.Visible = selected;
            if (selected)
            {
                content.BringToFront();
            }
        }

        foreach ((RaceSettingsPage page, Button button) in raceSettingsNavButtons)
        {
            bool selected = page == selectedPage;
            button.BackColor = selected ? UiTheme.Accent : UiTheme.SurfaceRaised;
            button.FlatAppearance.BorderColor = selected ? UiTheme.Accent : UiTheme.Border;
        }
    }

    private Control CreateConnectionSection()
    {
        TableLayoutPanel section = uiFactory.CreateSection("Connection");
        TableLayoutPanel grid = uiFactory.CreateGrid(
            SettingsUiFactory.ColumnStyleAbsolute(RaceSettingsLabelColumnWidth),
            SettingsUiFactory.ColumnStylePercent(100f));
        uiFactory.AddSettingRow(grid, "Server", serverBox);
        uiFactory.AddSettingRow(grid, "Nickname", nicknameBox);
        uiFactory.AddSettingRow(grid, "Role", CreateRoleButtonPanel());
        SettingsUiFactory.AddSectionControl(section, grid);
        return section;
    }

    private Control CreateRoleButtonPanel()
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(panel);
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, UiDpiScale.ScaleFloatForControl(panel, 54f)));
        AddSelectorButton(panel, hostRoleButton, 0);
        AddSelectorButton(panel, memberRoleButton, 1);
        return panel;
    }

    private Control CreateInterfaceSection()
    {
        TableLayoutPanel section = uiFactory.CreateSection("Leaderboard display");
        TableLayoutPanel grid = uiFactory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(ScaleLeaderboardVisualColumnWidth(76f)),
            SettingsUiFactory.ColumnStyleAbsolute(ScaleLeaderboardVisualColumnWidth(82f)),
            SettingsUiFactory.ColumnStyleAbsolute(ScaleLeaderboardVisualColumnWidth(180f)),
            SettingsUiFactory.ColumnStyleAbsolute(ScaleLeaderboardVisualColumnWidth(82f)),
            SettingsUiFactory.ColumnStyleAbsolute(ScaleLeaderboardVisualColumnWidth(72f)),
            SettingsUiFactory.ColumnStyleAbsolute(ScaleLeaderboardVisualColumnWidth(72f)),
            SettingsUiFactory.ColumnStyleAbsolute(ScaleLeaderboardVisualColumnWidth(96f)),
            SettingsUiFactory.ColumnStyleAbsolute(ScaleLeaderboardVisualColumnWidth(96f)),
            SettingsUiFactory.ColumnStyleAbsolute(ScaleLeaderboardVisualColumnWidth(104f)));
        uiFactory.AddHeaderRow(
            grid,
            ContentAlignment.MiddleLeft,
            "Column",
            "Show",
            "Width",
            "Font family",
            "Size",
            "Bold",
            "Italic",
            "Opacity %",
            "Shadow %",
            "Outline %");
        AddLeaderboardSettingsRow(
            grid,
            RaceLeaderboardColumnKeys.Rank,
            "Rank",
            leaderboardSettings.Rank,
            leaderboardSettings.TextEffects.Rank);
        AddLeaderboardSettingsRow(
            grid,
            RaceLeaderboardColumnKeys.Player,
            "Nickname",
            leaderboardSettings.Player,
            leaderboardSettings.TextEffects.Player);
        AddLeaderboardSettingsRow(
            grid,
            RaceLeaderboardColumnKeys.Icon,
            "Icon",
            leaderboardSettings.Icon,
            leaderboardSettings.TextEffects.Icon,
            includeFontFamily: false,
            includeBold: false,
            includeItalic: false);
        AddLeaderboardSettingsRow(
            grid,
            RaceLeaderboardColumnKeys.Time,
            "Time",
            leaderboardSettings.Time,
            leaderboardSettings.TextEffects.Time);
        SettingsUiFactory.AddSectionControl(section, grid);
        AddLeaderboardApplyActions(section, "Apply UI settings");
        return section;
    }

    private Control CreateColorsSection()
    {
        TableLayoutPanel section = uiFactory.CreateSection("Colors");
        leaderboardSettings.Colors.RankGradient ??= new RaceLeaderboardRankGradientColorSettings();
        leaderboardSettings.Colors.Player ??= new RaceLeaderboardColumnColorSettings();
        leaderboardSettings.Colors.PlayerSelf ??= CloneLeaderboardColumnColor(leaderboardSettings.Colors.Player);
        leaderboardSettings.Colors.PlayerOther ??= CloneLeaderboardColumnColor(leaderboardSettings.Colors.Player);
        SettingsUiFactory.AddSectionControl(section, CreateRankTimerColorOptionGrid());

        TableLayoutPanel colorGrid = uiFactory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f),
            SettingsUiFactory.ColumnStyleAbsolute(214f),
            SettingsUiFactory.ColumnStyleAbsolute(214f));
        uiFactory.AddHeaderRow(colorGrid, "Text type", "Text", "Outline", "Shadow");
        AddLeaderboardRankColorRow(
            colorGrid,
            leaderboardSettings.Colors.Rank,
            leaderboardSettings.Colors.RankGradient);
        AddLeaderboardColorRow(
            colorGrid,
            RaceLeaderboardColumnKeys.PlayerSelf,
            "Nickname: self",
            leaderboardSettings.Colors.PlayerSelf);
        AddLeaderboardColorRow(
            colorGrid,
            RaceLeaderboardColumnKeys.PlayerOther,
            "Nickname: other",
            leaderboardSettings.Colors.PlayerOther);
        AddLeaderboardColorRow(
            colorGrid,
            RaceLeaderboardColumnKeys.Icon,
            "Icon",
            leaderboardSettings.Colors.Icon,
            includeTextColor: false);
        AddLeaderboardColorRow(
            colorGrid,
            RaceLeaderboardColumnKeys.Time,
            "Time",
            leaderboardSettings.Colors.Time);
        SettingsUiFactory.AddSectionControl(section, colorGrid);
        AddLeaderboardApplyActions(section, "Apply color settings");
        return section;
    }

    private Control CreateRankTimerColorOptionGrid()
    {
        useRankColorForMainTimerBox = uiFactory.CreateCheckBox(leaderboardSettings.UseRankColorForMainTimer);
        TableLayoutPanel grid = uiFactory.CreateTwoColumnGrid(280f);
        uiFactory.AddSettingRow(grid, "Use rank color for main timer", useRankColorForMainTimerBox);
        return grid;
    }

    private void AddLeaderboardApplyActions(TableLayoutPanel section, string label)
    {
        FlowLayoutPanel actions = CreateButtonRow();
        AddButton(actions, label, accent: false, () =>
        {
            SaveLeaderboardSettings();
            return Task.CompletedTask;
        });
        SettingsUiFactory.AddSectionControl(section, actions);
    }

    private void AddLeaderboardSettingsRow(
        TableLayoutPanel grid,
        string key,
        string label,
        UiColumnSettings column,
        RaceLeaderboardColumnEffectSettings effect,
        bool includeFontFamily = true,
        bool includeFontSize = true,
        bool includeBold = true,
        bool includeItalic = true)
    {
        var showBox = uiFactory.CreateCheckBox(column.Show);
        TextBox widthBox = uiFactory.CreateNumberBox(column.Width, 1, 1000);
        FontFamilySelector? fontFamilyBox = includeFontFamily ? CreateFontFamilyBox(column.FontFamily) : null;
        TextBox? fontSizeBox = includeFontSize ? uiFactory.CreateDecimalBox(column.FontSize, 6, 96) : null;
        CheckBox? boldBox = includeBold ? uiFactory.CreateCheckBox(column.Bold) : null;
        CheckBox? italicBox = includeItalic ? uiFactory.CreateCheckBox(column.Italic) : null;
        TextBox opacityBox = uiFactory.CreateNumberBox(effect.OpacityPercent, 0, 100);
        TextBox shadowBox = uiFactory.CreateNumberBox(effect.ShadowPercent, 0, 100);
        TextBox outlineBox = uiFactory.CreateNumberBox(effect.OutlineThicknessPercent, 0, 100);

        leaderboardColumnControls[key] = new LeaderboardColumnControls(
            showBox,
            widthBox,
            fontFamilyBox,
            fontSizeBox,
            boldBox,
            italicBox,
            opacityBox,
            shadowBox,
            outlineBox);

        int row = uiFactory.AddGridRow(grid);
        grid.Controls.Add(uiFactory.CreateRowLabel(label), 0, row);
        grid.Controls.Add(uiFactory.CreateCenteredCell(showBox, ScaleLeaderboardVisualCellWidth(28)), 1, row);
        grid.Controls.Add(uiFactory.CreateCenteredCell(widthBox, ScaleLeaderboardVisualCellWidth(68)), 2, row);
        grid.Controls.Add(fontFamilyBox is null ? CreateEmptyCell() : uiFactory.CreateCenteredCell(fontFamilyBox, ScaleLeaderboardVisualCellWidth(164)), 3, row);
        grid.Controls.Add(fontSizeBox is null ? CreateEmptyCell() : uiFactory.CreateCenteredCell(fontSizeBox, ScaleLeaderboardVisualCellWidth(68)), 4, row);
        grid.Controls.Add(boldBox is null ? CreateEmptyCell() : uiFactory.CreateCenteredCell(boldBox, ScaleLeaderboardVisualCellWidth(28)), 5, row);
        grid.Controls.Add(italicBox is null ? CreateEmptyCell() : uiFactory.CreateCenteredCell(italicBox, ScaleLeaderboardVisualCellWidth(28)), 6, row);
        grid.Controls.Add(uiFactory.CreateCenteredCell(opacityBox, ScaleLeaderboardVisualCellWidth(78)), 7, row);
        grid.Controls.Add(uiFactory.CreateCenteredCell(shadowBox, ScaleLeaderboardVisualCellWidth(78)), 8, row);
        grid.Controls.Add(uiFactory.CreateCenteredCell(outlineBox, ScaleLeaderboardVisualCellWidth(78)), 9, row);
    }

    private static float ScaleLeaderboardVisualColumnWidth(float width)
    {
        return width * LeaderboardVisualColumnWidthScale;
    }

    private static int ScaleLeaderboardVisualCellWidth(int width)
    {
        return Math.Max(1, (int)MathF.Round(width * LeaderboardVisualColumnWidthScale));
    }

    private void AddLeaderboardRankColorRow(
        TableLayoutPanel grid,
        RaceLeaderboardColumnColorSettings colors,
        RaceLeaderboardRankGradientColorSettings gradient)
    {
        TextBox startColorBox = CreateTextBox(gradient.Start);
        TextBox middleColorBox = CreateTextBox(gradient.Middle);
        TextBox endColorBox = CreateTextBox(gradient.End);
        TextBox outlineColorBox = CreateTextBox(colors.Outline);
        TextBox shadowColorBox = CreateTextBox(colors.Shadow);

        leaderboardRankGradientControls = new LeaderboardRankGradientControls(
            startColorBox,
            middleColorBox,
            endColorBox);
        leaderboardColorControls[RaceLeaderboardColumnKeys.Rank] = new LeaderboardColorControls(
            null,
            outlineColorBox,
            shadowColorBox);

        int row = uiFactory.AddGridRow(grid);
        grid.Controls.Add(uiFactory.CreateRowLabel("Rank"), 0, row);
        grid.Controls.Add(CreateRankGradientEditor(startColorBox, middleColorBox, endColorBox), 1, row);
        grid.Controls.Add(CreateColorEditor(outlineColorBox), 2, row);
        grid.Controls.Add(CreateColorEditor(shadowColorBox), 3, row);
    }

    private Control CreateRankGradientEditor(TextBox startColorBox, TextBox middleColorBox, TextBox endColorBox)
    {
        var preview = new Panel
        {
            BackColor = UiTheme.Surface,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 0)
        };
        preview.Paint += (_, e) => PaintRankGradientPreview(e, startColorBox.Text, middleColorBox.Text, endColorBox.Text);

        startColorBox.TextChanged += (_, _) => preview.Invalidate();
        middleColorBox.TextChanged += (_, _) => preview.Invalidate();
        endColorBox.TextChanged += (_, _) => preview.Invalidate();

        var editor = new TableLayoutPanel
        {
            BackColor = UiTheme.Surface,
            ColumnCount = 3,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 2
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334f));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, UiDpiScale.ScaleFloatForControl(this, 16f)));

        AddRankGradientColorEditor(editor, 0, startColorBox);
        AddRankGradientColorEditor(editor, 1, middleColorBox);
        AddRankGradientColorEditor(editor, 2, endColorBox);
        editor.Controls.Add(preview, 0, 1);
        editor.SetColumnSpan(preview, 3);
        return editor;
    }

    private void AddRankGradientColorEditor(TableLayoutPanel grid, int column, TextBox textBox)
    {
        grid.Controls.Add(CreateColorEditor(textBox), column, 0);
    }

    private static void PaintRankGradientPreview(PaintEventArgs e, string startText, string middleText, string endText)
    {
        Rectangle bounds = e.ClipRectangle;
        bounds.Inflate(-1, -1);
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        Color start = ColorText.Parse(startText, ColorText.Parse(new RaceLeaderboardRankGradientColorSettings().Start, Color.Gold));
        Color middle = ColorText.Parse(middleText, ColorText.Parse(new RaceLeaderboardRankGradientColorSettings().Middle, Color.White));
        Color end = ColorText.Parse(endText, ColorText.Parse(new RaceLeaderboardRankGradientColorSettings().End, Color.Red));
        int middleX = bounds.Left + bounds.Width / 2;
        Rectangle left = Rectangle.FromLTRB(bounds.Left, bounds.Top, Math.Max(bounds.Left + 1, middleX), bounds.Bottom);
        Rectangle right = Rectangle.FromLTRB(Math.Min(bounds.Right - 1, middleX), bounds.Top, bounds.Right, bounds.Bottom);

        using (var leftBrush = new LinearGradientBrush(left, start, middle, LinearGradientMode.Horizontal))
        {
            e.Graphics.FillRectangle(leftBrush, left);
        }

        using (var rightBrush = new LinearGradientBrush(right, middle, end, LinearGradientMode.Horizontal))
        {
            e.Graphics.FillRectangle(rightBrush, right);
        }

        using var borderPen = new Pen(UiTheme.Border);
        e.Graphics.DrawRectangle(borderPen, bounds);
    }

    private void AddLeaderboardColorRow(
        TableLayoutPanel grid,
        string key,
        string label,
        RaceLeaderboardColumnColorSettings colors,
        bool includeTextColor = true)
    {
        TextBox? textColorBox = includeTextColor ? CreateTextBox(colors.Text) : null;
        TextBox outlineColorBox = CreateTextBox(colors.Outline);
        TextBox shadowColorBox = CreateTextBox(colors.Shadow);

        leaderboardColorControls[key] = new LeaderboardColorControls(
            textColorBox,
            outlineColorBox,
            shadowColorBox);

        int row = uiFactory.AddGridRow(grid);
        grid.Controls.Add(uiFactory.CreateRowLabel(label), 0, row);
        grid.Controls.Add(textColorBox is null ? CreateEmptyCell() : CreateColorEditor(textColorBox), 1, row);
        grid.Controls.Add(CreateColorEditor(outlineColorBox), 2, row);
        grid.Controls.Add(CreateColorEditor(shadowColorBox), 3, row);
    }

    private Control CreateColorEditor(TextBox textBox)
    {
        Button pickButton = CreateColorButton(textBox);
        textBox.TextChanged += (_, _) => UpdateColorButton(pickButton, textBox.Text);

        var editor = new TableLayoutPanel
        {
            BackColor = UiTheme.Surface,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58f));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        editor.Controls.Add(textBox, 0, 0);
        editor.Controls.Add(pickButton, 1, 0);
        return editor;
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
        button.FlatAppearance.BorderColor = UiTheme.Border;
        button.Click += (_, _) => dialogs.PickColor(textBox);
        UpdateColorButton(button, textBox.Text);
        return button;
    }

    private static void UpdateColorButton(Button button, string colorText)
    {
        Color color = ColorText.Parse(colorText, UiTheme.Text);
        bool transparent = color.A == 0;
        button.BackColor = transparent ? UiTheme.SurfaceRaised : color;
        button.ForeColor = transparent ? UiTheme.Text : GetReadableTextColor(color);
        button.Text = transparent ? "X" : string.Empty;
        button.FlatAppearance.MouseOverBackColor = transparent ? UiTheme.SurfaceRaised : color;
        button.FlatAppearance.MouseDownBackColor = transparent ? UiTheme.SurfaceRaised : color;
    }

    private static Color GetReadableTextColor(Color color)
    {
        double luminance = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
        return luminance >= 150 ? Color.Black : Color.White;
    }

    private static Control CreateEmptyCell()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = string.Empty
        };
    }

    private static FontFamilySelector CreateFontFamilyBox(string familyName)
    {
        var selector = new FontFamilySelector();
        selector.SetSelectedFontFamily(familyName);
        return selector;
    }

    private Control CreateHostSection()
    {
        TableLayoutPanel section = uiFactory.CreateSection("Select World");
        SettingsUiFactory.AddSectionControl(section, CreateWorldSourceSelector());

        randomWorldConfig = CreateRandomWorldConfig();
        customSeedWorldConfig = CreateCustomSeedWorldConfig();
        existingWorldFileConfig = CreateExistingWorldFileConfig();
        SettingsUiFactory.AddSectionControl(section, randomWorldConfig);
        SettingsUiFactory.AddSectionControl(section, customSeedWorldConfig);
        SettingsUiFactory.AddSectionControl(section, existingWorldFileConfig);

        SettingsUiFactory.AddSectionControl(section, CreateHostWorldActionRow());
        UpdateWorldSourceVisibility();
        UpdateCheatAvailability();
        return section;
    }

    private Control CreateRaceStatusSection()
    {
        Button copyRoomInfoButton = CreateActionButton(
            "Copy Room Info",
            accent: false,
            shell.CopyRoomInfoAsync,
            minimumWidth: 220);
        copyRoomInfoButton.Margin = Padding.Empty;
        TableLayoutPanel section = uiFactory.CreateSection("Room Info", copyRoomInfoButton);

        statusRouteOverrideHint = uiFactory.CreateWrappedFieldLabel(string.Empty, UiTheme.MutedText);
        statusRouteOverrideHint.Margin = new Padding(0, 2, 0, 8);
        SettingsUiFactory.AddSectionControl(section, statusRouteOverrideHint);
        SettingsUiFactory.AddSectionControl(section, CreateStatusWorldNameGrid());

        statusPlayerList = CreateStatusPlayerList();
        SettingsUiFactory.AddSectionControl(section, statusPlayerList);

        FlowLayoutPanel roomActions = CreateButtonRow();
        roomLifecycleButton = AddButton(roomActions, IsLocalHost(shell.State) ? "Close room" : "Leave room", accent: false, CloseOrLeaveRoomAsync);
        SettingsUiFactory.AddSectionControl(section, roomActions);

        RefreshStatusWorldName(shell.State);
        RefreshStatusRouteOverrideHint(shell.State);
        RefreshStatusPlayerList(shell.State);
        return section;
    }

    private void RefreshStatusRouteOverrideHint(RaceRoomState? state)
    {
        if (statusRouteOverrideHint is null)
        {
            return;
        }

        statusRouteOverrideHint.Text = Localize(IsLocalHost(state)
            ? "Room host route override hint"
            : "Room member route override hint");
    }

    private Control CreateStatusWorldNameGrid()
    {
        var grid = uiFactory.CreateGrid(
            SettingsUiFactory.ColumnStyleAbsolute(RaceSettingsLabelColumnWidth),
            SettingsUiFactory.ColumnStylePercent(100f));
        statusWorldNameValue = uiFactory.CreateValueLabel();
        uiFactory.AddSettingRow(grid, "World name", statusWorldNameValue);
        return grid;
    }

    private void RefreshStatusWorldName(RaceRoomState? state)
    {
        if (statusWorldNameValue is null)
        {
            return;
        }

        statusWorldNameValue.Text = ResolveStatusWorldName(state);
    }

    private string ResolveStatusWorldName(RaceRoomState? state)
    {
        string worldName = state?.WorldSettings?.WorldName?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(worldName))
        {
            return worldName;
        }

        string fileName = Path.GetFileNameWithoutExtension(state?.WorldFile?.FileName ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(fileName)
            ? Localize("Not uploaded")
            : fileName;
    }

    private TableLayoutPanel CreateStatusPlayerList()
    {
        var list = uiFactory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(240f),
            SettingsUiFactory.ColumnStyleAbsolute(156f));
        list.Margin = new Padding(0, 10, 0, 0);
        return list;
    }

    private void RefreshStatusPlayerList(RaceRoomState? state)
    {
        if (statusPlayerList is null)
        {
            return;
        }

        bool canKickPlayers = IsLocalHost(state);
        statusPlayerList.SuspendLayout();
        try
        {
            statusPlayerList.Controls.Clear();
            statusPlayerList.RowStyles.Clear();
            statusPlayerList.RowCount = 0;
            AddStatusPlayerHeaderRow(statusPlayerList);

            IReadOnlyList<RacePlayerState> players = state?.Players ?? Array.Empty<RacePlayerState>();
            if (players.Count == 0)
            {
                int emptyRow = AddGridRow(statusPlayerList, StatusPlayerListRowHeight);
                Label emptyLabel = uiFactory.CreateMutedLabel("No players");
                statusPlayerList.Controls.Add(emptyLabel, 0, emptyRow);
                statusPlayerList.SetColumnSpan(emptyLabel, 3);
                return;
            }

            foreach (RacePlayerState player in players)
            {
                AddStatusPlayerRow(statusPlayerList, player, canKickPlayers);
            }
        }
        finally
        {
            statusPlayerList.ResumeLayout(true);
            statusPlayerList.PerformLayout();
            raceStatusSection?.PerformLayout();
            settingsScrollPanel?.PerformLayout();
        }
    }

    private void AddStatusPlayerHeaderRow(TableLayoutPanel list)
    {
        int row = AddGridRow(list, StatusPlayerListHeaderHeight);
        list.Controls.Add(uiFactory.CreateHeaderLabel("Player"), 0, row);
        list.Controls.Add(uiFactory.CreateHeaderLabel("Status", ContentAlignment.MiddleCenter), 1, row);
        list.Controls.Add(uiFactory.CreateHeaderLabel(string.Empty, ContentAlignment.MiddleCenter), 2, row);
    }

    private void AddStatusPlayerRow(TableLayoutPanel list, RacePlayerState player, bool canKickPlayers)
    {
        int row = AddGridRow(list, StatusPlayerListRowHeight);
        Label nameLabel = uiFactory.CreateRawRowLabel(player.IsHost
            ? player.Nickname + " (" + Localize("Host") + ")"
            : player.Nickname);
        Label statusLabel = uiFactory.CreateRawRowLabel(LocalizePlayerStatus(player.Status));
        Control actionControl = CreateStatusPlayerActionControl(player, canKickPlayers);

        AddStatusPlayerCell(list, nameLabel, 0, row);
        AddStatusPlayerCell(list, statusLabel, 1, row);
        AddStatusPlayerCell(list, actionControl, 2, row);
    }

    private Control CreateStatusPlayerActionControl(RacePlayerState player, bool canKickPlayers)
    {
        if (!canKickPlayers || player.IsHost)
        {
            return uiFactory.CreateMutedLabel(string.Empty);
        }

        Button button = uiFactory.CreateButton("Kick", accent: false, minimumWidth: 116);
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(6, 8, 6, 8);
        button.Click += async (_, _) =>
        {
            await RunActionAsync(button, () => shell.KickPlayerAsync(player.Nickname));
        };
        return button;
    }

    private static void AddStatusPlayerCell(TableLayoutPanel list, Control control, int column, int row)
    {
        if (control is Label label)
        {
            label.AutoEllipsis = true;
            label.Margin = new Padding(6, 0, 6, 0);
            label.TextAlign = column == 0 ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter;
        }

        list.Controls.Add(control, column, row);
    }

    private string LocalizePlayerStatus(RacePlayerStatus status)
    {
        return status switch
        {
            RacePlayerStatus.WorldReady => Localize("Ready"),
            RacePlayerStatus.Running => Localize("Running"),
            RacePlayerStatus.Joined => Localize("Not Ready"),
            _ => Localize("Not Ready")
        };
    }

    private Control CreateHostWorldActionRow()
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 10, 0, 0),
            Padding = Padding.Empty,
            RowCount = 1
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiDpiScale.ScaleFloatForControl(row, 280f)));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, UiDpiScale.ScaleFloatForControl(row, 60f)));
        UiTheme.EnableDoubleBuffering(row);

        hostWorldProgressBar = new RaceActionProgressBar
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 8),
            Value = 0
        };

        Button button = uiFactory.CreateButton(HostWorldActionTextKey(), accent: true, minimumWidth: 256);
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(0, 0, 0, 8);
        button.Click += async (_, _) => await HandleHostWorldActionButtonClickAsync();
        hostWorldActionButton = button;

        row.Controls.Add(hostWorldProgressBar, 0, 0);
        row.Controls.Add(button, 1, 0);
        return row;
    }

    private Control CreateMemberSection()
    {
        TableLayoutPanel section = uiFactory.CreateSection("Join room");
        TableLayoutPanel grid = uiFactory.CreateGrid(
            SettingsUiFactory.ColumnStyleAbsolute(RaceSettingsCompactLabelColumnWidth),
            SettingsUiFactory.ColumnStylePercent(100f));
        AddGridRow(grid);
        AddField(grid, "Room code", roomCodeBox, 0, 0);
        SettingsUiFactory.AddSectionControl(section, grid);

        FlowLayoutPanel actions = CreateButtonRow();
        AddButton(actions, "Join room", accent: true, JoinRoomFromMemberPanelAsync);
        SettingsUiFactory.AddSectionControl(section, actions);
        return section;
    }

    private async Task JoinRoomFromMemberPanelAsync()
    {
        RaceOperationResult<RaceRoomState> result = await shell.JoinRoomAsync(
            serverBox.Text,
            roomCodeBox.Text,
            nicknameBox.Text);
        if (!result.Succeeded)
        {
            ShowJoinRoomFailure(result);
        }
    }

    private Control CreateWorldSettingsGrid()
    {
        TableLayoutPanel grid = uiFactory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(RaceSettingsValueColumnWidth));
        uiFactory.AddSettingRow(grid, "World size", sizeBox);
        uiFactory.AddSettingRow(grid, "World difficulty", difficultyBox);
        uiFactory.AddSettingRow(grid, "World evil", evilBox);
        return grid;
    }

    private Control CreateWorldSourceSelector()
    {
        TableLayoutPanel container = CreateConfigContainer();
        SettingsUiFactory.AddSectionControl(container, uiFactory.CreateFieldLabel("World source"));
        SettingsUiFactory.AddSectionControl(container, CreateWorldSourceButtonPanel());
        return container;
    }

    private Control CreateWorldSourceButtonPanel()
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = 3,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(panel);
        for (int i = 0; i < 3; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
        }

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, UiDpiScale.ScaleFloatForControl(panel, 54f)));
        AddSelectorButton(panel, randomWorldSourceButton, 0);
        AddSelectorButton(panel, customSeedWorldSourceButton, 1);
        AddSelectorButton(panel, existingWorldFileSourceButton, 2);
        return panel;
    }

    private Control CreateRandomWorldConfig()
    {
        TableLayoutPanel container = CreateConfigContainer();
        SettingsUiFactory.AddSectionControl(container, uiFactory.CreateSubsectionLabel("World options"));
        SettingsUiFactory.AddSectionControl(container, CreateWorldSettingsGrid());
        SettingsUiFactory.AddSectionControl(container, uiFactory.CreateFieldLabel("Special seeds"));
        SettingsUiFactory.AddSectionControl(container, CreateSpecialSeedSelector());

        TableLayoutPanel seedGrid = uiFactory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(RaceSettingsValueColumnWidth));
        uiFactory.AddSettingRow(seedGrid, "Secret seed", randomSecretSeedsBox);
        SettingsUiFactory.AddSectionControl(container, seedGrid);

        SettingsUiFactory.AddSectionControl(container, uiFactory.CreateSubsectionLabel("Cheats"));
        TableLayoutPanel cheatsGrid = uiFactory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(RaceSettingsValueColumnWidth));
        uiFactory.AddSettingRow(cheatsGrid, "Enabled", cheatsEnabledBox);
        SettingsUiFactory.AddSectionControl(container, cheatsGrid);
        SettingsUiFactory.AddSectionControl(container, CreatePyramidItemSelector());
        SettingsUiFactory.AddSectionControl(container, CreateCrimsonDistanceSelector());
        SettingsUiFactory.AddSectionControl(container, CreateResourceItemSelector());
        SettingsUiFactory.AddSectionControl(container, CreateMinimumSelector(
            AutoCreateResourceMinimum.LifeCrystals,
            lifeCrystalMinimumButtons));
        SettingsUiFactory.AddSectionControl(container, CreateHookMinimumSelector());
        SettingsUiFactory.AddSectionControl(container, CreateMinimumSelector(
            AutoCreateResourceMinimum.Potions,
            spelunkerMinimumButtons));
        SettingsUiFactory.AddSectionControl(container, CreateMinimumSelector(
            AutoCreateResourceMinimum.Potions,
            featherfallMinimumButtons));

        return container;
    }

    private Control CreateCustomSeedWorldConfig()
    {
        TableLayoutPanel container = CreateConfigContainer();
        TableLayoutPanel grid = uiFactory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(RaceSettingsValueColumnWidth));
        uiFactory.AddSettingRow(grid, "Fixed seed", seedBox);
        SettingsUiFactory.AddSectionControl(container, grid);

        return container;
    }

    private Control CreateExistingWorldFileConfig()
    {
        TableLayoutPanel container = CreateConfigContainer();
        TableLayoutPanel grid = uiFactory.CreateGrid(
            SettingsUiFactory.ColumnStyleAbsolute(RaceSettingsLabelColumnWidth),
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(RaceWorldFileBrowseColumnWidth));
        int row = uiFactory.AddGridRow(grid);
        Button browseButton = uiFactory.CreateSmallButton("Browse");
        browseButton.Margin = Padding.Empty;
        browseButton.Click += (_, _) => ChooseWorldFile();
        grid.Controls.Add(uiFactory.CreateRowLabel("World file"), 0, row);
        grid.Controls.Add(worldPathBox, 1, row);
        grid.Controls.Add(uiFactory.CreateAlignedCell(browseButton, 136, HorizontalAlignment.Right), 2, row);
        SettingsUiFactory.AddSectionControl(container, grid);
        return container;
    }

    private static TableLayoutPanel CreateConfigContainer()
    {
        var container = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        UiTheme.EnableDoubleBuffering(container);
        return container;
    }

    private Control CreatePyramidItemSelector()
    {
        int columnCount = AutoCreatePyramidFilterItem.All.Length + 1;
        TableLayoutPanel panel = CreateCheatSelectorPanel(columnCount);
        pyramidEnabledBox.Margin = new Padding(0, 0, 0, CheatSelectorGap);
        panel.Controls.Add(pyramidEnabledBox, 0, 0);

        for (int index = 0; index < AutoCreatePyramidFilterItem.All.Length; index++)
        {
            string item = AutoCreatePyramidFilterItem.All[index];
            CheckBox button = pyramidItemButtons[item];
            button.Margin = CheatSelectorMargin(index, AutoCreatePyramidFilterItem.All.Length);
            panel.Controls.Add(button, index + 2, 0);
        }

        FinishCheatSelectorRow(panel);
        UpdateCheatAvailability();
        return panel;
    }

    private Control CreateCrimsonDistanceSelector()
    {
        int columnCount = AutoCreateCrimsonDistance.All.Length + 1;
        TableLayoutPanel panel = CreateCheatSelectorPanel(columnCount);
        crimsonEnabledBox.Margin = new Padding(0, 0, 0, CheatSelectorGap);
        panel.Controls.Add(crimsonEnabledBox, 0, 0);
        for (int index = 0; index < AutoCreateCrimsonDistance.All.Length; index++)
        {
            CheckBox button = crimsonDistanceButtons[AutoCreateCrimsonDistance.All[index]];
            button.Margin = CheatSelectorMargin(index, AutoCreateCrimsonDistance.All.Length);
            panel.Controls.Add(button, index + 2, 0);
        }

        FinishCheatSelectorRow(panel);
        return panel;
    }

    private Control CreateResourceItemSelector()
    {
        TableLayoutPanel panel = CreateCheatSelectorPanel(1);
        for (int index = 0; index < AutoCreateResourceFilterItem.All.Length; index++)
        {
            CheckBox button = resourceItemButtons[AutoCreateResourceFilterItem.All[index]];
            button.Margin = new Padding(0, 0, 0, CheatSelectorGap);
            panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, CheatSelectorRowHeight));
            panel.Controls.Add(button, 0, index);
        }

        return panel;
    }

    private static Control CreateMinimumSelector(
        IReadOnlyList<int> values,
        IReadOnlyDictionary<int, CheckBox> buttons)
    {
        TableLayoutPanel panel = CreateCheatSelectorPanel(values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            int value = values[index];
            CheckBox button = buttons[value];
            button.Margin = value == 0
                ? new Padding(0, 0, 0, CheatSelectorGap)
                : CheatSelectorMargin(index - 1, values.Count - 1);
            panel.Controls.Add(button, value == 0 ? 0 : index + 1, 0);
        }

        FinishCheatSelectorRow(panel);
        return panel;
    }

    private Control CreateHookMinimumSelector()
    {
        TableLayoutPanel panel = CreateCheatSelectorPanel(AutoCreateResourceHook.All.Length);
        for (int index = 0; index < AutoCreateResourceHook.All.Length; index++)
        {
            string hook = AutoCreateResourceHook.All[index];
            CheckBox button = hookMinimumButtons[hook];
            button.Margin = hook == AutoCreateResourceHook.None
                ? new Padding(0, 0, 0, CheatSelectorGap)
                : CheatSelectorMargin(index - 1, AutoCreateResourceHook.All.Length - 1);
            panel.Controls.Add(button, hook == AutoCreateResourceHook.None ? 0 : index + 1, 0);
        }

        FinishCheatSelectorRow(panel);
        return panel;
    }

    private static TableLayoutPanel CreateCheatSelectorPanel(int columnCount)
    {
        int physicalColumnCount = columnCount == 1 ? 3 : columnCount + 1;
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = physicalColumnCount,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(panel);
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, CheatActivationButtonPercent));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, CheatActivationSpacerPercent));
        if (columnCount == 1)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, CheatOptionButtonsPercent));
        }
        else
        {
            for (int index = 1; index < columnCount; index++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(
                    SizeType.Percent,
                    CheatOptionButtonsPercent / (columnCount - 1)));
            }
        }

        return panel;
    }

    private static Padding CheatSelectorMargin(int index, int count) =>
        new(0, 0, index == count - 1 ? 0 : CheatSelectorHorizontalGap, CheatSelectorGap);

    private static void FinishCheatSelectorRow(TableLayoutPanel panel)
    {
        panel.RowCount = 1;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, CheatSelectorRowHeight));
    }

    private Control CreateSpecialSeedSelector()
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = 3,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 8),
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(panel);
        for (int column = 0; column < 3; column++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
        }

        for (int index = 0; index < AutoCreateSpecialWorldSeed.All.Length; index++)
        {
            string seed = AutoCreateSpecialWorldSeed.All[index];
            int column = index % 3;
            int row = index / 3;
            if (column == 0)
            {
                panel.RowCount++;
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            }

            CheckBox button = specialSeedButtons[seed];
            button.Margin = new Padding(0, 0, column == 2 ? 0 : 8, 10);
            panel.Controls.Add(button, column, row);
        }

        UpdateSpecialSeedAvailability();
        return panel;
    }

    private TextBox CreateTextBox(string value)
    {
        TextBox textBox = uiFactory.CreateTextBox(value);
        textBox.TextAlign = HorizontalAlignment.Center;
        return textBox;
    }

    private CheckBox CreateRoleButton(string textKey, RacePanelRole role, bool selected)
    {
        CheckBox button = CreateSelectorButton(textKey, selected);
        button.CheckedChanged += (_, _) =>
        {
            if (button.Checked)
            {
                SelectRole(role);
            }
            else if (selectedRole == role)
            {
                button.Checked = true;
            }
        };
        return button;
    }

    private CheckBox CreateWorldSourceButton(string textKey, RacePanelWorldSource source, bool selected)
    {
        CheckBox button = CreateSelectorButton(textKey, selected);
        button.CheckedChanged += (_, _) =>
        {
            if (button.Checked)
            {
                SelectWorldSource(source);
            }
            else if (selectedWorldSource == source)
            {
                button.Checked = true;
            }
        };
        return button;
    }

    private CheckBox CreatePyramidItemButton(string textKey, bool selected)
    {
        CheckBox button = CreateSelectorButton(textKey, selected);
        button.CheckedChanged += (_, _) => UpdateSelectorButtonState(button);
        return button;
    }

    private void InitializeMinimumButtons(
        IReadOnlyList<int> values,
        Dictionary<int, CheckBox> buttons,
        string nameKey)
    {
        for (int index = 0; index < values.Count; index++)
        {
            int value = values[index];
            string label = value == 0
                ? nameKey
                : index == values.Count - 1
                    ? $"{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}+"
                    : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            CheckBox button = CreateSelectorButton(label, selected: false);
            if (value != 0)
            {
                button.AutoEllipsis = false;
                button.Padding = new Padding(0, 0, 0, 2);
            }

            button.CheckedChanged += (_, _) => SelectMinimum(value, button.Checked, values, buttons);
            buttons[value] = button;
        }

        ApplyMinimumSelection(0, buttons);
    }

    private void InitializeHookMinimumButtons()
    {
        foreach (string hook in AutoCreateResourceHook.All)
        {
            CheckBox button = CreateSelectorButton(
                hook == AutoCreateResourceHook.None ? "Hook" : hook,
                selected: false);
            button.CheckedChanged += (_, _) => SelectHookMinimum(hook, button.Checked);
            hookMinimumButtons[hook] = button;
        }

        ApplyHookMinimumSelection(AutoCreateResourceHook.None);
    }

    private CheckBox CreateSpecialSeedButton(string textKey, bool selected)
    {
        CheckBox button = CreateSelectorButton(textKey, selected);
        button.CheckedChanged += (_, _) =>
        {
            if (string.Equals(textKey, AutoCreateSpecialWorldSeed.Zenith, StringComparison.OrdinalIgnoreCase))
            {
                UpdateSpecialSeedAvailability();
            }
            else
            {
                UpdateSelectorButtonState(button);
            }
        };
        return button;
    }

    private CheckBox CreateSelectorButton(string textKey, bool selected)
    {
        var button = new CheckBox
        {
            Appearance = Appearance.Button,
            AutoEllipsis = true,
            BackColor = selected ? UiTheme.Selection : UiTheme.SurfaceRaised,
            Checked = selected,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Font = UiTheme.FormFont(9f),
            ForeColor = UiTheme.Text,
            Height = 44,
            MinimumSize = new Size(0, 44),
            Padding = new Padding(8, 0, 8, 2),
            Text = Localize(textKey),
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.CheckedBackColor = UiTheme.Selection;
        button.EnabledChanged += (_, _) => UpdateSelectorButtonState(button);
        UpdateSelectorButtonState(button);
        return button;
    }

    private static void AddSelectorButton(TableLayoutPanel panel, CheckBox button, int column)
    {
        button.Margin = new Padding(0, 0, column == panel.ColumnCount - 1 ? 0 : 8, 10);
        panel.Controls.Add(button, column, 0);
    }

    private static void AddSelectorButton(TableLayoutPanel panel, CheckBox button, int column, int row)
    {
        bool lastColumn = column == panel.ColumnCount - 1;
        bool lastRow = row == panel.RowCount - 1;
        button.Margin = new Padding(0, 0, lastColumn ? 0 : 8, lastRow ? 0 : 10);
        panel.Controls.Add(button, column, row);
    }

    private ThemedDropDownList CreateDropDown(params (string Text, object Value)[] items)
    {
        ThemedDropDownList dropDown = uiFactory.CreateDropDownList();
        foreach ((string text, object value) in items)
        {
            dropDown.Items.Add(new OptionItem(Localize(text), value));
        }

        dropDown.SelectedIndex = items.Length > 0 ? 0 : -1;
        return dropDown;
    }

    private static void SelectDropDownByValue(ThemedDropDownList dropDown, object value)
    {
        for (int index = 0; index < dropDown.Items.Count; index++)
        {
            if (dropDown.Items[index] is OptionItem item && Equals(item.Value, value))
            {
                dropDown.SelectedIndex = index;
                return;
            }
        }
    }

    private RaceWorldSettings BuildRandomWorldSettings()
    {
        return new RaceWorldSettings(
            shell.State?.WorldSettings?.TerrariaVersion ?? string.Empty,
            GetSelectedInt(sizeBox, 2),
            GetSelectedInt(difficultyBox, 1),
            GetSelectedInt(evilBox, 2) == 2,
            GetSpecialSeedMask(),
            BuildCheatSettings(),
            SecretSeeds: randomSecretSeedsBox.Text.Trim());
    }

    private RaceWorldSettings BuildCustomSeedWorldSettings()
    {
        RaceWorldSettings? roomSettings = shell.State?.WorldSettings;
        return new RaceWorldSettings(
            roomSettings?.TerrariaVersion ?? string.Empty,
            roomSettings?.SizeCode ?? 2,
            roomSettings?.DifficultyCode ?? 1,
            roomSettings?.HasCrimson ?? true,
            SpecialSeedMask: 0,
            Cheats: RaceCheatSettings.Disabled);
    }

    private RaceWorldSettings BuildUploadWorldSettings()
    {
        return BuildUploadWorldSettings(selectedWorldSource);
    }

    private RaceWorldSettings BuildUploadWorldSettings(RacePanelWorldSource worldSource)
    {
        return worldSource switch
        {
            RacePanelWorldSource.Random => BuildRandomWorldSettings(),
            _ => BuildCustomSeedWorldSettings()
        };
    }

    private RaceCheatSettings BuildCheatSettings()
    {
        int pyramidItemMask = AutoCreatePyramidFilterItem.ToMask(
            AutoCreatePyramidFilterItem.All.Where(item => pyramidItemButtons[item].Checked));
        int resourceItemMask = AutoCreateResourceFilterItem.ToMask(
            AutoCreateResourceFilterItem.All.Where(item => resourceItemButtons[item].Checked));
        return new RaceCheatSettings(
            cheatsEnabledBox.Checked,
            pyramidEnabledBox.Checked,
            pyramidItemMask,
            crimsonEnabledBox.Checked,
            GetSelectedCrimsonDistance(),
            resourceItemMask,
            GetSelectedMinimum(lifeCrystalMinimumButtons, AutoCreateResourceMinimum.LifeCrystals),
            GetSelectedHookMinimum(),
            GetSelectedMinimum(spelunkerMinimumButtons, AutoCreateResourceMinimum.Potions),
            GetSelectedMinimum(featherfallMinimumButtons, AutoCreateResourceMinimum.Potions));
    }

    private async Task HandleHostWorldActionButtonClickAsync()
    {
        if (hostWorldActionCancellation is not null)
        {
            await CancelHostWorldActionAsync();
            return;
        }

        await RunHostWorldActionAsync();
    }

    private async Task RunHostWorldActionAsync()
    {
        using var cancellation = new CancellationTokenSource();
        hostWorldActionCancellation = cancellation;
        hostWorldActionCancelRequested = false;
        hostWorldActionSource = selectedWorldSource;
        hostWorldActionWorldPath = null;
        ApplyHostWorldActionButtonState(HostWorldActionButtonState.Running);
        UpdateConnectionInputLockState();
        try
        {
            PersistDraftState();
            await HandleHostWorldActionAsync(cancellation.Token, hostWorldActionSource);
            PersistDraftState();
        }
        catch (OperationCanceledException)
        {
            await CleanupCancelledHostWorldActionAsync();
        }
        catch (Exception ex)
        {
            await CleanupFailedHostWorldActionAsync(ex);
        }
        finally
        {
            if (ReferenceEquals(hostWorldActionCancellation, cancellation))
            {
                hostWorldActionCancellation = null;
            }

            hostWorldActionCancelRequested = false;
            hostWorldActionWorldPath = null;
            ApplyHostWorldActionButtonState(HostWorldActionButtonState.Idle);
            UpdateConnectionInputLockState();
        }
    }

    private async Task CancelHostWorldActionAsync()
    {
        if (hostWorldActionCancellation is null || hostWorldActionCancelRequested)
        {
            return;
        }

        hostWorldActionCancelRequested = true;
        hostWorldActionCancellation.Cancel();
        ApplyHostWorldActionButtonState(HostWorldActionButtonState.Cancelling);
        UpdateConnectionInputLockState();
        await shell.CancelWorldGenerationAsync();
    }

    private async Task CleanupCancelledHostWorldActionAsync()
    {
        if (hostWorldActionSource != RacePanelWorldSource.ExistingFile)
        {
            string worldPath = !string.IsNullOrWhiteSpace(hostWorldActionWorldPath)
                ? hostWorldActionWorldPath
                : shell.LocalWorldPath ?? worldPathBox.Text;
            if (!string.IsNullOrWhiteSpace(worldPath))
            {
                await shell.DiscardLocalWorldAsync(worldPath);
                worldPathBox.Text = string.Empty;
            }
        }

        ResetHostWorldProgress();
    }

    private async Task CleanupFailedHostWorldActionAsync(Exception exception)
    {
        if (hostWorldActionSource != RacePanelWorldSource.ExistingFile)
        {
            string worldPath = !string.IsNullOrWhiteSpace(hostWorldActionWorldPath)
                ? hostWorldActionWorldPath
                : shell.LocalWorldPath ?? worldPathBox.Text;
            if (!string.IsNullOrWhiteSpace(worldPath))
            {
                await shell.DiscardLocalWorldAsync(worldPath);
                worldPathBox.Text = string.Empty;
            }
        }

        ResetHostWorldProgress();
        string detail = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;
        ShowWorldUploadFailure(RaceOperationResult<RaceRoomState>.Failure(
            "world_upload_failed",
            detail));
    }

    private async Task HandleHostWorldActionAsync(CancellationToken cancellationToken, RacePanelWorldSource worldSource)
    {
        int progressVersion = StartHostWorldProgress();
        var generationProgress = new Progress<int>(value => HandleHostWorldProgressReport(progressVersion, value));
        if (worldSource == RacePanelWorldSource.Random)
        {
            worldPathBox.Text = string.Empty;
            await shell.GenerateRandomWorldAsync(BuildRandomWorldSettings(), generationProgress);
            cancellationToken.ThrowIfCancellationRequested();
            SyncLocalWorldPath();
            if (string.IsNullOrWhiteSpace(shell.LocalWorldPath))
            {
                ResetHostWorldProgress();
                return;
            }

            hostWorldActionWorldPath = shell.LocalWorldPath;
            SetHostWorldProgress(progressVersion, 90);
        }
        else if (worldSource == RacePanelWorldSource.CustomSeed)
        {
            worldPathBox.Text = string.Empty;
            await shell.GenerateCustomSeedWorldAsync(BuildCustomSeedWorldSettings(), seedBox.Text, generationProgress);
            cancellationToken.ThrowIfCancellationRequested();
            SyncLocalWorldPath();
            if (string.IsNullOrWhiteSpace(shell.LocalWorldPath))
            {
                ResetHostWorldProgress();
                return;
            }

            hostWorldActionWorldPath = shell.LocalWorldPath;
            SetHostWorldProgress(progressVersion, 90);
        }

        string uploadWorldPath = worldPathBox.Text;
        if (!RaceWorldFileValidator.IsValidWorldFilePath(uploadWorldPath))
        {
            ResetHostWorldProgress();
            ShowWorldUploadFailure(RaceOperationResult<RaceRoomState>.Failure(
                "world_upload_required",
                "A valid world file is required."));
            return;
        }

        IProgress<int> uploadProgress = CreateHostUploadProgress(worldSource, progressVersion);
        RaceOperationResult<RaceRoomState> upload = await shell.UploadWorldAsync(
            serverBox.Text,
            nicknameBox.Text,
            uploadWorldPath,
            BuildUploadWorldSettings(worldSource),
            seedBox.Text,
            uploadProgress,
            cancellationToken);
        if (upload.Succeeded)
        {
            hostWorldActionWorldPath = null;
            hostWorldUploaded = true;
            CompleteHostWorldProgress(100);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (worldSource != RacePanelWorldSource.ExistingFile)
        {
            await shell.DiscardLocalWorldAsync(uploadWorldPath);
            worldPathBox.Text = string.Empty;
        }

        ResetHostWorldProgress();
        ShowWorldUploadFailure(upload);
    }

    private void HandleHostWorldProgressReport(int progressVersion, int value)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => HandleHostWorldProgressReport(progressVersion, value)));
            return;
        }

        if (progressVersion != hostWorldProgressVersion)
        {
            return;
        }

        int clamped = Math.Clamp(value, 0, 100);
        if (clamped == 0)
        {
            SetHostWorldProgress(progressVersion, 0);
            return;
        }

        if (clamped >= 90)
        {
            SetHostWorldProgress(progressVersion, 90);
            return;
        }

        SetHostWorldProgress(progressVersion, clamped);
    }

    private IProgress<int> CreateHostUploadProgress(RacePanelWorldSource worldSource, int progressVersion)
    {
        return new Progress<int>(value => HandleHostUploadProgressReport(worldSource, progressVersion, value));
    }

    private void HandleHostUploadProgressReport(RacePanelWorldSource worldSource, int progressVersion, int value)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => HandleHostUploadProgressReport(worldSource, progressVersion, value)));
            return;
        }

        if (progressVersion != hostWorldProgressVersion)
        {
            return;
        }

        int clamped = Math.Clamp(value, 0, 100);
        if (worldSource == RacePanelWorldSource.ExistingFile)
        {
            SetHostWorldProgress(progressVersion, clamped);
            return;
        }

        int mapped = 90 + (int)Math.Round(clamped * 0.1d, MidpointRounding.AwayFromZero);
        SetHostWorldProgress(progressVersion, Math.Clamp(mapped, 90, 100));
    }

    private int StartHostWorldProgress()
    {
        hostWorldProgressVersion++;
        SetHostWorldProgress(0);
        return hostWorldProgressVersion;
    }

    private void ResetHostWorldProgress()
    {
        hostWorldProgressVersion++;
        SetHostWorldProgress(0);
    }

    private void CompleteHostWorldProgress(int value)
    {
        hostWorldProgressVersion++;
        SetHostWorldProgress(value);
    }

    private void SetHostWorldProgress(int progressVersion, int value)
    {
        if (progressVersion != hostWorldProgressVersion)
        {
            return;
        }

        SetHostWorldProgress(value);
    }

    private void SetHostWorldProgress(int value)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetHostWorldProgress(value)));
            return;
        }

        if (hostWorldProgressBar is not null)
        {
            hostWorldProgressBar.Value = Math.Clamp(value, 0, 100);
        }
    }

    private void ShowWorldUploadFailure(RaceOperationResult<RaceRoomState> result)
    {
        string detail = string.IsNullOrWhiteSpace(result.Message) ? result.ErrorCode : result.Message;
        string message = string.IsNullOrWhiteSpace(detail)
            ? Localize("Upload failed.")
            : Localize("Upload failed.") + Environment.NewLine + detail;
        dialogs.ShowWarning(message, Localize("Race"));
    }

    private void ShowJoinRoomFailure(RaceOperationResult<RaceRoomState> result)
    {
        string detail = string.IsNullOrWhiteSpace(result.Message) ? result.ErrorCode : result.Message;
        string message = string.IsNullOrWhiteSpace(detail)
            ? Localize("Join room failed.")
            : Localize("Join room failed.") + Environment.NewLine + detail;
        dialogs.ShowWarning(message, Localize("Race"));
    }

    private string HostWorldActionTextKey()
    {
        if (hostWorldUploaded)
        {
            return selectedWorldSource == RacePanelWorldSource.ExistingFile
                ? "Reupload"
                : "Regenerate and upload";
        }

        return selectedWorldSource == RacePanelWorldSource.ExistingFile
            ? "Upload"
            : "Generate and upload";
    }

    private void UpdateHostWorldActionButton()
    {
        if (hostWorldActionCancellation is not null)
        {
            ApplyHostWorldActionButtonState(hostWorldActionCancelRequested
                ? HostWorldActionButtonState.Cancelling
                : HostWorldActionButtonState.Running);
            return;
        }

        ApplyHostWorldActionButtonState(HostWorldActionButtonState.Idle);
    }

    private void ApplyHostWorldActionButtonState(HostWorldActionButtonState state)
    {
        if (hostWorldActionButton is null)
        {
            return;
        }

        bool running = state == HostWorldActionButtonState.Running;
        hostWorldActionButton.Text = Localize(running ? "Cancel" : HostWorldActionTextKey());
        hostWorldActionButton.Enabled = state != HostWorldActionButtonState.Cancelling;
        ApplyHostWorldActionButtonAccent(state == HostWorldActionButtonState.Idle && !hostWorldUploaded);
        UpdateConnectionInputLockState();
    }

    private void ApplyHostWorldActionButtonAccent(bool accent)
    {
        if (hostWorldActionButton is null)
        {
            return;
        }

        hostWorldActionButton.BackColor = accent ? UiTheme.Accent : UiTheme.SurfaceRaised;
        hostWorldActionButton.FlatAppearance.BorderColor = accent ? UiTheme.Accent : UiTheme.Border;
        hostWorldActionButton.FlatAppearance.MouseDownBackColor = accent
            ? UiTheme.AccentDown
            : Color.FromArgb(41, 50, 56);
        hostWorldActionButton.FlatAppearance.MouseOverBackColor = accent
            ? UiTheme.AccentHover
            : Color.FromArgb(47, 58, 64);
        hostWorldActionButton.Invalidate();
    }

    private void ApplyWorldSettings(RaceWorldSettings? worldSettings)
    {
        if (worldSettings is null)
        {
            return;
        }

        SelectDropDownByValue(sizeBox, worldSettings.SizeCode);
        SelectDropDownByValue(difficultyBox, worldSettings.DifficultyCode);
        SelectDropDownByValue(evilBox, worldSettings.HasCrimson ? 2 : 1);
        ApplySpecialSeedMask(worldSettings.SpecialSeedMask);
        if (!randomSecretSeedsBox.Focused)
        {
            randomSecretSeedsBox.Text = worldSettings.SecretSeeds ?? string.Empty;
        }

        RaceCheatSettings cheats = worldSettings.Cheats;
        cheatsEnabledBox.Checked = cheats.Enabled;
        pyramidEnabledBox.Checked = cheats.PyramidEnabled;
        int pyramidItemMask = cheats.PyramidEnabled
            ? AutoCreatePyramidFilterItem.NormalizeMaskOrAll(cheats.PyramidItemMask)
            : AutoCreatePyramidFilterItem.NormalizeMask(cheats.PyramidItemMask);
        foreach ((string item, CheckBox button) in pyramidItemButtons)
        {
            int itemMask = AutoCreatePyramidFilterItem.Mask(item);
            button.Checked = (pyramidItemMask & itemMask) == itemMask;
        }

        crimsonEnabledBox.Checked = cheats.CrimsonEnabled;
        ApplyCrimsonDistanceSelection(AutoCreateCrimsonDistance.Normalize(cheats.CrimsonDistance));
        int resourceItemMask = AutoCreateResourceFilterItem.NormalizeMask(cheats.ResourceItemMask);
        foreach ((string item, CheckBox button) in resourceItemButtons)
        {
            button.Checked = (resourceItemMask & AutoCreateResourceFilterItem.Mask(item)) != 0;
        }

        ApplyMinimumSelection(
            AutoCreateResourceMinimum.NormalizeLifeCrystals(cheats.LifeCrystalMinimum),
            lifeCrystalMinimumButtons);
        ApplyHookMinimumSelection(AutoCreateResourceHook.Normalize(cheats.HookMinimum));
        ApplyMinimumSelection(
            AutoCreateResourceMinimum.NormalizePotions(cheats.SpelunkerPotionMinimum),
            spelunkerMinimumButtons);
        ApplyMinimumSelection(
            AutoCreateResourceMinimum.NormalizePotions(cheats.FeatherfallPotionMinimum),
            featherfallMinimumButtons);
        UpdateCheatAvailability();
    }

    private void PersistDraftState()
    {
        if (IsProgrammaticUpdate)
        {
            return;
        }

        shell.SaveDraftState(new RacePanelDraftState(
            serverBox.Text,
            nicknameBox.Text,
            roomCodeBox.Text,
            seedBox.Text,
            worldPathBox.Text,
            selectedRole,
            selectedWorldSource));
    }

    private void SaveLeaderboardSettings()
    {
        if (IsProgrammaticUpdate)
        {
            return;
        }

        shell.SaveLeaderboardSettings(BuildLeaderboardSettings());
    }

    private RaceLeaderboardSettings BuildLeaderboardSettings()
    {
        RaceLeaderboardSettings settings = CloneLeaderboardSettings(leaderboardSettings);
        settings.UseRankColorForMainTimer = useRankColorForMainTimerBox?.Checked ?? settings.UseRankColorForMainTimer;
        ApplyLeaderboardSettingsRow(
            RaceLeaderboardColumnKeys.Rank,
            settings.Rank,
            settings.TextEffects.Rank,
            settings.Colors.Rank);
        ApplyLeaderboardSettingsRow(
            RaceLeaderboardColumnKeys.Player,
            settings.Player,
            settings.TextEffects.Player,
            settings.Colors.Player);
        ApplyLeaderboardColorRow(RaceLeaderboardColumnKeys.PlayerSelf, settings.Colors.PlayerSelf);
        ApplyLeaderboardColorRow(RaceLeaderboardColumnKeys.PlayerOther, settings.Colors.PlayerOther);
        ApplyLeaderboardSettingsRow(
            RaceLeaderboardColumnKeys.Icon,
            settings.Icon,
            settings.TextEffects.Icon,
            settings.Colors.Icon);
        ApplyLeaderboardSettingsRow(
            RaceLeaderboardColumnKeys.Time,
            settings.Time,
            settings.TextEffects.Time,
            settings.Colors.Time);
        ApplyLeaderboardRankGradient(settings.Colors.RankGradient);
        return settings;
    }

    private void ApplyLeaderboardColorRow(string key, RaceLeaderboardColumnColorSettings colors)
    {
        if (!leaderboardColorControls.TryGetValue(key, out LeaderboardColorControls? colorControls))
        {
            return;
        }

        if (colorControls.Text is not null)
        {
            colors.Text = NormalizeColorBox(colorControls.Text, colors.Text);
        }

        colors.Outline = NormalizeColorBox(colorControls.Outline, colors.Outline);
        colors.Shadow = NormalizeColorBox(colorControls.Shadow, colors.Shadow);
    }

    private void ApplyLeaderboardRankGradient(RaceLeaderboardRankGradientColorSettings gradient)
    {
        if (leaderboardRankGradientControls is null)
        {
            return;
        }

        gradient.Start = NormalizeColorBox(leaderboardRankGradientControls.Start, gradient.Start);
        gradient.Middle = NormalizeColorBox(leaderboardRankGradientControls.Middle, gradient.Middle);
        gradient.End = NormalizeColorBox(leaderboardRankGradientControls.End, gradient.End);
    }

    private void ApplyLeaderboardSettingsRow(
        string key,
        UiColumnSettings column,
        RaceLeaderboardColumnEffectSettings effect,
        RaceLeaderboardColumnColorSettings colors)
    {
        if (!leaderboardColumnControls.TryGetValue(key, out LeaderboardColumnControls? columnControls))
        {
            return;
        }

        column.Show = columnControls.Show.Checked;
        column.Width = SettingsValueParser.ParseIntBox(columnControls.Width, column.Width, 1, 1000);
        if (columnControls.FontFamily is not null)
        {
            column.FontFamily = UiFontFactory.Default.NormalizeFamilyName(columnControls.FontFamily.SelectedFontFamily);
        }

        if (columnControls.FontSize is not null)
        {
            column.FontSize = SettingsValueParser.ParseFloatBox(columnControls.FontSize, column.FontSize, 6f, 96f);
        }

        if (columnControls.Bold is not null)
        {
            column.Bold = columnControls.Bold.Checked;
        }
        if (columnControls.Italic is not null)
        {
            column.Italic = columnControls.Italic.Checked;
        }
        effect.OpacityPercent = SettingsValueParser.ParseIntBox(columnControls.Opacity, effect.OpacityPercent, 0, 100);
        effect.ShadowPercent = SettingsValueParser.ParseIntBox(columnControls.Shadow, effect.ShadowPercent, 0, 100);
        effect.OutlineThicknessPercent = SettingsValueParser.ParseIntBox(columnControls.Outline, effect.OutlineThicknessPercent, 0, 100);
        ApplyLeaderboardColorRow(key, colors);
    }

    private static string NormalizeColorBox(TextBox textBox, string fallback)
    {
        return ColorText.Format(ColorText.Parse(textBox.Text, ColorText.Parse(fallback, Color.White)));
    }

    private static RaceLeaderboardColumnColorSettings CloneLeaderboardColumnColor(RaceLeaderboardColumnColorSettings source)
    {
        return new RaceLeaderboardColumnColorSettings
        {
            Text = source.Text,
            Outline = source.Outline,
            Shadow = source.Shadow
        };
    }

    private static RaceLeaderboardSettings CloneLeaderboardSettings(RaceLeaderboardSettings source)
    {
        return new RaceLeaderboardSettings
        {
            UseRankColorForMainTimer = source.UseRankColorForMainTimer,
            Rank = CloneColumnSettings(source.Rank),
            Player = CloneColumnSettings(source.Player),
            Icon = CloneColumnSettings(source.Icon),
            Time = CloneColumnSettings(source.Time),
            TextEffects = new RaceLeaderboardTextEffectSettings
            {
                Rank = CloneEffectSettings(source.TextEffects?.Rank),
                Player = CloneEffectSettings(source.TextEffects?.Player),
                Icon = CloneEffectSettings(source.TextEffects?.Icon),
                Time = CloneEffectSettings(source.TextEffects?.Time)
            },
            Colors = new RaceLeaderboardColorSettings
            {
                RankGradient = CloneRankGradient(source.Colors?.RankGradient),
                Rank = CloneLeaderboardColumnColor(source.Colors?.Rank ?? new RaceLeaderboardColumnColorSettings()),
                Player = CloneLeaderboardColumnColor(source.Colors?.Player ?? new RaceLeaderboardColumnColorSettings()),
                PlayerSelf = CloneLeaderboardColumnColor(source.Colors?.PlayerSelf ?? source.Colors?.Player ?? new RaceLeaderboardColumnColorSettings()),
                PlayerOther = CloneLeaderboardColumnColor(source.Colors?.PlayerOther ?? source.Colors?.Player ?? new RaceLeaderboardColumnColorSettings()),
                Icon = CloneLeaderboardColumnColor(source.Colors?.Icon ?? new RaceLeaderboardColumnColorSettings()),
                Time = CloneLeaderboardColumnColor(source.Colors?.Time ?? new RaceLeaderboardColumnColorSettings())
            }
        };
    }

    private static UiColumnSettings CloneColumnSettings(UiColumnSettings? source)
    {
        source ??= new UiColumnSettings();
        return new UiColumnSettings
        {
            Show = source.Show,
            Width = source.Width,
            FontFamily = source.FontFamily,
            FontSize = source.FontSize,
            Bold = source.Bold,
            Italic = source.Italic
        };
    }

    private static RaceLeaderboardColumnEffectSettings CloneEffectSettings(RaceLeaderboardColumnEffectSettings? source)
    {
        source ??= new RaceLeaderboardColumnEffectSettings();
        return new RaceLeaderboardColumnEffectSettings
        {
            OpacityPercent = source.OpacityPercent,
            ShadowPercent = source.ShadowPercent,
            OutlineThicknessPercent = source.OutlineThicknessPercent
        };
    }

    private static RaceLeaderboardRankGradientColorSettings CloneRankGradient(RaceLeaderboardRankGradientColorSettings? source)
    {
        source ??= new RaceLeaderboardRankGradientColorSettings();
        return new RaceLeaderboardRankGradientColorSettings
        {
            Start = source.Start,
            Middle = source.Middle,
            End = source.End
        };
    }

    private void ChooseWorldFile()
    {
        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = Localize("Terraria world files") + "|*.wld|" + Localize("All files") + "|*.*",
            Multiselect = false,
            Title = Localize("Choose world file")
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!RaceWorldFileValidator.IsValidWorldFilePath(dialog.FileName))
        {
            worldPathBox.Text = string.Empty;
            PersistDraftState();
            return;
        }

        worldPathBox.Text = dialog.FileName;
        PersistDraftState();
    }

    private void SyncLocalWorldPath()
    {
        RacePanelDraftState draft = shell.DraftState;
        if (!seedBox.Focused && !string.IsNullOrWhiteSpace(draft.SeedText))
        {
            seedBox.Text = draft.SeedText;
        }

        if (!string.IsNullOrWhiteSpace(shell.LocalWorldPath))
        {
            worldPathBox.Text = shell.LocalWorldPath;
            PersistDraftState();
        }
    }

    private void UpdateRoleVisibility(bool persist = true)
    {
        bool host = selectedRole == RacePanelRole.Host;
        if (hostSection is not null)
        {
            hostSection.Visible = host;
        }

        if (raceStatusSection is not null)
        {
            raceStatusSection.Visible = HasOpenRaceRoom(shell.State);
        }

        if (memberSection is not null)
        {
            memberSection.Visible = !host && !HasOpenRaceRoom(shell.State);
        }

        if (persist)
        {
            PersistDraftState();
        }

        UpdateConnectionInputLockState();
        UpdateRoomLifecycleButton();
        RefreshStatusRouteOverrideHint(shell.State);
    }

    private static bool HasOpenRaceRoom(RaceRoomState? state)
    {
        return state is not null &&
            state.Status != RaceRoomStatus.Closed;
    }

    private static bool HasUploadedOpenWorld(RaceRoomState? state)
    {
        return state is not null &&
            state.Status != RaceRoomStatus.Closed &&
            state.WorldFile is not null;
    }

    private bool IsLocalHost(RaceRoomState? state)
    {
        return state is null
            ? selectedRole == RacePanelRole.Host
            : shell.IsHostInCurrentRoom;
    }

    private void UpdateRoomLifecycleButton()
    {
        if (roomLifecycleButton is null)
        {
            return;
        }

        roomLifecycleButton.Text = Localize(IsLocalHost(shell.State) ? "Close room" : "Leave room");
    }

    private Task CloseOrLeaveRoomAsync()
    {
        return IsLocalHost(shell.State)
            ? shell.CloseRoomAsync()
            : shell.LeaveAsync();
    }

    private bool AreConnectionInputsLocked()
    {
        return HasOpenRaceRoom(shell.State) ||
            (selectedRole == RacePanelRole.Host &&
                (hostWorldActionCancellation is not null || hostWorldUploaded || HasUploadedOpenWorld(shell.State)));
    }

    private void UpdateConnectionInputLockState()
    {
        bool enabled = !AreConnectionInputsLocked();
        SetControlEnabled(serverBox, enabled);
        SetControlEnabled(nicknameBox, enabled);
        SetControlEnabled(hostRoleButton, enabled);
        SetControlEnabled(memberRoleButton, enabled);
    }

    private static void SetControlEnabled(Control? control, bool enabled)
    {
        if (control is not null && control.Enabled != enabled)
        {
            control.Enabled = enabled;
        }
    }

    private void SelectRole(RacePanelRole role)
    {
        RunSettingsLayoutBatch(() =>
        {
            selectedRole = role;
            SetRoleButtonState(hostRoleButton, role == RacePanelRole.Host);
            SetRoleButtonState(memberRoleButton, role == RacePanelRole.Member);
            UpdateRoleVisibility();
        });
        PersistDraftState();
    }

    private void SelectWorldSource(RacePanelWorldSource source)
    {
        RunSettingsLayoutBatch(() =>
        {
            RacePanelWorldSource previousSource = selectedWorldSource;
            selectedWorldSource = source;
            if (source == RacePanelWorldSource.ExistingFile &&
                previousSource != RacePanelWorldSource.ExistingFile &&
                !string.IsNullOrWhiteSpace(shell.LocalWorldPath) &&
                string.Equals(worldPathBox.Text, shell.LocalWorldPath, StringComparison.OrdinalIgnoreCase))
            {
                worldPathBox.Text = string.Empty;
            }

            SetWorldSourceButtonState(randomWorldSourceButton, source == RacePanelWorldSource.Random);
            SetWorldSourceButtonState(customSeedWorldSourceButton, source == RacePanelWorldSource.CustomSeed);
            SetWorldSourceButtonState(existingWorldFileSourceButton, source == RacePanelWorldSource.ExistingFile);
            UpdateWorldSourceVisibility();
        });
        PersistDraftState();
    }

    private static void SetRoleButtonState(CheckBox button, bool selected)
    {
        if (button.Checked != selected)
        {
            button.Checked = selected;
        }

        UpdateSelectorButtonState(button);
    }

    private void UpdateWorldSourceVisibility()
    {
        if (randomWorldConfig is not null)
        {
            randomWorldConfig.Visible = selectedWorldSource == RacePanelWorldSource.Random;
        }

        if (customSeedWorldConfig is not null)
        {
            customSeedWorldConfig.Visible = selectedWorldSource == RacePanelWorldSource.CustomSeed;
        }

        if (existingWorldFileConfig is not null)
        {
            existingWorldFileConfig.Visible = selectedWorldSource == RacePanelWorldSource.ExistingFile;
        }

        UpdateHostWorldActionButton();
    }

    private void UpdateCheatAvailability()
    {
        bool cheatsEnabled = cheatsEnabledBox.Checked;
        pyramidEnabledBox.Enabled = cheatsEnabled;
        crimsonEnabledBox.Enabled = cheatsEnabled && GetSelectedInt(evilBox, 2) == 2;
        UpdateSelectorButtonState(pyramidEnabledBox);
        UpdateSelectorButtonState(crimsonEnabledBox);

        foreach (CheckBox button in pyramidItemButtons.Values)
        {
            button.Enabled = cheatsEnabled && pyramidEnabledBox.Checked;
            UpdateSelectorButtonState(button);
        }

        bool crimsonDistanceEnabled = crimsonEnabledBox.Enabled && crimsonEnabledBox.Checked;
        foreach (CheckBox button in crimsonDistanceButtons.Values)
        {
            button.Enabled = crimsonDistanceEnabled;
            UpdateSelectorButtonState(button);
        }

        bool resourcesSupported = cheatsEnabled &&
            GetSelectedInt(sizeBox, 2) == 1 &&
            GetSelectedInt(evilBox, 2) == 2;
        foreach (CheckBox button in resourceItemButtons.Values)
        {
            button.Enabled = resourcesSupported;
            UpdateSelectorButtonState(button);
        }

        UpdateMinimumAvailability(lifeCrystalMinimumButtons, resourcesSupported);
        UpdateHookMinimumAvailability(resourcesSupported);
        UpdateMinimumAvailability(spelunkerMinimumButtons, resourcesSupported);
        UpdateMinimumAvailability(featherfallMinimumButtons, resourcesSupported);
    }

    private static void UpdateMinimumAvailability(
        IReadOnlyDictionary<int, CheckBox> buttons,
        bool supported)
    {
        bool enabled = supported && buttons.TryGetValue(0, out CheckBox? toggle) && toggle.Checked;
        foreach ((int value, CheckBox button) in buttons)
        {
            button.Enabled = supported && (value == 0 || enabled);
            UpdateSelectorButtonState(button);
        }
    }

    private void UpdateHookMinimumAvailability(bool supported)
    {
        bool enabled = supported &&
            hookMinimumButtons.TryGetValue(AutoCreateResourceHook.None, out CheckBox? toggle) &&
            toggle.Checked;
        foreach ((string hook, CheckBox button) in hookMinimumButtons)
        {
            button.Enabled = supported && (hook == AutoCreateResourceHook.None || enabled);
            UpdateSelectorButtonState(button);
        }
    }

    private void SelectMinimum(
        int selectedMinimum,
        bool selected,
        IReadOnlyList<int> values,
        Dictionary<int, CheckBox> buttons)
    {
        if (updatingResourceMinimumSelection)
        {
            return;
        }

        int normalized = selectedMinimum == 0
            ? selected ? values.FirstOrDefault(value => value > 0) : 0
            : values.Contains(selectedMinimum) ? selectedMinimum : 0;
        ApplyMinimumSelection(normalized, buttons);
        UpdateCheatAvailability();
    }

    private void ApplyMinimumSelection(int selectedMinimum, Dictionary<int, CheckBox> buttons)
    {
        updatingResourceMinimumSelection = true;
        try
        {
            bool enabled = selectedMinimum > 0;
            foreach ((int value, CheckBox button) in buttons)
            {
                button.Checked = enabled && (value == 0 || value >= selectedMinimum);
                UpdateSelectorButtonState(button);
            }
        }
        finally
        {
            updatingResourceMinimumSelection = false;
        }
    }

    private static int GetSelectedMinimum(
        IReadOnlyDictionary<int, CheckBox> buttons,
        IReadOnlyList<int> values)
    {
        if (!buttons.TryGetValue(0, out CheckBox? toggle) || !toggle.Checked)
        {
            return 0;
        }

        foreach (int value in values.Where(value => value > 0))
        {
            if (buttons.TryGetValue(value, out CheckBox? button) && button.Checked)
            {
                return value;
            }
        }

        return 0;
    }

    private void SelectHookMinimum(string selectedMinimum, bool selected)
    {
        if (updatingResourceMinimumSelection)
        {
            return;
        }

        string normalized = selectedMinimum == AutoCreateResourceHook.None
            ? selected ? AutoCreateResourceHook.Amethyst : AutoCreateResourceHook.None
            : selectedMinimum;
        ApplyHookMinimumSelection(normalized);
        UpdateCheatAvailability();
    }

    private void ApplyHookMinimumSelection(string selectedMinimum)
    {
        updatingResourceMinimumSelection = true;
        try
        {
            bool enabled = selectedMinimum != AutoCreateResourceHook.None;
            foreach ((string hook, CheckBox button) in hookMinimumButtons)
            {
                button.Checked = enabled &&
                    (hook == AutoCreateResourceHook.None || AutoCreateResourceHook.Includes(selectedMinimum, hook));
                UpdateSelectorButtonState(button);
            }
        }
        finally
        {
            updatingResourceMinimumSelection = false;
        }
    }

    private string GetSelectedHookMinimum()
    {
        if (!hookMinimumButtons.TryGetValue(AutoCreateResourceHook.None, out CheckBox? toggle) ||
            !toggle.Checked)
        {
            return AutoCreateResourceHook.None;
        }

        foreach (string hook in AutoCreateResourceHook.All.Where(hook => hook != AutoCreateResourceHook.None))
        {
            if (hookMinimumButtons.TryGetValue(hook, out CheckBox? button) && button.Checked)
            {
                return hook;
            }
        }

        return AutoCreateResourceHook.None;
    }

    private void SelectCrimsonDistance(string selectedDistance)
    {
        if (!updatingCrimsonDistanceSelection)
        {
            ApplyCrimsonDistanceSelection(selectedDistance);
        }
    }

    private void ApplyCrimsonDistanceSelection(string selectedDistance)
    {
        updatingCrimsonDistanceSelection = true;
        try
        {
            foreach ((string distance, CheckBox button) in crimsonDistanceButtons)
            {
                button.Checked = AutoCreateCrimsonDistance.Includes(selectedDistance, distance);
                UpdateSelectorButtonState(button);
            }
        }
        finally
        {
            updatingCrimsonDistanceSelection = false;
        }
    }

    private string GetSelectedCrimsonDistance()
    {
        for (int index = AutoCreateCrimsonDistance.All.Length - 1; index >= 0; index--)
        {
            string distance = AutoCreateCrimsonDistance.All[index];
            if (crimsonDistanceButtons.TryGetValue(distance, out CheckBox? button) && button.Checked)
            {
                return distance;
            }
        }

        return AutoCreateCrimsonDistance.Default;
    }

    private int GetSpecialSeedMask()
    {
        if (selectedWorldSource != RacePanelWorldSource.Random)
        {
            return 0;
        }

        int mask = 0;
        foreach ((string seed, CheckBox button) in specialSeedButtons)
        {
            if (button.Checked)
            {
                mask |= TerrariaWorldSeedOptions.SpecialSeedMask(seed);
            }
        }

        return mask;
    }

    private void ApplySpecialSeedMask(int mask)
    {
        bool zenithSelected = HasZenithSeedMask(mask);
        foreach ((string seed, CheckBox button) in specialSeedButtons)
        {
            int seedMask = TerrariaWorldSeedOptions.SpecialSeedMask(seed);
            button.Checked = seedMask != 0 &&
                (mask & seedMask) == seedMask &&
                !(zenithSelected && AutoCreateSpecialWorldSeed.IsZenithDependency(seed));
            UpdateSelectorButtonState(button);
        }

        UpdateSpecialSeedAvailability();
    }

    private static bool HasZenithSeedMask(int mask)
    {
        int dependencyMask = 0;
        foreach (string seed in AutoCreateSpecialWorldSeed.All)
        {
            if (AutoCreateSpecialWorldSeed.IsZenithDependency(seed))
            {
                dependencyMask |= TerrariaWorldSeedOptions.SpecialSeedMask(seed);
            }
        }

        int zenithOnlyMask = TerrariaWorldSeedOptions.SpecialSeedMask(AutoCreateSpecialWorldSeed.Zenith) & ~dependencyMask;
        return zenithOnlyMask != 0 && (mask & zenithOnlyMask) == zenithOnlyMask;
    }

    private void UpdateSpecialSeedAvailability()
    {
        bool zenithSelected = specialSeedButtons.TryGetValue(AutoCreateSpecialWorldSeed.Zenith, out CheckBox? zenithBox) &&
            zenithBox.Checked;

        foreach ((string seed, CheckBox button) in specialSeedButtons)
        {
            if (AutoCreateSpecialWorldSeed.IsZenithDependency(seed))
            {
                if (zenithSelected)
                {
                    button.Checked = false;
                }

                button.Enabled = !zenithSelected;
            }
            else
            {
                button.Enabled = true;
            }

            UpdateSelectorButtonState(button);
        }
    }

    private static void SetWorldSourceButtonState(CheckBox button, bool selected)
    {
        if (button.Checked != selected)
        {
            button.Checked = selected;
        }

        UpdateSelectorButtonState(button);
    }

    private static void UpdateSelectorButtonState(CheckBox button)
    {
        button.BackColor = !button.Enabled
            ? UiTheme.Surface
            : button.Checked
                ? UiTheme.Selection
                : UiTheme.SurfaceRaised;
        button.ForeColor = button.Enabled ? UiTheme.Text : UiTheme.MutedText;
        button.FlatAppearance.BorderColor = button.Checked && button.Enabled ? UiTheme.Accent : UiTheme.Border;
        button.FlatAppearance.CheckedBackColor = UiTheme.Selection;
        button.FlatAppearance.MouseOverBackColor = button.Checked
            ? SelectorButtonSelectedHover
            : SelectorButtonHover;
        button.FlatAppearance.MouseDownBackColor = button.Checked
            ? SelectorButtonSelectedDown
            : SelectorButtonDown;
        button.Invalidate();
    }

    private static int GetSelectedInt(ThemedDropDownList dropDown, int fallback)
    {
        return dropDown.SelectedItem is OptionItem { Value: int value } ? value : fallback;
    }

    private static Label CreateValueLabel()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            AutoSize = false,
            ForeColor = UiTheme.Text,
            Font = UiTheme.FormFont(10f, FontStyle.Bold),
            Margin = Padding.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label CreateMutedStatusLabel()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            AutoSize = false,
            ForeColor = UiTheme.MutedText,
            Font = UiTheme.FormFont(9.5f),
            Margin = Padding.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private FlowLayoutPanel CreateButtonRow()
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 10, 0, 0),
            Padding = Padding.Empty,
            WrapContents = true
        };
        UiTheme.EnableDoubleBuffering(panel);
        return panel;
    }

    private Button AddButton(
        FlowLayoutPanel panel,
        string text,
        bool accent,
        Func<Task> action)
    {
        Button button = CreateActionButton(text, accent, action, minimumWidth: 128);
        panel.Controls.Add(button);
        return button;
    }

    private Button CreateActionButton(
        string text,
        bool accent,
        Func<Task> action,
        int minimumWidth)
    {
        Button button = uiFactory.CreateButton(text, accent, minimumWidth);
        button.Margin = new Padding(0, 0, 8, 8);
        button.Click += async (_, _) => await RunActionAsync(button, action);
        return button;
    }

    private async Task RunActionAsync(Button button, Func<Task> action)
    {
        button.Enabled = false;
        try
        {
            PersistDraftState();
            await action();
            PersistDraftState();
        }
        catch
        {
        }
        finally
        {
            if (!button.IsDisposed)
            {
                button.Enabled = true;
            }
        }
    }

    private void AddField(
        TableLayoutPanel grid,
        string label,
        Control control,
        int column,
        int row)
    {
        grid.Controls.Add(uiFactory.CreateRowLabel(label), column, row);
        grid.Controls.Add(control, column + 1, row);
    }

    private static int AddGridRow(TableLayoutPanel grid)
    {
        return AddGridRow(grid, 56f);
    }

    private static int AddGridRow(TableLayoutPanel grid, float height)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, UiDpiScale.ScaleFloatForControl(grid, height)));
        return row;
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

    private string Localize(string key)
    {
        return shell.Localize(key);
    }

    private sealed class RaceActionProgressBar : Control
    {
        private int value;

        public RaceActionProgressBar()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = UiTheme.Surface;
            ForeColor = UiTheme.Text;
            Font = UiTheme.FormFont(9.5f);
            Height = 52;
            MinimumSize = new Size(0, 52);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => value;
            set
            {
                int clamped = Math.Clamp(value, 0, 100);
                if (this.value == clamped)
                {
                    return;
                }

                this.value = clamped;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);

            Rectangle frame = GetFrameBounds();
            Rectangle fill = frame;
            fill.Inflate(-1, -1);
            fill.Width = (int)Math.Round(fill.Width * (Value / 100d));

            using var frameBrush = new SolidBrush(UiTheme.Field);
            using var fillBrush = new SolidBrush(UiTheme.Accent);
            using var borderPen = new Pen(UiTheme.Border);
            FillRoundedRectangle(e.Graphics, frameBrush, frame, UiDpiScale.ScaleIntFromBase200(8));
            if (fill.Width > 0)
            {
                FillRoundedRectangle(e.Graphics, fillBrush, fill, UiDpiScale.ScaleIntFromBase200(7));
            }

            DrawRoundedRectangle(e.Graphics, borderPen, frame, UiDpiScale.ScaleIntFromBase200(8));
            string text = Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
            TextRenderer.DrawText(
                e.Graphics,
                text,
                Font,
                frame,
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private Rectangle GetFrameBounds()
        {
            int height = Math.Min(ClientSize.Height, UiDpiScale.ScaleIntFromBase200(44));
            int top = Math.Max(0, (ClientSize.Height - height) / 2);
            return new Rectangle(0, top, Math.Max(0, ClientSize.Width - 1), Math.Max(0, height - 1));
        }

        private static void FillRoundedRectangle(Graphics graphics, Brush brush, Rectangle bounds, int radius)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            if (radius <= 0)
            {
                graphics.FillRectangle(brush, bounds);
                return;
            }

            int effectiveRadius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
            int diameter = effectiveRadius * 2;
            using var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            graphics.FillPath(brush, path);
        }

        private static void DrawRoundedRectangle(Graphics graphics, Pen pen, Rectangle bounds, int radius)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            if (radius <= 0)
            {
                graphics.DrawRectangle(pen, bounds);
                return;
            }

            int effectiveRadius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
            int diameter = effectiveRadius * 2;
            using var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            graphics.DrawPath(pen, path);
        }
    }

    private sealed record OptionItem(string Text, object Value)
    {
        public override string ToString()
        {
            return Text;
        }
    }

    private sealed record LeaderboardColumnControls(
        CheckBox Show,
        TextBox Width,
        FontFamilySelector? FontFamily,
        TextBox? FontSize,
        CheckBox? Bold,
        CheckBox? Italic,
        TextBox Opacity,
        TextBox Shadow,
        TextBox Outline);

    private sealed record LeaderboardRankGradientControls(
        TextBox Start,
        TextBox Middle,
        TextBox End);

    private sealed record LeaderboardColorControls(
        TextBox? Text,
        TextBox Outline,
        TextBox Shadow);

    private static class RaceLeaderboardColumnKeys
    {
        public const string Rank = "RaceLeaderboardRank";
        public const string Player = "RaceLeaderboardPlayer";
        public const string PlayerSelf = "RaceLeaderboardPlayerSelf";
        public const string PlayerOther = "RaceLeaderboardPlayerOther";
        public const string Icon = "RaceLeaderboardIcon";
        public const string Time = "RaceLeaderboardTime";
    }
}
