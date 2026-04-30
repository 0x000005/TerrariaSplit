namespace TerrariaSplit;

internal sealed class BossSplitTracker
{
    private readonly List<BossSplitStatus> statuses =
        BossSplitDefinitions.All.Select(definition => new BossSplitStatus(definition)).ToList();

    private int currentIndex;

    public IReadOnlyList<BossSplitStatus> Statuses => statuses;

    public int CurrentIndex => currentIndex;

    public void Reset()
    {
        foreach (BossSplitStatus status in statuses)
        {
            status.Reset();
        }

        currentIndex = 0;
    }

    public void OnRunStarted(TerrariaWatchSnapshot snapshot)
    {
        Reset();

        while (currentIndex < statuses.Count && statuses[currentIndex].Definition.IsComplete(snapshot.BossStates))
        {
            statuses[currentIndex].Skip();
            currentIndex++;
        }

    }

    public BossSplitRecord? Update(TerrariaWatchSnapshot snapshot, TimeSpan elapsed)
    {
        if (snapshot.IsGameMenu != false || currentIndex >= statuses.Count)
        {
            return null;
        }

        BossSplitStatus current = statuses[currentIndex];
        BossSplitRecord? split = current.TryComplete(snapshot.BossStates, elapsed);
        if (split is not null)
        {
            currentIndex++;
        }

        return split;
    }

    public TimeSpan? SetPracticeTime(int index, TimeSpan? time)
    {
        if (index < 0 || index >= statuses.Count)
        {
            return null;
        }

        TimeSpan? adjustedTime = time;
        if (adjustedTime is TimeSpan value)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if (statuses[i].Time is TimeSpan previousTime)
                {
                    if (value < previousTime)
                    {
                        statuses[i].SetTime(value);
                    }
                }
            }
        }

        statuses[index].SetTime(adjustedTime);
        currentIndex = statuses.FindIndex(status => !status.IsSkipped && !status.IsCompleted);
        if (currentIndex < 0)
        {
            currentIndex = statuses.Count;
        }

        return adjustedTime;
    }

    public void ClampCompletedTimes(TimeSpan maximumTime)
    {
        foreach (BossSplitStatus status in statuses)
        {
            if (status.Time is TimeSpan time && time > maximumTime)
            {
                status.SetTime(maximumTime);
            }
        }

        currentIndex = statuses.FindIndex(status => !status.IsSkipped && !status.IsCompleted);
        if (currentIndex < 0)
        {
            currentIndex = statuses.Count;
        }
    }
}
