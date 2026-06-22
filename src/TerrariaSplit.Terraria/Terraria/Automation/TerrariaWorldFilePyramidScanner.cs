using System.Drawing;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class TerrariaWorldFilePyramidScanner
{
    private const ulong ReLogicMagic = 27981915666277746UL;
    private const byte WorldFileType = 2;
    private const double CorridorLeftRatio = 0.32d;
    private const double CorridorRightRatio = 0.68d;
    private const double CorridorTopRatio = 0.15d;
    private const double CorridorBottomRatio = 0.35d;

    // Reads the world evil from the header section (true = crimson, false = corruption).
    public bool TryReadWorldEvil(string worldPath, out bool hasCrimson, out string detail)
    {
        if (TryReadWorldSeedMetadata(worldPath, out TerrariaWorldSeedMetadata metadata, out detail))
        {
            hasCrimson = metadata.HasCrimson;
            return true;
        }

        hasCrimson = false;
        return false;
    }

    public bool TryReadWorldSeedAndEvil(
        string worldPath,
        out string seedText,
        out bool hasCrimson,
        out string detail)
    {
        if (TryReadWorldSeedMetadata(worldPath, out TerrariaWorldSeedMetadata metadata, out detail))
        {
            seedText = metadata.SeedText;
            hasCrimson = metadata.HasCrimson;
            return true;
        }

        seedText = string.Empty;
        hasCrimson = false;
        return false;
    }

    public bool TryReadWorldSeedMetadata(
        string worldPath,
        out TerrariaWorldSeedMetadata metadata,
        out string detail)
    {
        metadata = default;
        detail = string.Empty;

        try
        {
            using FileStream stream = new(
                worldPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using BinaryReader reader = new(stream);

            if (!TryReadSectionPointers(reader, out int version, out int[] sectionPointers, out detail))
            {
                return false;
            }

            if (sectionPointers.Length < 1 || sectionPointers[0] <= 0 || sectionPointers[0] >= stream.Length)
            {
                detail = "invalid world header section offset";
                return false;
            }

            stream.Position = sectionPointers[0];
            metadata = ReadWorldSeedMetadata(reader, version);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException or ArgumentException or InvalidDataException)
        {
            detail = ex.Message;
            StaticAppLogger.Instance.Error(ex, $"World pool failed to read world seed metadata from Terraria world file: {worldPath}");
            return false;
        }
    }

    public bool TryScanCandidateItemChests(
        string worldPath,
        string worldSize,
        int requiredItemMask,
        out PyramidChestScanResult result,
        out Rectangle corridorBounds,
        out string detail)
    {
        result = PyramidChestScanResult.Empty;
        corridorBounds = Rectangle.Empty;
        detail = string.Empty;
        string phase = "open world file";

        try
        {
            phase = "build scan bounds";
            TerrariaWorldDimensions dimensions = TerrariaWorldDimensions.FromWorldSize(worldSize);
            corridorBounds = BuildSpeedrunCorridorBounds(dimensions);
            if (corridorBounds.Width <= 0 || corridorBounds.Height <= 0)
            {
                detail = "empty scan corridor";
                return true;
            }

            using FileStream stream = new(
                worldPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using BinaryReader reader = new(stream);

            phase = "read world file sections";
            if (!TryReadWorldFileSections(reader, out WorldFileSections sections, out detail))
            {
                return false;
            }

            if (sections.ChestDataOffset <= 0)
            {
                return true;
            }

            phase = "read chest section";
            stream.Position = sections.ChestDataOffset;
            List<WorldChestData> chests = ReadChestData(reader, sections.Version);
            if (chests.Count == 0)
            {
                return true;
            }

            int normalizedRequiredItemMask = PyramidFilterItemMatcher.ResolveRequiredMaskOrAll(requiredItemMask);
            var candidateChests = new List<PyramidChestInfo>();
            foreach (WorldChestData chest in chests)
            {
                if (!corridorBounds.IntersectsWith(new Rectangle(chest.X, chest.Y, 2, 2)))
                {
                    continue;
                }

                var chestInfo = new PyramidChestInfo(
                    chest.X,
                    chest.Y,
                    chest.Items.Select(item => new PyramidChestItem(item.Slot, item.Type, item.Stack, item.Prefix)).ToList());
                if (PyramidFilterItemMatcher.Matches(new PyramidChestScanResult([chestInfo]), normalizedRequiredItemMask))
                {
                    candidateChests.Add(chestInfo);
                }
            }

            result = new PyramidChestScanResult(candidateChests);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException or ArgumentException or InvalidDataException)
        {
            detail = $"{phase}: {ex.Message}";
            StaticAppLogger.Instance.Error(ex, $"Pyramid filter failed to scan Terraria candidate chest data: {worldPath}");
            return false;
        }
    }

    private static bool TryReadSectionPointers(BinaryReader reader, out int version, out int[] sectionPointers, out string detail)
    {
        sectionPointers = Array.Empty<int>();
        detail = string.Empty;
        version = reader.ReadInt32();
        if (version >= 135)
        {
            ulong metadata = reader.ReadUInt64();
            if ((metadata & 0x00FFFFFFFFFFFFFFUL) != ReLogicMagic)
            {
                detail = $"unexpected Terraria world metadata magic 0x{metadata:X16}";
                return false;
            }

            byte fileType = (byte)(metadata >> 56);
            if (fileType != WorldFileType)
            {
                detail = $"unexpected Terraria world file type {fileType}";
                return false;
            }

            _ = reader.ReadUInt32();
            _ = reader.ReadUInt64();
        }

        short sectionCount = reader.ReadInt16();
        if (sectionCount < 1)
        {
            detail = $"unexpected world section count {sectionCount}";
            return false;
        }

        sectionPointers = new int[sectionCount];
        for (int i = 0; i < sectionPointers.Length; i++)
        {
            sectionPointers[i] = reader.ReadInt32();
        }

        return true;
    }

    // Mirrors WorldFile.LoadHeader field order up to WorldGen.crimson, gated by the file
    // version so it self-adapts to whichever world version the file was written with.
    private static TerrariaWorldSeedMetadata ReadWorldSeedMetadata(BinaryReader reader, int version)
    {
        _ = reader.ReadString(); // world name
        string seedText = string.Empty;
        if (version >= 179)
        {
            seedText = version == 179
                ? reader.ReadInt32().ToString(System.Globalization.CultureInfo.InvariantCulture)
                : reader.ReadString();

            _ = reader.ReadUInt64(); // world generator version
        }

        if (version >= 181)
        {
            _ = reader.ReadBytes(16); // unique id
        }

        _ = reader.ReadInt32(); // world id
        _ = reader.ReadInt32(); // left world
        _ = reader.ReadInt32(); // right world
        _ = reader.ReadInt32(); // top world
        _ = reader.ReadInt32(); // bottom world
        int maxTilesY = reader.ReadInt32();
        int maxTilesX = reader.ReadInt32();

        int gameMode = 0;
        bool drunkWorld = false;
        bool forTheWorthy = false;
        bool celebration = false;
        bool dontStarve = false;
        bool notTheBees = false;
        bool remix = false;
        bool noTraps = false;
        bool zenith = false;
        bool skyblock = false;
        if (version >= 209)
        {
            gameMode = reader.ReadInt32();
            if (version >= 222) drunkWorld = reader.ReadBoolean();
            if (version >= 227) forTheWorthy = reader.ReadBoolean();
            if (version >= 238) celebration = reader.ReadBoolean();
            if (version >= 239) dontStarve = reader.ReadBoolean();
            if (version >= 241) notTheBees = reader.ReadBoolean();
            if (version >= 249) remix = reader.ReadBoolean();
            if (version >= 266) noTraps = reader.ReadBoolean();
            if (version >= 267) zenith = reader.ReadBoolean();
            if (version >= 302) skyblock = reader.ReadBoolean();
        }
        else
        {
            bool expert = false;
            bool master = false;
            if (version >= 112) expert = reader.ReadBoolean(); // legacy expert flag
            if (version == 208) master = reader.ReadBoolean(); // legacy master flag
            gameMode = master ? 2 : expert ? 1 : 0;
        }

        if (version >= 141) _ = reader.ReadInt64(); // creation time
        if (version >= 284) _ = reader.ReadInt64(); // last played

        _ = reader.ReadByte(); // moon type
        for (int i = 0; i < 19; i++)
        {
            // treeX[3], treeStyle[4], caveBackX[3], caveBackStyle[4], ice/jungle/hell back styles, spawnTileX/Y
            _ = reader.ReadInt32();
        }

        _ = reader.ReadDouble(); // world surface
        _ = reader.ReadDouble(); // rock layer
        _ = reader.ReadDouble(); // temp time
        _ = reader.ReadBoolean(); // temp day time
        _ = reader.ReadInt32(); // temp moon phase
        _ = reader.ReadBoolean(); // temp blood moon
        _ = reader.ReadBoolean(); // temp eclipse
        _ = reader.ReadInt32(); // dungeon x
        _ = reader.ReadInt32(); // dungeon y
        bool hasCrimson = reader.ReadBoolean();

        return new TerrariaWorldSeedMetadata(
            seedText,
            WorldSizeCode(maxTilesX, maxTilesY),
            gameMode + 1,
            hasCrimson,
            SpecialSeedMask(
                drunkWorld,
                notTheBees,
                forTheWorthy,
                celebration,
                dontStarve,
                remix,
                noTraps,
                zenith,
                skyblock));
    }

    private static int SpecialSeedMask(
        bool drunkWorld,
        bool notTheBees,
        bool forTheWorthy,
        bool celebration,
        bool dontStarve,
        bool remix,
        bool noTraps,
        bool zenith,
        bool skyblock)
    {
        int mask = 0;
        if (drunkWorld) mask += 1;
        if (notTheBees) mask += 2;
        if (forTheWorthy) mask += 4;
        if (celebration) mask += 8;
        if (dontStarve) mask += 16;
        if (remix) mask += 32;
        if (noTraps) mask += 64;
        if (zenith) mask += 128;
        if (skyblock) mask += 256;
        return mask;
    }

    private static int WorldSizeCode(int maxTilesX, int maxTilesY)
    {
        return (maxTilesX, maxTilesY) switch
        {
            (4200, 1200) => 1,
            (8400, 2400) => 3,
            _ => 2
        };
    }

    internal static Rectangle BuildSpeedrunCorridorBounds(TerrariaWorldDimensions dimensions)
    {
        int left = Math.Max(1, (int)Math.Floor(dimensions.Width * CorridorLeftRatio));
        int rightExclusive = Math.Min(dimensions.Width - 1, (int)Math.Ceiling(dimensions.Width * CorridorRightRatio));
        int top = Math.Max(1, (int)Math.Floor(dimensions.Height * CorridorTopRatio));
        int bottom = Math.Min(
            dimensions.Height - 2,
            Math.Max(top, (int)Math.Ceiling(dimensions.Height * CorridorBottomRatio)));
        if (rightExclusive <= left || bottom < top)
        {
            return Rectangle.Empty;
        }

        return Rectangle.FromLTRB(left, top, rightExclusive, bottom + 1);
    }

    private static bool TryReadWorldFileSections(
        BinaryReader reader,
        out WorldFileSections sections,
        out string detail)
    {
        sections = default;
        detail = string.Empty;

        if (!TryReadSectionPointers(reader, out int version, out int[] sectionPointers, out detail))
        {
            return false;
        }

        short importanceCount = reader.ReadInt16();
        if (importanceCount < 0)
        {
            detail = $"unexpected tile importance count {importanceCount}";
            return false;
        }

        int importanceByteCount = (importanceCount + 7) / 8;
        _ = reader.ReadBytes(importanceByteCount);

        int chestDataOffset = 0;
        if (sectionPointers.Length > 2 && sectionPointers[2] > 0 && sectionPointers[2] < reader.BaseStream.Length)
        {
            chestDataOffset = sectionPointers[2];
        }

        sections = new WorldFileSections(version, chestDataOffset);
        return true;
    }

    private static List<WorldChestData> ReadChestData(BinaryReader reader, int version)
    {
        int chestCount = reader.ReadInt16();
        if (chestCount < 0 || chestCount > 8000)
        {
            throw new InvalidDataException($"unexpected chest count {chestCount}");
        }

        int legacyMaxItems = version < 294 ? reader.ReadInt16() : 0;
        var chests = new List<WorldChestData>(Math.Max(0, chestCount));
        for (int chestIndex = 0; chestIndex < chestCount; chestIndex++)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            _ = reader.ReadString();
            int maxItems = version >= 294 ? reader.ReadInt32() : legacyMaxItems;
            var items = new List<WorldChestItemData>();
            for (int slot = 0; slot < maxItems; slot++)
            {
                short stack = reader.ReadInt16();
                if (stack == 0)
                {
                    continue;
                }

                int type = reader.ReadInt32();
                byte prefix = reader.ReadByte();
                items.Add(new WorldChestItemData(slot, type, stack > 0 ? stack : 1, prefix));
            }

            chests.Add(new WorldChestData(x, y, items));
        }

        return chests;
    }

    private readonly record struct WorldFileSections(int Version, int ChestDataOffset);

    private readonly record struct WorldChestData(int X, int Y, IReadOnlyList<WorldChestItemData> Items);

    private readonly record struct WorldChestItemData(int Slot, int Type, int Stack, byte Prefix);
}

internal readonly record struct TerrariaWorldDimensions(int Width, int Height)
{
    public static TerrariaWorldDimensions FromWorldSize(string? worldSize)
    {
        return AutoCreateWorldSize.Normalize(worldSize) switch
        {
            AutoCreateWorldSize.Small => new TerrariaWorldDimensions(4200, 1200),
            AutoCreateWorldSize.Large => new TerrariaWorldDimensions(8400, 2400),
            _ => new TerrariaWorldDimensions(6400, 1800)
        };
    }
}

internal readonly record struct PyramidChestScanResult(IReadOnlyList<PyramidChestInfo> Chests)
{
    public static PyramidChestScanResult Empty => new([]);

    public bool ContainsItem(int itemType)
    {
        return Chests is not null && Chests.Any(chest => chest.ContainsItem(itemType));
    }

    public string FormatSummary()
    {
        if (Chests is null || Chests.Count == 0)
        {
            return "none";
        }

        return string.Join("; ", Chests.Select(chest => chest.FormatSummary()));
    }
}

internal readonly record struct PyramidChestInfo(
    int X,
    int Y,
    IReadOnlyList<PyramidChestItem> Items)
{
    public bool ContainsItem(int itemType)
    {
        return Items is not null && Items.Any(item => item.Type == itemType && item.Stack > 0);
    }

    public string FormatSummary()
    {
        string items = Items is null || Items.Count == 0
            ? "empty"
            : string.Join(", ", Items.Select(PyramidChestItemNames.Format));
        return $"({X},{Y}): {items}";
    }
}

internal readonly record struct PyramidChestItem(int Slot, int Type, int Stack, byte Prefix);

internal static class PyramidChestItemNames
{
    public const int PharaohMask = 848;
    public const int SandstormInABottle = 857;
    public const int PharaohRobe = 866;
    public const int FlyingCarpet = 934;

    private static readonly Dictionary<int, string> KnownNames = new()
    {
        [PharaohMask] = "Pharaoh's Mask",
        [SandstormInABottle] = "Sandstorm in a Bottle",
        [PharaohRobe] = "Pharaoh's Robe",
        [FlyingCarpet] = "Flying Carpet"
    };

    public static string Format(PyramidChestItem item)
    {
        string name = KnownNames.TryGetValue(item.Type, out string? knownName)
            ? knownName
            : "#" + item.Type.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return item.Stack == 1
            ? name
            : name + " x" + item.Stack.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
