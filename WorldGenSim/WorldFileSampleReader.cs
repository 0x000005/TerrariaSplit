using System.Globalization;

namespace WorldGenSim;

internal sealed class WorldFileSampleReader
{
    private readonly string root;
    private readonly WorldFileMetadataReader metadataReader = new();

    public WorldFileSampleReader(string root)
    {
        this.root = Path.GetFullPath(root);
    }

    public bool TryRead(string worldPath, out WorldSample sample, out string detail)
    {
        sample = default;
        if (!metadataReader.TryRead(worldPath, out WorldSeedMetadata metadata, out detail))
        {
            return false;
        }

        if (!TryReadPyramidChests(worldPath, metadata, out PyramidChestSet chests, out detail))
        {
            return false;
        }

        sample = new WorldSample(ClassificationOf(worldPath), metadata, chests);
        return true;
    }

    private string ClassificationOf(string worldPath)
    {
        string fullPath = Path.GetFullPath(worldPath);
        string? relative = Path.GetRelativePath(root, fullPath);
        if (string.IsNullOrWhiteSpace(relative) || relative.StartsWith("..", StringComparison.Ordinal))
        {
            return Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty;
        }

        string[] parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (string part in parts)
        {
            if (IsKnownClassification(part))
            {
                return part;
            }
        }

        string? firstPart = parts.FirstOrDefault();
        return firstPart ?? string.Empty;
    }

    private static bool IsKnownClassification(string value)
    {
        return value is "飞毯" or "沙暴" or "其他" or "无金字塔";
    }

    private static bool TryReadPyramidChests(
        string worldPath,
        WorldSeedMetadata metadata,
        out PyramidChestSet chests,
        out string detail)
    {
        Bounds bounds = Bounds.ForSpeedrunCorridor(metadata.SizeCode);
        chests = PyramidChestSet.Empty;
        detail = string.Empty;

        try
        {
            using FileStream stream = new(
                worldPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using BinaryReader reader = new(stream);

            if (!WorldFileSectionReader.TryReadSections(reader, out int version, out int[] sectionPointers, out detail))
            {
                return false;
            }

            short importanceCount = reader.ReadInt16();
            if (importanceCount < 0)
            {
                detail = $"unexpected tile importance count {importanceCount.ToString(CultureInfo.InvariantCulture)}";
                return false;
            }

            int importanceByteCount = (importanceCount + 7) / 8;
            _ = reader.ReadBytes(importanceByteCount);

            if (sectionPointers.Length <= 2 || sectionPointers[2] <= 0 || sectionPointers[2] >= stream.Length)
            {
                return true;
            }

            stream.Position = sectionPointers[2];
            List<WorldChest> allChests = ReadChests(reader, version);
            var pyramidChests = allChests
                .Where(chest => bounds.Intersects(chest.X, chest.Y, width: 2, height: 2))
                .Select(chest => new PyramidChest(chest.X, chest.Y, chest.Items))
                .Where(chest => chest.Items.Any(PyramidChestItemNames.IsKnownPyramidItem))
                .ToList();

            chests = new PyramidChestSet(pyramidChests);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            EndOfStreamException or
            ArgumentException or
            InvalidDataException)
        {
            detail = ex.Message;
            return false;
        }
    }

    private static List<WorldChest> ReadChests(BinaryReader reader, int version)
    {
        int chestCount = reader.ReadInt16();
        if (chestCount < 0 || chestCount > 8000)
        {
            throw new InvalidDataException($"unexpected chest count {chestCount.ToString(CultureInfo.InvariantCulture)}");
        }

        int legacyMaxItems = version < 294 ? reader.ReadInt16() : 0;
        var chests = new List<WorldChest>(Math.Max(0, chestCount));
        for (int chestIndex = 0; chestIndex < chestCount; chestIndex++)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            _ = reader.ReadString();
            int maxItems = version >= 294 ? reader.ReadInt32() : legacyMaxItems;
            var items = new List<PyramidChestItem>();
            for (int slot = 0; slot < maxItems; slot++)
            {
                short stack = reader.ReadInt16();
                if (stack == 0)
                {
                    continue;
                }

                int type = reader.ReadInt32();
                byte prefix = reader.ReadByte();
                items.Add(new PyramidChestItem(slot, type, stack > 0 ? stack : 1, prefix));
            }

            chests.Add(new WorldChest(x, y, items));
        }

        return chests;
    }

    private readonly record struct WorldChest(int X, int Y, IReadOnlyList<PyramidChestItem> Items);
}

internal readonly record struct WorldSample(
    string Classification,
    WorldSeedMetadata Metadata,
    PyramidChestSet PyramidChests);

internal readonly record struct PyramidChestSet(IReadOnlyList<PyramidChest> Chests)
{
    public static PyramidChestSet Empty => new([]);

    public bool MatchesExpectedClass(PyramidTargetClass expectedClass)
    {
        return expectedClass switch
        {
            PyramidTargetClass.FlyingCarpet => ContainsItem(PyramidChestItemNames.FlyingCarpet),
            PyramidTargetClass.SandstormInABottle => ContainsItem(PyramidChestItemNames.SandstormInABottle),
            PyramidTargetClass.Other => Chests.Count > 0 &&
                !ContainsItem(PyramidChestItemNames.FlyingCarpet) &&
                !ContainsItem(PyramidChestItemNames.SandstormInABottle),
            PyramidTargetClass.None => Chests.Count == 0,
            _ => false
        };
    }

    public string FormatTargetClass()
    {
        bool hasFlyingCarpet = ContainsItem(PyramidChestItemNames.FlyingCarpet);
        bool hasSandstorm = ContainsItem(PyramidChestItemNames.SandstormInABottle);
        return (Chests.Count, hasFlyingCarpet, hasSandstorm) switch
        {
            (0, _, _) => "无金字塔",
            (_, true, true) => "飞毯+沙暴",
            (_, true, _) => "飞毯",
            (_, _, true) => "沙暴",
            _ => "其他"
        };
    }

    public string FormatLootSummary()
    {
        return Chests.Count == 0
            ? "none"
            : string.Join("; ", Chests
                .Select(static chest => chest.FormatLootSummary())
                .Order(StringComparer.Ordinal));
    }

    public string FormatSummary()
    {
        return Chests.Count == 0
            ? "none"
            : string.Join("; ", Chests.Select(static chest => chest.FormatSummary()));
    }

    private bool ContainsItem(int itemType)
    {
        return Chests.Any(chest => chest.Items.Any(item => item.Type == itemType));
    }

}

internal enum PyramidTargetClass
{
    FlyingCarpet,
    SandstormInABottle,
    Other,
    None
}

internal readonly record struct PyramidChest(
    int X,
    int Y,
    IReadOnlyList<PyramidChestItem> Items)
{
    public string FormatLootSummary()
    {
        return Items.Count == 0
            ? "empty"
            : string.Join("|", Items.Select(PyramidChestItemNames.Format));
    }

    public string FormatSummary()
    {
        return $"({X.ToString(CultureInfo.InvariantCulture)},{Y.ToString(CultureInfo.InvariantCulture)}):{FormatLootSummary()}";
    }

}

internal readonly record struct PyramidChestItem(int Slot, int Type, int Stack, byte Prefix);

internal static class PyramidChestItemNames
{
    public const int PharaohsMask = 848;
    public const int SandstormInABottle = 857;
    public const int PharaohsRobe = 866;
    public const int FlyingCarpet = 934;

    private static readonly Dictionary<int, string> KnownNames = new()
    {
        [PharaohsMask] = "Pharaoh's Mask",
        [SandstormInABottle] = "Sandstorm in a Bottle",
        [PharaohsRobe] = "Pharaoh's Robe",
        [FlyingCarpet] = "Flying Carpet"
    };

    public static string Format(PyramidChestItem item)
    {
        string name = KnownNames.TryGetValue(item.Type, out string? knownName)
            ? knownName
            : "#" + item.Type.ToString(CultureInfo.InvariantCulture);
        return item.Stack == 1
            ? name
            : name + "x" + item.Stack.ToString(CultureInfo.InvariantCulture);
    }

    public static bool IsKnownPyramidItem(PyramidChestItem item)
    {
        return KnownNames.ContainsKey(item.Type);
    }
}

internal readonly record struct Bounds(int Left, int Top, int RightExclusive, int BottomExclusive)
{
    public bool Intersects(int x, int y, int width, int height)
    {
        return x < RightExclusive &&
            x + width > Left &&
            y < BottomExclusive &&
            y + height > Top;
    }

    public static Bounds ForSpeedrunCorridor(int sizeCode)
    {
        (int width, int height) = sizeCode switch
        {
            1 => (4200, 1200),
            3 => (8400, 2400),
            _ => (6400, 1800)
        };

        int left = Math.Max(1, (int)Math.Floor(width * 0.35d));
        int rightExclusive = Math.Min(width - 1, (int)Math.Ceiling(width * 0.75d));
        int top = Math.Max(1, (int)Math.Floor(height * 0.15d));
        int bottomExclusive = Math.Min(height - 1, Math.Max(top + 1, (int)Math.Ceiling(height * 0.35d)));
        return new Bounds(left, top, rightExclusive, bottomExclusive);
    }
}
