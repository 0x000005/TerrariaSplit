namespace TerrariaSplit.Application;

public interface IRunStatisticsRecorder
{
    void RecordRun(IReadOnlyList<SplitStatusSnapshot> statuses);
}

public interface IPersonalBestSnapshotStore
{
    PersonalBestSnapshotSaveResult SavePersonalBestTimeSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime);

    PersonalBestSnapshotSaveResult SavePersonalBestSegmentSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime);
}

public sealed record PersonalBestSnapshotSaveResult(
    ReferenceSplitSet? Snapshot,
    OperationResult Result)
{
    public bool Succeeded => Result.Succeeded && Snapshot is not null;

    public bool Failed => !Succeeded;

    public OperationResult Failure => Result.Failed
        ? Result
        : OperationResult.Failure("Failed to save personal best snapshot.");

    public static PersonalBestSnapshotSaveResult Success(ReferenceSplitSet snapshot)
    {
        return new PersonalBestSnapshotSaveResult(snapshot, OperationResult.Success());
    }

    public static PersonalBestSnapshotSaveResult FromResult(
        OperationResult result,
        ReferenceSplitSet? snapshot)
    {
        return result.Succeeded && snapshot is not null
            ? Success(snapshot)
            : new PersonalBestSnapshotSaveResult(null, result.Failed
                ? result
                : OperationResult.Failure("Failed to save personal best snapshot."));
    }
}

public sealed class DelegateRunStatisticsRecorder : IRunStatisticsRecorder
{
    private readonly Action<IReadOnlyList<SplitStatusSnapshot>> recordRun;

    public DelegateRunStatisticsRecorder(Action<IReadOnlyList<SplitStatusSnapshot>> recordRun)
    {
        this.recordRun = recordRun;
    }

    public void RecordRun(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        recordRun(statuses);
    }
}

public sealed class DelegatePersonalBestSnapshotStore : IPersonalBestSnapshotStore
{
    private readonly Func<Dictionary<string, string>, string, string?, string, PersonalBestSnapshotSaveResult> saveTimeSnapshot;
    private readonly Func<Dictionary<string, string>, string, string?, string, PersonalBestSnapshotSaveResult> saveSegmentSnapshot;

    public DelegatePersonalBestSnapshotStore(
        Func<Dictionary<string, string>, string, string?, string, PersonalBestSnapshotSaveResult> saveTimeSnapshot,
        Func<Dictionary<string, string>, string, string?, string, PersonalBestSnapshotSaveResult> saveSegmentSnapshot)
    {
        this.saveTimeSnapshot = saveTimeSnapshot;
        this.saveSegmentSnapshot = saveSegmentSnapshot;
    }

    public PersonalBestSnapshotSaveResult SavePersonalBestTimeSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime)
    {
        return saveTimeSnapshot(splits, bossName, previousTime, newTime);
    }

    public PersonalBestSnapshotSaveResult SavePersonalBestSegmentSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime)
    {
        return saveSegmentSnapshot(splits, bossName, previousTime, newTime);
    }
}

public sealed class NullRunStatisticsRecorder : IRunStatisticsRecorder
{
    public static NullRunStatisticsRecorder Instance { get; } = new();

    private NullRunStatisticsRecorder()
    {
    }

    public void RecordRun(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
    }
}

public sealed class InMemoryPersonalBestSnapshotStore : IPersonalBestSnapshotStore
{
    public static InMemoryPersonalBestSnapshotStore Instance { get; } = new();

    private InMemoryPersonalBestSnapshotStore()
    {
    }

    public PersonalBestSnapshotSaveResult SavePersonalBestTimeSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime)
    {
        return PersonalBestSnapshotSaveResult.Success(CreateSnapshot(splits, bossName, previousTime, newTime));
    }

    public PersonalBestSnapshotSaveResult SavePersonalBestSegmentSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime)
    {
        return PersonalBestSnapshotSaveResult.Success(CreateSnapshot(splits, bossName, previousTime, newTime));
    }

    private static ReferenceSplitSet CreateSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime)
    {
        string oldTime = string.IsNullOrWhiteSpace(previousTime) ? "None" : previousTime.Trim();
        string name = $"{NormalizeSnapshotPart(bossName)}_{oldTime}-{NormalizeSnapshotPart(newTime)}";
        return new ReferenceSplitSet
        {
            Name = name,
            Splits = new Dictionary<string, string>(splits, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static string NormalizeSnapshotPart(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    }
}
