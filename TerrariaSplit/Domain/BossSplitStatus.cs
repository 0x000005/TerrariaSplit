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

    public void SetTime(TimeSpan? time)
    {
        Time = time;
        IsSkipped = false;
    }

    public BossSplitStatusState CaptureState()
    {
        return new BossSplitStatusState(Time, IsSkipped);
    }

    public void ApplyState(BossSplitStatusState state)
    {
        Time = state.Time;
        IsSkipped = state.Time.HasValue ? false : state.IsSkipped;
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

internal readonly record struct BossSplitStatusState(TimeSpan? Time, bool IsSkipped);
