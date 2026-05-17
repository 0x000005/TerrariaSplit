using System.Drawing;

namespace TerrariaSplit;

internal sealed class EnterWorldWorkflow : IDisposable
{
    private readonly TerrariaSavePreparation savePreparation = new();
    private readonly TerrariaAutomationContext automation = new("Enter world");
    private TimeSpan menuActionDelay = TimeSpan.FromMilliseconds(AutoCreateWorldSettings.DefaultMenuActionDelayMilliseconds);

    public event Action? CompletedSuccessfully;

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

            TerrariaSaveCleanupResult cleanup = default;
            if (!await automation.RunStepAsync(
                    "save cleanup",
                    _ =>
                    {
                        cleanup = savePreparation.MoveNonFavoritesToBackup();
                        return Task.FromResult(true);
                    },
                    cancellationToken))
            {
                return;
            }

            if (!await automation.RunStepAsync(
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
                    cancellationToken))
            {
                return;
            }

            if (!await automation.ClickAsync("Single Player", geometry.MainMenuSinglePlayer(), menuActionDelay, cancellationToken))
            {
                return;
            }

            if (!await automation.ClickAsync("first non-favorite player play button", geometry.PlayerPlayButton(cleanup.FavoritePlayers), menuActionDelay, cancellationToken))
            {
                return;
            }

            if (!await automation.ClickAsync("first non-favorite world play button", geometry.WorldPlayButton(cleanup.FavoriteWorlds), menuActionDelay, cancellationToken))
            {
                return;
            }

            CompletedSuccessfully?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Enter world automation failed.");
        }
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
