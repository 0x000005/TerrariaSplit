namespace TerrariaSplit;

internal sealed record BossSplitDefinition(
    string Name,
    string DisplayName,
    IReadOnlyList<BossFlag> RequiredFlags,
    IReadOnlyList<string> IconFileNames,
    IReadOnlyList<string> IconKeys,
    IReadOnlyList<string> BossIds)
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
