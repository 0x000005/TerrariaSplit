using System.ComponentModel;
using System.Windows;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.UI.Wpf.Controls;
using TerrariaSplit.UI.Wpf.Race;
using TerrariaSplit.UI.Wpf.Settings;
using WpfRadioButton = System.Windows.Controls.RadioButton;

namespace TerrariaSplit.UI.Wpf.Windows;

internal sealed partial class RaceWindow : ModernWindow
{
    private const string IconConnection = "M 3.5,9.5 A 4.8,4.8 0 0 1 10.5,9.5 M 1.5,7 A 7.8,7.8 0 0 1 12.5,7 M 7,11.5 A 0.8,0.8 0 1 1 6.99,11.5 Z";
    private const string IconPenalty = "M 7,2.2 A 4.8,4.8 0 1 1 6.99,2.2 Z M 7,4.2 V 7.2 L 9.2,8.4";
    private const string IconUi = "M 1.5,2.5 H 12.5 V 11.5 H 1.5 Z M 1.5,5.5 H 12.5 M 5.5,5.5 V 11.5";
    private const string IconColors = "M 7,1.5 C 3.8,1.5 1.5,4 1.5,7 C 1.5,10 3.8,12.5 7,12.5 C 8.4,12.5 9.2,11.7 9.2,10.7 C 9.2,10 8.8,9.4 9.6,9.1 C 11.4,8.6 12.5,7.4 12.5,5.8 C 12.5,3.3 10.1,1.5 7,1.5 Z";
    private const string IconVoice = "M 2,5.5 H 4.5 L 7.8,2.8 V 11.2 L 4.5,8.5 H 2 Z M 9.8,4.8 C 10.8,5.8 10.8,8.2 9.8,9.2 M 11.5,3.2 C 13,5 13,9 11.5,10.8";

    private enum RacePageId
    {
        Connection,
        Penalty,
        Interface,
        Colors,
        Voice
    }

    private readonly IRacePanelShell shell;
    private readonly WpfSettingsDialogService dialogs;
    private readonly WpfRaceConnectionPage connectionPage;
    private readonly WpfRacePenaltyPage penaltyPage;
    private readonly WpfRaceLeaderboardPages leaderboardPages;
    private readonly WpfRaceVoicePage voicePage;
    private readonly Dictionary<RacePageId, FrameworkElement> pageElements = new();
    private readonly Dictionary<RacePageId, WpfRadioButton> navButtons = new();
    private RacePageId? selectedPage;

    public RaceWindow(IRacePanelShell shell)
    {
        WpfBootstrap.EnsureInitialized();
        InitializeComponent();

        this.shell = shell;
        dialogs = new WpfSettingsDialogService(this, shell.Localize);
        connectionPage = new WpfRaceConnectionPage(shell, dialogs);
        penaltyPage = new WpfRacePenaltyPage(shell);
        leaderboardPages = new WpfRaceLeaderboardPages(shell, dialogs);
        voicePage = new WpfRaceVoicePage(shell);

        Title = shell.Localize("Race");

        AddPage(RacePageId.Connection, "Connection", IconConnection, connectionPage.Build());
        AddPage(RacePageId.Penalty, "Penalty", IconPenalty, penaltyPage.Build());
        AddPage(RacePageId.Interface, "UI", IconUi, leaderboardPages.BuildInterfacePage());
        AddPage(RacePageId.Colors, "Colors", IconColors, leaderboardPages.BuildColorsPage());
        AddPage(RacePageId.Voice, "Voice", IconVoice, voicePage.Build());
        SelectPage(RacePageId.Connection);
    }

    internal int PageCount => pageElements.Count;

    public void UpdateRaceState(RaceRoomState? state)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(new Action(() => UpdateRaceState(state)));
            return;
        }

        connectionPage.UpdateState(state);
        penaltyPage.UpdateState(state);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        connectionPage.PersistDraftState();
        base.OnClosing(e);
    }

    private void AddPage(RacePageId id, string titleKey, string iconGeometry, FrameworkElement content)
    {
        content.Visibility = Visibility.Collapsed;
        pageElements[id] = content;
        PageHostGrid.Children.Add(content);

        WpfRadioButton nav = WpfSettingsUi.CreateNavRadio(shell.Localize(titleKey), iconGeometry);
        nav.GroupName = "RaceNav";
        nav.Checked += (_, _) =>
        {
            if (selectedPage != id)
            {
                SelectPage(id);
            }
        };
        navButtons[id] = nav;
        NavPanel.Children.Add(nav);
    }

    private void SelectPage(RacePageId id)
    {
        selectedPage = id;
        foreach ((RacePageId key, FrameworkElement element) in pageElements)
        {
            element.Visibility = key == id ? Visibility.Visible : Visibility.Collapsed;
        }

        foreach ((RacePageId key, WpfRadioButton nav) in navButtons)
        {
            bool isSelected = key == id;
            if (nav.IsChecked != isSelected)
            {
                nav.IsChecked = isSelected;
            }
        }
    }
}
