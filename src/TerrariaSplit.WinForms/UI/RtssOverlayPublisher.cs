using System.Diagnostics;
using System.Drawing;
using System.Text;
using TerrariaSplit.UI.Rendering;

namespace TerrariaSplit.UI;

internal sealed class RtssOverlayPublisher : IDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StartRetryInterval = TimeSpan.FromSeconds(2);
    private readonly RtssOsdWriter writer;
    private readonly Func<bool> isTerrariaProcessRunning;
    private readonly Func<bool> isRtssProcessRunning;
    private RtssOsdUpdateResult lastFailure;
    private RtssOverlayPublishResult lastResult = RtssOverlayPublishResult.Disabled();
    private DateTime nextRetryUtc;
    private DateTime nextRefreshUtc;
    private DateTime nextStartAttemptUtc;
    private RtssOsdStyle lastPublishedStyle;
    private string lastPublishedText = string.Empty;
    private bool hasPublishedText;
    private bool hasPublishedStyle;
    private bool disposed;

    public RtssOverlayPublisher()
        : this(new RtssOsdWriter())
    {
    }

    public RtssOverlayPublisher(RtssOsdWriter writer)
        : this(writer, IsTerrariaProcessRunning, IsRtssProcessRunning)
    {
    }

    public RtssOverlayPublisher(
        RtssOsdWriter writer,
        Func<bool> isTerrariaProcessRunning,
        Func<bool> isRtssProcessRunning)
    {
        this.writer = writer;
        this.isTerrariaProcessRunning = isTerrariaProcessRunning ?? IsTerrariaProcessRunning;
        this.isRtssProcessRunning = isRtssProcessRunning ?? IsRtssProcessRunning;
    }

    public RtssOsdUpdateResult LastFailure => lastFailure;

    public RtssOverlayPublishResult LastResult => lastResult;

    public RtssOverlayPublishResult Publish(AppSettings settings, ApplicationViewState viewState)
    {
        if (disposed)
        {
            lastResult = RtssOverlayPublishResult.Disabled();
            return lastResult;
        }

        bool enabled = settings.Advanced?.EnableRtssOverlay == true;
        if (!enabled)
        {
            Clear();
            lastResult = RtssOverlayPublishResult.Disabled();
            return lastResult;
        }

        DateTime nowUtc = DateTime.UtcNow;
        if (!isTerrariaProcessRunning())
        {
            ClearPublishedText();
            nextRetryUtc = DateTime.MinValue;
            lastFailure = default;
            lastResult = RtssOverlayPublishResult.WaitingForTerraria();
            return lastResult;
        }

        string configuredPath = settings.Advanced?.RtssExecutablePath?.Trim() ?? string.Empty;
        if (!TryGetConfiguredRtssExecutable(configuredPath, out string rtssExecutablePath, out RtssOverlayPublishResult pathFailure))
        {
            ClearPublishedText();
            nextRetryUtc = DateTime.MinValue;
            lastFailure = default;
            lastResult = pathFailure;
            return lastResult;
        }

        TryEnsureRtssRunning(rtssExecutablePath, nowUtc);
        if (nowUtc < nextRetryUtc)
        {
            return lastResult;
        }

        long timestamp = Stopwatch.GetTimestamp();
        RtssOsdStyle style = RtssOverlayTextFormatter.CreateStyle(viewState, timestamp);
        string text = RtssOverlayTextFormatter.Format(viewState, timestamp, style);
        RtssOsdStyle targetStyle = RtssOverlayTextFormatter.CreateTargetStyle(style);
        if (hasPublishedText &&
            hasPublishedStyle &&
            nowUtc < nextRefreshUtc &&
            style.Equals(lastPublishedStyle) &&
            string.Equals(text, lastPublishedText, StringComparison.Ordinal))
        {
            return lastResult;
        }

        RtssOsdUpdateResult styleResult = writer.TryUpdateTargetStyle("Terraria.exe", targetStyle);
        if (!styleResult.Success)
        {
            lastFailure = styleResult;
            if (ShouldThrottle(styleResult.Status))
            {
                nextRetryUtc = nowUtc + RetryInterval;
            }

            lastResult = RtssOverlayPublishResult.FromWriteFailure(styleResult);
            return lastResult;
        }

        RtssOsdUpdateResult result = writer.TryUpdate(text);
        if (result.Success)
        {
            hasPublishedText = true;
            hasPublishedStyle = true;
            lastPublishedText = text;
            lastPublishedStyle = style;
            lastFailure = default;
            nextRetryUtc = DateTime.MinValue;
            nextRefreshUtc = nowUtc + RetryInterval;
            lastResult = RtssOverlayPublishResult.Updated();
            return lastResult;
        }

        lastFailure = result;
        if (ShouldThrottle(result.Status))
        {
            nextRetryUtc = nowUtc + RetryInterval;
        }

        lastResult = RtssOverlayPublishResult.FromWriteFailure(result);
        return lastResult;
    }

    public void Clear()
    {
        ClearPublishedText();
        nextRetryUtc = DateTime.MinValue;
        nextRefreshUtc = DateTime.MinValue;
        nextStartAttemptUtc = DateTime.MinValue;
        lastResult = RtssOverlayPublishResult.Disabled();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        writer.Dispose();
    }

    private static bool ShouldThrottle(RtssOsdUpdateStatus status)
    {
        return status is RtssOsdUpdateStatus.MissingSharedMemory or
            RtssOsdUpdateStatus.InvalidSharedMemory or
            RtssOsdUpdateStatus.AccessDenied or
            RtssOsdUpdateStatus.NoFreeSlot;
    }

    private void ClearPublishedText()
    {
        if (!hasPublishedText)
        {
            return;
        }

        writer.Clear();
        hasPublishedText = false;
        hasPublishedStyle = false;
        lastPublishedText = string.Empty;
        lastPublishedStyle = default;
    }

    private void TryEnsureRtssRunning(string rtssExecutablePath, DateTime nowUtc)
    {
        if (isRtssProcessRunning() || nowUtc < nextStartAttemptUtc)
        {
            return;
        }

        nextStartAttemptUtc = nowUtc + StartRetryInterval;
        if (string.IsNullOrWhiteSpace(rtssExecutablePath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = rtssExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(rtssExecutablePath) ?? AppContext.BaseDirectory,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized
            });
        }
        catch
        {
            // The shared-memory write below reports the user-visible RTSS state.
        }
    }

    private static bool TryGetConfiguredRtssExecutable(
        string configuredPath,
        out string executablePath,
        out RtssOverlayPublishResult failure)
    {
        executablePath = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            failure = RtssOverlayPublishResult.RtssExecutablePathRequired();
            return false;
        }

        if (!File.Exists(configuredPath))
        {
            failure = RtssOverlayPublishResult.RtssExecutablePathNotFound();
            return false;
        }

        executablePath = configuredPath;
        failure = RtssOverlayPublishResult.Disabled();
        return true;
    }

    private static bool IsRtssProcessRunning()
    {
        return IsProcessRunning("RTSS");
    }

    private static bool IsTerrariaProcessRunning()
    {
        return IsProcessRunning("Terraria");
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    if (!process.HasExited)
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
        }

        return false;
    }
}

internal enum RtssOverlayPublishStatus
{
    Disabled,
    Updated,
    WaitingForTerraria,
    MissingRtssExecutablePath,
    InvalidRtssExecutablePath,
    WaitingForRtss,
    InvalidSharedMemory,
    Busy,
    NoFreeSlot,
    AccessDenied,
    Failed
}

internal readonly record struct RtssOverlayPublishResult(
    RtssOverlayPublishStatus Status,
    string Message)
{
    public bool Success => Status == RtssOverlayPublishStatus.Updated;

    public bool NeedsUserAttention => Status is RtssOverlayPublishStatus.MissingRtssExecutablePath or
        RtssOverlayPublishStatus.InvalidRtssExecutablePath or
        RtssOverlayPublishStatus.AccessDenied or
        RtssOverlayPublishStatus.InvalidSharedMemory or
        RtssOverlayPublishStatus.NoFreeSlot or
        RtssOverlayPublishStatus.Failed;

    public static RtssOverlayPublishResult Disabled()
    {
        return new RtssOverlayPublishResult(RtssOverlayPublishStatus.Disabled, string.Empty);
    }

    public static RtssOverlayPublishResult Updated()
    {
        return new RtssOverlayPublishResult(RtssOverlayPublishStatus.Updated, string.Empty);
    }

    public static RtssOverlayPublishResult WaitingForTerraria()
    {
        return new RtssOverlayPublishResult(RtssOverlayPublishStatus.WaitingForTerraria, string.Empty);
    }

    public static RtssOverlayPublishResult RtssExecutablePathRequired()
    {
        return new RtssOverlayPublishResult(
            RtssOverlayPublishStatus.MissingRtssExecutablePath,
            "RTSS fullscreen projection requires RTSS.exe to be configured in Advanced options.");
    }

    public static RtssOverlayPublishResult RtssExecutablePathNotFound()
    {
        return new RtssOverlayPublishResult(
            RtssOverlayPublishStatus.InvalidRtssExecutablePath,
            "Configured RTSS executable was not found. Choose RTSS.exe in Advanced options.");
    }

    public static RtssOverlayPublishResult FromWriteFailure(RtssOsdUpdateResult result)
    {
        return result.Status switch
        {
            RtssOsdUpdateStatus.MissingSharedMemory => new RtssOverlayPublishResult(
                RtssOverlayPublishStatus.WaitingForRtss,
                string.Empty),
            RtssOsdUpdateStatus.AccessDenied => new RtssOverlayPublishResult(
                RtssOverlayPublishStatus.AccessDenied,
                "RTSS fullscreen projection cannot write to RTSS. Run TerrariaSplit with the same privileges as RTSS."),
            RtssOsdUpdateStatus.InvalidSharedMemory => new RtssOverlayPublishResult(
                RtssOverlayPublishStatus.InvalidSharedMemory,
                result.Message),
            RtssOsdUpdateStatus.Busy => new RtssOverlayPublishResult(
                RtssOverlayPublishStatus.Busy,
                result.Message),
            RtssOsdUpdateStatus.NoFreeSlot => new RtssOverlayPublishResult(
                RtssOverlayPublishStatus.NoFreeSlot,
                result.Message),
            _ => new RtssOverlayPublishResult(
                RtssOverlayPublishStatus.Failed,
                result.Message)
        };
    }
}

internal static class RtssOverlayTextFormatter
{
    public static string Format(ApplicationViewState viewState, long timestamp)
    {
        return Format(viewState, timestamp, CreateStyle(viewState, timestamp));
    }

    public static string Format(ApplicationViewState viewState, long timestamp, RtssOsdStyle style)
    {
        TimeSpan elapsed = viewState.ElapsedAt(timestamp);
        string timer = FormatTimer(viewState.Settings, elapsed);
        string color = FormatRtssColor(style.RgbColor);
        int sizePercent = Math.Clamp(style.PixelZoom, 1, 8) * 100;
        return $"<S={sizePercent}><C={color}>{timer}<C><S>";
    }

    public static RtssOsdStyle CreateStyle(ApplicationViewState viewState, long timestamp)
    {
        AdvancedSettings? advanced = viewState.Settings.Advanced;
        Color fill = ResolveTimerFillColor(viewState, timestamp);
        return new RtssOsdStyle(
            Math.Clamp(advanced?.RtssOverlayX ?? 10, -10000, 10000),
            Math.Clamp(advanced?.RtssOverlayY ?? 10, -10000, 10000),
            Math.Clamp(advanced?.RtssOverlayZoom ?? 1, 1, 8),
            ToRtssRgb(fill));
    }

    public static RtssOsdStyle CreateTargetStyle(RtssOsdStyle style)
    {
        return new RtssOsdStyle(style.X, style.Y, 1, style.RgbColor);
    }

    private static string FormatTimer(AppSettings settings, TimeSpan elapsed)
    {
        bool showTimer = settings.Overlay.Columns.Timer.Show;
        bool showMilliseconds = settings.Overlay.Columns.TimerMilliseconds.Show;
        if (!showTimer && !showMilliseconds)
        {
            return SplitTimerFormatter.Format(elapsed);
        }

        var builder = new StringBuilder(capacity: 12);
        if (showTimer)
        {
            builder.Append(SplitTimerFormatter.FormatWithoutMilliseconds(elapsed));
        }

        if (showMilliseconds)
        {
            builder.Append(SplitTimerFormatter.FormatMilliseconds(elapsed));
        }

        return builder.ToString();
    }

    private static Color ResolveTimerFillColor(ApplicationViewState viewState, long timestamp)
    {
        TimeSpan elapsed = viewState.ElapsedAt(timestamp);
        UiPalette palette = UiPalette.From(viewState.Settings.Overlay.Colors);
        TextRenderStyle style = OverlayTextStyles.GetTimerTextStyle(
            viewState.Settings,
            viewState.DisplayStatuses,
            viewState.CurrentSplitIndex,
            viewState.TimerPhase,
            elapsed,
            palette,
            milliseconds: false);
        return style.Fill;
    }

    private static int ToRtssRgb(Color color)
    {
        return color.ToArgb() & 0x00FFFFFF;
    }

    private static string FormatRtssColor(int rgbColor)
    {
        return (rgbColor & 0x00FFFFFF).ToString("X6", System.Globalization.CultureInfo.InvariantCulture);
    }
}
