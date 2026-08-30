using System.Drawing;
using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class TerrariaWorldFilePyramidScanner
{
    private const ulong ReLogicMagic = 27981915666277746UL;
    private const byte WorldFileType = 2;
    private const double CorridorLeftRatio = 0.32d;
    private const double CorridorRightRatio = 0.68d;
    private const double CorridorTopRatio = 0.15d;
    private const double CorridorBottomRatio = 0.35d;
    private const int CrimsonBiomeTileThreshold = 300;
    private const int PyramidCoinPileHorizontalRadius = 16;
    private const int PyramidCoinPileVerticalRadius = 12;
    private const int PyramidGeometryHorizontalRadius = 180;
    private const int PyramidGeometryBottomPadding = 32;
    private const int PyramidSandstoneBrickTileType = 151;
    private const int PyramidSandstoneBrickWallType = 34;
    private static readonly HashSet<int> CrimsonTileTypes =
    [
        199, // Crimson grass
        662, // Crimson jungle grass
        203, // Crimstone
        234, // Crimsand
        200, // Red ice
        399, // Hardened crimsand
        401, // Crimsandstone
        205, // Crimson thorn
        201, // Crimson plants
        352  // Crimson vines
    ];

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
        out string detail,
        bool logErrors = true)
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
            metadata = ReadWorldHeaderData(reader, version).SeedMetadata;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException or ArgumentException or InvalidDataException)
        {
            detail = ex.Message;
            if (logErrors)
            {
                FileAppLogger.Instance.Error(ex, $"World pool failed to read world seed metadata from Terraria world file: {worldPath}");
            }

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
            _ = worldSize;

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

            if (sections.HeaderDataOffset <= 0 ||
                sections.TileDataOffset <= sections.HeaderDataOffset ||
                sections.ChestDataOffset <= sections.TileDataOffset)
            {
                detail = "invalid world header, tile or chest section offset";
                return false;
            }

            phase = "read world header";
            stream.Position = sections.HeaderDataOffset;
            WorldHeaderData header = ReadWorldHeaderData(reader, sections.Version);
            corridorBounds = BuildSpeedrunCorridorBounds(new TerrariaWorldDimensions(header.Width, header.Height));
            if (corridorBounds.Width <= 0 || corridorBounds.Height <= 0)
            {
                detail = "empty scan corridor";
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
                    chest.Items.Select(item => new PyramidChestItem(item.Slot, item.Type, item.Stack, item.Prefix)).ToList(),
                    PyramidCoinPileCounts.Empty);
                if (PyramidFilterItemMatcher.Matches(new PyramidChestScanResult([chestInfo]), normalizedRequiredItemMask))
                {
                    candidateChests.Add(chestInfo);
                }
            }

            if (candidateChests.Count > 0)
            {
                phase = "scan pyramid tiles";
                stream.Position = sections.TileDataOffset;
                PyramidTileScanResult tileScan = ScanPyramidTiles(
                    reader,
                    sections.FrameImportance,
                    header.Width,
                    header.Height,
                    header.WorldSurface,
                    candidateChests);
                for (int i = 0; i < candidateChests.Count; i++)
                {
                    candidateChests[i] = candidateChests[i] with
                    {
                        CoinPiles = tileScan.CoinPiles[i],
                        TunnelTopX = tileScan.Geometry[i].TunnelTopX,
                        TunnelTopY = tileScan.Geometry[i].TunnelTopY,
                        TunnelOpeningSide = tileScan.Geometry[i].TunnelOpeningSide,
                        TunnelSurfaceDistance = tileScan.Geometry[i].TunnelSurfaceDistance,
                        TunnelDepthDetail = tileScan.Geometry[i].Detail
                    };
                }
            }

            result = new PyramidChestScanResult(candidateChests);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException or ArgumentException or InvalidDataException)
        {
            detail = $"{phase}: {ex.Message}";
            FileAppLogger.Instance.Error(ex, $"Pyramid filter failed to scan Terraria candidate chest data: {worldPath}");
            return false;
        }
    }

    public bool TryScanCrimsonBetweenDungeonAndSpawn(
        string worldPath,
        out CrimsonCorridorScanResult result,
        out string detail,
        string crimsonDistance = AutoCreateCrimsonDistance.Default)
    {
        result = default;
        detail = string.Empty;
        string phase = "open world file";
        try
        {
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

            if (sections.HeaderDataOffset <= 0 || sections.TileDataOffset <= sections.HeaderDataOffset)
            {
                detail = "invalid world header or tile section offset";
                return false;
            }

            phase = "read world header";
            stream.Position = sections.HeaderDataOffset;
            WorldHeaderData header = ReadWorldHeaderData(reader, sections.Version);
            if (header.Width <= 0 ||
                header.Height <= 0 ||
                header.SpawnTileX < 0 ||
                header.SpawnTileX >= header.Width ||
                header.DungeonTileX < 0 ||
                header.DungeonTileX >= header.Width ||
                header.DungeonTileX == header.SpawnTileX)
            {
                detail = "invalid spawn, dungeon or world dimensions";
                return false;
            }

            Rectangle bounds = BuildCrimsonCorridorBounds(
                header.Width,
                header.Height,
                header.SpawnTileX,
                header.DungeonTileX,
                crimsonDistance);
            if (bounds.Width <= 0)
            {
                detail = "empty Crimson distance corridor";
                return false;
            }

            int left = bounds.Left;
            int rightExclusive = bounds.Right;
            phase = "scan world tiles";
            stream.Position = sections.TileDataOffset;
            int crimsonTileCount = 0;
            for (int x = 0; x < header.Width; x++)
            {
                int y = 0;
                while (y < header.Height)
                {
                    WorldTileRecord tile = ReadWorldTileRecord(reader, sections.FrameImportance);
                    int tileCount = checked(tile.RunLength + 1);
                    if (tileCount <= 0 || y + tileCount > header.Height)
                    {
                        throw new InvalidDataException($"invalid tile run length {tile.RunLength} at {x},{y}");
                    }

                    if (x >= left && x < rightExclusive && tile.Active && CrimsonTileTypes.Contains(tile.Type))
                    {
                        crimsonTileCount += tileCount;
                        if (crimsonTileCount >= CrimsonBiomeTileThreshold)
                        {
                            result = new CrimsonCorridorScanResult(bounds, crimsonTileCount, HasCrimson: true);
                            return true;
                        }
                    }

                    y += tileCount;
                }
            }

            result = new CrimsonCorridorScanResult(bounds, crimsonTileCount, HasCrimson: false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException or ArgumentException or InvalidDataException or OverflowException)
        {
            detail = $"{phase}: {ex.Message}";
            FileAppLogger.Instance.Error(ex, $"World post-generation filter failed to scan Crimson corridor: {worldPath}");
            return false;
        }
    }

    public bool TryMeasureJungleTunnelAlignment(
        string worldPath,
        IReadOnlyCollection<Point> centerline,
        out JungleTunnelAlignmentResult result,
        out string detail)
    {
        result = default;
        detail = string.Empty;
        try
        {
            using FileStream stream = new(
                worldPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using BinaryReader reader = new(stream);

            if (!TryReadWorldFileSections(reader, out WorldFileSections sections, out detail))
            {
                return false;
            }

            stream.Position = sections.HeaderDataOffset;
            WorldHeaderData header = ReadWorldHeaderData(reader, sections.Version);
            Dictionary<int, int[]> targetsByX = centerline
                .Where(point => point.X >= 0 && point.X < header.Width && point.Y >= 0 && point.Y < header.Height)
                .Distinct()
                .GroupBy(point => point.X)
                .ToDictionary(group => group.Key, group => group.Select(point => point.Y).Order().ToArray());
            int sampleCount = targetsByX.Values.Sum(static values => values.Length);
            int openCount = 0;

            stream.Position = sections.TileDataOffset;
            for (int x = 0; x < header.Width; x++)
            {
                targetsByX.TryGetValue(x, out int[]? targetYs);
                int targetIndex = 0;
                int y = 0;
                while (y < header.Height)
                {
                    WorldTileRecord tile = ReadWorldTileRecord(reader, sections.FrameImportance);
                    int tileCount = checked(tile.RunLength + 1);
                    if (tileCount <= 0 || y + tileCount > header.Height)
                    {
                        throw new InvalidDataException($"invalid tile run length {tile.RunLength} at {x},{y}");
                    }

                    if (targetYs is not null)
                    {
                        while (targetIndex < targetYs.Length && targetYs[targetIndex] < y)
                        {
                            targetIndex++;
                        }

                        while (targetIndex < targetYs.Length && targetYs[targetIndex] < y + tileCount)
                        {
                            if (!tile.Active)
                            {
                                openCount++;
                            }

                            targetIndex++;
                        }
                    }

                    y += tileCount;
                }
            }

            result = new JungleTunnelAlignmentResult(sampleCount, openCount);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException or ArgumentException or InvalidDataException or OverflowException)
        {
            detail = ex.Message;
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
    private static WorldHeaderData ReadWorldHeaderData(BinaryReader reader, int version)
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
        for (int i = 0; i < 17; i++)
        {
            // treeX[3], treeStyle[4], caveBackX[3], caveBackStyle[4], ice/jungle/hell back styles.
            _ = reader.ReadInt32();
        }

        int spawnTileX = reader.ReadInt32();
        int spawnTileY = reader.ReadInt32();

        double worldSurface = reader.ReadDouble();
        _ = reader.ReadDouble(); // rock layer
        _ = reader.ReadDouble(); // temp time
        _ = reader.ReadBoolean(); // temp day time
        _ = reader.ReadInt32(); // temp moon phase
        _ = reader.ReadBoolean(); // temp blood moon
        _ = reader.ReadBoolean(); // temp eclipse
        int dungeonX = reader.ReadInt32();
        int dungeonY = reader.ReadInt32();
        bool hasCrimson = reader.ReadBoolean();

        return new WorldHeaderData(
            new TerrariaWorldSeedMetadata(
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
                    skyblock)),
            maxTilesX,
            maxTilesY,
            spawnTileX,
            spawnTileY,
            dungeonX,
            dungeonY,
            worldSurface);
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

    internal static Rectangle BuildCrimsonCorridorBounds(
        int worldWidth,
        int worldHeight,
        int spawnTileX,
        int dungeonTileX,
        string crimsonDistance)
    {
        int maximumDistance = AutoCreateCrimsonDistance.MaximumDistanceTiles(worldWidth, crimsonDistance);
        if (worldWidth <= 0 || worldHeight <= 0 || maximumDistance <= 0 || spawnTileX == dungeonTileX)
        {
            return Rectangle.Empty;
        }

        if (dungeonTileX < spawnTileX)
        {
            int left = Math.Max(dungeonTileX + 1, spawnTileX - maximumDistance);
            return Rectangle.FromLTRB(left, 0, spawnTileX, worldHeight);
        }

        int rightExclusive = Math.Min(dungeonTileX, spawnTileX + maximumDistance + 1);
        return Rectangle.FromLTRB(spawnTileX + 1, 0, rightExclusive, worldHeight);
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

        ushort importanceCount = reader.ReadUInt16();
        var frameImportance = new bool[importanceCount];
        byte importanceBits = 0;
        byte importanceMask = 128;
        for (int index = 0; index < importanceCount; index++)
        {
            if (importanceMask == 128)
            {
                importanceBits = reader.ReadByte();
                importanceMask = 1;
            }
            else
            {
                importanceMask <<= 1;
            }

            frameImportance[index] = (importanceBits & importanceMask) == importanceMask;
        }

        int headerDataOffset = sectionPointers.Length > 0 ? sectionPointers[0] : 0;
        int tileDataOffset = sectionPointers.Length > 1 ? sectionPointers[1] : 0;
        int chestDataOffset = 0;
        if (sectionPointers.Length > 2 && sectionPointers[2] > 0 && sectionPointers[2] < reader.BaseStream.Length)
        {
            chestDataOffset = sectionPointers[2];
        }

        sections = new WorldFileSections(
            version,
            headerDataOffset,
            tileDataOffset,
            chestDataOffset,
            frameImportance);
        return true;
    }

    private static WorldTileRecord ReadWorldTileRecord(BinaryReader reader, bool[] frameImportance)
    {
        byte header1 = reader.ReadByte();
        byte header2 = 0;
        byte header3 = 0;
        if ((header1 & 1) != 0)
        {
            header2 = reader.ReadByte();
            if ((header2 & 1) != 0)
            {
                header3 = reader.ReadByte();
                if ((header3 & 1) != 0)
                {
                    _ = reader.ReadByte();
                }
            }
        }

        bool active = (header1 & 2) != 0;
        int type = -1;
        short frameX = -1;
        short frameY = -1;
        if (active)
        {
            type = reader.ReadByte();
            if ((header1 & 0x20) != 0)
            {
                type |= reader.ReadByte() << 8;
            }

            if (type < 0 || type >= frameImportance.Length)
            {
                throw new InvalidDataException($"tile type {type} exceeds frame-importance table");
            }

            if (frameImportance[type])
            {
                frameX = reader.ReadInt16();
                frameY = reader.ReadInt16();
            }

            if ((header3 & 8) != 0)
            {
                _ = reader.ReadByte();
            }
        }

        int wallType = 0;
        if ((header1 & 4) != 0)
        {
            wallType = reader.ReadByte();
            if ((header3 & 0x10) != 0)
            {
                wallType |= reader.ReadByte() << 8;
            }
        }

        if ((header1 & 0x18) != 0)
        {
            _ = reader.ReadByte();
        }

        if ((header3 & 0x40) != 0)
        {
            _ = reader.ReadByte();
        }

        int runLength = (header1 & 0xC0) switch
        {
            0x40 => reader.ReadByte(),
            0x80 or 0xC0 => reader.ReadInt16(),
            _ => 0
        };
        return new WorldTileRecord(active, type, wallType, frameX, frameY, runLength);
    }

    private static PyramidTileScanResult ScanPyramidTiles(
        BinaryReader reader,
        bool[] frameImportance,
        int worldWidth,
        int worldHeight,
        double worldSurface,
        IReadOnlyList<PyramidChestInfo> chests)
    {
        var counts = new PyramidCoinPileCounts[chests.Count];
        int minX = Math.Max(0, chests.Min(static chest => chest.X) - PyramidGeometryHorizontalRadius);
        int maxX = Math.Min(worldWidth - 1, chests.Max(static chest => chest.X) + PyramidGeometryHorizontalRadius);
        int maxY = Math.Min(
            worldHeight - 1,
            chests.Max(static chest => chest.Y) + PyramidGeometryBottomPadding);
        var snapshot = new PyramidTileSnapshot(minX, maxX, maxY);

        for (int x = 0; x < worldWidth; x++)
        {
            int y = 0;
            while (y < worldHeight)
            {
                WorldTileRecord tile = ReadWorldTileRecord(reader, frameImportance);
                int tileCount = checked(tile.RunLength + 1);
                if (tileCount <= 0 || y + tileCount > worldHeight)
                {
                    throw new InvalidDataException($"invalid tile run length {tile.RunLength} at {x},{y}");
                }

                if (x >= minX && x <= maxX)
                {
                    int lastSnapshotY = Math.Min(maxY, y + tileCount - 1);
                    for (int snapshotY = y; snapshotY <= lastSnapshotY; snapshotY++)
                    {
                        snapshot.Set(x, snapshotY, new PyramidWorldTile(tile.Active, tile.Type, tile.WallType));
                    }
                }

                if (tile.Active &&
                    PyramidCoinPileFrameClassifier.TryClassify(tile.Type, tile.FrameX, tile.FrameY, out PyramidCoinPileKind kind))
                {
                    for (int offset = 0; offset < tileCount; offset++)
                    {
                        int chestIndex = FindAssociatedPyramidChest(chests, x, y + offset);
                        if (chestIndex >= 0)
                        {
                            counts[chestIndex] = counts[chestIndex].Add(kind);
                        }
                    }
                }

                y += tileCount;
            }
        }

        var geometry = new PyramidTunnelGeometry[chests.Count];
        for (int i = 0; i < chests.Count; i++)
        {
            geometry[i] = MeasurePyramidTunnelGeometry(snapshot, chests[i], worldSurface);
        }

        return new PyramidTileScanResult(counts, geometry);
    }

    private static PyramidTunnelGeometry MeasurePyramidTunnelGeometry(
        PyramidTileSnapshot snapshot,
        PyramidChestInfo chest,
        double worldSurface)
    {
        int left = Math.Max(snapshot.MinX, chest.X - PyramidGeometryHorizontalRadius);
        int right = Math.Min(snapshot.MaxX, chest.X + PyramidGeometryHorizontalRadius);
        int bottom = Math.Min(snapshot.MaxY, chest.Y + PyramidGeometryBottomPadding);
        Point? corridorSeed = FindNearestTile(
            snapshot,
            chest.X,
            chest.Y,
            horizontalRadius: 32,
            verticalRadius: 32,
            static tile => !tile.Active && tile.WallType == PyramidSandstoneBrickWallType);
        if (corridorSeed is null)
        {
            return PyramidTunnelGeometry.Unknown("no connected pyramid-wall corridor near chest");
        }

        bool[] corridor = FloodFill(
            snapshot,
            corridorSeed.Value,
            left,
            right,
            bottom,
            static tile => !tile.Active && tile.WallType == PyramidSandstoneBrickWallType);
        int tunnelTopY = int.MaxValue;
        int tunnelTopLeft = int.MaxValue;
        int tunnelTopRight = int.MinValue;
        for (int y = 0; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (!corridor[snapshot.IndexOf(x, y)])
                {
                    continue;
                }

                if (y < tunnelTopY)
                {
                    tunnelTopY = y;
                    tunnelTopLeft = x;
                    tunnelTopRight = x;
                }
                else if (y == tunnelTopY)
                {
                    tunnelTopLeft = Math.Min(tunnelTopLeft, x);
                    tunnelTopRight = Math.Max(tunnelTopRight, x);
                }
            }
        }

        if (tunnelTopY == int.MaxValue)
        {
            return PyramidTunnelGeometry.Unknown("pyramid-wall corridor contains no tiles");
        }

        if (!TryEstimatePyramidCenter(
                snapshot,
                left,
                right,
                Math.Max(0, tunnelTopY - 16),
                bottom,
                out int pyramidCenterX))
        {
            return PyramidTunnelGeometry.Unknown("could not estimate pyramid center from sandstone bricks");
        }

        int tunnelTopMidpoint = (tunnelTopLeft + tunnelTopRight) / 2;
        int openingSide = tunnelTopMidpoint < pyramidCenterX ? -1 : 1;
        int tunnelTopX = openingSide < 0 ? tunnelTopLeft : tunnelTopRight;

        int minimumDistance = int.MaxValue;
        int surfaceSearchStart = Math.Max(0, (int)Math.Floor(worldSurface) - 180);
        for (int x = tunnelTopX - 2; x <= tunnelTopX + 2; x++)
        {
            if (x < left || x > right)
            {
                continue;
            }

            int surfaceY = FindTerrainSurfaceY(
                snapshot,
                x,
                surfaceSearchStart,
                tunnelTopY);
            if (surfaceY >= 0)
            {
                minimumDistance = Math.Min(minimumDistance, tunnelTopY - surfaceY);
            }
        }

        return minimumDistance == int.MaxValue
            ? PyramidTunnelGeometry.Unknown("could not locate connected terrain surface above tunnel")
            : new PyramidTunnelGeometry(tunnelTopX, tunnelTopY, openingSide, minimumDistance, string.Empty);
    }

    private static int FindTerrainSurfaceY(
        PyramidTileSnapshot snapshot,
        int x,
        int startY,
        int tunnelTopY)
    {
        int fallback = -1;
        for (int y = Math.Max(0, startY); y < tunnelTopY; y++)
        {
            PyramidWorldTile tile = snapshot.Get(x, y);
            if (!tile.Active)
            {
                continue;
            }

            if (IsSurfaceDecoration(tile.Type))
            {
                continue;
            }

            fallback = fallback < 0 ? y : fallback;
            int supportedTiles = 0;
            for (int sampleX = Math.Max(snapshot.MinX, x - 2);
                 sampleX <= Math.Min(snapshot.MaxX, x + 2);
                 sampleX++)
            {
                for (int sampleY = y; sampleY <= Math.Min(tunnelTopY - 1, y + 7); sampleY++)
                {
                    PyramidWorldTile sample = snapshot.Get(sampleX, sampleY);
                    if (sample.Active && !IsSurfaceDecoration(sample.Type))
                    {
                        supportedTiles++;
                    }
                }
            }

            if (supportedTiles >= 10)
            {
                return y;
            }
        }

        return fallback;
    }

    private static bool IsSurfaceDecoration(int tileType) => tileType is
        3 or   // plants
        5 or   // trees
        24 or  // corruption plants
        32 or  // thorns
        61 or  // jungle plants
        69 or  // thorny bushes
        71 or 72 or 73 or 74 or // mushroom plants
        80 or  // cactus
        82 or 83 or 84 or // herbs
        110 or 113 or 115 or // hallowed plants
        184 or // small piles
        185 or // large piles
        189 or 196 or // clouds and rain clouds
        201 or 205 or // crimson plants and thorns
        227 or 233 or // dye plants
        323 or // palm trees
        324 or // beach piles
        352 or // crimson vines
        382 or // herb sprouts
        528 or // bamboo
        636;   // vanity tree foliage

    private static Point? FindNearestTile(
        PyramidTileSnapshot snapshot,
        int centerX,
        int centerY,
        int horizontalRadius,
        int verticalRadius,
        Func<PyramidWorldTile, bool> predicate)
    {
        Point? nearest = null;
        int nearestDistance = int.MaxValue;
        int left = Math.Max(snapshot.MinX, centerX - horizontalRadius);
        int right = Math.Min(snapshot.MaxX, centerX + horizontalRadius);
        int top = Math.Max(0, centerY - verticalRadius);
        int bottom = Math.Min(snapshot.MaxY, centerY + verticalRadius);
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (!predicate(snapshot.Get(x, y)))
                {
                    continue;
                }

                int distance = Math.Abs(x - centerX) + Math.Abs(y - centerY);
                if (distance < nearestDistance)
                {
                    nearest = new Point(x, y);
                    nearestDistance = distance;
                }
            }
        }

        return nearest;
    }

    private static bool[] FloodFill(
        PyramidTileSnapshot snapshot,
        Point seed,
        int left,
        int right,
        int bottom,
        Func<PyramidWorldTile, bool> predicate)
    {
        var visited = new bool[snapshot.CellCount];
        var queue = new Queue<Point>();
        visited[snapshot.IndexOf(seed.X, seed.Y)] = true;
        queue.Enqueue(seed);
        ReadOnlySpan<Point> directions =
        [
            new Point(-1, 0),
            new Point(1, 0),
            new Point(0, -1),
            new Point(0, 1)
        ];
        while (queue.Count > 0)
        {
            Point point = queue.Dequeue();
            foreach (Point direction in directions)
            {
                int x = point.X + direction.X;
                int y = point.Y + direction.Y;
                if (x < left || x > right || y < 0 || y > bottom)
                {
                    continue;
                }

                int index = snapshot.IndexOf(x, y);
                if (visited[index] || !predicate(snapshot.Get(x, y)))
                {
                    continue;
                }

                visited[index] = true;
                queue.Enqueue(new Point(x, y));
            }
        }

        return visited;
    }

    private static bool TryEstimatePyramidCenter(
        PyramidTileSnapshot snapshot,
        int left,
        int right,
        int top,
        int bottom,
        out int centerX)
    {
        centerX = 0;
        int widestSpan = -1;
        int strongestCount = -1;
        for (int y = top; y <= bottom; y++)
        {
            int rowLeft = int.MaxValue;
            int rowRight = int.MinValue;
            int count = 0;
            for (int x = left; x <= right; x++)
            {
                PyramidWorldTile tile = snapshot.Get(x, y);
                if (!tile.Active || tile.Type != PyramidSandstoneBrickTileType)
                {
                    continue;
                }

                rowLeft = Math.Min(rowLeft, x);
                rowRight = Math.Max(rowRight, x);
                count++;
            }

            if (count < 2)
            {
                continue;
            }

            int span = rowRight - rowLeft;
            if (span > widestSpan || (span == widestSpan && count > strongestCount))
            {
                widestSpan = span;
                strongestCount = count;
                centerX = (rowLeft + rowRight) / 2;
            }
        }

        return widestSpan >= 0;
    }

    private static int FindAssociatedPyramidChest(IReadOnlyList<PyramidChestInfo> chests, int pileX, int pileY)
    {
        int bestIndex = -1;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < chests.Count; i++)
        {
            PyramidChestInfo chest = chests[i];
            int deltaX = Math.Abs(pileX - chest.X);
            int deltaY = Math.Abs(pileY - chest.Y);
            if (deltaX > PyramidCoinPileHorizontalRadius || deltaY > PyramidCoinPileVerticalRadius)
            {
                continue;
            }

            int distance = deltaX * deltaX + deltaY * deltaY;
            if (distance < bestDistance)
            {
                bestIndex = i;
                bestDistance = distance;
            }
        }

        return bestIndex;
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

    private sealed class PyramidTileSnapshot
    {
        private readonly PyramidWorldTile[] tiles;

        public PyramidTileSnapshot(int minX, int maxX, int maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MaxY = maxY;
            Width = checked(maxX - minX + 1);
            tiles = new PyramidWorldTile[checked(Width * (maxY + 1))];
        }

        public int MinX { get; }

        public int MaxX { get; }

        public int MaxY { get; }

        public int Width { get; }

        public int CellCount => tiles.Length;

        public int IndexOf(int x, int y) => checked(y * Width + x - MinX);

        public PyramidWorldTile Get(int x, int y) => tiles[IndexOf(x, y)];

        public void Set(int x, int y, PyramidWorldTile tile) => tiles[IndexOf(x, y)] = tile;
    }

    private readonly record struct PyramidWorldTile(bool Active, int Type, int WallType);

    private readonly record struct PyramidTunnelGeometry(
        int TunnelTopX,
        int TunnelTopY,
        int TunnelOpeningSide,
        int TunnelSurfaceDistance,
        string Detail)
    {
        public static PyramidTunnelGeometry Unknown(string detail) => new(-1, -1, 0, -1, detail);
    }

    private readonly record struct PyramidTileScanResult(
        PyramidCoinPileCounts[] CoinPiles,
        PyramidTunnelGeometry[] Geometry);

    private readonly record struct WorldFileSections(
        int Version,
        int HeaderDataOffset,
        int TileDataOffset,
        int ChestDataOffset,
        bool[] FrameImportance);

    private readonly record struct WorldHeaderData(
        TerrariaWorldSeedMetadata SeedMetadata,
        int Width,
        int Height,
        int SpawnTileX,
        int SpawnTileY,
        int DungeonTileX,
        int DungeonTileY,
        double WorldSurface);

    private readonly record struct WorldTileRecord(
        bool Active,
        int Type,
        int WallType,
        short FrameX,
        short FrameY,
        int RunLength);

    private readonly record struct WorldChestData(int X, int Y, IReadOnlyList<WorldChestItemData> Items);

    private readonly record struct WorldChestItemData(int Slot, int Type, int Stack, byte Prefix);
}

internal readonly record struct CrimsonCorridorScanResult(
    Rectangle Bounds,
    int CrimsonTileCount,
    bool HasCrimson);

internal readonly record struct JungleTunnelAlignmentResult(int SampleCount, int OpenCenterCount)
{
    public double OpenRatio => SampleCount == 0 ? 0d : OpenCenterCount / (double)SampleCount;
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
    IReadOnlyList<PyramidChestItem> Items,
    PyramidCoinPileCounts CoinPiles,
    int TunnelTopX = -1,
    int TunnelTopY = -1,
    int TunnelOpeningSide = 0,
    int TunnelSurfaceDistance = -1,
    string TunnelDepthDetail = "")
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
        string depth = TunnelSurfaceDistance >= 0
            ? $"depth={TunnelSurfaceDistance}, tunnel=({TunnelTopX},{TunnelTopY}), side={TunnelOpeningSide}"
            : $"depth=unknown ({TunnelDepthDetail})";
        return $"({X},{Y}): {items}; {CoinPiles.Format()}; {depth}";
    }
}

internal static class PyramidCoinPileFrameClassifier
{
    private const int SmallPilesTileType = 185;
    private const short TwoTilePileFrameY = 18;

    public static bool TryClassify(
        int tileType,
        short frameX,
        short frameY,
        out PyramidCoinPileKind kind)
    {
        kind = default;
        if (tileType != SmallPilesTileType || frameY != TwoTilePileFrameY)
        {
            return false;
        }

        switch (frameX)
        {
            case 576:
                kind = PyramidCoinPileKind.Copper;
                return true;
            case 612:
                kind = PyramidCoinPileKind.Silver;
                return true;
            case 648:
                kind = PyramidCoinPileKind.Gold;
                return true;
            default:
                return false;
        }
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
