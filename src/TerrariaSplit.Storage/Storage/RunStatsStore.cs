namespace TerrariaSplit.Storage;

public sealed class RunStatsRepository
{
    private readonly SplitTimeSetRepository splitTimeSets;

    public RunStatsRepository(SplitTimeSetRepository? splitTimeSets = null)
    {
        this.splitTimeSets = splitTimeSets ?? new SplitTimeSetRepository();
    }

    public RunStats Load()
    {
        return new RunStats
        {
            LastRunSplits = splitTimeSets.LoadLatestLastRun()
        };
    }

    public void RecordRun(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        if (!statuses.Any(status => status.Time is not null))
        {
            return;
        }

        RunStats stats = Load();
        stats.LastRunSplits.Clear();
        SplitStatusSnapshot? lastCompleted = null;

        foreach (SplitStatusSnapshot status in statuses)
        {
            if (status.Time is not TimeSpan splitTime)
            {
                continue;
            }

            lastCompleted = status;
            stats.LastRunSplits[status.Definition.Id] = TimeText.FormatRecord(splitTime);
        }

        splitTimeSets.SaveLastRun(stats.LastRunSplits, lastCompleted?.Definition.DisplayName, lastCompleted?.Time);
    }
}

public static class RunStatsStore
{
    private static readonly RunStatsRepository Repository = new();

    public static RunStats Load()
    {
        return Repository.Load();
    }

    public static void RecordRun(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        Repository.RecordRun(statuses);
    }
}
