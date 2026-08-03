namespace TerrariaSplit.Application;

internal sealed class RunLifecycleController
{
    private readonly RunFinalizer runFinalizer = new();
    private bool runStatsRecorded;
    private long nextPersonalBestPlanId;

    public void MarkRunStarted()
    {
        runStatsRecorded = false;
    }

    public RunFinalizationRequest Reset(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        bool recordStats)
    {
        return Reset(settings, settings, statuses, recordStats);
    }

    public RunFinalizationRequest Reset(
        AppSettings routeSettings,
        AppSettings updateTargetSettings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        bool recordStats)
    {
        if (!recordStats)
        {
            runStatsRecorded = false;
            return RunFinalizationRequest.None;
        }

        bool shouldRecordStatistics = !runStatsRecorded;
        runStatsRecorded = true;
        PersonalBestFinalizationPlan? personalBestPlan = runFinalizer.CreatePersonalBestPlan(
            ++nextPersonalBestPlanId,
            routeSettings,
            updateTargetSettings,
            statuses);

        runStatsRecorded = false;
        return new RunFinalizationRequest(
            shouldRecordStatistics ? [.. statuses] : [],
            personalBestPlan);
    }

    public IReadOnlyList<SplitStatusSnapshot>? CaptureRunStatisticsOnce(
        IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        if (runStatsRecorded)
        {
            return null;
        }

        runStatsRecorded = true;
        return [.. statuses];
    }
}

internal sealed record RunFinalizationRequest(
    IReadOnlyList<SplitStatusSnapshot> Statistics,
    PersonalBestFinalizationPlan? PersonalBestPlan)
{
    public static RunFinalizationRequest None { get; } = new([], null);
}
