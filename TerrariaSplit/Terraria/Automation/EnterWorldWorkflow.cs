using System.Drawing;

namespace TerrariaSplit;

internal sealed class EnterWorldWorkflow : IDisposable
{
    private readonly TerrariaAutomationContext automation = new("Enter world");
    private TimeSpan menuActionDelay = TimeSpan.FromMilliseconds(AutoCreateWorldSettings.DefaultMenuActionDelayMilliseconds);

    public async Task RunAsync(AppSettings settings, PracticeWorldSlot slot, CancellationToken cancellationToken = default)
    {
        automation.BeginRun();
        try
        {
            ApplyTiming(settings.AutoCreate);
            if (!EnterWorldSaveInstaller.TryValidate(slot, out string validationMessage))
            {
                AppLogger.Info(validationMessage);
                return;
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
                return;
            }

            TerrariaMenuGeometry geometry = TerrariaMenuGeometry.From(clientSize);
            if (!await InstallPracticeSaveFilesAsync(slot, cancellationToken))
            {
                return;
            }

            await automation.ClickAsync("Single Player", geometry.MainMenuSinglePlayer(), menuActionDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Enter world automation failed.");
        }
    }

    private async Task<bool> InstallPracticeSaveFilesAsync(
        PracticeWorldSlot slot,
        CancellationToken cancellationToken)
    {
        return await automation.RunStepAsync(
            "install practice save files",
            _ =>
            {
                if (!EnterWorldSaveInstaller.TryInstall(slot, out string installMessage))
                {
                    AppLogger.Info($"Enter world automation could not install save files: {installMessage}");
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            },
            cancellationToken);
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
