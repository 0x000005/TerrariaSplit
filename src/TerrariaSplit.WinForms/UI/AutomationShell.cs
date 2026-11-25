using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using TerrariaSplit.UI.Wpf.Dialogs;
using TerrariaSplit.UI.Wpf.Interop;
using TerrariaSplit.UI.Wpf.Windows;

namespace TerrariaSplit.UI;

internal sealed class AutomationShell : IDisposable
{
    private readonly TerrariaWorldAutomation worldAutomation;
    private readonly Func<AppSettings> getSettings;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly ProgramModalWindowCoordinator modalWindows;
    private readonly Control owner;
    private readonly Action clearPendingMenuActions;
    private readonly ILogger logger;

    public AutomationShell(
        WorldPoolStore worldPoolStore,
        Func<AppSettings> getSettings,
        ISettingsSnapshotFactory settingsSnapshots,
        ProgramModalWindowCoordinator modalWindows,
        Control owner,
        Action clearPendingMenuActions,
        ILogger logger)
    {
        this.getSettings = getSettings;
        this.settingsSnapshots = settingsSnapshots;
        this.modalWindows = modalWindows;
        this.owner = owner;
        this.clearPendingMenuActions = clearPendingMenuActions;
        this.logger = logger;
        worldAutomation = new TerrariaWorldAutomation(worldPoolStore, logger);
    }

    public bool IsCreateWorldRunning => worldAutomation.IsCreateWorldRunning;

    public bool IsEnterWorldRunning => worldAutomation.IsEnterWorldRunning;

    public async void StartCreateWorld()
    {
        try
        {
            AutomationResult result = await worldAutomation.StartCreateWorldAsync(settingsSnapshots.CreateSnapshot(getSettings()));
            await HandleAutomationResultAsync(result, "Create World", "World generation failed.");
        }
        catch (Exception ex)
        {
            await HandleAutomationResultAsync(
                AutomationResult.Failure(
                    "Create world automation failed unexpectedly.",
                    "Unhandled create world automation error.",
                    ex),
                "Create World",
                "World generation failed.");
        }
    }

    public void ShowPracticeWorldSelector()
    {
        AppSettings settings = getSettings();
        var palette = new PracticeWorldPaletteWindow(settings);
        var window = new TerrariaWindowController();
        Rectangle targetBounds = window.TryGetClientScreenBounds(out Rectangle terrariaBounds)
            ? terrariaBounds
            : Screen.FromControl(owner).WorkingArea;
        palette.ContentRendered += (_, _) => WpfWindowInterop.CenterOnPixelBounds(palette, targetBounds);
        WpfWindowInterop.CenterOnPixelBounds(palette, targetBounds);

        if (modalWindows.ShowWpfDialog(palette, ModalWindowOptions.ForceTopMostForeground) == true &&
            palette.SelectedSlot is PracticeWorldSlot selectedSlot)
        {
            StartPracticeWorld(selectedSlot);
        }
    }

    public bool CancelCreateWorld()
    {
        return worldAutomation.CancelCreateWorld();
    }

    public bool CancelEnterWorld()
    {
        return worldAutomation.CancelEnterWorld();
    }

    public void Dispose()
    {
        worldAutomation.Dispose();
    }

    private async void StartPracticeWorld(PracticeWorldSlot selectedSlot)
    {
        OperationResult validation = EnterWorldSaveInstaller.Validate(selectedSlot);
        if (validation.Failed)
        {
            logger.LogInformation(validation.Message);
            return;
        }

        clearPendingMenuActions();

        try
        {
            AutomationResult result = await worldAutomation.StartEnterWorldAsync(
                settingsSnapshots.CreateSnapshot(getSettings()),
                selectedSlot);
            await HandleAutomationResultAsync(result, "Practice world", "Failed");
        }
        catch (Exception ex)
        {
            await HandleAutomationResultAsync(
                AutomationResult.Failure(
                    "Practice world automation failed unexpectedly.",
                    "Unhandled practice world automation error.",
                    ex),
                "Practice world",
                "Failed");
        }
    }

    private async Task HandleAutomationResultAsync(
        AutomationResult result,
        string titleKey,
        string failureMessageKey)
    {
        if (result.Succeeded || result.Cancelled)
        {
            if (!string.IsNullOrWhiteSpace(result.DiagnosticMessage))
            {
                logger.LogInformation(result.DiagnosticMessage);
            }

            return;
        }

        result = result.RecordFailure();
        try { await AppLog.Provider.FlushAsync(TimeSpan.FromMilliseconds(500)); }
        catch (TimeoutException) { }
        if (owner.IsDisposed || owner.Disposing) return;
        if (result.Fault is { } fault) result = result with { Fault = fault with { LogPath = AppLog.Provider.LogPath } };
        string detail = AutomationFailureReport.Build(result);
        ShowAutomationFailure(
            titleKey,
            failureMessageKey,
            detail,
            fault: result.Fault);
    }

    private void ShowAutomationFailure(
        string titleKey,
        string failureMessageKey,
        string detail,
        AppFault? fault = null)
    {
        AppSettings settings = getSettings();
        string title = Localizer.Get(titleKey, settings);
        string failureMessage = failureMessageKey;
        string message = string.IsNullOrWhiteSpace(detail)
            ? failureMessage
            : failureMessage + Environment.NewLine + detail;
        var dialog = new ModernMessageDialog(
            title,
            message,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning,
            key => Localizer.Get(key, settings),
            selectableMessage: true,
            fault: fault)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        modalWindows.ShowWpfDialog(dialog, ModalWindowOptions.ForceTopMostForeground);
    }
}
