using System.Windows;
using TerrariaSplit.UI.Wpf.Controls;
using TerrariaSplit.UI.Wpf.Settings;
using FormsDialogResult = System.Windows.Forms.DialogResult;

namespace TerrariaSplit.UI.Wpf.Windows;

internal sealed partial class SettingsWindow : ModernWindow, IWpfSettingsWindowOwner, IDisposable
{
    private const string IconGeneral = "M 7,4.2 A 2.8,2.8 0 1 1 6.99,4.2 Z M 7,1.2 V 2.5 M 7,11.5 V 12.8 M 1.2,7 H 2.5 M 11.5,7 H 12.8 M 2.9,2.9 L 3.8,3.8 M 10.2,10.2 L 11.1,11.1 M 11.1,2.9 L 10.2,3.8 M 3.8,10.2 L 2.9,11.1";
    private const string IconRoute = "M 2,3 H 12 M 2,7 H 12 M 2,11 H 9";
    private const string IconData = "M 2,3.5 C 2,2.2 12,2.2 12,3.5 C 12,4.8 2,4.8 2,3.5 Z M 2,3.5 V 10.5 C 2,11.8 12,11.8 12,10.5 V 3.5 M 2,7 C 2,8.3 12,8.3 12,7";
    private const string IconUi = "M 1.5,2.5 H 12.5 V 11.5 H 1.5 Z M 1.5,5.5 H 12.5 M 5.5,5.5 V 11.5";
    private const string IconEffects = "M 7,1.5 L 8.4,5.6 L 12.5,7 L 8.4,8.4 L 7,12.5 L 5.6,8.4 L 1.5,7 L 5.6,5.6 Z";
    private const string IconAutomation = "M 7.8,1.5 L 2.8,7.8 H 6.8 L 6.2,12.5 L 11.2,6.2 H 7.2 Z";
    private const string IconSounds = "M 2,5.5 H 4.5 L 7.8,2.8 V 11.2 L 4.5,8.5 H 2 Z M 9.8,4.8 C 10.8,5.8 10.8,8.2 9.8,9.2 M 11.5,3.2 C 13,5 13,9 11.5,10.8";
    private const string IconColors = "M 7,1.5 C 3.8,1.5 1.5,4 1.5,7 C 1.5,10 3.8,12.5 7,12.5 C 8.4,12.5 9.2,11.7 9.2,10.7 C 9.2,10 8.8,9.4 9.6,9.1 C 11.4,8.6 12.5,7.4 12.5,5.8 C 12.5,3.3 10.1,1.5 7,1.5 Z";
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
