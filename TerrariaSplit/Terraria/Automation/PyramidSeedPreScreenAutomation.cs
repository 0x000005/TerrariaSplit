namespace TerrariaSplit.Terraria.Automation;

internal sealed class PyramidSeedPreScreenAutomation
{
    private readonly TerrariaAutomationContext automation;
    private readonly IPyramidSeedPreScreenEvaluator evaluator;

    public PyramidSeedPreScreenAutomation(
        TerrariaAutomationContext automation,
        IPyramidSeedPreScreenEvaluator? evaluator = null)
    {
        this.automation = automation;
        this.evaluator = evaluator ?? new PyramidSeedPreScreenEvaluator();
    }

    public static bool IsEnabledFor(AutoCreateWorldSettings settings)
    {
        return PyramidSeedPreScreenEvaluator.IsEnabledFor(settings);
    }

    public async Task<PyramidSeedPreScreenAutomationResult> RandomizeUntilAcceptedAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        TimeSpan clickDelay,
        CancellationToken cancellationToken)
    {
        if (!IsEnabledFor(settings))
        {
            bool randomized = await RandomizeOnceAsync("randomize visible seed", geometry, clickDelay, cancellationToken);
            return randomized
                ? PyramidSeedPreScreenAutomationResult.FromAccepted()
                : PyramidSeedPreScreenAutomationResult.FromFailed("Visible seed randomize click failed.");
        }

        if (!TerrariaVisibleSeedReader.TryCreate(
                automation.DelayAsync,
                out TerrariaVisibleSeedReader? seedReader,
                out string detail))
        {
            AppLogger.Info($"Pyramid seed pre-screen could not start seed reader; randomizing once and continuing without prediction: {detail}");
            return await RandomizeOnceAndContinueWithoutPredictionAsync(geometry, clickDelay, detail, cancellationToken);
        }

        if (seedReader is null)
        {
            const string missingReaderDetail = "Visible seed reader was not created.";
            AppLogger.Info($"Pyramid seed pre-screen could not start seed reader; randomizing once and continuing without prediction: {missingReaderDetail}");
            return await RandomizeOnceAndContinueWithoutPredictionAsync(geometry, clickDelay, missingReaderDetail, cancellationToken);
        }

        using (seedReader)
        {
            AppLogger.Info("Pyramid seed pre-screen active for small crimson world; seedReadTimeout=1000ms.");
            var loop = new PyramidSeedPreScreenLoop(evaluator, AppLogger.Info);
            PyramidSeedPreScreenLoopResult result = await loop.RunAsync(
                settings,
                new TerrariaVisibleSeedRandomizer(automation, geometry, clickDelay),
                seedReader,
                cancellationToken);
            return result.Status switch
            {
                PyramidSeedPreScreenLoopStatus.Accepted => PyramidSeedPreScreenAutomationResult.FromAccepted(),
                PyramidSeedPreScreenLoopStatus.RejectedSeed => PyramidSeedPreScreenAutomationResult.FromRetryFromMainMenu(result.Detail),
                PyramidSeedPreScreenLoopStatus.SeedReadFailed or PyramidSeedPreScreenLoopStatus.PredictionUnavailable =>
                    PyramidSeedPreScreenAutomationResult.FromContinueWithoutPreScreen(result.Detail),
                _ => PyramidSeedPreScreenAutomationResult.FromFailed(result.Detail)
            };
        }
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
}

internal enum PyramidSeedPreScreenAutomationStatus
{
    Accepted,
    ContinueWithoutPreScreen,
    RetryFromMainMenu,
    Failed
}

internal readonly record struct PyramidSeedPreScreenAutomationResult(
    PyramidSeedPreScreenAutomationStatus Status,
    string Detail)
{
    public bool CanCreateWorld => Status == PyramidSeedPreScreenAutomationStatus.Accepted ||
        Status == PyramidSeedPreScreenAutomationStatus.ContinueWithoutPreScreen;

    public static PyramidSeedPreScreenAutomationResult FromAccepted() =>
        new(PyramidSeedPreScreenAutomationStatus.Accepted, "accepted");

    public static PyramidSeedPreScreenAutomationResult FromContinueWithoutPreScreen(string detail) =>
        new(PyramidSeedPreScreenAutomationStatus.ContinueWithoutPreScreen, detail);

    public static PyramidSeedPreScreenAutomationResult FromRetryFromMainMenu(string detail) =>
        new(PyramidSeedPreScreenAutomationStatus.RetryFromMainMenu, detail);

    public static PyramidSeedPreScreenAutomationResult FromFailed(string detail) =>
        new(PyramidSeedPreScreenAutomationStatus.Failed, detail);
}
