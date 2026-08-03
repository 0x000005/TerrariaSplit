namespace TerrariaSplit.Application;

public sealed record PersonalBestSnapshotRequest(
    IReadOnlyDictionary<string, string> Splits,
    string BossName,
    string PreviousTime,
    string NewTime);

public sealed record PersonalBestFinalizationPlan(
    long PlanId,
    bool RequiresConfirmation,
    string ConfirmationText,
    IReadOnlyDictionary<string, string>? SegmentBestValues,
    PersonalBestSnapshotRequest? SegmentSnapshot,
    IReadOnlyDictionary<string, string>? PersonalBestTimes,
    PersonalBestSnapshotRequest? TimeSnapshot);

public sealed record PersonalBestFinalizationResult(
    long PlanId,
    bool Approved,
    ReferenceSplitSet? SegmentSnapshot,
    ReferenceSplitSet? TimeSnapshot,
    IReadOnlyList<OperationResult> Failures)
{
    public static PersonalBestFinalizationResult Declined(long planId)
    {
        return new PersonalBestFinalizationResult(planId, false, null, null, []);
    }
}
