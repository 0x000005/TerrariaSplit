namespace TerrariaSplit;

internal sealed record SplitDefinition(
    string Id,
    string DisplayName,
    SplitCondition Condition,
    IReadOnlyList<string> IconFileNames,
    IReadOnlyList<string> IconKeys,
    IReadOnlyList<string> TargetIds,
    bool IsAttached = false)
{
    public IReadOnlyList<SplitCondition> IconLightingConditions { get; init; } = [];

    public bool IsComplete(TerrariaGameFacts facts)
    {
        return Condition.Evaluate(facts) == SplitConditionResult.True;
    }

    public bool IsKnownIncomplete(TerrariaGameFacts facts)
    {
        return Condition.Evaluate(facts) == SplitConditionResult.False;
    }

    public IReadOnlyList<string> GetMatchedFactKeys(TerrariaGameFacts facts)
    {
        return Condition.GetMatchedFactKeys(facts);
    }

    public IReadOnlyList<string> GetSatisfiedFactKeys(TerrariaGameFacts facts)
    {
        return Condition.GetSatisfiedFactKeys(facts);
    }

    public bool ContainsTarget(string targetId)
    {
        return TargetIds.Any(id => string.Equals(id, targetId, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed record SplitTargetDefinition(
    string Id,
    string Kind,
    string DisplayName,
    string FactKey,
    string IconFileName);

internal static class SplitTargetKind
{
    public const string Boss = "Boss";
    public const string Item = "Item";
    public const string Npc = "NPC";
    public const string Biome = "Biome";
}

internal static class SplitIconOverrideSource
{
    public const string All = "All";
    public const string Target = "Target";
    public const string CustomFile = "CustomFile";

    public static string Normalize(string? value)
    {
        return value switch
        {
            All or Target or CustomFile => value,
            _ when string.Equals(value, Target, StringComparison.OrdinalIgnoreCase) => Target,
            _ when string.Equals(value, CustomFile, StringComparison.OrdinalIgnoreCase) => CustomFile,
            _ when string.Equals(value, "None", StringComparison.OrdinalIgnoreCase) => All,
            _ => All
        };
    }
}

internal sealed class SplitIconOverride
{
    public string Source { get; set; } = SplitIconOverrideSource.All;

    public string TargetId { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;
}

internal sealed class SplitRouteEntry
{
    public string Id { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public string DisplayName { get; set; } = string.Empty;

    public SplitCondition Condition { get; set; } = SplitCondition.Fact(string.Empty);

    public List<string> IconTargetIds { get; set; } = new();

    public SplitIconOverride IconOverride { get; set; } = new();

    public bool IsAttached { get; set; }

    public bool UseAdvancedConditionEditor { get; set; }

    [System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ExpandDetails { get; set; }
}

internal readonly record struct SplitRecord(int Index, string Name, TimeSpan Time);
