using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class ZenithStarCatchAutomation
{
    private static readonly TimeSpan CursorStepInterval = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan WatcherPollInterval = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan BoundsRefreshInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan AttachGracePeriod = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(5);

    private readonly TerrariaAutomationContext automation;
    private readonly Func<ITerrariaWorldWatcher> watcherFactory;

    public ZenithStarCatchAutomation(
        TerrariaAutomationContext automation,
        Func<ITerrariaWorldWatcher>? watcherFactory = null)
    {
        this.automation = automation;
        this.watcherFactory = watcherFactory ?? (() => new TerrariaWorldWatcher(observeWorldGeneration: true));
    }

    public static bool IsEnabledFor(AutoCreateWorldSettings settings)
    {
        return settings.EnableZenithStarCatch &&
            AutoCreateSpecialWorldSeed.ParseList(settings.SpecialSeeds)
                .Contains(AutoCreateSpecialWorldSeed.Zenith, StringComparer.OrdinalIgnoreCase);
    }

    public async Task RunAsync(AutoCreateWorldSettings settings, CancellationToken cancellationToken)
    {
        if (!IsEnabledFor(settings))
        {
            return;
        }

        string stopStage = AutoCreateZenithStarCatchStage.Normalize(settings.ZenithStarCatchStopStage);
        double speedMultiplier = AutoCreateZenithStarCatchSpeed.GetMultiplier(settings.ZenithStarCatchSpeedSliderValue);
        StaticAppLogger.Instance.Info($"Create world automation will catch Zenith stars through {stopStage} at speed {speedMultiplier:0.0}.");

        using ITerrariaWorldWatcher watcher = watcherFactory();
        var path = new CursorPath(speedMultiplier);
        Rectangle clientBounds = Rectangle.Empty;
        DateTime nextBoundsRefreshUtc = DateTime.MinValue;
        DateTime nextWatcherPollUtc = DateTime.MinValue;
        bool observedTrackedPass = false;
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            while (stopwatch.Elapsed < MaxDuration)
            {
                automation.ThrowIfCancellationRequested(cancellationToken);

                DateTime nowUtc = DateTime.UtcNow;
                if (nowUtc >= nextBoundsRefreshUtc)
                {
                    if (!automation.Window.TryGetClientScreenBounds(out clientBounds) ||
                        clientBounds.Width <= 0 ||
                        clientBounds.Height <= 0)
                    {
                        if (stopwatch.Elapsed > AttachGracePeriod)
                        {
                            StaticAppLogger.Instance.Info("Create world automation could not resolve Terraria client bounds for Zenith star catch.");
                            return;
                        }
                    }
                    else
                    {
                        nextBoundsRefreshUtc = nowUtc + BoundsRefreshInterval;
                    }
                }

                if (nowUtc >= nextWatcherPollUtc)
                {
                    nextWatcherPollUtc = nowUtc + WatcherPollInterval;
                    TerrariaWatchSnapshot snapshot = watcher.Poll();
                    if (!snapshot.IsAttached)
                    {
                        if (stopwatch.Elapsed > AttachGracePeriod)
                        {
                            StaticAppLogger.Instance.Info("Create world automation could not attach watcher during Zenith star catch.");
                            return;
                        }
                    }
                    else
                    {
                        if (snapshot.IsReady && snapshot.IsGameMenu == false)
                        {
                            StaticAppLogger.Instance.Info("Create world automation stopped Zenith star catch after leaving the menu generation screen.");
                            return;
                        }

                        string? currentPassName = snapshot.WorldGeneration.CurrentPassName;
                        if (AutoCreateZenithStarCatchStage.TryGetTrackedPassIndex(currentPassName, out _))
                        {
                            observedTrackedPass = true;
                        }

                        if (observedTrackedPass && AutoCreateZenithStarCatchStage.ShouldStopAtPass(stopStage, currentPassName))
                        {
                            StaticAppLogger.Instance.Info($"Create world automation stopped Zenith star catch at world generation pass '{currentPassName}'.");
                            return;
                        }
                    }
                }

                if (clientBounds.Width > 0 && clientBounds.Height > 0)
                {
                    Point clientPoint = path.Advance(clientBounds.Size, CursorStepInterval);
                    _ = automation.Window.TryMoveScreenCursor(
                        clientBounds.Left + clientPoint.X,
                        clientBounds.Top + clientPoint.Y);
                }

                await automation.DelayAsync(CursorStepInterval, cancellationToken);
            }

            StaticAppLogger.Instance.Info("Create world automation stopped Zenith star catch after reaching the safety timeout.");
        }
        finally
        {
            MoveCursorToSafeRestPosition(clientBounds);
        }
    }

    private void MoveCursorToSafeRestPosition(Rectangle clientBounds)
    {
        if (clientBounds.Width <= 0 || clientBounds.Height <= 0)
        {
            return;
        }

        Rectangle virtualScreen = SystemInformation.VirtualScreen;
        const int padding = 72;
        Point[] candidates =
        [
            new(clientBounds.Left + clientBounds.Width / 2, clientBounds.Top - padding),
            new(clientBounds.Left - padding, clientBounds.Top + clientBounds.Height / 2),
            new(clientBounds.Right + padding, clientBounds.Top + clientBounds.Height / 2),
            new(clientBounds.Left + clientBounds.Width / 2, clientBounds.Bottom + padding)
        ];

        foreach (Point candidate in candidates)
        {
            if (virtualScreen.Contains(candidate) && !clientBounds.Contains(candidate))
            {
                _ = automation.Window.TryMoveScreenCursor(candidate.X, candidate.Y);
                return;
            }
        }

        Point fallback = new(
            Math.Clamp(clientBounds.Left + 8, virtualScreen.Left, virtualScreen.Right - 1),
            Math.Clamp(clientBounds.Top + 8, virtualScreen.Top, virtualScreen.Bottom - 1));
        _ = automation.Window.TryMoveScreenCursor(fallback.X, fallback.Y);
    }

    private sealed class CursorPath
    {
        private static readonly TimeSpan BaseHorizontalHalfSweepDuration = TimeSpan.FromSeconds(0.16);
        private const float MinXFraction = 0.04f;
        private const float MaxXFraction = 0.96f;
        private const float FixedYFraction = 0.90f;

        private double elapsedSeconds;
        private readonly double horizontalHalfSweepSeconds;

        public CursorPath(double speedMultiplier)
        {
            horizontalHalfSweepSeconds = Math.Max(0.001d, BaseHorizontalHalfSweepDuration.TotalSeconds / Math.Max(0.001d, speedMultiplier));
        }

        public Point Advance(Size clientSize, TimeSpan elapsed)
        {
            if (clientSize.Width <= 0 || clientSize.Height <= 0)
            {
                return Point.Empty;
            }

            elapsedSeconds += elapsed.TotalSeconds;
            float horizontalProgress = TriangleWave(elapsedSeconds / horizontalHalfSweepSeconds);
            float xFraction = MinXFraction + (MaxXFraction - MinXFraction) * horizontalProgress;
            float yFraction = FixedYFraction;
            int x = Math.Clamp((int)Math.Round((clientSize.Width - 1) * xFraction), 0, Math.Max(0, clientSize.Width - 1));
            int y = Math.Clamp((int)Math.Round((clientSize.Height - 1) * yFraction), 0, Math.Max(0, clientSize.Height - 1));
            return new Point(x, y);
        }

        private static float TriangleWave(double phase)
        {
            double normalized = phase % 2d;
            if (normalized < 0d)
            {
                normalized += 2d;
            }

            return normalized <= 1d
                ? (float)normalized
                : (float)(2d - normalized);
        }
    }
}
