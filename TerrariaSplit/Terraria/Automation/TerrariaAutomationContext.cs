using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class TerrariaAutomationContext
{
    private static readonly TimeSpan EscapePollInterval = TimeSpan.FromMilliseconds(25);

    private readonly string name;
    private bool escapeCancellationLogged;

    public TerrariaAutomationContext(string name)
    {
        this.name = name;
    }

    public TerrariaWindowController Window { get; } = new();

    public void ConfigureTiming(AutoCreateWorldSettings settings)
    {
        Window.WindowActivationDelayMilliseconds = settings.WindowActivationDelayMilliseconds;
        Window.ClickFocusDelayMilliseconds = settings.ClickFocusDelayMilliseconds;
        Window.InputPressDurationMilliseconds = settings.InputPressDurationMilliseconds;
    }

    public void BeginRun()
    {
        escapeCancellationLogged = false;
        ClearEscapeKeyState();
    }

    public bool TryActivate(out Size clientSize)
    {
        bool success = Window.TryActivate(out clientSize);
        LogActivationResult(success, clientSize);
        return success;
    }

    public bool TryActivate(out Size clientSize, int activationDelayMilliseconds)
    {
        bool success = Window.TryActivate(out clientSize, activationDelayMilliseconds);
        LogActivationResult(success, clientSize);
        return success;
    }

    private void LogActivationResult(bool success, Size clientSize)
    {
        Log(new AutomationStepResult(
            "activate Terraria window",
            success,
            ClientSize: clientSize,
            Detail: success ? null : "window activation failed"));
    }

    public async Task<bool> RunStepAsync(
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
            AppLogger.Error(ex, $"{name} automation step '{step}' failed.");
            return false;
        }
    }

    public Task<bool> ClickAsync(
        string step,
        Point point,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        return RunStepAsync(
            $"click {step}",
            ct => ClickOnceAsync(step, point, delay, ct),
            cancellationToken);
    }

    public async Task<bool> ClickOnceAsync(
        string step,
        Point point,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        ThrowIfCancellationRequested(cancellationToken);
        bool clicked = Window.TryClickClient(point.X, point.Y, out Size clientSize);
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

    public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
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

    public void ThrowIfCancellationRequested(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsEscapePressed())
        {
            return;
        }

        if (!escapeCancellationLogged)
        {
            escapeCancellationLogged = true;
            AppLogger.Info($"{name} automation cancelled by Escape.");
        }

        throw new OperationCanceledException($"{name} automation cancelled by Escape.", cancellationToken);
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
}
