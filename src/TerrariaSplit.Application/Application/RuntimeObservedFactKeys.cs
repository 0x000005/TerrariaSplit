namespace TerrariaSplit.Application;

internal static class RuntimeObservedFactKeys
{
    public static IReadOnlySet<string> FromDefinitions(IReadOnlyList<SplitDefinition> definitions)
    {
        var factKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SplitDefinition definition in definitions)
        {
            AddConditionFactKeys(factKeys, definition.Condition);

            foreach (SplitCondition condition in definition.IconLightingConditions)
            {
                AddConditionFactKeys(factKeys, condition);
            }

            foreach (string targetId in definition.TargetIds)
            {
                if (SplitCatalog.TryGetTarget(targetId, out SplitTargetDefinition target))
                {
                    AddFactKey(factKeys, target.FactKey);
                }
            }
        }

        return factKeys;
    }

    private static void AddConditionFactKeys(HashSet<string> factKeys, SplitCondition condition)
    {
        foreach (string factKey in condition.GetFactKeys())
        {
            AddFactKey(factKeys, factKey);
        }
    }

    private static void AddFactKey(HashSet<string> factKeys, string factKey)
    {
        if (!string.IsNullOrWhiteSpace(factKey))
        {
            factKeys.Add(factKey.Trim());
        }
    }
}
