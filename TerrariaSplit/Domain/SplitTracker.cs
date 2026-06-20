namespace TerrariaSplit;

internal sealed class SplitTracker
{
    private readonly List<SplitStatus> statuses = new();
    private readonly Dictionary<int, int> maxOwnedItemCounts = new();
    private bool[] initialStateResolved = Array.Empty<bool>();
    private int currentIndex;

    public IReadOnlyList<SplitStatus> Statuses => statuses;

    public int CurrentIndex => currentIndex;

    public void SetDefinitions(IReadOnlyList<SplitDefinition> definitions)
    {
        statuses.Clear();
        statuses.AddRange(definitions.Select(definition => new SplitStatus(definition)));
        maxOwnedItemCounts.Clear();
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
        maxOwnedItemCounts.Clear();
        MarkAllInitialStatesPending();
        TerrariaGameFacts facts = AddRunDerivedFacts(snapshot.Facts);
        ResolveInitialStates(facts);
    }

    public SplitRecord? Update(TerrariaWatchSnapshot snapshot, TimeSpan elapsed)
    {
        if (snapshot.IsGameMenu != false || currentIndex >= statuses.Count)
        {
            return null;
        }

        TerrariaGameFacts facts = AddRunDerivedFacts(snapshot.Facts);
        AddSatisfiedFactKeysToCompletedStatuses(facts, elapsed);
        ResolveInitialStates(facts);
        if (currentIndex >= statuses.Count)
        {
            return null;
        }

        SplitRecord? split = TryCompleteNextEligibleSplit(facts, elapsed);
        if (split is not null)
        {
            currentIndex = FindNextActiveIndex();
            ResolveInitialStates(facts);
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
                if (statuses[i].Time is TimeSpan previousTime && value < previousTime)
                {
                    statuses[i].SetTime(value);
                }
            }
        }

        statuses[index].SetTime(adjustedTime);
        currentIndex = FindNextActiveIndex();

        return adjustedTime;
    }

    public SplitTrackerState CaptureState()
    {
        return new SplitTrackerState(
            statuses.Select(status => status.CaptureState()).ToArray(),
            currentIndex,
            new Dictionary<int, int>(maxOwnedItemCounts));
    }

    public void ApplyState(SplitTrackerState state)
    {
        if (state.Statuses.Length != statuses.Count)
        {
            ResetStatuses();
            MarkAllInitialStatesResolved();
            return;
        }

        for (int i = 0; i < statuses.Count; i++)
        {
            statuses[i].ApplyState(state.Statuses[i]);
        }

        maxOwnedItemCounts.Clear();
        foreach ((int itemId, int count) in state.MaxOwnedItemCounts ?? new Dictionary<int, int>())
        {
            maxOwnedItemCounts[itemId] = Math.Max(0, count);
        }

        currentIndex = Math.Clamp(state.CurrentIndex, 0, statuses.Count);
        MarkAllInitialStatesResolved();
    }

    public void ClampCompletedTimes(TimeSpan maximumTime)
    {
        foreach (SplitStatus status in statuses)
        {
            if (status.Time is TimeSpan time && time > maximumTime)
            {
                status.SetTime(maximumTime);
            }
        }

        currentIndex = FindNextActiveIndex();
    }

    private void SkipStatusesBeforeResolvedLaterSplits()
    {
        for (int i = 0; i < statuses.Count; i++)
        {
            SplitStatus status = statuses[i];
            if (status.IsCompleted || status.IsSkipped)
            {
                continue;
            }

            for (int laterIndex = i + 1; laterIndex < statuses.Count; laterIndex++)
            {
                SplitStatus later = statuses[laterIndex];
                if (!later.Definition.IsAttached &&
                    (later.IsCompleted || later.IsSkipped))
                {
                    SkipStatusesBefore(i, laterIndex);
                    break;
                }
            }
        }
    }

    private void ResetStatuses()
    {
        foreach (SplitStatus status in statuses)
        {
            status.Reset();
        }

        maxOwnedItemCounts.Clear();
        currentIndex = 0;
    }

    private TerrariaGameFacts AddRunDerivedFacts(TerrariaGameFacts facts)
    {
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        builder.Merge(facts);
        bool addedDerivedFact = false;
        foreach ((string factKey, FactValue value) in facts.Values)
        {
            if (!SplitCatalog.TryParseItemOwnedCountFactKey(factKey, out int itemId))
            {
                continue;
            }

            addedDerivedFact = true;
            string everFactKey = SplitCatalog.CreateItemEverOwnedFactKey(itemId);
            if (value.AsInteger() is int currentCount)
            {
                int maxCount = maxOwnedItemCounts.TryGetValue(itemId, out int previous)
                    ? Math.Max(previous, currentCount)
                    : currentCount;
                maxOwnedItemCounts[itemId] = maxCount;
                builder.SetInteger(everFactKey, maxCount);
            }
            else if (maxOwnedItemCounts.TryGetValue(itemId, out int maxCount))
            {
                builder.SetInteger(everFactKey, maxCount);
            }
            else
            {
                builder.SetInteger(everFactKey, null);
            }
        }

        return addedDerivedFact ? builder.Build() : facts;
    }

    private void AddSatisfiedFactKeysToCompletedStatuses(TerrariaGameFacts facts, TimeSpan elapsed)
    {
        foreach (SplitStatus status in statuses)
        {
            status.AddSatisfiedFactKeys(facts, elapsed);
        }
    }

    private SplitRecord? TryCompleteNextEligibleSplit(TerrariaGameFacts facts, TimeSpan elapsed)
    {
        int activeIndex = currentIndex;
        if (activeIndex < 0 || activeIndex >= statuses.Count)
        {
            return null;
        }

        int activeMainIndex = FindNextMainIndex(activeIndex);
        SplitRecord? attachedSplit = TryCompleteAttachedSplitInCurrentStage(activeIndex, activeMainIndex, facts, elapsed);
        if (attachedSplit is not null)
        {
            return attachedSplit;
        }

        if (activeMainIndex < 0)
        {
            return null;
        }

        for (int i = activeMainIndex; i < statuses.Count; i++)
        {
            if (statuses[i].Definition.IsAttached)
            {
                continue;
            }

            if (!initialStateResolved[i])
            {
                continue;
            }

            SplitRecord? split = statuses[i].TryComplete(facts, elapsed, i);
            if (split is null)
            {
                continue;
            }

            SkipStatusesBefore(activeIndex, i);
            return split;
        }

        return null;
    }

    private SplitRecord? TryCompleteAttachedSplitInCurrentStage(
        int activeIndex,
        int activeMainIndex,
        TerrariaGameFacts facts,
        TimeSpan elapsed)
    {
        int stageEndIndex = activeMainIndex >= 0 ? activeMainIndex : statuses.Count;
        for (int i = activeIndex; i < stageEndIndex; i++)
        {
            if (!statuses[i].Definition.IsAttached ||
                !initialStateResolved[i] ||
                !IsAttachedStageOpen(i))
            {
                continue;
            }

            SplitRecord? split = statuses[i].TryComplete(facts, elapsed, i);
            if (split is not null)
            {
                return split;
            }
        }

        return null;
    }

    private int FindNextMainIndex(int startIndex)
    {
        for (int i = startIndex; i < statuses.Count; i++)
        {
            if (!statuses[i].Definition.IsAttached)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindPreviousMainIndex(int startIndex)
    {
        for (int i = Math.Min(startIndex, statuses.Count - 1); i >= 0; i--)
        {
            if (!statuses[i].Definition.IsAttached)
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsAttachedStageOpen(int index)
    {
        if (index < 0 || index >= statuses.Count || !statuses[index].Definition.IsAttached)
        {
            return false;
        }

        for (int i = 0; i < index; i++)
        {
            SplitStatus previous = statuses[i];
            if (!previous.Definition.IsAttached && !previous.IsCompleted && !previous.IsSkipped)
            {
                return false;
            }
        }

        return true;
    }

    private void SkipStatusesBefore(int startIndex, int completedIndex)
    {
        for (int i = startIndex; i < completedIndex; i++)
        {
            SplitStatus status = statuses[i];
            if (!status.IsCompleted && !status.IsSkipped)
            {
                status.Skip();
            }
        }
    }

    private void ResolveInitialStates(TerrariaGameFacts facts)
    {
        for (int i = 0; i < statuses.Count; i++)
        {
            if (initialStateResolved[i])
            {
                continue;
            }

            SplitStatus status = statuses[i];
            if (status.IsSkipped || status.IsCompleted)
            {
                initialStateResolved[i] = true;
            }
            else if (status.Definition.IsAttached && !IsAttachedStageOpen(i))
            {
                continue;
            }
            else if (status.Definition.IsComplete(facts))
            {
                status.Skip(facts);
                initialStateResolved[i] = true;
            }
            else if (status.Definition.IsKnownIncomplete(facts))
            {
                initialStateResolved[i] = true;
            }
        }

        SkipStatusesBeforeResolvedLaterSplits();
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
}

internal readonly record struct SplitTrackerState(
    SplitStatusState[] Statuses,
    int CurrentIndex,
    IReadOnlyDictionary<int, int> MaxOwnedItemCounts);
