namespace TerrariaSplit.Domain;

internal sealed class SplitStatus
{
    public SplitStatus(SplitDefinition definition)
    {
        Definition = definition;
    }

    public SplitDefinition Definition { get; }

    public TimeSpan? Time { get; private set; }

    public bool IsSkipped { get; private set; }

    public IReadOnlyList<string> CompletedFactKeys { get; private set; } = [];

    public IReadOnlyDictionary<string, TimeSpan> FactCompletionTimes => factCompletionTimes;

    public bool IsCompleted => Time.HasValue;

    private readonly Dictionary<string, TimeSpan> factCompletionTimes = new(StringComparer.OrdinalIgnoreCase);

    public void Reset()
    {
        Time = null;
        IsSkipped = false;
        CompletedFactKeys = [];
        factCompletionTimes.Clear();
    }

    public void Skip()
    {
        Skip([]);
    }

    public void Skip(TerrariaGameFacts facts)
    {
        Skip(Definition.GetSatisfiedFactKeys(facts));
    }

    private void Skip(IReadOnlyList<string> completedFactKeys)
    {
        IsSkipped = true;
        CompletedFactKeys = completedFactKeys.ToArray();
        factCompletionTimes.Clear();
    }

    public void SetTime(TimeSpan? time)
    {
        Time = time;
        IsSkipped = false;
        CompletedFactKeys = [];
        factCompletionTimes.Clear();
    }

    public SplitStatusState CaptureState()
    {
        return new SplitStatusState(
            Time,
            IsSkipped,
            CompletedFactKeys.ToArray(),
            new Dictionary<string, TimeSpan>(factCompletionTimes, StringComparer.OrdinalIgnoreCase));
    }

    public SplitStatus CreateRenderCopy()
    {
        var copy = new SplitStatus(Definition);
        copy.ApplyState(CaptureState());
        return copy;
    }

    public void ApplyState(SplitStatusState state)
    {
        Time = state.Time;
        IsSkipped = state.Time.HasValue ? false : state.IsSkipped;
        CompletedFactKeys = state.CompletedFactKeys?.ToArray() ?? [];
        factCompletionTimes.Clear();
        foreach ((string factKey, TimeSpan time) in state.FactCompletionTimes ?? new Dictionary<string, TimeSpan>())
        {
            if (!string.IsNullOrWhiteSpace(factKey))
            {
                factCompletionTimes[factKey] = time;
            }
        }
    }

    public void AddSatisfiedFactKeys(TerrariaGameFacts facts, TimeSpan elapsed)
    {
        if (!IsCompleted && !(IsSkipped && CompletedFactKeys.Count > 0))
        {
            return;
        }

        IReadOnlyList<string> satisfiedFactKeys = Definition.GetSatisfiedFactKeys(facts);
        if (satisfiedFactKeys.Count == 0)
        {
            return;
        }

        MergeCompletedFactKeys(satisfiedFactKeys);

        foreach (string factKey in satisfiedFactKeys)
        {
            if (!string.IsNullOrWhiteSpace(factKey))
            {
                factCompletionTimes.TryAdd(factKey, elapsed);
            }
        }
    }

    public SplitRecord? TryComplete(TerrariaGameFacts facts, TimeSpan elapsed, int index)
    {
        if (IsSkipped || IsCompleted)
        {
            return null;
        }

        TrackSatisfiedFactTimes(facts, elapsed);
        if (!Definition.IsComplete(facts))
        {
            return null;
        }

        Time = elapsed;
        MergeCompletedFactKeys(factCompletionTimes.Keys.Concat(Definition.GetMatchedFactKeys(facts)));
        return new SplitRecord(index, Definition.Id, elapsed);
    }

    private void TrackSatisfiedFactTimes(TerrariaGameFacts facts, TimeSpan elapsed)
    {
        foreach (string factKey in Definition.GetSatisfiedFactKeys(facts))
        {
            if (!string.IsNullOrWhiteSpace(factKey))
            {
                factCompletionTimes.TryAdd(factKey, elapsed);
            }
        }
    }

    private void MergeCompletedFactKeys(IEnumerable<string> factKeys)
    {
        string[] merged = CompletedFactKeys
            .Concat(factKeys)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (merged.Length != CompletedFactKeys.Count ||
            !merged.SequenceEqual(CompletedFactKeys, StringComparer.OrdinalIgnoreCase))
        {
            CompletedFactKeys = merged;
        }
    }
}

internal readonly record struct SplitStatusState(
    TimeSpan? Time,
    bool IsSkipped,
    IReadOnlyList<string>? CompletedFactKeys,
    IReadOnlyDictionary<string, TimeSpan>? FactCompletionTimes = null);
