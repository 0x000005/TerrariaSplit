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
}
