namespace TerrariaSplit;

internal sealed class BossSplitTracker
{
    private readonly List<BossSplitStatus> statuses = new();
    private bool[] initialStateResolved = Array.Empty<bool>();

    private int currentIndex;

    public IReadOnlyList<BossSplitStatus> Statuses => statuses;

    public int CurrentIndex => currentIndex;

    public void SetDefinitions(IReadOnlyList<BossSplitDefinition> definitions)
    {
        statuses.Clear();
        statuses.AddRange(definitions.Select(definition => new BossSplitStatus(definition)));
        initialStateResolved = new bool[statuses.Count];
        MarkAllInitialStatesResolved();
        currentIndex = 0;
    }

    public void Reset()
    {
        ResetStatuses();
        MarkAllInitialStatesResolved();
    }

    public void OnRunStarted(TerrariaWatchSnapshot snapshot)
    {
        ResetStatuses();
        MarkAllInitialStatesPending();
        ResolveInitialStates(snapshot.BossStates);
    }

    public BossSplitRecord? Update(TerrariaWatchSnapshot snapshot, TimeSpan elapsed)
    {
        if (snapshot.IsGameMenu != false || currentIndex >= statuses.Count)
        {
            return null;
        }

        ResolveInitialStates(snapshot.BossStates);
        if (currentIndex >= statuses.Count || !initialStateResolved[currentIndex])
        {
            return null;
        }

        BossSplitStatus current = statuses[currentIndex];
        BossSplitRecord? split = current.TryComplete(snapshot.BossStates, elapsed);
        if (split is not null)
        {
            currentIndex = FindNextActiveIndex();
        }

        return split;
    }

    private void ResetStatuses()
    {
        foreach (BossSplitStatus status in statuses)
        {
            status.Reset();
        }

        currentIndex = 0;
    }

    private void ResolveInitialStates(TerrariaBossStates states)
    {
        for (int i = 0; i < statuses.Count; i++)
        {
            if (initialStateResolved[i])
            {
                continue;
            }

            BossSplitStatus status = statuses[i];
            if (status.IsSkipped || status.IsCompleted)
            {
                initialStateResolved[i] = true;
            }
            else if (status.Definition.IsComplete(states))
            {
                status.Skip();
                initialStateResolved[i] = true;
            }
            else if (status.Definition.IsKnownIncomplete(states))
            {
                initialStateResolved[i] = true;
            }
        }

        currentIndex = FindNextActiveIndex();
    }

    private void MarkAllInitialStatesResolved()
    {
        Array.Fill(initialStateResolved, true);
    }

    private void MarkAllInitialStatesPending()
    {
        if (initialStateResolved.Length != statuses.Count)
        {
            initialStateResolved = new bool[statuses.Count];
        }

        Array.Fill(initialStateResolved, false);
    }

    private int FindNextActiveIndex()
    {
        int index = statuses.FindIndex(status => !status.IsSkipped && !status.IsCompleted);
        return index >= 0 ? index : statuses.Count;
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
        currentIndex = FindNextActiveIndex();

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

        currentIndex = FindNextActiveIndex();
    }
}
