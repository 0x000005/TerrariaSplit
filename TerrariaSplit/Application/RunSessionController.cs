namespace TerrariaSplit.Application;

internal sealed class RunLifecycleController
{
    private readonly RunFinalizer runFinalizer = new();
    private bool runStatsRecorded;

    public void MarkRunStarted()
    {
        runStatsRecorded = false;
    }

    public bool Reset(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        bool recordStats,
        Func<string, bool> confirmPersonalBestUpdate)
    {
        return Reset(settings, settings, statuses, recordStats, confirmPersonalBestUpdate);
    }

    public bool Reset(
        AppSettings routeSettings,
        AppSettings updateTargetSettings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        bool recordStats,
        Func<string, bool> confirmPersonalBestUpdate)
    {
        bool settingsUpdated = false;
        if (recordStats)
        {
            settingsUpdated = runFinalizer.Finalize(
                routeSettings,
                updateTargetSettings,
                statuses,
                runStatsRecorded,
                confirmPersonalBestUpdate);
            runStatsRecorded = true;
        }

        runStatsRecorded = false;
        return settingsUpdated;
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
