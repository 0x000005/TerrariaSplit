namespace TerrariaSplit.Storage;

public static class RunStatsStore
{
    public static RunStats Load()
    {
        return new RunStats
        {
            LastRunSplits = SplitTimeSetStore.LoadLatestLastRun()
        };
    }

    public static void RecordRun(IReadOnlyList<SplitStatusSnapshot> statuses)
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

        SplitTimeSetStore.SaveLastRun(stats.LastRunSplits, lastCompleted?.Definition.DisplayName, lastCompleted?.Time);
    }
}
