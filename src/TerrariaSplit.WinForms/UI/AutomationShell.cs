using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed class AutomationShell : IDisposable
{
    private readonly TerrariaWorldAutomation worldAutomation;
    private readonly Func<AppSettings> getSettings;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly ProgramModalWindowCoordinator modalWindows;
    private readonly Control owner;
    private readonly Action clearPendingMenuActions;
    private readonly IAppLogger logger;

    public AutomationShell(
        WorldPoolStore worldPoolStore,
        Func<AppSettings> getSettings,
        ISettingsSnapshotFactory settingsSnapshots,
        ProgramModalWindowCoordinator modalWindows,
        Control owner,
        Action clearPendingMenuActions,
        IAppLogger logger)
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
            LogAutomationFailure(result);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Unhandled create world automation error.");
        }
    }

    public void ShowPracticeWorldSelector()
    {
        AppSettings settings = getSettings();
        using var form = new PracticeWorldSelectorForm(settings);
        var window = new TerrariaWindowController();
        if (window.TryGetClientScreenBounds(out Rectangle terrariaBounds))
        {
            form.Location = new Point(
                terrariaBounds.Left + Math.Max(0, (terrariaBounds.Width - form.Width) / 2),
                terrariaBounds.Top + Math.Max(0, (terrariaBounds.Height - form.Height) / 2));
        }
        else
        {
            Rectangle workingArea = Screen.FromControl(owner).WorkingArea;
            form.Location = new Point(
                workingArea.Left + Math.Max(0, (workingArea.Width - form.Width) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - form.Height) / 2));
        }

        if (modalWindows.ShowDialog(form, ModalWindowOptions.ForceTopMostForeground) == DialogResult.OK &&
            form.SelectedSlot is PracticeWorldSlot selectedSlot)
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
            logger.Info(validation.Message);
            ShowFailure("Practice world failed", validation.Message);
            return;
        }

        clearPendingMenuActions();

        try
        {
            AutomationResult result = await worldAutomation.StartEnterWorldAsync(
                settingsSnapshots.CreateSnapshot(getSettings()),
                selectedSlot);
            ShowAutomationFailure("Practice world failed", result);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Unhandled practice world automation error.");
            ShowFailure("Practice world failed", "Practice world automation failed.");
        }
    }

    private void ShowAutomationFailure(string title, AutomationResult result)
    {
        if (LogAutomationFailure(result))
        {
            ShowFailure(title, result.UserMessage);
        }
    }

    private bool LogAutomationFailure(AutomationResult result)
    {
        if (result.Succeeded || result.Cancelled)
        {
            if (!string.IsNullOrWhiteSpace(result.DiagnosticMessage))
            {
                logger.Info(result.DiagnosticMessage);
            }

            return false;
        }

        if (result.Exception is not null)
        {
            logger.Error(result.Exception, result.DiagnosticMessage);
        }
        else if (!string.IsNullOrWhiteSpace(result.DiagnosticMessage))
        {
            logger.Info(result.DiagnosticMessage);
        }

        return true;
    }

    private void ShowFailure(string title, string message)
    {
        string displayMessage = string.IsNullOrWhiteSpace(message)
            ? "Automation failed."
            : message;
        using var dialog = new HotkeyWarningDialog(title, displayMessage);
        modalWindows.ShowDialog(dialog);
    }
}
