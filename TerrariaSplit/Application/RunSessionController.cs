namespace TerrariaSplit;

internal sealed class RunLifecycleController
{
    private readonly RunFinalizer runFinalizer = new();
    private bool runStatsRecorded;

    public void MarkRunStarted()
    {
        runStatsRecorded = false;
    }

    public void Reset(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        bool recordStats,
        Func<string, bool> confirmPersonalBestUpdate)
    {
        Reset(settings, settings, statuses, recordStats, confirmPersonalBestUpdate);
    }

    public void Reset(
        AppSettings routeSettings,
        AppSettings updateTargetSettings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        bool recordStats,
        Func<string, bool> confirmPersonalBestUpdate)
    {
        if (recordStats)
        {
            runFinalizer.Finalize(
                routeSettings,
                updateTargetSettings,
                statuses,
                runStatsRecorded,
                confirmPersonalBestUpdate);
            runStatsRecorded = true;
        }

        runStatsRecorded = false;
    }

    public void RecordRunStatsOnce(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        if (runStatsRecorded)
        {
            return;
        }

        RunStatsStore.RecordRun(statuses);
        runStatsRecorded = true;
    }
}
