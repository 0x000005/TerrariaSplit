using System.Windows;
using System.Windows.Controls;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.UI.Wpf.Settings;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Race;

internal sealed class WpfRaceConnectionPage
{
    private readonly IRacePanelShell shell;
    private readonly WpfSettingsDialogService dialogs;
    private readonly RacePanelDraftState initialDraft;

    private WpfButton raceModeButton = null!;
    private WpfTextBox serverBox = null!;
    private WpfTextBox nicknameBox = null!;
    private WpfTextBox playerTemplateCodeBox = null!;

    private bool raceModeChangeRunning;
    private bool programmaticUpdate;

    public WpfRaceConnectionPage(IRacePanelShell shell, WpfSettingsDialogService dialogs)
    {
        this.shell = shell;
        this.dialogs = dialogs;
        initialDraft = shell.DraftState;
    }

    public FrameworkElement Build()
    {
        return WpfSettingsUi.CreateScrollPage(parent =>
        {
            var card = WpfSettingsUi.CreateCard(Localize("Connection"), out StackPanel body);

            raceModeButton = WpfSettingsUi.CreateButton(
                Localize(shell.IsRaceEnabled ? "Exit Race mode" : "Enter Race mode"),
                accent: !shell.IsRaceEnabled,
                minWidth: 136);
            raceModeButton.Click += async (_, _) => await ToggleRaceModeAsync();

            serverBox = WpfSettingsUi.CreateTextBox(initialDraft.ServerUrl, width: WpfSettingsUi.WideFieldWidth);
            nicknameBox = WpfSettingsUi.CreateTextBox(initialDraft.Nickname, width: WpfSettingsUi.WideFieldWidth);
            nicknameBox.MaxLength = RacePlayerNameRules.MaximumLength;

            playerTemplateCodeBox = WpfSettingsUi.CreateMultilineTextBox(initialDraft.PlayerTemplateCode, height: 96);

            WpfSettingsUi.AddSettingRow(body, Localize("Race mode"), raceModeButton);
            WpfSettingsUi.AddSettingRow(body, Localize("Server"), serverBox);
            WpfSettingsUi.AddSettingRow(body, Localize("Username"), nicknameBox);
            TextBlock playerCodeLabel = WpfSettingsUi.CreateRowLabel(Localize("Player code"));
            playerCodeLabel.Margin = new Thickness(0, 8, 0, 2);
            body.Children.Add(playerCodeLabel);
            body.Children.Add(playerTemplateCodeBox);

            parent.Children.Add(card);
            UpdateState(shell.State);
        });
    }

    public void UpdateState(RaceRoomState? state)
    {
        if (raceModeButton is null)
        {
            return;
        }

        programmaticUpdate = true;
        try
        {
            bool inRace = shell.IsRaceEnabled;
            raceModeButton.Content = Localize(inRace ? "Exit Race mode" : "Enter Race mode");
            raceModeButton.IsEnabled = !raceModeChangeRunning;
            raceModeButton.SetResourceReference(
                FrameworkElement.StyleProperty,
                inRace ? typeof(WpfButton) : "AccentButtonStyle");

            bool inputsEnabled = !inRace && !raceModeChangeRunning && !HasOpenRaceRoom(state);
            serverBox.IsEnabled = inputsEnabled;
            nicknameBox.IsEnabled = inputsEnabled;
            playerTemplateCodeBox.IsEnabled = inputsEnabled;
        }
        finally
        {
            programmaticUpdate = false;
        }
    }

    public void PersistDraftState()
    {
        if (programmaticUpdate || serverBox is null)
        {
            return;
        }

        RacePanelDraftState current = shell.DraftState;
        shell.SaveDraftState(new RacePanelDraftState(
            serverBox.Text,
            nicknameBox.Text,
            current.RoomCode,
            current.SeedText,
            current.LocalWorldPath,
            playerTemplateCodeBox.Text,
            current.Role,
            current.WorldSource));
    }

    private async Task ToggleRaceModeAsync()
    {
        if (raceModeChangeRunning)
        {
            return;
        }

        raceModeChangeRunning = true;
        UpdateState(shell.State);
        try
        {
            PersistDraftState();
            if (shell.IsRaceEnabled)
            {
                if (HasOpenRaceRoom(shell.State))
                {
                    await (shell.IsHostInCurrentRoom ? shell.CloseRoomAsync() : shell.LeaveAsync());
                }

                shell.SaveRaceEnabled(false);
            }
            else
            {
                shell.SaveRaceEnabled(true);
                if (shell.IsRaceEnabled)
                {
                    shell.OpenInGameMenu();
                }
            }
        }
        catch (Exception ex)
        {
            dialogs.ShowWarning(ex.Message, Localize("Race"));
        }
        finally
        {
            raceModeChangeRunning = false;
            UpdateState(shell.State);
        }
    }

    private static bool HasOpenRaceRoom(RaceRoomState? state) =>
        state is not null && state.Status != RaceRoomStatus.Closed;

    private string Localize(string key) => shell.Localize(key);
}
