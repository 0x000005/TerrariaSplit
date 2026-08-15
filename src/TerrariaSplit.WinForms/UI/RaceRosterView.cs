using System.Drawing;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.UI;

internal sealed class RaceRosterView : TableLayoutPanel
{
    private const float HeaderHeight = 64f;
    private const float RowHeight = 64f;
    private static readonly Color StatusSuccess = Color.FromArgb(91, 204, 139);
    private static readonly Color StatusFailure = Color.FromArgb(235, 99, 99);
    private readonly Func<string, string> localize;
    private readonly Func<string, Task> kickPlayer;
    private readonly SettingsUiFactory uiFactory;

    public RaceRosterView(Func<string, string> localize, Func<string, Task> kickPlayer)
    {
        this.localize = localize;
        this.kickPlayer = kickPlayer;
        uiFactory = new SettingsUiFactory(localize);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = UiTheme.Surface;
        ColumnCount = 6;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 10, 0, 0);
        Padding = Padding.Empty;
        ColumnStyles.Add(SettingsUiFactory.ColumnStylePercent(28f));
        ColumnStyles.Add(SettingsUiFactory.ColumnStylePercent(18f));
        ColumnStyles.Add(SettingsUiFactory.ColumnStylePercent(18f));
        ColumnStyles.Add(SettingsUiFactory.ColumnStylePercent(18f));
        ColumnStyles.Add(SettingsUiFactory.ColumnStylePercent(18f));
        ColumnStyles.Add(SettingsUiFactory.ColumnStyleAbsolute(132f));
        UiTheme.EnableDoubleBuffering(this);
    }

    public void UpdateRoster(
        RaceRoomState? state,
        string? localNickname,
        RaceServerConnectionStatus localConnectionStatus,
        bool canKickPlayers)
    {
        SuspendLayout();
        try
        {
            Controls.Clear();
            RowStyles.Clear();
            RowCount = 0;
            AddHeaderRow();

            IReadOnlyList<RacePlayerState> players = state?.Players ?? Array.Empty<RacePlayerState>();
            if (players.Count == 0)
            {
                int emptyRow = AddRow(RowHeight);
                Label emptyLabel = uiFactory.CreateMutedLabel("No players");
                Controls.Add(emptyLabel, 0, emptyRow);
                SetColumnSpan(emptyLabel, 6);
                return;
            }

            foreach (RacePlayerState player in players)
            {
                AddPlayerRow(player, localNickname, localConnectionStatus, canKickPlayers);
            }
        }
        finally
        {
            ResumeLayout(true);
            PerformLayout();
        }
    }

    private void AddHeaderRow()
    {
        int row = AddRow(HeaderHeight);
        Controls.Add(uiFactory.CreateHeaderLabel("Player"), 0, row);
        Controls.Add(uiFactory.CreateHeaderLabel("Player file", ContentAlignment.MiddleCenter), 1, row);
        Controls.Add(uiFactory.CreateHeaderLabel("World file", ContentAlignment.MiddleCenter), 2, row);
        Controls.Add(uiFactory.CreateHeaderLabel("RNG control", ContentAlignment.MiddleCenter), 3, row);
        Controls.Add(uiFactory.CreateHeaderLabel("Server connection", ContentAlignment.MiddleCenter), 4, row);
        Controls.Add(uiFactory.CreateHeaderLabel(string.Empty, ContentAlignment.MiddleCenter), 5, row);
    }

    private void AddPlayerRow(
        RacePlayerState player,
        string? localNickname,
        RaceServerConnectionStatus localConnectionStatus,
        bool canKickPlayers)
    {
        int row = AddRow(RowHeight);
        Label nameLabel = uiFactory.CreateRawRowLabel(player.IsHost
            ? player.Nickname + " (" + localize("Host") + ")"
            : player.Nickname);
        Label playerFileLabel = CreateStatusLabel(
            Localize(player.PlayerFileStatus),
            player.PlayerFileStatus == RacePlayerFileStatus.Ready,
            player.PlayerFileStatus == RacePlayerFileStatus.Failed);
        Label worldFileLabel = CreateStatusLabel(
            Localize(player.WorldFileStatus),
            player.WorldFileStatus == RaceWorldFileStatus.Ready,
            player.WorldFileStatus == RaceWorldFileStatus.Failed);
        Label rngControlLabel = CreateStatusLabel(
            Localize(player.RngControlStatus),
            player.RngControlStatus == RaceRngControlStatus.Enabled,
            player.RngControlStatus == RaceRngControlStatus.EnableFailed);
        RaceServerConnectionStatus connectionStatus = ResolveConnectionStatus(
            player,
            localNickname,
            localConnectionStatus);
        Label connectionLabel = CreateStatusLabel(
            Localize(connectionStatus),
            connectionStatus == RaceServerConnectionStatus.Connected,
            connectionStatus == RaceServerConnectionStatus.ConnectionFailed);

        AddCell(nameLabel, 0, row);
        AddCell(playerFileLabel, 1, row);
        AddCell(worldFileLabel, 2, row);
        AddCell(rngControlLabel, 3, row);
        AddCell(connectionLabel, 4, row);
        AddCell(CreateActionControl(player, canKickPlayers), 5, row);
    }

    private Control CreateActionControl(RacePlayerState player, bool canKickPlayers)
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
            button.Enabled = false;
            try
            {
                await kickPlayer(player.Nickname);
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
        };
        return button;
    }

    private int AddRow(float height)
    {
        int row = RowCount++;
        RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        return row;
    }

    private void AddCell(Control control, int column, int row)
    {
        if (control is Label label)
        {
            label.AutoEllipsis = true;
            label.Margin = new Padding(6, 0, 6, 0);
            label.TextAlign = column == 0 ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter;
        }

        Controls.Add(control, column, row);
    }

    private Label CreateStatusLabel(string text, bool succeeded, bool failed)
    {
        Label label = uiFactory.CreateRawRowLabel(text);
        label.ForeColor = succeeded
            ? StatusSuccess
            : failed
                ? StatusFailure
                : UiTheme.Text;
        return label;
    }

    private static RaceServerConnectionStatus ResolveConnectionStatus(
        RacePlayerState player,
        string? localNickname,
        RaceServerConnectionStatus localConnectionStatus)
    {
        return !string.IsNullOrWhiteSpace(localNickname) &&
            string.Equals(player.Nickname, localNickname, StringComparison.OrdinalIgnoreCase)
                ? localConnectionStatus
                : player.ServerConnectionStatus;
    }

    private string Localize(RacePlayerFileStatus status) => status switch
    {
        RacePlayerFileStatus.Creating => localize("Creating"),
        RacePlayerFileStatus.Ready => localize("Ready"),
        RacePlayerFileStatus.Failed => localize("Failed"),
        _ => localize("Waiting")
    };

    private string Localize(RaceWorldFileStatus status) => status switch
    {
        RaceWorldFileStatus.Downloading => localize("Downloading"),
        RaceWorldFileStatus.Ready => localize("Ready"),
        RaceWorldFileStatus.Failed => localize("Failed"),
        _ => localize("Waiting")
    };

    private string Localize(RaceRngControlStatus status) => status switch
    {
        RaceRngControlStatus.Enabling => localize("Enabling"),
        RaceRngControlStatus.Enabled => localize("Enabled"),
        RaceRngControlStatus.EnableFailed => localize("Enable failed"),
        RaceRngControlStatus.NotEnabled => localize("Not enabled"),
        _ => localize("Closed")
    };

    private string Localize(RaceServerConnectionStatus status) => status switch
    {
        RaceServerConnectionStatus.Connecting => localize("Connecting"),
        RaceServerConnectionStatus.Connected => localize("Connected"),
        RaceServerConnectionStatus.Reconnecting => localize("Reconnecting"),
        RaceServerConnectionStatus.ConnectionFailed => localize("Connection failed"),
        _ => localize("Disconnected")
    };
}
