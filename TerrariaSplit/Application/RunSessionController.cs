namespace TerrariaSplit;

internal sealed class RunSessionController
{
    private readonly RunFinalizer runFinalizer = new();
    private bool runStatsRecorded;

    public SplitTimer Timer { get; } = new();

    public BossSplitTracker SplitTracker { get; } = new();

    public void SetDefinitions(IReadOnlyList<BossSplitDefinition> definitions)
    {
        SplitTracker.SetDefinitions(definitions);
    }

    public void MarkRunStarted()
    {
        runStatsRecorded = false;
    }

    public void Reset(
        AppSettings settings,
        bool recordStats,
        Func<string, bool> confirmPersonalBestUpdate)
    {
        if (recordStats)
        {
            runFinalizer.Finalize(settings, SplitTracker.Statuses, runStatsRecorded, confirmPersonalBestUpdate);
            runStatsRecorded = true;
        }

        Timer.Reset();
        SplitTracker.Reset();
        runStatsRecorded = false;
    }

    public void RecordRunStatsOnce()
    {
        if (runStatsRecorded)
        {
            return;
        }

        RunStatsStore.RecordRun(SplitTracker.Statuses);
        runStatsRecorded = true;
    }
}
