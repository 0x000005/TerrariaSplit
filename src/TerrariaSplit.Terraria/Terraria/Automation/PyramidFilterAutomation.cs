namespace TerrariaSplit.Terraria.Automation;

internal sealed class PyramidFilterAutomation
{
    private static readonly IReadOnlySet<string> NoObservedFactKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static readonly PyramidFilterWaitTimings DefaultWaitTimings = new(
        WorldFileTimeout: TimeSpan.FromMinutes(5),
        LegacyPollInterval: TimeSpan.FromMilliseconds(100),
        LegacyStableFileDuration: TimeSpan.FromMilliseconds(400),
        GenerationPollInterval: TimeSpan.FromMilliseconds(30),
        FastOpenTimeout: TimeSpan.FromMilliseconds(1000));

    private readonly TerrariaAutomationContext automation;
    private readonly PyramidFilterWorldFileEvaluator worldFileEvaluator;
    private readonly Func<ITerrariaWorldWatcher> watcherFactory;
    private readonly Func<string> worldsDirectoryProvider;
    private readonly PyramidFilterWaitTimings waitTimings;

    public PyramidFilterAutomation(
        TerrariaAutomationContext automation,
        TerrariaWorldFilePyramidScanner? scanner = null,
        Func<ITerrariaWorldWatcher>? watcherFactory = null,
        Func<string>? worldsDirectoryProvider = null,
        PyramidFilterWaitTimings? waitTimings = null)
    {
        this.automation = automation;
        worldFileEvaluator = new PyramidFilterWorldFileEvaluator(scanner);
        this.watcherFactory = watcherFactory ?? (() => new TerrariaWorldWatcher(observeWorldGeneration: true));
        this.worldsDirectoryProvider = worldsDirectoryProvider ?? DefaultWorldsDirectory;
        this.waitTimings = waitTimings ?? DefaultWaitTimings;
    }

    public async Task<PyramidFilterOutcome> RunAsync(
        AutoCreateWorldSettings settings,
        IReadOnlyDictionary<string, DateTime> worldsBefore,
        CancellationToken cancellationToken)
    {
        bool pyramidEnabled = PyramidFilterWorldFileEvaluator.IsPyramidFilterEnabled(settings);
        if (!pyramidEnabled)
        {
            return PyramidFilterOutcome.Disabled;
        }

        string? worldPath = await WaitForStableCreatedWorldFileAsync(worldsBefore, cancellationToken);
        if (string.IsNullOrWhiteSpace(worldPath))
        {
            StaticAppLogger.Instance.Info("World post-generation filter rejected the attempt because no completed world file was observed before timeout.");
            return PyramidFilterOutcome.Rejected;
        }

        StaticAppLogger.Instance.Info($"World post-generation filter will scan world file '{Path.GetFileName(worldPath)}'.");

        PyramidFilterWorldFileResult result = worldFileEvaluator.Evaluate(worldPath, settings);
        if (!result.ScanSucceeded)
        {
            StaticAppLogger.Instance.Info(
                $"World post-generation filter rejected '{Path.GetFileName(worldPath)}' because the world file could not be scanned. " +
                $"requiredItems={PyramidFilterItemMatcher.FormatRequiredItems(result.RequiredItemMask)}, detail={result.Detail}, " +
                $"scanMs={result.ScanDuration.TotalMilliseconds:0}");
            return PyramidFilterOutcome.Rejected;
        }

        StaticAppLogger.Instance.Info(
            $"World post-generation filter scan '{Path.GetFileName(worldPath)}': keep={result.Keep}, " +
            $"pyramidEnabled={result.PyramidFilterEnabled}, pyramidKeep={result.PyramidKeep}, " +
            $"requiredItems={PyramidFilterItemMatcher.FormatRequiredItems(result.RequiredItemMask)}, " +
            $"corridor={result.ScanBounds.Left},{result.ScanBounds.Top},{result.ScanBounds.Right},{result.ScanBounds.Bottom}, " +
            $"candidateChests={result.CandidateChests.FormatSummary()}, " +
            $"scanMs={result.ScanDuration.TotalMilliseconds:0}");

        return result.Keep
            ? PyramidFilterOutcome.Kept
            : PyramidFilterOutcome.Rejected;
    }

    private async Task<string?> WaitForStableCreatedWorldFileAsync(
        IReadOnlyDictionary<string, DateTime> worldsBefore,
        CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + waitTimings.WorldFileTimeout;
        string? stablePath = null;
        long stableLength = -1;
        DateTime stableWriteTime = DateTime.MinValue;
        DateTime stableSince = DateTime.MinValue;
        ITerrariaWorldWatcher? generationWatcher = TryCreateGenerationWatcher();
        bool generationWasVisible = false;
        bool observedGeneration = false;
        DateTime fastOpenDeadline = DateTime.MinValue;
        bool fastOpenExpiredLogged = true;

        try
        {
            while (DateTime.UtcNow <= deadline)
            {
                automation.ThrowIfCancellationRequested(cancellationToken);
                DateTime nowUtc = DateTime.UtcNow;
                PollGenerationState(
                    ref generationWatcher,
                    nowUtc,
                    ref generationWasVisible,
                    ref observedGeneration,
                    ref fastOpenDeadline,
                    ref fastOpenExpiredLogged);

                bool hasCandidate = TryFindNewestCreatedWorldFile(
                    worldsBefore,
                    out string? candidatePath,
                    out long length,
                    out DateTime writeTime);
                if (hasCandidate && candidatePath is not null)
                {
                    bool fastOpenActive = IsFastOpenActive(nowUtc, fastOpenDeadline);
                    if (fastOpenActive && FileAccessProbe.CanOpenForExclusiveRead(candidatePath))
                    {
                        StaticAppLogger.Instance.Info(
                            $"Pyramid filter will scan '{Path.GetFileName(candidatePath)}' after world generation state ended.");
                        return candidatePath;
                    }

                    if (TryAcceptStableCandidate(
                            candidatePath,
                            length,
                            writeTime,
                            ref stablePath,
                            ref stableLength,
                            ref stableWriteTime,
                            ref stableSince))
                    {
                        return candidatePath;
                    }
                }

                if (!fastOpenExpiredLogged && fastOpenDeadline != DateTime.MinValue && nowUtc > fastOpenDeadline)
                {
                    fastOpenExpiredLogged = true;
                    StaticAppLogger.Instance.Info("Pyramid filter fast world-file open window expired; falling back to stable file wait.");
                }

                await automation.DelayAsync(NextWaitInterval(nowUtc, fastOpenDeadline), cancellationToken);
            }
        }
        finally
        {
            generationWatcher?.Dispose();
        }

        return null;
    }

    private void PollGenerationState(
        ref ITerrariaWorldWatcher? generationWatcher,
        DateTime nowUtc,
        ref bool generationWasVisible,
        ref bool observedGeneration,
        ref DateTime fastOpenDeadline,
        ref bool fastOpenExpiredLogged)
    {
        if (generationWatcher is null)
        {
            return;
        }

        TerrariaWatchSnapshot snapshot;
        try
        {
            snapshot = generationWatcher.Poll();
        }
        catch (Exception ex)
        {
            StaticAppLogger.Instance.Error(ex, "Pyramid filter world generation watcher failed; falling back to stable file wait.");
            generationWatcher.Dispose();
            generationWatcher = null;
            generationWasVisible = false;
            return;
        }

        bool generationVisible = snapshot.WorldGeneration.HasAnyData;
        if (generationVisible)
        {
            generationWasVisible = true;
            if (!observedGeneration)
            {
                observedGeneration = true;
                StaticAppLogger.Instance.Info("Pyramid filter observed active world generation state.");
            }

            return;
        }

        if (!generationWasVisible)
        {
            return;
        }

        generationWasVisible = false;
        fastOpenDeadline = nowUtc + waitTimings.FastOpenTimeout;
        fastOpenExpiredLogged = false;
        StaticAppLogger.Instance.Info(
            $"Pyramid filter observed world generation state end; trying completed world file open for " +
            $"{(int)waitTimings.FastOpenTimeout.TotalMilliseconds}ms.");
    }

    private ITerrariaWorldWatcher? TryCreateGenerationWatcher()
    {
        try
        {
            ITerrariaWorldWatcher watcher = watcherFactory();
            watcher.SetObservedFactKeys(NoObservedFactKeys);
            return watcher;
        }
        catch (Exception ex)
        {
            StaticAppLogger.Instance.Error(ex, "Pyramid filter could not start world generation watcher; falling back to stable file wait.");
            return null;
        }
    }

    private bool TryAcceptStableCandidate(
        string candidatePath,
        long length,
        DateTime writeTime,
        ref string? stablePath,
        ref long stableLength,
        ref DateTime stableWriteTime,
        ref DateTime stableSince)
    {
        if (string.Equals(stablePath, candidatePath, StringComparison.OrdinalIgnoreCase) &&
            stableLength == length &&
            stableWriteTime == writeTime &&
            FileAccessProbe.CanOpenForExclusiveRead(candidatePath))
        {
            if (stableSince == DateTime.MinValue)
            {
                stableSince = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow - stableSince >= waitTimings.LegacyStableFileDuration)
            {
                return true;
            }
        }
        else
        {
            stablePath = candidatePath;
            stableLength = length;
            stableWriteTime = writeTime;
            stableSince = DateTime.MinValue;
        }

        return false;
    }

    private TimeSpan NextWaitInterval(DateTime nowUtc, DateTime fastOpenDeadline)
    {
        TimeSpan interval = IsFastOpenActive(nowUtc, fastOpenDeadline)
            ? waitTimings.GenerationPollInterval
            : waitTimings.GenerationPollInterval < waitTimings.LegacyPollInterval
                ? waitTimings.GenerationPollInterval
                : waitTimings.LegacyPollInterval;
        return interval <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : interval;
    }

    private static bool IsFastOpenActive(DateTime nowUtc, DateTime fastOpenDeadline)
    {
        return fastOpenDeadline != DateTime.MinValue && nowUtc <= fastOpenDeadline;
    }

    private bool TryFindNewestCreatedWorldFile(
        IReadOnlyDictionary<string, DateTime> worldsBefore,
        out string? path,
        out long length,
        out DateTime writeTimeUtc)
    {
        path = null;
        length = -1;
        writeTimeUtc = DateTime.MinValue;
        string worldsPath = worldsDirectoryProvider();
        if (!Directory.Exists(worldsPath))
        {
            return false;
        }

        foreach (string worldFile in Directory.EnumerateFiles(worldsPath, "*.wld", SearchOption.TopDirectoryOnly))
        {
            FileInfo info;
            try
            {
                info = new FileInfo(worldFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            string fileName = info.Name;
            bool createdOrChanged = !worldsBefore.TryGetValue(fileName, out DateTime previousWriteTime) ||
                info.LastWriteTimeUtc > previousWriteTime;
            if (!createdOrChanged || info.LastWriteTimeUtc < writeTimeUtc)
            {
                continue;
            }

            path = info.FullName;
            length = info.Length;
            writeTimeUtc = info.LastWriteTimeUtc;
        }

        return path is not null;
    }

    private static string DefaultWorldsDirectory()
    {
        return Path.Combine(TerrariaSavePaths.SaveRoot(), "Worlds");
    }

}

internal readonly record struct PyramidFilterWaitTimings(
    TimeSpan WorldFileTimeout,
    TimeSpan LegacyPollInterval,
    TimeSpan LegacyStableFileDuration,
    TimeSpan GenerationPollInterval,
    TimeSpan FastOpenTimeout);

internal enum PyramidFilterOutcome
{
    Disabled,
    Kept,
    Rejected
}
