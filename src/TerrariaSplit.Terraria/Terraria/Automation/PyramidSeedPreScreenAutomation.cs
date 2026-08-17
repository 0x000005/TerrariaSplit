using System.Drawing;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class PyramidSeedPreScreenAutomation : IDisposable
{
    private readonly TerrariaAutomationContext automation;
    private readonly WorldSeedFilterEvaluator evaluator;
    private readonly bool ownsEvaluator;
    private readonly object preparationGate = new();
    private Task<TerrariaVisibleSeedReaderPreparation>? readerPreparationTask;

    public PyramidSeedPreScreenAutomation(
        TerrariaAutomationContext automation,
        WorldSeedFilterEvaluator? evaluator = null)
    {
        this.automation = automation;
        this.evaluator = evaluator ?? new WorldSeedFilterEvaluator();
        ownsEvaluator = evaluator is null;
    }

    public static bool IsEnabledFor(AutoCreateWorldSettings settings)
    {
        return WorldSeedFilterEvaluator.IsEnabledFor(settings);
    }

    public void BeginVisibleSeedReaderPreparation(
        AutoCreateWorldSettings settings)
    {
        if (!IsEnabledFor(settings))
        {
            return;
        }

        lock (preparationGate)
        {
            if (readerPreparationTask is null)
            {
                FileAppLogger.Instance.Info(
                    "Visible seed reader prewarm started at new-world entry.");
                readerPreparationTask = Task.Run(
                    () => TerrariaVisibleSeedReader.Prepare(automation.DelayAsync));
            }
        }
    }

    public async Task<PyramidSeedPreScreenAutomationResult> RandomizeUntilAcceptedAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        TimeSpan clickDelay,
        CancellationToken cancellationToken)
    {
        if (!geometry.Profile.SupportsPyramidSeedPreScreen)
        {
            string unsupportedProfileDetail = $"{geometry.Profile.Name} does not expose the modern advanced seed randomize control.";
            FileAppLogger.Instance.Info($"Pyramid seed pre-screen skipped: {unsupportedProfileDetail}");
            return PyramidSeedPreScreenAutomationResult.FromContinueWithoutPreScreen(unsupportedProfileDetail);
        }

        if (!IsEnabledFor(settings))
        {
            bool randomized = await RandomizeOnceAsync("randomize visible seed", geometry, clickDelay, cancellationToken);
            return randomized
                ? PyramidSeedPreScreenAutomationResult.FromAccepted()
                : PyramidSeedPreScreenAutomationResult.FromFailed("Visible seed randomize click failed.");
        }

        (TerrariaVisibleSeedReader? seedReader, string detail) =
            await AcquireVisibleSeedReaderAsync(cancellationToken);
        if (seedReader is null)
        {
            FileAppLogger.Instance.Info($"Pyramid seed pre-screen could not start seed reader; randomizing once and continuing without prediction: {detail}");
            return await RandomizeOnceAndContinueWithoutPredictionAsync(geometry, clickDelay, detail, cancellationToken);
        }

        using (seedReader)
        {
            FileAppLogger.Instance.Info("World seed pre-screen active for small Crimson world; seedReadTimeout=1000ms.");
            return await RunLoopAsync(
                settings,
                geometry,
                new TerrariaVisibleSeedRandomizer(automation, geometry, clickDelay),
                geometry.AdvancedSeedRandomizeButton(),
                seedReader,
                cancellationToken);
        }
    }

    public async Task<PyramidSeedPreScreenAutomationResult> RandomizeCurrentSeedUntilAcceptedAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        TimeSpan clickDelay,
        CancellationToken cancellationToken)
    {
        if (!IsEnabledFor(settings))
        {
            return PyramidSeedPreScreenAutomationResult.FromAccepted();
        }

        (TerrariaVisibleSeedReader? seedReader, string detail) =
            await AcquireVisibleSeedReaderAsync(cancellationToken);
        if (seedReader is null)
        {
            FileAppLogger.Instance.Info($"Pyramid seed pre-screen could not start seed reader for 1.4.4.9 seed randomizer; randomizing once and continuing without prediction: {detail}");
            return await RandomizeLegacyOnceAndContinueWithoutPredictionAsync(geometry, clickDelay, detail, cancellationToken);
        }

        using (seedReader)
        {
            FileAppLogger.Instance.Info("World seed pre-screen active for 1.4.4.9 small Crimson world; seedReadTimeout=1000ms.");
            return await RunLoopAsync(
                settings,
                geometry,
                new TerrariaLegacy1449SeedRandomizer(automation, geometry, clickDelay),
                geometry.WorldAdvancedSeedButton(),
                seedReader,
                cancellationToken);
        }
    }

    private async Task<PyramidSeedPreScreenAutomationResult> RunLoopAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        IPyramidSeedRandomizer randomizer,
        Point randomizeTarget,
        TerrariaVisibleSeedReader seedReader,
        CancellationToken cancellationToken)
    {
        try
        {
            var loop = new PyramidSeedPreScreenLoop(evaluator, FileAppLogger.Instance.Info);
            PyramidSeedPreScreenLoopResult result = await loop.RunAsync(
                settings,
                geometry.Profile,
                randomizer,
                seedReader,
                cancellationToken);
            return result.Status switch
            {
                PyramidSeedPreScreenLoopStatus.Accepted => PyramidSeedPreScreenAutomationResult.FromAccepted(),
                PyramidSeedPreScreenLoopStatus.SeedReadFailed or PyramidSeedPreScreenLoopStatus.PredictionUnavailable =>
                    PyramidSeedPreScreenAutomationResult.FromContinueWithoutPreScreen(result.Detail),
                _ => PyramidSeedPreScreenAutomationResult.FromFailed(
                    BuildLoopFailureDetail(
                        result.Status.ToString(),
                        result.Attempts.ToString(),
                        result.Detail,
                        settings,
                        geometry,
                        randomizeTarget),
                    detailedDiagnostics: true)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, "Advanced seed pre-screen loop failed unexpectedly.");
            return PyramidSeedPreScreenAutomationResult.FromFailed(
                BuildLoopFailureDetail(
                    "InternalException",
                    "unknown",
                    $"{ex.GetType().FullName}: {ex.Message}",
                    settings,
                    geometry,
                    randomizeTarget),
                detailedDiagnostics: true,
                exception: ex);
        }
    }

    private string BuildLoopFailureDetail(
        string status,
        string attempts,
        string detail,
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        Point randomizeTarget)
    {
        int clientWidth = (int)Math.Round(geometry.LogicalWidth * geometry.Scale);
        int clientHeight = (int)Math.Round(geometry.LogicalHeight * geometry.Scale);
        string diagnostic =
            $"status={status}; attempts={attempts}; menuProfile={geometry.Profile.Name}; " +
            $"clientSize={clientWidth}x{clientHeight}; uiScale={geometry.Scale:0.###}; " +
            $"randomizeTarget=({randomizeTarget.X}, {randomizeTarget.Y}); " +
            $"world={settings.WorldSize}/{settings.WorldDifficulty}/{settings.WorldEvil}; " +
            $"pyramidFilter={settings.EnablePyramidFilter}; pyramidItemMask={settings.PyramidFilterItemMask}; " +
            $"crimsonFilter={settings.RequireCrimsonBetweenDungeonAndSpawn}; crimsonDistance={settings.CrimsonDistance}; " +
            $"jungleDepth={settings.JungleRouteDepth}; resourceItemMask={settings.ResourceFilterItemMask}; " +
            $"lifeCrystalMinimum={settings.ResourceFilterLifeCrystalMinimum}; " +
            $"spelunkerMinimum={settings.ResourceFilterSpelunkerPotionMinimum}; " +
            $"featherfallMinimum={settings.ResourceFilterFeatherfallPotionMinimum}; detail={detail}";
        return string.IsNullOrWhiteSpace(automation.LastFailureDiagnostic)
            ? diagnostic
            : diagnostic + "; lastFailedStep=" + automation.LastFailureDiagnostic;
    }

    public void Dispose()
    {
        Task<TerrariaVisibleSeedReaderPreparation>? pendingPreparation;
        lock (preparationGate)
        {
            pendingPreparation = readerPreparationTask;
            readerPreparationTask = null;
        }
        if (pendingPreparation is not null)
        {
            DisposePreparedReaderWhenCompleted(pendingPreparation);
        }

        if (ownsEvaluator)
        {
            evaluator.Dispose();
        }
    }

    private async Task<(TerrariaVisibleSeedReader? Reader, string Detail)>
        AcquireVisibleSeedReaderAsync(CancellationToken cancellationToken)
    {
        Task<TerrariaVisibleSeedReaderPreparation>? preparationTask;
        lock (preparationGate)
        {
            preparationTask = readerPreparationTask;
            readerPreparationTask = null;
        }

        if (preparationTask is not null)
        {
            try
            {
                TerrariaVisibleSeedReaderPreparation preparation =
                    await preparationTask.WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                FileAppLogger.Instance.Info(
                    $"Visible seed reader prewarm completed; " +
                    $"elapsedMs={preparation.Duration.TotalMilliseconds:F0}; " +
                    $"detail={preparation.Detail}");
                if (preparation.Reader is not null)
                {
                    return (preparation.Reader, preparation.Detail);
                }

                FileAppLogger.Instance.Info(
                    "Visible seed reader prewarm did not produce a reader; " +
                    "retrying synchronously at the seed screen.");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                DisposePreparedReaderWhenCompleted(preparationTask);
                throw;
            }
            catch (Exception ex)
            {
                FileAppLogger.Instance.Info(
                    "Visible seed reader prewarm failed; retrying synchronously " +
                    "at the seed screen: " + ex.Message);
            }
        }

        return TerrariaVisibleSeedReader.TryCreate(
                automation.DelayAsync,
                out TerrariaVisibleSeedReader? reader,
                out string detail)
            ? (reader, detail)
            : (null, detail);
    }

    private static void DisposePreparedReaderWhenCompleted(
        Task<TerrariaVisibleSeedReaderPreparation> preparationTask)
    {
        _ = preparationTask.ContinueWith(
            static completed =>
            {
                if (completed.Status == TaskStatus.RanToCompletion)
                {
                    completed.Result.Reader?.Dispose();
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task<PyramidSeedPreScreenAutomationResult> RandomizeLegacyOnceAndContinueWithoutPredictionAsync(
        TerrariaMenuGeometry geometry,
        TimeSpan clickDelay,
        string detail,
        CancellationToken cancellationToken)
    {
        bool randomized = await automation.ClickAsync(
            "randomize 1.4.4.9 visible seed without pre-screen",
            geometry.WorldAdvancedSeedButton(),
            clickDelay,
            cancellationToken);
        return randomized
            ? PyramidSeedPreScreenAutomationResult.FromContinueWithoutPreScreen(detail)
            : PyramidSeedPreScreenAutomationResult.FromFailed("1.4.4.9 visible seed randomize click failed.");
    }

    private async Task<PyramidSeedPreScreenAutomationResult> RandomizeOnceAndContinueWithoutPredictionAsync(
        TerrariaMenuGeometry geometry,
        TimeSpan clickDelay,
        string detail,
        CancellationToken cancellationToken)
    {
        bool randomized = await RandomizeOnceAsync("randomize visible seed without pre-screen", geometry, clickDelay, cancellationToken);
        return randomized
            ? PyramidSeedPreScreenAutomationResult.FromContinueWithoutPreScreen(detail)
            : PyramidSeedPreScreenAutomationResult.FromFailed("Visible seed randomize click failed.");
    }

    private Task<bool> RandomizeOnceAsync(
        string step,
        TerrariaMenuGeometry geometry,
        TimeSpan clickDelay,
        CancellationToken cancellationToken)
    {
        return automation.ClickAsync(step, geometry.AdvancedSeedRandomizeButton(), clickDelay, cancellationToken);
    }

    private sealed class TerrariaVisibleSeedRandomizer : IPyramidSeedRandomizer
    {
        private readonly TerrariaAutomationContext automation;
        private readonly TerrariaMenuGeometry geometry;
        private readonly TimeSpan clickDelay;

        public TerrariaVisibleSeedRandomizer(
            TerrariaAutomationContext automation,
            TerrariaMenuGeometry geometry,
            TimeSpan clickDelay)
        {
            this.automation = automation;
            this.geometry = geometry;
            this.clickDelay = clickDelay;
        }

        public Task<bool> RandomizeVisibleSeedAsync(int attempt, CancellationToken cancellationToken)
        {
            return automation.ClickAsync(
                $"randomize visible seed pre-screen attempt {attempt}",
                geometry.AdvancedSeedRandomizeButton(),
                clickDelay,
                cancellationToken);
        }
    }

    private sealed class TerrariaLegacy1449SeedRandomizer : IPyramidSeedRandomizer
    {
        private readonly TerrariaAutomationContext automation;
        private readonly TerrariaMenuGeometry geometry;
        private readonly TimeSpan clickDelay;

        public TerrariaLegacy1449SeedRandomizer(
            TerrariaAutomationContext automation,
            TerrariaMenuGeometry geometry,
            TimeSpan clickDelay)
        {
            this.automation = automation;
            this.geometry = geometry;
            this.clickDelay = clickDelay;
        }

        public Task<bool> RandomizeVisibleSeedAsync(int attempt, CancellationToken cancellationToken)
        {
            return automation.ClickAsync(
                $"randomize 1.4.4.9 visible seed pre-screen attempt {attempt}",
                geometry.WorldAdvancedSeedButton(),
                clickDelay,
                cancellationToken);
        }
    }
}

internal enum PyramidSeedPreScreenAutomationStatus
{
    Accepted,
    ContinueWithoutPreScreen,
    Failed
}

internal readonly record struct PyramidSeedPreScreenAutomationResult(
    PyramidSeedPreScreenAutomationStatus Status,
    string Detail,
    bool DetailedDiagnostics = false,
    Exception? Exception = null)
{
    public bool CanCreateWorld => Status == PyramidSeedPreScreenAutomationStatus.Accepted ||
        Status == PyramidSeedPreScreenAutomationStatus.ContinueWithoutPreScreen;

    public static PyramidSeedPreScreenAutomationResult FromAccepted() =>
        new(PyramidSeedPreScreenAutomationStatus.Accepted, "accepted");

    public static PyramidSeedPreScreenAutomationResult FromContinueWithoutPreScreen(string detail) =>
        new(PyramidSeedPreScreenAutomationStatus.ContinueWithoutPreScreen, detail);

    public static PyramidSeedPreScreenAutomationResult FromFailed(
        string detail,
        bool detailedDiagnostics = false,
        Exception? exception = null) =>
        new(
            PyramidSeedPreScreenAutomationStatus.Failed,
            detail,
            detailedDiagnostics,
            exception);
}
