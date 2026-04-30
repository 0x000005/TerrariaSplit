namespace TerrariaSplit;

internal sealed class BossSplitStatus
{
    public BossSplitStatus(BossSplitDefinition definition)
    {
        Definition = definition;
    }

    public BossSplitDefinition Definition { get; }
    public TimeSpan? Time { get; private set; }
    public bool IsSkipped { get; private set; }

    public bool IsCompleted => Time.HasValue;

    public void Reset()
    {
        Time = null;
        IsSkipped = false;
    }

    public void Skip()
    {
        IsSkipped = true;
    }

    public BossSplitRecord? TryComplete(TerrariaBossStates states, TimeSpan elapsed)
    {
        if (IsSkipped || IsCompleted || !Definition.IsComplete(states))
        {
            return null;
        }

        Time = elapsed;
        return new BossSplitRecord(Definition.Name, elapsed);
    }
}
