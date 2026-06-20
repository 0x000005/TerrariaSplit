namespace TerrariaSplit;

internal sealed class BiomeFactProvider
{
    public TerrariaGameFacts Read(IProcessMemoryReader memory, TerrariaMemoryContext context)
    {
        if (context.Is64Bit ||
            context.BiomeLayout is null ||
            !TerrariaLocalPlayerResolver.TryResolve(memory, context.BiomeLayout, out IntPtr localPlayerAddress))
        {
            return TerrariaGameFacts.Unknown;
        }

        Dictionary<string, byte?> zoneValues = ReadZoneValues(memory, context.BiomeLayout, localPlayerAddress);
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        foreach (TerrariaBiomeDefinition biome in TerrariaBiomeCatalog.Items)
        {
            bool? value = TryEvaluateBiomeRule(zoneValues, biome.Rule);
            builder.SetBoolean(SplitCatalog.CreateBiomeActiveFactKey(biome.Id), value);
        }

        return builder.Build();
    }

    private static Dictionary<string, byte?> ReadZoneValues(
        IProcessMemoryReader memory,
        TerrariaBiomeMemoryLayout layout,
        IntPtr localPlayerAddress)
    {
        Dictionary<string, byte?> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string zoneFieldName in TerrariaBiomeCatalog.RequiredZoneFieldNames)
        {
            if (!layout.ZoneBitsByteFieldOffsets.TryGetValue(zoneFieldName, out int offset) ||
                !memory.TryReadBytes(IntPtr.Add(localPlayerAddress, offset), 1, out byte[]? bytes) ||
                bytes.Length == 0)
            {
                values[zoneFieldName] = null;
                continue;
            }

            values[zoneFieldName] = bytes[0];
        }

        return values;
    }

    private static bool? TryEvaluateBiomeRule(
        IReadOnlyDictionary<string, byte?> zoneValues,
        TerrariaBiomeRule rule)
    {
        bool hasUnknown = false;
        foreach (TerrariaBiomeZoneBit required in rule.Required)
        {
            bool? value = TryReadZoneBit(zoneValues, required);
            if (value == false)
            {
                return false;
            }

            hasUnknown |= value is null;
        }

        foreach (TerrariaBiomeZoneBit excluded in rule.Excluded)
        {
            bool? value = TryReadZoneBit(zoneValues, excluded);
            if (value == true)
            {
                return false;
            }

            hasUnknown |= value is null;
        }

        if (rule.AnyOf.Count > 0)
        {
            bool anyMatched = false;
            bool anyUnknown = false;
            foreach (TerrariaBiomeZoneBit anyOf in rule.AnyOf)
            {
                bool? value = TryReadZoneBit(zoneValues, anyOf);
                anyMatched |= value == true;
                anyUnknown |= value is null;
            }

            if (!anyMatched)
            {
                return anyUnknown ? null : false;
            }
        }

        return hasUnknown ? null : true;
    }

    private static bool? TryReadZoneBit(
        IReadOnlyDictionary<string, byte?> zoneValues,
        TerrariaBiomeZoneBit zoneBit)
    {
        if (!zoneValues.TryGetValue(zoneBit.ZoneFieldName, out byte? zoneValue) ||
            !zoneValue.HasValue ||
            zoneBit.BitIndex is < 0 or > 7)
        {
            return null;
        }

        return (zoneValue.Value & (1 << zoneBit.BitIndex)) != 0;
    }
}
