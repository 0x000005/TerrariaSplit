namespace TerrariaSplit.UI;

internal sealed class RunFinalizationPersistence
{
    private readonly RunStatsRepository runStatistics;
    private readonly SplitTimeSetRepository splitTimeSets;
    private readonly Func<string, bool> confirmPersonalBestUpdate;

    public RunFinalizationPersistence(
        RunStatsRepository runStatistics,
        SplitTimeSetRepository splitTimeSets,
        Func<string, bool> confirmPersonalBestUpdate)
    {
        this.runStatistics = runStatistics;
        this.splitTimeSets = splitTimeSets;
        this.confirmPersonalBestUpdate = confirmPersonalBestUpdate;
    }

    public OperationResult RecordStatistics(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        try
        {
            runStatistics.RecordRun(statuses);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("Failed to save run statistics.", ex);
        }
    }

    public PersonalBestFinalizationResult FinalizePersonalBest(PersonalBestFinalizationPlan plan)
    {
        try
        {
            if (plan.RequiresConfirmation &&
                !confirmPersonalBestUpdate(plan.ConfirmationText))
            {
                return PersonalBestFinalizationResult.Declined(plan.PlanId);
            }

            var failures = new List<OperationResult>();
            ReferenceSplitSet? segmentSnapshot = SaveSegmentSnapshot(plan.SegmentSnapshot, failures);
            ReferenceSplitSet? timeSnapshot = failures.Count == 0
                ? SaveTimeSnapshot(plan.TimeSnapshot, failures)
                : null;
            return new PersonalBestFinalizationResult(
                plan.PlanId,
                Approved: true,
                segmentSnapshot,
                timeSnapshot,
                failures);
        }
        catch (Exception ex)
        {
            return new PersonalBestFinalizationResult(
                plan.PlanId,
                Approved: true,
                null,
                null,
                [OperationResult.Failure("Failed to finalize personal best snapshots.", ex)]);
        }
    }

    private ReferenceSplitSet? SaveSegmentSnapshot(
        PersonalBestSnapshotRequest? request,
        List<OperationResult> failures)
    {
        if (request is null)
        {
            return null;
        }

        OperationResult result = splitTimeSets.TrySavePersonalBestSegmentSnapshot(
            new Dictionary<string, string>(request.Splits, StringComparer.OrdinalIgnoreCase),
            request.BossName,
            request.PreviousTime,
            request.NewTime,
            out ReferenceSplitSet? snapshot);
        if (result.Failed || snapshot is null)
        {
            failures.Add(result.Failed
                ? result
                : OperationResult.Failure("Failed to save personal best segment snapshot."));
            return null;
        }

        return snapshot;
    }

    private ReferenceSplitSet? SaveTimeSnapshot(
        PersonalBestSnapshotRequest? request,
        List<OperationResult> failures)
    {
        if (request is null)
        {
            return null;
        }

        OperationResult result = splitTimeSets.TrySavePersonalBestTimeSnapshot(
            new Dictionary<string, string>(request.Splits, StringComparer.OrdinalIgnoreCase),
            request.BossName,
            request.PreviousTime,
            request.NewTime,
            out ReferenceSplitSet? snapshot);
        if (result.Failed || snapshot is null)
        {
            failures.Add(result.Failed
                ? result
                : OperationResult.Failure("Failed to save personal best time snapshot."));
            return null;
        }

        return snapshot;
    }
}
