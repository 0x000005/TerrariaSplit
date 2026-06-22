namespace TerrariaSplit.Application;

internal sealed class RunLifecycleController
{
    private readonly IRunStatisticsRecorder runStatisticsRecorder;
    private readonly RunFinalizer runFinalizer;
    private bool runStatsRecorded;

    public RunLifecycleController(
        IRunStatisticsRecorder? runStatisticsRecorder = null,
        IPersonalBestSnapshotStore? personalBestSnapshotStore = null)
    {
        this.runStatisticsRecorder = runStatisticsRecorder ?? NullRunStatisticsRecorder.Instance;
        runFinalizer = new RunFinalizer(this.runStatisticsRecorder, personalBestSnapshotStore);
    }

    public void MarkRunStarted()
    {
        runStatsRecorded = false;
    }

    public RunFinalizationResult Reset(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        bool recordStats,
        Func<string, bool> confirmPersonalBestUpdate)
    {
        return Reset(settings, settings, statuses, recordStats, confirmPersonalBestUpdate);
    }

    public RunFinalizationResult Reset(
        AppSettings routeSettings,
        AppSettings updateTargetSettings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        bool recordStats,
        Func<string, bool> confirmPersonalBestUpdate)
    {
        RunFinalizationResult result = RunFinalizationResult.NoChanges;
        if (recordStats)
        {
            result = runFinalizer.Finalize(
                routeSettings,
                updateTargetSettings,
                statuses,
                runStatsRecorded,
                confirmPersonalBestUpdate);
            runStatsRecorded = true;
        }

        runStatsRecorded = false;
        return result;
    }

    public void RecordRunStatsOnce(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        if (runStatsRecorded)
        {
            return;
        }

        runStatisticsRecorder.RecordRun(statuses);
        runStatsRecorded = true;
    }
}
