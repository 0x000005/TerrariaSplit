using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class TerrariaMenuNavigator : IDisposable
{
    private static readonly TimeSpan FastPollInterval = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly TerrariaMenuStateReader menuState = new();
    private readonly TerrariaWindowController window = new();

    public void ApplyTiming(AutoCreateWorldSettings settings)
    {
        window.WindowActivationDelayMilliseconds = settings.WindowActivationDelayMilliseconds;
        window.ClickFocusDelayMilliseconds = settings.ClickFocusDelayMilliseconds;
        window.InputPressDurationMilliseconds = settings.InputPressDurationMilliseconds;
    }

    public bool IsAtMenuMode(params int[] expectedModes)
    {
        return menuState.TryReadMenuMode(out int menuMode, expectedModes) &&
            expectedModes.Contains(menuMode);
    }

    public bool TryActivate(out Size clientSize)
    {
        bool success = window.TryActivate(out clientSize);
        Log(new AutomationStepResult(
            "activate Terraria window",
            success,
            ClientSize: clientSize,
            Detail: success ? null : "window activation failed"));
        return success;
    }

    public async Task<bool> ClickAsync(
        string step,
        Point point,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
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
        return true;
    }

    public async Task<bool> RequireMenuModeAsync(
        string step,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        params int[] expectedModes)
    {
        MenuModeWaitResult result = await WaitForMenuModeAsync(
            timeout,
            PollInterval,
            cancellationToken,
            expectedModes);
        Log(new AutomationStepResult(
            $"wait for {step}",
            result.Success,
            ExpectedMenuModes: FormatExpectedModes(expectedModes),
            LastMenuMode: result.LastMode));
        return result.Success;
    }

    public async Task<bool> ObserveMenuModeAsync(
        string step,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        params int[] expectedModes)
    {
        MenuModeWaitResult result = await WaitForMenuModeAsync(
            timeout,
            FastPollInterval,
            cancellationToken,
            expectedModes);
        Log(new AutomationStepResult(
            $"observe {step}",
            result.Success,
            ExpectedMenuModes: FormatExpectedModes(expectedModes),
            LastMenuMode: result.LastMode));
        return result.Success;
    }

    public void PressKey(Keys key)
    {
        AppLogger.Info($"Create world automation pressing key {key}.");
        window.PressKey(key);
    }

    public void PressModifiedKey(Keys modifier, Keys key)
    {
        AppLogger.Info($"Create world automation pressing modified key {modifier}+{key}.");
        window.PressModifiedKey(modifier, key);
    }

    public void Dispose()
    {
        menuState.Dispose();
    }

    private async Task<MenuModeWaitResult> WaitForMenuModeAsync(
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken,
        params int[] expectedModes)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        int? lastMode = null;
        while (DateTime.UtcNow <= deadline)
        {
            if (menuState.TryReadMenuMode(out int mode, expectedModes))
            {
                lastMode = mode;
                if (expectedModes.Contains(mode))
                {
                    return new MenuModeWaitResult(true, lastMode);
                }
            }

            await DelayAsync(pollInterval, cancellationToken);
        }

        return new MenuModeWaitResult(false, lastMode);
    }

    private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }

    private static void Log(AutomationStepResult result)
    {
        AppLogger.Info(result.ToLogMessage());
    }

    private static string FormatExpectedModes(IReadOnlyCollection<int> expectedModes)
    {
        return string.Join(", ", expectedModes);
    }

    private readonly record struct MenuModeWaitResult(bool Success, int? LastMode);
}
