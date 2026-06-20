namespace TerrariaSplit;

internal enum SplitConditionResult
{
    Unknown,
    False,
    True
}

internal static class SplitConditionKind
{
    public const string All = "All";
    public const string Any = "Any";
    public const string AtLeast = "AtLeast";
    public const string Fact = "Fact";

    public static string Normalize(string? value)
    {
        return value switch
        {
            All or Any or AtLeast or Fact => value,
            _ when string.Equals(value, All, StringComparison.OrdinalIgnoreCase) => All,
            _ when string.Equals(value, Any, StringComparison.OrdinalIgnoreCase) => Any,
            _ when string.Equals(value, AtLeast, StringComparison.OrdinalIgnoreCase) => AtLeast,
            _ when string.Equals(value, Fact, StringComparison.OrdinalIgnoreCase) => Fact,
            _ => Fact
        };
    }

    public static string NormalizeGroup(string? value)
    {
        if (string.Equals(value, Any, StringComparison.OrdinalIgnoreCase))
        {
            return Any;
        }

        return string.Equals(value, AtLeast, StringComparison.OrdinalIgnoreCase)
            ? AtLeast
            : All;
    }
}

internal static class SplitFactComparison
{
    public const string IsTrue = "IsTrue";
    public const string IsFalse = "IsFalse";
    public const string AtLeast = "AtLeast";
    public const string Equal = "Equal";

    public static string Normalize(string? value)
    {
        return value switch
        {
            IsTrue or IsFalse or AtLeast or Equal => value,
            _ when string.Equals(value, IsTrue, StringComparison.OrdinalIgnoreCase) => IsTrue,
            _ when string.Equals(value, IsFalse, StringComparison.OrdinalIgnoreCase) => IsFalse,
            _ when string.Equals(value, AtLeast, StringComparison.OrdinalIgnoreCase) => AtLeast,
            _ when string.Equals(value, Equal, StringComparison.OrdinalIgnoreCase) => Equal,
            _ when string.Equals(value, "Equals", StringComparison.OrdinalIgnoreCase) => Equal,
            _ => IsTrue
        };
    }
}

internal sealed class SplitCondition
{
    public string Kind { get; set; } = SplitConditionKind.Fact;

    public List<SplitCondition> Children { get; set; } = new();

    public string FactKey { get; set; } = string.Empty;

    public string Comparison { get; set; } = SplitFactComparison.IsTrue;

    public int Value { get; set; } = 1;

    public static SplitCondition Fact(string factKey, string comparison = SplitFactComparison.IsTrue, int value = 1)
    {
        return new SplitCondition
        {
            Kind = SplitConditionKind.Fact,
            FactKey = factKey,
            Comparison = comparison,
            Value = value
        };
    }

    public static SplitCondition All(IEnumerable<SplitCondition> children)
    {
        return new SplitCondition
        {
            Kind = SplitConditionKind.All,
            Children = children.ToList()
        };
    }

    public static SplitCondition Any(IEnumerable<SplitCondition> children)
    {
        return new SplitCondition
        {
            Kind = SplitConditionKind.Any,
            Children = children.ToList()
        };
    }

    public static SplitCondition AtLeast(IEnumerable<SplitCondition> children, int requiredCount)
    {
        return new SplitCondition
        {
            Kind = SplitConditionKind.AtLeast,
            Children = children.ToList(),
            Value = requiredCount
        };
    }

    public SplitCondition Clone()
    {
        return new SplitCondition
        {
            Kind = Kind,
            Children = Children.Select(child => child.Clone()).ToList(),
            FactKey = FactKey,
            Comparison = Comparison,
            Value = Value
        };
    }

    public SplitConditionResult Evaluate(TerrariaGameFacts facts)
    {
        return SplitConditionKind.Normalize(Kind) switch
        {
            SplitConditionKind.All => EvaluateAll(facts),
            SplitConditionKind.Any => EvaluateAny(facts),
            SplitConditionKind.AtLeast => EvaluateAtLeast(facts),
            _ => EvaluateFact(facts)
        };
    }

    public IEnumerable<string> GetFactKeys()
    {
        foreach (SplitCondition fact in GetFactConditions())
        {
            yield return fact.FactKey;
        }
    }

    public IReadOnlyList<string> GetMatchedFactKeys(TerrariaGameFacts facts)
    {
        return SplitConditionKind.Normalize(Kind) switch
        {
            SplitConditionKind.All => GetAllMatchedFactKeys(facts),
            SplitConditionKind.Any => GetAnyMatchedFactKeys(facts),
            SplitConditionKind.AtLeast => GetAtLeastMatchedFactKeys(facts),
            _ => EvaluateFact(facts) == SplitConditionResult.True ? [FactKey] : []
        };
    }

    public IReadOnlyList<string> GetSatisfiedFactKeys(TerrariaGameFacts facts)
    {
        return GetFactConditions()
            .Where(condition => condition.Evaluate(facts) == SplitConditionResult.True)
            .Select(condition => condition.FactKey)
            .ToArray();
    }

    public void Normalize()
    {
        Kind = SplitConditionKind.Normalize(Kind);
        Comparison = SplitFactComparison.Normalize(Comparison);
        FactKey = FactKey?.Trim() ?? string.Empty;
        Children ??= new List<SplitCondition>();
        foreach (SplitCondition child in Children)
        {
            child.Normalize();
        }
    }

    public SplitCondition ToFlatGroup()
    {
        List<SplitCondition> facts = GetFactConditions().ToList();
        int requiredCount = GetRequiredCount(SplitConditionKind.Normalize(Kind), facts.Count, Value);
        return FlatGroup(SplitConditionKind.AtLeast, facts, requiredCount);
    }

    public static SplitCondition FlatGroup(string groupKind, IEnumerable<SplitCondition> facts, int requiredCount = 1)
    {
        List<SplitCondition> flatFacts = facts
            .Select(CloneFact)
            .Where(fact => !string.IsNullOrWhiteSpace(fact.FactKey))
            .ToList();
        var condition = new SplitCondition
        {
            Kind = SplitConditionKind.AtLeast,
            Children = flatFacts,
            Value = GetRequiredCount(SplitConditionKind.NormalizeGroup(groupKind), flatFacts.Count, requiredCount)
        };
        condition.Normalize();
        return condition;
    }

    public int GetRequiredCount()
    {
        return GetRequiredCount(SplitConditionKind.Normalize(Kind), GetFactConditions().Count(), Value);
    }

    public IEnumerable<SplitCondition> GetFactConditions()
    {
        string kind = SplitConditionKind.Normalize(Kind);
        bool hasChildren = Children is { Count: > 0 };
        if (kind == SplitConditionKind.Fact && !hasChildren)
        {
            SplitCondition fact = CloneFact(this);
            if (!string.IsNullOrWhiteSpace(fact.FactKey))
            {
                yield return fact;
            }

            yield break;
        }

        foreach (SplitCondition child in Children ?? [])
        {
            foreach (SplitCondition fact in child.GetFactConditions())
            {
                yield return fact;
            }
        }
    }

    private static SplitCondition CloneFact(SplitCondition condition)
    {
        return Fact(condition.FactKey, condition.Comparison, condition.Value);
    }

    private static int GetRequiredCount(string kind, int factCount, int value)
    {
        if (factCount <= 0)
        {
            return Math.Max(1, value);
        }

        return kind switch
        {
            SplitConditionKind.Any => 1,
            SplitConditionKind.AtLeast => value,
            SplitConditionKind.Fact => 1,
            _ => factCount
        };
    }

    private SplitConditionResult EvaluateAll(TerrariaGameFacts facts)
    {
        if (Children.Count == 0)
        {
            return SplitConditionResult.False;
        }

        bool hasUnknown = false;
        foreach (SplitCondition child in Children)
        {
            SplitConditionResult result = child.Evaluate(facts);
            if (result == SplitConditionResult.False)
            {
                return SplitConditionResult.False;
            }

            hasUnknown |= result == SplitConditionResult.Unknown;
        }

        return hasUnknown ? SplitConditionResult.Unknown : SplitConditionResult.True;
    }

    private IReadOnlyList<string> GetAllMatchedFactKeys(TerrariaGameFacts facts)
    {
        if (Children.Count == 0)
        {
            return [];
        }

        var keys = new List<string>();
        foreach (SplitCondition child in Children)
        {
            if (child.Evaluate(facts) != SplitConditionResult.True)
            {
                return [];
            }

            keys.AddRange(child.GetMatchedFactKeys(facts));
        }

        return keys;
    }

    private SplitConditionResult EvaluateAny(TerrariaGameFacts facts)
    {
        if (Children.Count == 0)
        {
            return SplitConditionResult.False;
        }

        bool hasUnknown = false;
        foreach (SplitCondition child in Children)
        {
            SplitConditionResult result = child.Evaluate(facts);
            if (result == SplitConditionResult.True)
            {
                return SplitConditionResult.True;
            }

            hasUnknown |= result == SplitConditionResult.Unknown;
        }

        return hasUnknown ? SplitConditionResult.Unknown : SplitConditionResult.False;
    }

    private IReadOnlyList<string> GetAnyMatchedFactKeys(TerrariaGameFacts facts)
    {
        var keys = new List<string>();
        foreach (SplitCondition child in Children)
        {
            if (child.Evaluate(facts) == SplitConditionResult.True)
            {
                keys.AddRange(child.GetMatchedFactKeys(facts));
            }
        }

        return keys;
    }

    private SplitConditionResult EvaluateAtLeast(TerrariaGameFacts facts)
    {
        if (Children.Count == 0)
        {
            return SplitConditionResult.False;
        }

        int requiredCount = Math.Max(1, Value);
        int trueCount = 0;
        int unknownCount = 0;
        foreach (SplitCondition child in Children)
        {
            SplitConditionResult result = child.Evaluate(facts);
            if (result == SplitConditionResult.True)
            {
                trueCount++;
                if (trueCount >= requiredCount)
                {
                    return SplitConditionResult.True;
                }

                continue;
            }

            if (result == SplitConditionResult.Unknown)
            {
                unknownCount++;
            }
        }

        return trueCount + unknownCount >= requiredCount
            ? SplitConditionResult.Unknown
            : SplitConditionResult.False;
    }

    private IReadOnlyList<string> GetAtLeastMatchedFactKeys(TerrariaGameFacts facts)
    {
        if (EvaluateAtLeast(facts) != SplitConditionResult.True)
        {
            return [];
        }

        var keys = new List<string>();
        foreach (SplitCondition child in Children)
        {
            if (child.Evaluate(facts) == SplitConditionResult.True)
            {
                keys.AddRange(child.GetMatchedFactKeys(facts));
            }
        }

        return keys;
    }

    private SplitConditionResult EvaluateFact(TerrariaGameFacts facts)
    {
        FactValue value = facts.Get(FactKey);
        if (value.Kind == FactValueKind.Unknown)
        {
            return SplitConditionResult.Unknown;
        }

        bool matched = SplitFactComparison.Normalize(Comparison) switch
        {
            SplitFactComparison.IsFalse => value.Kind == FactValueKind.Boolean && !value.BooleanValue,
            SplitFactComparison.AtLeast => value.IntegerValue >= Value,
            SplitFactComparison.Equal => value.IntegerValue == Value,
            _ => value.Kind == FactValueKind.Boolean
                ? value.BooleanValue
                : value.IntegerValue != 0
        };

        return matched ? SplitConditionResult.True : SplitConditionResult.False;
    }
}
