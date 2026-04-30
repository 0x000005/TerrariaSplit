namespace TerrariaSplit;

internal sealed record BossSplitDefinition(
    BossSplitName Name,
    string DisplayName,
    IReadOnlyList<BossFlag> RequiredFlags)
{
    public bool IsComplete(TerrariaBossStates states)
    {
        return RequiredFlags.All(flag => states.Get(flag) == true);
    }

    public bool IsKnownIncomplete(TerrariaBossStates states)
    {
        return RequiredFlags.Any(flag => states.Get(flag) == false);
    }
}
