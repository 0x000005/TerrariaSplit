namespace TerrariaSplit.Terraria.Automation;

internal sealed class EnterWorldWorkflow : IDisposable
{
    private readonly TerrariaAutomationContext automation = new("Enter world");
    private readonly WindowActivationService windowActivation;
    private TimeSpan menuActionDelay = TimeSpan.FromMilliseconds(AppSettingsDefaults.Automation.AutoCreate.MenuActionDelayMilliseconds);

    public EnterWorldWorkflow()
    {
        windowActivation = new WindowActivationService(automation, "Enter world");
    }

    public async Task<AutomationResult> RunAsync(AppSettings settings, PracticeWorldSlot slot, CancellationToken cancellationToken = default)
    {
        automation.BeginRun();
        try
        {
            ApplyTiming(settings.Automation.AutoCreate);
            OperationResult validation = EnterWorldSaveInstaller.Validate(slot);
            if (validation.Failed)
            {
                StaticAppLogger.Instance.Info(validation.Message);
                return AutomationResult.Failure(
                    validation.Message,
                    $"Enter world automation validation failed: {validation.Message}",
                    validation.Exception);
            }

            WindowActivationResult activation = await windowActivation.ActivateAsync(cancellationToken);
            if (!activation.Succeeded)
            {
                return AutomationResult.Failure(activation.UserMessage, activation.DiagnosticMessage);
            }

            TerrariaMenuProfile menuProfile = TerrariaMenuProfile.ResolveRunningProcess();
            TerrariaMenuGeometry geometry = TerrariaMenuGeometry.From(activation.ClientSize, menuProfile);
            StaticAppLogger.Instance.Info($"Enter world automation using menu profile: {menuProfile.Name}.");
            OperationResult install = await InstallPracticeSaveFilesAsync(slot, cancellationToken);
            if (install.Failed)
            {
                return AutomationResult.Failure(
                    install.Message,
                    $"Enter world automation could not install save files: {install.Message}",
                    install.Exception);
            }

            if (!await automation.ClickAsync("Single Player", geometry.MainMenuSinglePlayer(), menuActionDelay, cancellationToken))
            {
                return AutomationResult.Failure(
                    "Could not open Terraria single player menu.",
                    "Enter world automation failed to click Single Player.");
            }

            return AutomationResult.Success("Enter world automation opened the single player menu.");
        }
        catch (OperationCanceledException)
        {
            return AutomationResult.CancelledByUser("Enter world automation was cancelled.");
        }
        catch (Exception ex)
        {
            StaticAppLogger.Instance.Error(ex, "Enter world automation failed.");
            return AutomationResult.Failure(
                "Enter world automation failed.",
                "Enter world automation threw an unhandled exception.",
                ex);
        }
    }

    private async Task<OperationResult> InstallPracticeSaveFilesAsync(
        PracticeWorldSlot slot,
        CancellationToken cancellationToken)
    {
        OperationResult result = OperationResult.Success();
        return await automation.RunStepAsync(
            "install practice save files",
            _ =>
            {
                result = EnterWorldSaveInstaller.Install(slot);
                if (result.Failed)
                {
                    StaticAppLogger.Instance.Info($"Enter world automation could not install save files: {result.Message}");
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            },
            cancellationToken)
            ? result
            : result.Failed
                ? result
                : OperationResult.Failure("Could not install practice world save files.");
    }

    private void ApplyTiming(AutoCreateWorldSettings settings)
    {
        menuActionDelay = TimeSpan.FromMilliseconds(settings.MenuActionDelayMilliseconds);
        automation.ConfigureTiming(settings);
    }

    public void Dispose()
    {
    }
}
