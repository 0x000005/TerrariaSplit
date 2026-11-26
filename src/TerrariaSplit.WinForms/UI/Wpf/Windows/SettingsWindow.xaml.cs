using System.Windows;
using TerrariaSplit.UI.Wpf.Controls;
using TerrariaSplit.UI.Wpf.Settings;
using FormsDialogResult = System.Windows.Forms.DialogResult;

namespace TerrariaSplit.UI.Wpf.Windows;

internal sealed partial class SettingsWindow : ModernWindow, IWpfSettingsWindowOwner, IDisposable
{
    private const string IconGeneral = "M 14,17 H 5 M 19,7 H 10 M 20,17 A 3,3 0 1 0 14,17 A 3,3 0 1 0 20,17 Z M 10,7 A 3,3 0 1 0 4,7 A 3,3 0 1 0 10,7 Z";
    private const string IconRoute = "M 9,5 H 17.5 A 3.5,3.5 0 0 1 17.5,12 H 6.5 A 3.5,3.5 0 0 0 6.5,19 H 15 M 21,19 A 3,3 0 1 0 15,19 A 3,3 0 1 0 21,19 Z M 9,5 A 3,3 0 1 0 3,5 A 3,3 0 1 0 9,5 Z";
    private const string IconData = "M 15,3 V 21 M 5,3 H 19 A 2,2 0 0 1 21,5 V 19 A 2,2 0 0 1 19,21 H 5 A 2,2 0 0 1 3,19 V 5 A 2,2 0 0 1 5,3 Z M 21,9 H 3 M 21,15 H 3";
    private const string IconUi = "M 1.5,2.5 H 12.5 V 11.5 H 1.5 Z M 1.5,5.5 H 12.5 M 5.5,5.5 V 11.5";
    private const string IconEffects = "M 7,1.5 L 8.4,5.6 L 12.5,7 L 8.4,8.4 L 7,12.5 L 5.6,8.4 L 1.5,7 L 5.6,5.6 Z";
    private const string IconAutomation = "M 7.8,1.5 L 2.8,7.8 H 6.8 L 6.2,12.5 L 11.2,6.2 H 7.2 Z";
    private const string IconSounds = "M 2,5.5 H 4.5 L 7.8,2.8 V 11.2 L 4.5,8.5 H 2 Z M 9.8,4.8 C 10.8,5.8 10.8,8.2 9.8,9.2 M 11.5,3.2 C 13,5 13,9 11.5,10.8";
    private const string IconColors = "M 12,22 A 10,10 0 0 1 12,2 A 10,9 0 0 1 22,11 A 5,5 0 0 1 17,16 H 14.75 A 1.75,1.75 0 0 0 13.35,18.8 L 13.65,19.2 A 1.75,1.75 0 0 1 12.25,22 Z M 14,6.5 A 0.5,0.5 0 1 0 13,6.5 A 0.5,0.5 0 1 0 14,6.5 Z M 18,10.5 A 0.5,0.5 0 1 0 17,10.5 A 0.5,0.5 0 1 0 18,10.5 Z M 7,12.5 A 0.5,0.5 0 1 0 6,12.5 A 0.5,0.5 0 1 0 7,12.5 Z M 9,7.5 A 0.5,0.5 0 1 0 8,7.5 A 0.5,0.5 0 1 0 9,7.5 Z";
    private const string IconAdvanced = "M 2,4 H 12 M 2,10 H 12 M 5,2.2 V 5.8 M 9,8.2 V 11.8";
    private const string IconAbout = "M 7,1.5 A 5.5,5.5 0 1 1 6.99,1.5 Z M 7,6.2 V 10.2 M 7,4.2 V 4.3";

    private readonly AppSettings settings;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly WpfSettingsDialogService dialogService;
    private readonly IApplicationUpdateService applicationUpdateService;
    private readonly Func<bool> canSaveSettings;
    private readonly WpfSettingsPageHost pageHost;
    private bool disposed;

    public SettingsWindow(
        AppSettings currentSettings,
        SettingsMessageBoxPresenter? messageBoxPresenter = null,
        ISettingsSnapshotFactory? settingsSnapshots = null,
        IApplicationUpdateService? applicationUpdateService = null,
        Func<bool>? canSaveSettings = null)
    {
        WpfBootstrap.EnsureInitialized();
        InitializeComponent();

        this.settingsSnapshots = settingsSnapshots ?? new SettingsSnapshotFactory();
        settings = this.settingsSnapshots.CreateSnapshot(currentSettings);
        this.applicationUpdateService = applicationUpdateService ?? new GitHubApplicationUpdateService();
        this.canSaveSettings = canSaveSettings ?? (() => true);
        dialogService = new WpfSettingsDialogService(this, Localize, messageBoxPresenter);

        Title = Localize("TerrariaSplit Settings");
        OkActionButton.Content = Localize("OK");
        ApplyActionButton.Content = Localize("Apply");
        CancelActionButton.Content = Localize("Cancel");

        OkActionButton.Click += (_, _) => CommitAndClose();
        ApplyActionButton.Click += (_, _) => ApplyAndNotify();
        CancelActionButton.Click += (_, _) =>
        {
            CloseResult = FormsDialogResult.Cancel;
            Close();
        };

        pageHost = new WpfSettingsPageHost(this, settings, dialogService, this.settingsSnapshots, PageHostGrid);
        pageHost.Register("General", IconGeneral, new WpfGeneralSettingsPage());
        pageHost.Register("Route", IconRoute, new WpfSplitSettingsPage());
        pageHost.Register("Data", IconData, new WpfDataSettingsPage());
        pageHost.Register("UI", IconUi, new WpfUiSettingsPage());
        pageHost.Register("Effects", IconEffects, new WpfAnimationSettingsPage());
        pageHost.Register("Automation", IconAutomation, new WpfAutomationSettingsPage());
        pageHost.Register("Sounds", IconSounds, new WpfSoundSettingsPage());
        pageHost.Register("Colors", IconColors, new WpfColorSettingsPage());
        pageHost.Register("Advanced", IconAdvanced, new WpfAdvancedSettingsPage());
        pageHost.Register("About", IconAbout, new WpfAboutSettingsPage(this.applicationUpdateService), pinBottom: true);
        pageHost.AttachNavigation(PrimaryNavPanel, BottomNavPanel);
        pageHost.Select(SettingsPageId.General);
    }

    public AppSettings Result => settings;

    public FormsDialogResult CloseResult { get; private set; } = FormsDialogResult.Cancel;

    internal WpfSettingsPageHost PageHost => pageHost;

    public Window Window => this;

    public bool IsDisposed => disposed;

    public event EventHandler? Applied;

    internal event Action<PreparedApplicationUpdate>? UpdateRestartRequested;

    public string Localize(string key)
    {
        return Localizer.Get(key, settings);
    }

    public bool RequestApplicationUpdate(PreparedApplicationUpdate update)
    {
        if (!CanSaveSettings() || !TryApplyToSettings(showError: true, out _))
        {
            return false;
        }

        if (UpdateRestartRequested is null)
        {
            dialogService.ShowWarning(
                Localize("Update could not be started."),
                Localize("TerrariaSplit Update"));
            return false;
        }

        UpdateRestartRequested(update);
        return true;
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

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        pageHost.DisposePages();
        applicationUpdateService.Dispose();
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    private void CommitAndClose()
    {
        if (!CanSaveSettings() || !TryApplyToSettings(showError: true, out _))
        {
            return;
        }

        CloseResult = FormsDialogResult.OK;
        Close();
    }

    private void ApplyAndNotify()
    {
        if (!CanSaveSettings() || !TryApplyToSettings(showError: true, out _))
        {
            return;
        }

        Applied?.Invoke(this, EventArgs.Empty);
    }

    private bool TryApplyToSettings(bool showError, out string message)
    {
        message = string.Empty;
        try
        {
            pageHost.ApplyToSettings();
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

    private bool CanSaveSettings()
    {
        if (canSaveSettings())
        {
            return true;
        }

        dialogService.ShowWarning(
            Localize("Settings cannot be saved while in a Race room."),
            Localize("TerrariaSplit Settings"));
        return false;
    }
}
