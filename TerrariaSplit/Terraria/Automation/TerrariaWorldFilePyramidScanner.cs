using System.Drawing;
namespace TerrariaSplit;

internal sealed class TerrariaWorldFilePyramidScanner
{
    private const ulong ReLogicMagic = 27981915666277746UL;
    private const byte WorldFileType = 2;
    private const int BasicChestTileType = 21;
    private const int PyramidChestStyle = 1;
    private const int PyramidWallType = 34;
    private const int PyramidTileType = 151;
    private const double CorridorHalfWidthRatio = 0.20d;
    private const double CorridorTopRatio = 0.15d;
    private const double CorridorBottomRatio = 0.35d;

    public bool TryScanSpeedrunCorridor(
        string worldPath,
        string worldSize,
        int wallThreshold,
        int tileThreshold,
        out PyramidEvidenceScanResult result,
        out Rectangle corridorBounds,
        out string detail)
    {
        result = new PyramidEvidenceScanResult(-1, -1);
        corridorBounds = Rectangle.Empty;
        detail = string.Empty;

        try
        {
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

            if (!TryReadWorldFileHeader(reader, out WorldFileTileSection tileSection, out detail))
            {
                return false;
            }

            stream.Position = tileSection.TileDataOffset;
            result = ScanTileData(
                reader,
                dimensions.Width,
                dimensions.Height,
                corridorBounds,
                tileSection.TileFrameImportant,
                wallThreshold,
                tileThreshold);
            if (!result.MeetsThreshold(wallThreshold, tileThreshold) &&
                tileSection.TileDataEndOffset > tileSection.TileDataOffset &&
                stream.Position != tileSection.TileDataEndOffset)
            {
                detail = $"tile section parse ended at {stream.Position}, expected {tileSection.TileDataEndOffset}";
                result = new PyramidEvidenceScanResult(-1, -1);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException or ArgumentException or InvalidDataException)
        {
            detail = ex.Message;
            AppLogger.Error(ex, $"Pyramid filter failed to scan Terraria world file: {worldPath}");
            return false;
        }
    }

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
            AppLogger.Error(ex, $"World pool failed to read world seed metadata from Terraria world file: {worldPath}");
            return false;
        }
    }

    public bool TryScanPyramidChests(
        string worldPath,
        string worldSize,
        out PyramidChestScanResult result,
        out string detail)
    {
        result = PyramidChestScanResult.Empty;
        detail = string.Empty;
        string phase = "open world file";

        try
        {
            phase = "build scan bounds";
            TerrariaWorldDimensions dimensions = TerrariaWorldDimensions.FromWorldSize(worldSize);
            Rectangle corridorBounds = BuildSpeedrunCorridorBounds(dimensions);
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

            phase = "read world file header";
            if (!TryReadWorldFileHeader(reader, out WorldFileTileSection tileSection, out detail))
            {
                return false;
            }

            if (tileSection.ChestDataOffset <= 0)
            {
                return true;
            }

            phase = "read chest section";
            stream.Position = tileSection.ChestDataOffset;
            List<WorldChestData> chests = ReadChestData(reader, tileSection.Version);
            if (chests.Count == 0)
            {
                return true;
            }

            Dictionary<Point, WorldChestData> targetChests = chests
                .Where(chest => corridorBounds.IntersectsWith(new Rectangle(chest.X, chest.Y, 2, 2)))
                .ToDictionary(chest => new Point(chest.X, chest.Y));
            if (targetChests.Count == 0)
            {
                return true;
            }

            phase = "read chest tiles";
            stream.Position = tileSection.TileDataOffset;
            Dictionary<Point, WorldTileData> chestTiles = ReadTargetTiles(
                reader,
                dimensions.Width,
                dimensions.Height,
                tileSection.TileFrameImportant,
                targetChests.Keys.ToHashSet());

            var pyramidChests = new List<PyramidChestInfo>();
            foreach ((Point point, WorldChestData chest) in targetChests)
            {
                chestTiles.TryGetValue(point, out WorldTileData tile);
                int chestStyle = tile.Active && tile.Type == BasicChestTileType ? tile.FrameX / 36 : -1;
                bool pyramidStyle = chestStyle == PyramidChestStyle;
                bool pyramidLoot = chest.Items.Any(item => PyramidChestItemNames.IsPyramidItem(item.Type));
                if (!pyramidStyle && !pyramidLoot)
                {
                    continue;
                }

                pyramidChests.Add(new PyramidChestInfo(
                    chest.X,
                    chest.Y,
                    chestStyle,
                    chest.Items.Select(item => new PyramidChestItem(item.Slot, item.Type, item.Stack, item.Prefix)).ToList()));
            }

            result = new PyramidChestScanResult(pyramidChests);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException or ArgumentException or InvalidDataException)
        {
            detail = $"{phase}: {ex.Message}";
            AppLogger.Error(ex, $"Pyramid filter failed to scan Terraria chest data: {worldPath}");
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
        int centerTileX = dimensions.Width / 2;
        int halfWidth = (int)Math.Round(dimensions.Width * CorridorHalfWidthRatio);
        int left = Math.Max(1, centerTileX - halfWidth);
        int right = Math.Min(dimensions.Width - 2, centerTileX + halfWidth);
        int top = Math.Max(1, (int)Math.Floor(dimensions.Height * CorridorTopRatio));
        int bottom = Math.Min(
            dimensions.Height - 2,
            Math.Max(top, (int)Math.Ceiling(dimensions.Height * CorridorBottomRatio)));
        if (right < left || bottom < top)
        {
            return Rectangle.Empty;
        }

        return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private static bool TryReadWorldFileHeader(
        BinaryReader reader,
        out WorldFileTileSection tileSection,
        out string detail)
    {
        tileSection = default;
        detail = string.Empty;

        int version = reader.ReadInt32();
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
        if (sectionCount < 2)
        {
            detail = $"unexpected world section count {sectionCount}";
            return false;
        }

        int[] sectionPointers = new int[sectionCount];
        for (int i = 0; i < sectionPointers.Length; i++)
        {
            sectionPointers[i] = reader.ReadInt32();
        }

        short importanceCount = reader.ReadInt16();
        if (importanceCount < 0)
        {
            detail = $"unexpected tile importance count {importanceCount}";
            return false;
        }

        bool[] tileFrameImportant = new bool[importanceCount];
        int importanceByteCount = (importanceCount + 7) / 8;
        for (int byteIndex = 0; byteIndex < importanceByteCount; byteIndex++)
        {
            byte bits = reader.ReadByte();
            for (int bit = 0; bit < 8; bit++)
            {
                int index = byteIndex * 8 + bit;
                if (index >= tileFrameImportant.Length)
                {
                    break;
                }

                tileFrameImportant[index] = (bits & (1 << bit)) != 0;
            }
        }

        int tileDataOffset = sectionPointers[1];
        if (tileDataOffset <= 0 || tileDataOffset >= reader.BaseStream.Length)
        {
            detail = $"invalid tile section offset {tileDataOffset}";
            return false;
        }

        int tileDataEndOffset = 0;
        if (sectionPointers.Length > 2 && sectionPointers[2] > tileDataOffset && sectionPointers[2] <= reader.BaseStream.Length)
        {
            tileDataEndOffset = sectionPointers[2];
        }

        int chestDataOffset = 0;
        if (sectionPointers.Length > 2 && sectionPointers[2] > 0 && sectionPointers[2] < reader.BaseStream.Length)
        {
            chestDataOffset = sectionPointers[2];
        }

        int chestDataEndOffset = 0;
        if (sectionPointers.Length > 3 && sectionPointers[3] > chestDataOffset && sectionPointers[3] <= reader.BaseStream.Length)
        {
            chestDataEndOffset = sectionPointers[3];
        }

        tileSection = new WorldFileTileSection(
            version,
            tileDataOffset,
            tileDataEndOffset,
            chestDataOffset,
            chestDataEndOffset,
            tileFrameImportant);
        return true;
    }

    private static PyramidEvidenceScanResult ScanTileData(
        BinaryReader reader,
        int width,
        int height,
        Rectangle bounds,
        bool[] tileFrameImportant,
        int wallThreshold,
        int tileThreshold)
    {
        int wallMatches = 0;
        int tileMatches = 0;
        for (int x = 0; x < width; x++)
        {
            int y = 0;
            while (y < height)
            {
                WorldTileData tile = ReadTileData(reader, tileFrameImportant);
                int runLength = Math.Min(tile.RunLength, height - y);
                if (x >= bounds.Left && x < bounds.Right)
                {
                    int overlapTop = Math.Max(y, bounds.Top);
                    int overlapBottom = Math.Min(y + runLength, bounds.Bottom);
                    int overlap = overlapBottom - overlapTop;
                    if (overlap > 0)
                    {
                        if (tile.Wall == PyramidWallType)
                        {
                            wallMatches += overlap;
                        }

                        if (tile.Active && tile.Type == PyramidTileType)
                        {
                            tileMatches += overlap;
                        }

                        PyramidEvidenceScanResult current = new(wallMatches, tileMatches);
                        if (current.MeetsThreshold(wallThreshold, tileThreshold))
                        {
                            return current;
                        }
                    }
                }

                y += runLength;
            }
        }

        return new PyramidEvidenceScanResult(wallMatches, tileMatches);
    }

    private static Dictionary<Point, WorldTileData> ReadTargetTiles(
        BinaryReader reader,
        int width,
        int height,
        bool[] tileFrameImportant,
        HashSet<Point> targets)
    {
        var result = new Dictionary<Point, WorldTileData>();
        if (targets.Count == 0)
        {
            return result;
        }

        for (int x = 0; x < width; x++)
        {
            int y = 0;
            while (y < height)
            {
                WorldTileData tile = ReadTileData(reader, tileFrameImportant);
                int runLength = Math.Min(tile.RunLength, height - y);
                for (int offset = 0; offset < runLength; offset++)
                {
                    var point = new Point(x, y + offset);
                    if (targets.Contains(point))
                    {
                        result[point] = tile;
                        if (result.Count == targets.Count)
                        {
                            return result;
                        }
                    }
                }

                y += runLength;
            }
        }

        return result;
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

    private static WorldTileData ReadTileData(BinaryReader reader, bool[] tileFrameImportant)
    {
        // Terraria world tiles can carry up to four chained header bytes in LoadWorldTiles:
        // primary (b4), secondary (b), tertiary (b2), quaternary (b3).
        byte flags1 = reader.ReadByte();
        byte flags2 = 0;
        byte flags3 = 0;
        byte flags4 = 0;
        if ((flags1 & 0x01) != 0)
        {
            flags2 = reader.ReadByte();
            if ((flags2 & 0x01) != 0)
            {
                flags3 = reader.ReadByte();
                if ((flags3 & 0x01) != 0)
                {
                    flags4 = reader.ReadByte();
                }
            }
        }

        bool active = (flags1 & 0x02) != 0;
        ushort type = 0;
        short frameX = 0;
        short frameY = 0;
        if (active)
        {
            type = reader.ReadByte();
            if ((flags1 & 0x20) != 0)
            {
                type = (ushort)(type | (reader.ReadByte() << 8));
            }

            if (type < tileFrameImportant.Length && tileFrameImportant[type])
            {
                frameX = reader.ReadInt16();
                frameY = reader.ReadInt16();
            }

            if ((flags3 & 0x08) != 0)
            {
                _ = reader.ReadByte();
            }
        }

        ushort wall = 0;
        if ((flags1 & 0x04) != 0)
        {
            wall = reader.ReadByte();
            if ((flags3 & 0x40) != 0)
            {
                wall = (ushort)(wall | (reader.ReadByte() << 8));
            }

            if ((flags3 & 0x10) != 0)
            {
                _ = reader.ReadByte();
            }
        }

        if ((flags1 & 0x18) != 0)
        {
            _ = reader.ReadByte();
        }

        int runLength = 1;
        int runCode = (flags1 & 0xC0) >> 6;
        if (runCode == 1)
        {
            runLength += reader.ReadByte();
        }
        else if (runCode is 2 or 3)
        {
            runLength += reader.ReadInt16();
        }

        _ = flags4;
        return new WorldTileData(active, type, wall, Math.Max(1, runLength), frameX, frameY);
    }

    private readonly record struct WorldFileTileSection(
        int Version,
        int TileDataOffset,
        int TileDataEndOffset,
        int ChestDataOffset,
        int ChestDataEndOffset,
        bool[] TileFrameImportant);

    private readonly record struct WorldTileData(bool Active, ushort Type, ushort Wall, int RunLength, short FrameX, short FrameY);

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

internal readonly record struct TerrariaWorldSeedMetadata(
    string SeedText,
    int SizeCode,
    int DifficultyCode,
    bool HasCrimson,
    int SpecialSeedMask)
{
    public bool MatchesWorldOptions(AutoCreateWorldSettings settings)
    {
        if (SizeCode != TerrariaWorldSeedOptions.SizeCode(settings.WorldSize) ||
            DifficultyCode != TerrariaWorldSeedOptions.CopiedDifficultyCode(settings.WorldDifficulty) ||
            SpecialSeedMask != TerrariaWorldSeedOptions.SpecialSeedMask(settings.SpecialSeeds))
        {
            return false;
        }

        return TerrariaWorldSeedOptions.EvilMatches(settings.WorldEvil, HasCrimson);
    }

    public string FormatWorldOptions()
    {
        return $"size={SizeCode}, difficulty={DifficultyCode}, evil={(HasCrimson ? 2 : 1)}, special={SpecialSeedMask}";
    }

    public static string FormatExpectedWorldOptions(AutoCreateWorldSettings settings)
    {
        return $"size={TerrariaWorldSeedOptions.SizeCode(settings.WorldSize)}, " +
            $"difficulty={TerrariaWorldSeedOptions.CopiedDifficultyCode(settings.WorldDifficulty)}, " +
            $"evil={TerrariaWorldSeedOptions.FormatExpectedEvil(settings.WorldEvil)}, " +
            $"special={TerrariaWorldSeedOptions.SpecialSeedMask(settings.SpecialSeeds)}";
    }
}

internal readonly record struct PyramidEvidenceScanResult(int Wall34Count, int ActiveTile151Count)
{
    public bool ScanFailed => Wall34Count < 0 || ActiveTile151Count < 0;

    public bool MeetsThreshold(int wallThreshold, int tileThreshold)
    {
        return (wallThreshold > 0 && Wall34Count >= wallThreshold) ||
            (tileThreshold > 0 && ActiveTile151Count >= tileThreshold);
    }
}

internal readonly record struct PyramidChestScanResult(IReadOnlyList<PyramidChestInfo> Chests)
{
    public static PyramidChestScanResult Empty => new([]);

    public bool HasPyramidChest => Chests.Count > 0;

    public bool ContainsItem(int itemType)
    {
        return Chests.Any(chest => chest.ContainsItem(itemType));
    }

    public string FormatSummary()
    {
        if (Chests.Count == 0)
        {
            return "none";
        }

        return string.Join("; ", Chests.Select(chest => chest.FormatSummary()));
    }
}

internal readonly record struct PyramidChestInfo(
    int X,
    int Y,
    int ChestStyle,
    IReadOnlyList<PyramidChestItem> Items)
{
    public bool ContainsItem(int itemType)
    {
        return Items.Any(item => item.Type == itemType && item.Stack > 0);
    }

    public string FormatSummary()
    {
        string items = Items.Count == 0
            ? "empty"
            : string.Join(", ", Items.Select(PyramidChestItemNames.Format));
        return $"({X},{Y}) style={ChestStyle}: {items}";
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

    public static bool IsPyramidItem(int type)
    {
        return type is PharaohMask or SandstormInABottle or PharaohRobe or FlyingCarpet;
    }

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
