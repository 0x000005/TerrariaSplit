namespace TerrariaSplit;

internal static class RunStatsStore
{
    public static RunStats Load()
    {
        return new RunStats
        {
            LastRunSplits = SplitTimeSetStore.LoadLatestLastRun()
        };
    }

    public static void RecordRun(IReadOnlyList<BossSplitStatus> statuses)
    {
        if (!HasCompletedSkeletron(statuses))
        {
            return;
        }

        RunStats stats = Load();
        stats.LastRunSplits.Clear();
        BossSplitStatus? lastCompleted = null;

        foreach (BossSplitStatus status in statuses)
        {
            if (status.Time is not TimeSpan splitTime)
            {
                continue;
            }

            lastCompleted = status;
            foreach (string bossId in status.Definition.BossIds)
            {
                stats.LastRunSplits[bossId] = TimeText.FormatRecord(splitTime);
            }
        }

        SplitTimeSetStore.SaveLastRun(stats.LastRunSplits, lastCompleted?.Definition.DisplayName, lastCompleted?.Time);
    }

    private static bool HasCompletedSkeletron(IReadOnlyList<BossSplitStatus> statuses)
    {
        return statuses.Any(status =>
            status.Time is not null &&
            status.Definition.BossIds.Any(bossId => string.Equals(
                bossId,
                BossSplitDefinitions.Skeletron,
                StringComparison.OrdinalIgnoreCase)));
    }
}
