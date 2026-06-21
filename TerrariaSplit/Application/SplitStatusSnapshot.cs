namespace TerrariaSplit.Application;

internal sealed record SplitStatusSnapshot(
    SplitDefinition Definition,
    TimeSpan? Time,
    bool IsSkipped,
    IReadOnlyList<string> CompletedFactKeys,
    IReadOnlyDictionary<string, TimeSpan>? FactCompletionTimes = null)
{
    public bool IsCompleted => Time.HasValue;

    public bool TryGetFactCompletionTime(string factKey, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        return !string.IsNullOrWhiteSpace(factKey) &&
            FactCompletionTimes is not null &&
            FactCompletionTimes.TryGetValue(factKey, out time);
    }

    public static SplitStatusSnapshot FromStatus(SplitStatus status)
    {
        return new SplitStatusSnapshot(
            status.Definition,
            status.Time,
            status.IsSkipped,
            status.CompletedFactKeys.ToArray(),
            new Dictionary<string, TimeSpan>(status.FactCompletionTimes, StringComparer.OrdinalIgnoreCase));
    }

    public static SplitStatusSnapshot FromDefinition(SplitDefinition definition)
    {
        return new SplitStatusSnapshot(definition, null, IsSkipped: false, CompletedFactKeys: []);
    }
}
