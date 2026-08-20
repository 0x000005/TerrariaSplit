namespace TerrariaSplit.Terraria.Memory;

internal sealed class BiomeFactProvider
{
    private Dictionary<string, byte?>? lastZoneValues;
    private bool lastReadsAll;
    private string[]? lastBiomeIds;
    private TerrariaGameFacts? lastFacts;

    public TerrariaGameFacts Read(IProcessMemoryReader memory, TerrariaMemoryContext context)
    {
        return Read(memory, context, TerrariaFactReadPlan.ReadAll);
    }

    public TerrariaGameFacts Read(
        IProcessMemoryReader memory,
        TerrariaMemoryContext context,
        TerrariaFactReadPlan readPlan)
    {
        if (context.Is64Bit ||
            !readPlan.ReadsBiomeFacts ||
            context.BiomeLayout is null ||
            !TryResolveLocalPlayer(memory, context, out IntPtr localPlayerAddress))
        {
            return TerrariaGameFacts.Unknown;
        }

        Dictionary<string, byte?> zoneValues = ReadZoneValues(memory, context.BiomeLayout, localPlayerAddress, readPlan);
        string[] selectedBiomeIds = readPlan.SelectedBiomeIds;
        if (lastZoneValues is not null &&
            lastFacts is not null &&
            SelectionEquals(readPlan, selectedBiomeIds) &&
            ZoneValuesEqual(lastZoneValues, zoneValues))
        {
            return lastFacts;
        }

        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        IEnumerable<TerrariaBiomeDefinition> biomes = readPlan.ReadsAll
            ? TerrariaBiomeCatalog.Items
            : selectedBiomeIds
                .Select(id => TerrariaBiomeCatalog.ById.TryGetValue(id, out TerrariaBiomeDefinition? biome) ? biome : null)
                .Where(biome => biome is not null)
                .Cast<TerrariaBiomeDefinition>();
        foreach (TerrariaBiomeDefinition biome in biomes)
        {
            bool? value = TryEvaluateBiomeRule(zoneValues, biome.Rule);
            builder.SetBoolean(SplitCatalog.CreateBiomeActiveFactKey(biome.Id), value);
        }

        TerrariaGameFacts facts = builder.Build();
        lastZoneValues = new Dictionary<string, byte?>(zoneValues, StringComparer.OrdinalIgnoreCase);
        lastReadsAll = readPlan.ReadsAll;
        lastBiomeIds = selectedBiomeIds;
        lastFacts = facts;
        return facts;
    }

    private static bool TryResolveLocalPlayer(
        IProcessMemoryReader memory,
        TerrariaMemoryContext context,
        out IntPtr localPlayerAddress)
    {
        localPlayerAddress = context.LocalPlayerAddress;
        return localPlayerAddress != IntPtr.Zero ||
            (context.BiomeLayout is not null &&
                TerrariaLocalPlayerResolver.TryResolve(memory, context.BiomeLayout, out localPlayerAddress));
    }

    private static Dictionary<string, byte?> ReadZoneValues(
        IProcessMemoryReader memory,
        TerrariaBiomeMemoryLayout layout,
        IntPtr localPlayerAddress,
        TerrariaFactReadPlan readPlan)
    {
        Dictionary<string, byte?> values = new(StringComparer.OrdinalIgnoreCase);
        string[] zoneFieldNames = GetRequiredZoneFieldNames(readPlan).ToArray();
        var resolvedOffsets = new List<(string FieldName, int Offset)>(zoneFieldNames.Length);
        foreach (string zoneFieldName in zoneFieldNames)
        {
            if (!layout.ZoneBitsByteFieldOffsets.TryGetValue(zoneFieldName, out int offset))
            {
                values[zoneFieldName] = null;
                continue;
            }

            resolvedOffsets.Add((zoneFieldName, offset));
        }

        if (resolvedOffsets.Count == 0)
        {
            return values;
        }

        int firstOffset = resolvedOffsets.Min(field => field.Offset);
        int lastOffset = resolvedOffsets.Max(field => field.Offset);
        long byteCountValue = (long)lastOffset - firstOffset + 1;
        if (byteCountValue is <= 0 or > 256)
        {
            foreach ((string fieldName, _) in resolvedOffsets)
            {
                values[fieldName] = null;
            }

            return values;
        }

        int byteCount = (int)byteCountValue;
        if (!memory.TryReadBytes(IntPtr.Add(localPlayerAddress, firstOffset), byteCount, out byte[]? bytes) ||
            bytes.Length < byteCount)
        {
            foreach ((string fieldName, _) in resolvedOffsets)
            {
                values[fieldName] = null;
            }

            return values;
        }

        foreach ((string fieldName, int offset) in resolvedOffsets)
        {
            values[fieldName] = bytes[offset - firstOffset];
        }

        return values;
    }

    private static IEnumerable<string> GetRequiredZoneFieldNames(TerrariaFactReadPlan readPlan)
    {
        if (readPlan.ReadsAll)
        {
            return TerrariaBiomeCatalog.RequiredZoneFieldNames;
        }

        return readPlan.BiomeIds
            .Select(id => TerrariaBiomeCatalog.ById.TryGetValue(id, out TerrariaBiomeDefinition? biome) ? biome : null)
            .Where(biome => biome is not null)
            .Cast<TerrariaBiomeDefinition>()
            .SelectMany(biome => biome.Rule.ZoneBits)
            .Select(bit => bit.ZoneFieldName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static bool ZoneValuesEqual(
        IReadOnlyDictionary<string, byte?> left,
        IReadOnlyDictionary<string, byte?> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach ((string key, byte? value) in left)
        {
            if (!right.TryGetValue(key, out byte? otherValue) || otherValue != value)
            {
                return false;
            }
        }

        return true;
    }

    private bool SelectionEquals(TerrariaFactReadPlan readPlan, IReadOnlyList<string> selectedBiomeIds)
    {
        if (lastReadsAll != readPlan.ReadsAll)
        {
            return false;
        }

        return readPlan.ReadsAll ||
            (lastBiomeIds is not null && lastBiomeIds.SequenceEqual(selectedBiomeIds, StringComparer.OrdinalIgnoreCase));
    }

}
