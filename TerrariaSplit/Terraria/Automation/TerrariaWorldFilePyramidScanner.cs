using System.Drawing;

namespace TerrariaSplit;

internal sealed class TerrariaWorldFilePyramidScanner
{
    private const ulong ReLogicMagic = 27981915666277746UL;
    private const byte WorldFileType = 2;
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

        tileSection = new WorldFileTileSection(tileDataOffset, tileDataEndOffset, tileFrameImportant);
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
        if (active)
        {
            type = reader.ReadByte();
            if ((flags1 & 0x20) != 0)
            {
                type = (ushort)(type | (reader.ReadByte() << 8));
            }

            if (type < tileFrameImportant.Length && tileFrameImportant[type])
            {
                _ = reader.ReadInt16();
                _ = reader.ReadInt16();
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
        return new WorldTileData(active, type, wall, Math.Max(1, runLength));
    }

    private readonly record struct WorldFileTileSection(int TileDataOffset, int TileDataEndOffset, bool[] TileFrameImportant);

    private readonly record struct WorldTileData(bool Active, ushort Type, ushort Wall, int RunLength);
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

internal readonly record struct PyramidEvidenceScanResult(int Wall34Count, int ActiveTile151Count)
{
    public bool ScanFailed => Wall34Count < 0 || ActiveTile151Count < 0;

    public bool MeetsThreshold(int wallThreshold, int tileThreshold)
    {
        return (wallThreshold > 0 && Wall34Count >= wallThreshold) ||
            (tileThreshold > 0 && ActiveTile151Count >= tileThreshold);
    }
}
