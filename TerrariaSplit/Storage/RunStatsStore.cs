namespace TerrariaSplit;

internal static class RunStatsStore
{
    public static string StatsPath => Path.Combine(AppContext.BaseDirectory, "run-stats.json");

    public static RunStats Load()
    {
        try
        {
            RunStats stats = JsonFileStore.Read<RunStats>(StatsPath, "run statistics") ?? new RunStats();
            Normalize(stats);
            Dictionary<string, string> latestLastRun = SplitTimeSetStore.LoadLatestLastRun();
            if (latestLastRun.Count > 0)
            {
                stats.LastRunSplits = latestLastRun;
            }
            else if (stats.LastRunSplits.Count > 0)
            {
                SplitTimeSetStore.SaveLastRun(stats.LastRunSplits);
                Save(stats);
            }

            return stats;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Failed to load run statistics");
            return new RunStats();
        }
    }

    public static void Save(RunStats stats)
    {
        Normalize(stats);
    }

    public static void RecordRun(IReadOnlyList<BossSplitStatus> statuses)
    {
        if (statuses.Count == 0 || statuses.All(status => status.Time is null))
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
        Save(stats);
    }

    private static void Normalize(RunStats stats)
    {
        stats.LastRunSplits ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
