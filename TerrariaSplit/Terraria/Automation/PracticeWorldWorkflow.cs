using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class PracticeWorldWorkflow : IDisposable
{
    private static readonly TimeSpan EscapePollInterval = TimeSpan.FromMilliseconds(25);

    private readonly TerrariaSavePreparation savePreparation = new();
    private readonly TerrariaWindowController window = new();
    private TimeSpan menuActionDelay = TimeSpan.FromMilliseconds(AutoCreateWorldSettings.DefaultMenuActionDelayMilliseconds);
    private bool escapeCancellationLogged;

    public bool IsRunning { get; private set; }

    public async Task RunAsync(AppSettings settings, PracticeWorldSlot slot, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        escapeCancellationLogged = false;
        ClearEscapeKeyState();
        try
        {
            ApplyTiming(settings.AutoCreate);
            if (!PracticeWorldSaveInstaller.TryValidate(slot, out string validationMessage))
            {
                AppLogger.Info(validationMessage);
                return;
            }

            Size clientSize = Size.Empty;
            if (!await RunStepAsync(
                    "activate Terraria window",
                    _ =>
                    {
                        if (!TryActivate(out Size activatedSize))
                        {
                            AppLogger.Info("Practice world automation could not activate Terraria window.");
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
            if (!await RunStepAsync(
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

            if (!await RunStepAsync(
                    "install practice save files",
                    _ =>
                    {
                        if (!PracticeWorldSaveInstaller.TryInstall(slot, out string installMessage))
                        {
                            AppLogger.Info($"Practice world automation could not install save files: {installMessage}");
                            return Task.FromResult(false);
                        }

                        return Task.FromResult(true);
                    },
                    cancellationToken))
            {
                return;
            }

            if (!await ClickAsync("Single Player", geometry.MainMenuSinglePlayer(), menuActionDelay, cancellationToken))
            {
                return;
            }

            if (!await ClickAsync("first non-favorite player play button", geometry.PlayerPlayButton(cleanup.FavoritePlayers), menuActionDelay, cancellationToken))
            {
                return;
            }

            if (!await ClickAsync("first non-favorite world play button", geometry.WorldPlayButton(cleanup.FavoriteWorlds), menuActionDelay, cancellationToken))
            {
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Practice world automation failed.");
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ApplyTiming(AutoCreateWorldSettings settings)
    {
        menuActionDelay = TimeSpan.FromMilliseconds(settings.MenuActionDelayMilliseconds);
        window.WindowActivationDelayMilliseconds = settings.WindowActivationDelayMilliseconds;
        window.ClickFocusDelayMilliseconds = settings.ClickFocusDelayMilliseconds;
        window.InputPressDurationMilliseconds = settings.InputPressDurationMilliseconds;
    }

    private bool TryActivate(out Size clientSize)
    {
        bool success = window.TryActivate(out clientSize);
        Log(new AutomationStepResult(
            "activate Terraria window",
            success,
            ClientSize: clientSize,
            Detail: success ? null : "window activation failed"));
        return success;
    }

    private async Task<bool> RunStepAsync(
        string step,
        Func<CancellationToken, Task<bool>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            ThrowIfCancellationRequested(cancellationToken);
            return await action(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Practice world automation step '{step}' failed.");
            return false;
        }
    }

    private async Task<bool> ClickAsync(string step, Point point, TimeSpan delay, CancellationToken cancellationToken)
    {
        return await RunStepAsync(
            $"click {step}",
            ct => ClickOnceAsync(step, point, delay, ct),
            cancellationToken);
    }

    private async Task<bool> ClickOnceAsync(
        string step,
        Point point,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        ThrowIfCancellationRequested(cancellationToken);
        bool clicked = window.TryClickClient(point.X, point.Y, out Size clientSize);
        Log(new AutomationStepResult(
            $"click {step}",
            clicked,
            point,
            clientSize,
            Detail: clicked ? null : "window click failed"));
        if (!clicked)
        {
            return false;
        }

        await DelayAsync(delay, cancellationToken);
        ThrowIfCancellationRequested(cancellationToken);
        return true;
    }

    private async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        ThrowIfCancellationRequested(cancellationToken);
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        DateTime deadline = DateTime.UtcNow + delay;
        while (true)
        {
            ThrowIfCancellationRequested(cancellationToken);
            TimeSpan remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            TimeSpan interval = remaining < EscapePollInterval ? remaining : EscapePollInterval;
            await Task.Delay(interval, cancellationToken);
        }
    }

    private void ThrowIfCancellationRequested(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsEscapePressed())
        {
            return;
        }

        if (!escapeCancellationLogged)
        {
            escapeCancellationLogged = true;
            AppLogger.Info("Practice world automation cancelled by Escape.");
        }

        throw new OperationCanceledException("Practice world automation cancelled by Escape.", cancellationToken);
    }

    private static bool IsEscapePressed()
    {
        short state = NativeMethods.GetAsyncKeyState((int)Keys.Escape);
        return (state & 0x8000) != 0 || (state & 0x0001) != 0;
    }

    private static void ClearEscapeKeyState()
    {
        _ = NativeMethods.GetAsyncKeyState((int)Keys.Escape);
    }

    private static void Log(AutomationStepResult result)
    {
        AppLogger.Info(result.ToLogMessage());
    }

    public void Dispose()
    {
    }
}
