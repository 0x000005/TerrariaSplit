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
        if (recordStats)
        {
            runFinalizer.Finalize(settings, statuses, runStatsRecorded, confirmPersonalBestUpdate);
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
