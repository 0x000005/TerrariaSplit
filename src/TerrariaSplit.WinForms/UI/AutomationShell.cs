using System.Drawing;
using System.Windows.Forms;
using TerrariaSplit.UI.Settings;

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
            HandleAutomationResult(result, "Create World", "World generation failed.");
        }
        catch (Exception ex)
        {
            HandleAutomationResult(
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
            return;
        }

        clearPendingMenuActions();

        try
        {
            AutomationResult result = await worldAutomation.StartEnterWorldAsync(
                settingsSnapshots.CreateSnapshot(getSettings()),
                selectedSlot);
            HandleAutomationResult(result, "Practice world", "Failed");
        }
        catch (Exception ex)
        {
            HandleAutomationResult(
                AutomationResult.Failure(
                    "Practice world automation failed unexpectedly.",
                    "Unhandled practice world automation error.",
                    ex),
                "Practice world",
                "Failed");
        }
    }

    private void HandleAutomationResult(
        AutomationResult result,
        string titleKey,
        string failureMessageKey)
    {
        if (result.Succeeded || result.Cancelled)
        {
            if (!string.IsNullOrWhiteSpace(result.DiagnosticMessage))
            {
                logger.Info(result.DiagnosticMessage);
            }

            return;
        }

        if (result.Exception is not null)
        {
            logger.Error(result.Exception, result.DiagnosticMessage);
        }
        else if (!string.IsNullOrWhiteSpace(result.DiagnosticMessage))
        {
            logger.Info(result.DiagnosticMessage);
        }

        bool hasDetailedFailureReport = AutomationFailureReport.TryBuild(result, out string detailedFailureReport);
        string detail = hasDetailedFailureReport
            ? detailedFailureReport
            : AutomationFailureReport.BuildSummary(result);
        ShowAutomationFailure(
            titleKey,
            failureMessageKey,
            detail,
            selectableDetail: hasDetailedFailureReport);
    }

    private void ShowAutomationFailure(
        string titleKey,
        string failureMessageKey,
        string detail,
        bool selectableDetail = false)
    {
        AppSettings settings = getSettings();
        string title = Localizer.Get(titleKey, settings);
        string failureMessage = Localizer.Get(failureMessageKey, settings);
        string message = string.IsNullOrWhiteSpace(detail)
            ? failureMessage
            : failureMessage + Environment.NewLine + detail;
        using var dialog = new SettingsMessageDialog(
            title,
            message,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning,
            key => Localizer.Get(key, settings),
            selectableDetail);
        modalWindows.ShowDialog(dialog, ModalWindowOptions.ForceTopMostForeground);
    }
}
