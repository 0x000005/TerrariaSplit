namespace TerrariaSplit.Application;

internal interface IRunStatisticsRecorder
{
    void RecordRun(IReadOnlyList<SplitStatusSnapshot> statuses);
}

internal interface IPersonalBestSnapshotStore
{
    ReferenceSplitSet SavePersonalBestTimeSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime);

    ReferenceSplitSet SavePersonalBestSegmentSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime);
}

internal sealed class DelegateRunStatisticsRecorder : IRunStatisticsRecorder
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

internal sealed class DelegatePersonalBestSnapshotStore : IPersonalBestSnapshotStore
{
    private readonly Func<Dictionary<string, string>, string, string?, string, ReferenceSplitSet> saveTimeSnapshot;
    private readonly Func<Dictionary<string, string>, string, string?, string, ReferenceSplitSet> saveSegmentSnapshot;

    public DelegatePersonalBestSnapshotStore(
        Func<Dictionary<string, string>, string, string?, string, ReferenceSplitSet> saveTimeSnapshot,
        Func<Dictionary<string, string>, string, string?, string, ReferenceSplitSet> saveSegmentSnapshot)
    {
        this.saveTimeSnapshot = saveTimeSnapshot;
        this.saveSegmentSnapshot = saveSegmentSnapshot;
    }

    public ReferenceSplitSet SavePersonalBestTimeSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime)
    {
        return saveTimeSnapshot(splits, bossName, previousTime, newTime);
    }

    public ReferenceSplitSet SavePersonalBestSegmentSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime)
    {
        return saveSegmentSnapshot(splits, bossName, previousTime, newTime);
    }
}

internal sealed class NullRunStatisticsRecorder : IRunStatisticsRecorder
{
    public static NullRunStatisticsRecorder Instance { get; } = new();

    private NullRunStatisticsRecorder()
    {
    }

    public void RecordRun(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
    }
}

internal sealed class InMemoryPersonalBestSnapshotStore : IPersonalBestSnapshotStore
{
    public static InMemoryPersonalBestSnapshotStore Instance { get; } = new();

    private InMemoryPersonalBestSnapshotStore()
    {
    }

    public ReferenceSplitSet SavePersonalBestTimeSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime)
    {
        return CreateSnapshot(splits, bossName, previousTime, newTime);
    }

    public ReferenceSplitSet SavePersonalBestSegmentSnapshot(
        Dictionary<string, string> splits,
        string bossName,
        string? previousTime,
        string newTime)
    {
        return CreateSnapshot(splits, bossName, previousTime, newTime);
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
