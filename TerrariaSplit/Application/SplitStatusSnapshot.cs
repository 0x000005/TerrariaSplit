namespace TerrariaSplit;

internal sealed record SplitStatusSnapshot(
    BossSplitDefinition Definition,
    TimeSpan? Time,
    bool IsSkipped)
{
    public bool IsCompleted => Time.HasValue;

    public static SplitStatusSnapshot FromStatus(BossSplitStatus status)
    {
        return new SplitStatusSnapshot(status.Definition, status.Time, status.IsSkipped);
    }

    public static SplitStatusSnapshot FromDefinition(BossSplitDefinition definition)
    {
        return new SplitStatusSnapshot(definition, null, IsSkipped: false);
    }
}
