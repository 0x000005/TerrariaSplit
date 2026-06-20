namespace TerrariaSplit;

internal enum FactValueKind
{
    Unknown,
    Boolean,
    Integer
}

internal readonly record struct FactValue(FactValueKind Kind, bool BooleanValue, int IntegerValue)
{
    public static FactValue Unknown => new(FactValueKind.Unknown, false, 0);

    public static FactValue FromBoolean(bool value)
    {
        return new FactValue(FactValueKind.Boolean, value, value ? 1 : 0);
    }

    public static FactValue FromInteger(int value)
    {
        return new FactValue(FactValueKind.Integer, false, value);
    }

    public bool? AsBoolean()
    {
        return Kind switch
        {
            FactValueKind.Boolean => BooleanValue,
            FactValueKind.Integer => IntegerValue != 0,
            _ => null
        };
    }

    public int? AsInteger()
    {
        return Kind switch
        {
            FactValueKind.Boolean => BooleanValue ? 1 : 0,
            FactValueKind.Integer => IntegerValue,
            _ => null
        };
    }
}

internal sealed class TerrariaGameFacts : IEquatable<TerrariaGameFacts>
{
    private readonly Dictionary<string, FactValue> values;

    public TerrariaGameFacts()
        : this(new Dictionary<string, FactValue>(StringComparer.OrdinalIgnoreCase))
    {
    }

    public TerrariaGameFacts(Dictionary<string, FactValue> values)
    {
        this.values = new Dictionary<string, FactValue>(values, StringComparer.OrdinalIgnoreCase);
        StatusHash = ComputeHash(this.values);
    }

    public static TerrariaGameFacts Unknown { get; } = new();

    public IReadOnlyDictionary<string, FactValue> Values => values;

    public int StatusHash { get; }

    public FactValue Get(string factKey)
    {
        return !string.IsNullOrWhiteSpace(factKey) && values.TryGetValue(factKey, out FactValue value)
            ? value
            : FactValue.Unknown;
    }

    public static Builder CreateBuilder()
    {
        return new Builder();
    }

    public bool Equals(TerrariaGameFacts? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null ||
            StatusHash != other.StatusHash ||
            values.Count != other.values.Count)
        {
            return false;
        }

        foreach ((string key, FactValue value) in values)
        {
            if (!other.values.TryGetValue(key, out FactValue otherValue) || otherValue != value)
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is TerrariaGameFacts other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StatusHash;
    }

    private static int ComputeHash(Dictionary<string, FactValue> values)
    {
        var hash = new HashCode();
        foreach ((string key, FactValue value) in values.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            hash.Add(key, StringComparer.OrdinalIgnoreCase);
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    internal sealed class Builder
    {
        private readonly Dictionary<string, FactValue> values = new(StringComparer.OrdinalIgnoreCase);

        public void Set(string factKey, FactValue value)
        {
            if (!string.IsNullOrWhiteSpace(factKey))
            {
                values[factKey] = value;
            }
        }

        public void SetBoolean(string factKey, bool? value)
        {
            Set(factKey, value.HasValue ? FactValue.FromBoolean(value.Value) : FactValue.Unknown);
        }

        public void SetInteger(string factKey, int? value)
        {
            Set(factKey, value.HasValue ? FactValue.FromInteger(value.Value) : FactValue.Unknown);
        }

        public void Merge(TerrariaGameFacts facts)
        {
            foreach ((string key, FactValue value) in facts.Values)
            {
                values[key] = value;
            }
        }

        public TerrariaGameFacts Build()
        {
            return new TerrariaGameFacts(values);
        }
    }
}
