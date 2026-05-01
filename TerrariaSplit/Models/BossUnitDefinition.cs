namespace TerrariaSplit;

internal sealed record BossUnitDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<BossFlag> RequiredFlags,
    IReadOnlyList<string> IconFileNames);
