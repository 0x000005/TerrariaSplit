using System.Drawing;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class EnterWorldWorkflow : IDisposable
{
    private readonly TerrariaAutomationContext automation = new("Enter world");
    private TimeSpan menuActionDelay = TimeSpan.FromMilliseconds(AppSettingsDefaults.Automation.AutoCreate.MenuActionDelayMilliseconds);

    public async Task<AutomationResult> RunAsync(AppSettings settings, PracticeWorldSlot slot, CancellationToken cancellationToken = default)
    {
        automation.BeginRun();
        try
        {
            ApplyTiming(settings.Automation.AutoCreate);
            OperationResult validation = EnterWorldSaveInstaller.Validate(slot);
            if (validation.Failed)
            {
                AppLogger.Info(validation.Message);
                return AutomationResult.Failure(
                    validation.Message,
                    $"Enter world automation validation failed: {validation.Message}",
                    validation.Exception);
            }

            Size clientSize = Size.Empty;
            if (!await automation.RunStepAsync(
                    "activate Terraria window",
                    _ =>
                    {
                        if (!automation.TryActivate(out Size activatedSize))
                        {
                            AppLogger.Info("Enter world automation could not activate Terraria window.");
                            return Task.FromResult(false);
                        }

                        clientSize = activatedSize;
                        return Task.FromResult(true);
                    },
                    cancellationToken))
            {
                return AutomationResult.Failure(
                    "Could not activate the Terraria window.",
                    "Enter world automation could not activate Terraria window.");
            }

            TerrariaMenuGeometry geometry = TerrariaMenuGeometry.From(clientSize);
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
            AppLogger.Error(ex, "Enter world automation failed.");
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
                    AppLogger.Info($"Enter world automation could not install save files: {result.Message}");
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
