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

        var lastRunSplits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SplitStatusSnapshot? lastCompleted = null;

        foreach (SplitStatusSnapshot status in statuses)
        {
            if (status.Time is not TimeSpan splitTime)
            {
                continue;
            }

            lastCompleted = status;
            lastRunSplits[status.Definition.Id] = TimeText.FormatRecord(splitTime);
        }

        splitTimeSets.SaveLastRun(lastRunSplits, lastCompleted?.Definition.DisplayName, lastCompleted?.Time);
    }
}
