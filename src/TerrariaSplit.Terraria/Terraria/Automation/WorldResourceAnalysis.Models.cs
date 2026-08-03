using System.Globalization;
using System.Text;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class TerrariaResourceWorldReader
{
    private const ulong ReLogicMagic = 27981915666277746UL;
    private const byte WorldFileType = 2;

    public WorldData Read(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);

        WorldFileFormatHeader fileHeader = ReadFileFormatHeader(reader);
        stream.Position = fileHeader.SectionPointers[0];
        WorldHeader header = ReadWorldHeader(reader, fileHeader.Version);

        TileGrid tiles = new(header.Width, header.Height);
        stream.Position = fileHeader.SectionPointers[1];
        ReadTiles(reader, tiles, fileHeader.TileFrameImportant);

        List<ChestData> chests = new();
        if (fileHeader.SectionPointers.Length > 2 && fileHeader.SectionPointers[2] > 0 && fileHeader.SectionPointers[2] < stream.Length)
        {
            stream.Position = fileHeader.SectionPointers[2];
            chests = ReadChests(reader, fileHeader.Version, tiles);
        }

        return new WorldData(path, fileHeader.Version, header, tiles, chests);
    }

    private static WorldFileFormatHeader ReadFileFormatHeader(BinaryReader reader)
    {
        int version = reader.ReadInt32();
        if (version >= 135)
        {
            ulong metadata = reader.ReadUInt64();
            if ((metadata & 0x00FFFFFFFFFFFFFFUL) != ReLogicMagic)
            {
                throw new InvalidDataException($"Unexpected Terraria world metadata magic 0x{metadata:X16}.");
            }

            byte fileType = (byte)(metadata >> 56);
            if (fileType != WorldFileType)
            {
                throw new InvalidDataException($"Unexpected Terraria world file type {fileType}.");
            }

            _ = reader.ReadUInt32();
            _ = reader.ReadUInt64();
        }

        short sectionCount = reader.ReadInt16();
        if (sectionCount < 3)
        {
            throw new InvalidDataException("World file does not contain the required header/tile/chest sections.");
        }

        int[] sectionPointers = new int[sectionCount];
        for (int i = 0; i < sectionPointers.Length; i++)
        {
            sectionPointers[i] = reader.ReadInt32();
        }

        short importanceCount = reader.ReadInt16();
        if (importanceCount <= 0)
        {
            throw new InvalidDataException("World file contains an invalid tile importance table.");
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

        return new WorldFileFormatHeader(version, sectionPointers, tileFrameImportant);
    }

    private static WorldHeader ReadWorldHeader(BinaryReader reader, int version)
    {
        string worldName = reader.ReadString();
        string seed = string.Empty;
        if (version >= 179)
        {
            seed = version == 179 ? reader.ReadInt32().ToString(CultureInfo.InvariantCulture) : reader.ReadString();
            _ = reader.ReadUInt64();
        }

        if (version >= 181)
        {
            _ = reader.ReadBytes(16);
        }

        int worldId = reader.ReadInt32();
        int leftWorld = reader.ReadInt32();
        int rightWorld = reader.ReadInt32();
        int topWorld = reader.ReadInt32();
        int bottomWorld = reader.ReadInt32();
        int height = reader.ReadInt32();
        int width = reader.ReadInt32();

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"Invalid world dimensions {width}x{height}.");
        }

        if (version >= 209)
        {
            _ = reader.ReadInt32();
            if (version >= 222)
            {
                _ = reader.ReadBoolean();
            }

            if (version >= 227)
            {
                _ = reader.ReadBoolean();
            }

            if (version >= 238)
            {
                _ = reader.ReadBoolean();
            }

            if (version >= 239)
            {
                _ = reader.ReadBoolean();
            }

            if (version >= 241)
            {
                _ = reader.ReadBoolean();
            }

            if (version >= 249)
            {
                _ = reader.ReadBoolean();
            }

            if (version >= 266)
            {
                _ = reader.ReadBoolean();
            }

            if (version >= 267)
            {
                _ = reader.ReadBoolean();
            }

            if (version >= 302)
            {
                _ = reader.ReadBoolean();
            }
        }
        else
        {
            if (version >= 112)
            {
                _ = reader.ReadBoolean();
            }

            if (version == 208)
            {
                _ = reader.ReadBoolean();
            }
        }

        if (version >= 141)
        {
            _ = reader.ReadInt64();
        }

        if (version >= 284)
        {
            _ = reader.ReadInt64();
        }

        _ = reader.ReadByte();
        for (int i = 0; i < 3; i++)
        {
            _ = reader.ReadInt32();
        }

        for (int i = 0; i < 4; i++)
        {
            _ = reader.ReadInt32();
        }

        for (int i = 0; i < 3; i++)
        {
            _ = reader.ReadInt32();
        }

        for (int i = 0; i < 4; i++)
        {
            _ = reader.ReadInt32();
        }

        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        int spawnX = reader.ReadInt32();
        int spawnY = reader.ReadInt32();
        double worldSurface = reader.ReadDouble();
        double rockLayer = reader.ReadDouble();
        _ = leftWorld;
        _ = rightWorld;
        _ = topWorld;
        _ = bottomWorld;
        _ = seed;

        int dungeonX = 0;
        try
        {
            SkipHeaderFieldsUntilDungeon(reader, version);
            dungeonX = reader.ReadInt32();
        }
        catch (EndOfStreamException)
        {
            dungeonX = 0;
        }

        return new WorldHeader(worldName, worldId, width, height, spawnX, spawnY, worldSurface, rockLayer, dungeonX);
    }

    private static void SkipHeaderFieldsUntilDungeon(BinaryReader reader, int version)
    {
        _ = reader.ReadDouble();
        _ = reader.ReadBoolean();
        _ = reader.ReadInt32();
        _ = reader.ReadBoolean();
        _ = reader.ReadBoolean();
    }

    private static void ReadTiles(BinaryReader reader, TileGrid tiles, bool[] tileFrameImportant)
    {
        Array.Fill(tiles.FrameX, (short)-1);
        Array.Fill(tiles.FrameY, (short)-1);

        for (int x = 0; x < tiles.Width; x++)
        {
            int y = 0;
            while (y < tiles.Height)
            {
                WorldTile tile = ReadTile(reader, tileFrameImportant);
                int runLength = Math.Min(tile.RunLength, tiles.Height - y);
                tiles.FillRun(x, y, runLength, tile);
                y += runLength;
            }
        }
    }

    private static WorldTile ReadTile(BinaryReader reader, bool[] tileFrameImportant)
    {
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
        short frameX = -1;
        short frameY = -1;
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
                if (type == TileIds.Timer)
                {
                    frameY = 0;
                }
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
            if ((flags3 & 0x10) != 0)
            {
                _ = reader.ReadByte();
            }
        }

        byte liquid = 0;
        byte liquidType = 0;
        int liquidCode = (flags1 & 0x18) >> 3;
        if (liquidCode != 0)
        {
            liquid = reader.ReadByte();
            liquidType = (flags3 & 0x80) != 0
                ? (byte)3
                : liquidCode switch
                {
                    1 => (byte)0,
                    2 => (byte)1,
                    3 => (byte)2,
                    _ => (byte)0
                };
        }

        if ((flags3 & 0x40) != 0)
        {
            wall = (ushort)((reader.ReadByte() << 8) | wall);
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

        _ = flags2;
        _ = flags4;
        return new WorldTile(active, type, wall, frameX, frameY, liquid, liquidType, Math.Max(1, runLength));
    }

    private static List<ChestData> ReadChests(BinaryReader reader, int version, TileGrid tiles)
    {
        int chestCount = reader.ReadInt16();
        int legacyItemCount = 0;
        if (version < 294)
        {
            legacyItemCount = reader.ReadInt16();
        }

        List<ChestData> chests = new(chestCount);
        for (int chestIndex = 0; chestIndex < chestCount; chestIndex++)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            string name = reader.ReadString();
            int itemCount = version >= 294 ? reader.ReadInt32() : legacyItemCount;
            List<ChestItem> items = new();
            for (int slot = 0; slot < itemCount; slot++)
            {
                short stack = reader.ReadInt16();
                if (stack > 0)
                {
                    int itemId = reader.ReadInt32();
                    byte prefix = reader.ReadByte();
                    items.Add(new ChestItem(slot, itemId, stack, prefix));
                }
                else if (stack < 0)
                {
                    int itemId = reader.ReadInt32();
                    byte prefix = reader.ReadByte();
                    items.Add(new ChestItem(slot, itemId, 1, prefix));
                }
            }

            int chestStyle = GetChestStyle(tiles, x, y);
            chests.Add(new ChestData(chestIndex, x, y, chestStyle, name, items));
        }

        return chests;
    }

    private static int GetChestStyle(TileGrid tiles, int x, int y)
    {
        if (!tiles.Contains(x, y))
        {
            return -1;
        }

        int index = tiles.Index(x, y);
        ushort type = tiles.Type[index];
        if (type is TileIds.Containers or TileIds.Containers2)
        {
            short frameX = tiles.FrameX[index];
            return frameX >= 0 ? frameX / 36 : -1;
        }

        return -1;
    }
}

internal sealed class TileGrid
{
    public TileGrid(int width, int height)
    {
        Width = width;
        Height = height;
        int count = checked(width * height);
        Active = new bool[count];
        Type = new ushort[count];
        Wall = new ushort[count];
        FrameX = new short[count];
        FrameY = new short[count];
        Liquid = new byte[count];
        LiquidType = new byte[count];
    }

    public int Width { get; }
    public int Height { get; }
    public bool[] Active { get; }
    public ushort[] Type { get; }
    public ushort[] Wall { get; }
    public short[] FrameX { get; }
    public short[] FrameY { get; }
    public byte[] Liquid { get; }
    public byte[] LiquidType { get; }

    public int Index(int x, int y) => x * Height + y;

    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public void FillRun(int x, int y, int runLength, WorldTile tile)
    {
        int start = Index(x, y);
        if (tile.Active)
        {
            Array.Fill(Active, true, start, runLength);
            Array.Fill(Type, tile.Type, start, runLength);
        }

        if (tile.Wall != 0)
        {
            Array.Fill(Wall, tile.Wall, start, runLength);
        }

        if (tile.FrameX >= 0)
        {
            Array.Fill(FrameX, tile.FrameX, start, runLength);
            Array.Fill(FrameY, tile.FrameY, start, runLength);
        }

        if (tile.Liquid > 0)
        {
            Array.Fill(Liquid, tile.Liquid, start, runLength);
            Array.Fill(LiquidType, tile.LiquidType, start, runLength);
        }
    }
}

internal sealed record WorldData(
    string Path,
    int Version,
    WorldHeader Header,
    TileGrid Tiles,
    IReadOnlyList<ChestData> Chests);

internal sealed record WorldHeader(
    string WorldName,
    int WorldId,
    int Width,
    int Height,
    int SpawnX,
    int SpawnY,
    double WorldSurface,
    double RockLayer,
    int DungeonX);

internal sealed record ChestData(
    int Index,
    int X,
    int Y,
    int Style,
    string Name,
    IReadOnlyList<ChestItem> Items);

internal sealed record ChestItem(int Slot, int ItemId, int Stack, byte Prefix);

internal readonly record struct WorldTile(
    bool Active,
    ushort Type,
    ushort Wall,
    short FrameX,
    short FrameY,
    byte Liquid,
    byte LiquidType,
    int RunLength);

internal readonly record struct WorldFileFormatHeader(int Version, int[] SectionPointers, bool[] TileFrameImportant);

internal static class TileIds
{
    public const ushort Dirt = 0;
    public const ushort Stone = 1;
    public const ushort Grass = 2;
    public const ushort Plants = 3;
    public const ushort Torches = 4;
    public const ushort Trees = 5;
    public const ushort Iron = 6;
    public const ushort Copper = 7;
    public const ushort Gold = 8;
    public const ushort Silver = 9;
    public const ushort ClosedDoor = 10;
    public const ushort OpenDoor = 11;
    public const ushort Heart = 12;
    public const ushort Chairs = 15;
    public const ushort Containers = 21;
    public const ushort Demonite = 22;
    public const ushort Pots = 28;
    public const ushort CorruptThorns = 32;
    public const ushort Meteorite = 37;
    public const ushort BlueDungeonBrick = 41;
    public const ushort GreenDungeonBrick = 43;
    public const ushort PinkDungeonBrick = 44;
    public const ushort Cobweb = 51;
    public const ushort Vines = 52;
    public const ushort Sand = 53;
    public const ushort Signs = 55;
    public const ushort Hellstone = 58;
    public const ushort Mud = 59;
    public const ushort JungleGrass = 60;
    public const ushort JunglePlants = 61;
    public const ushort JungleVines = 62;
    public const ushort Sapphire = 63;
    public const ushort Ruby = 64;
    public const ushort Emerald = 65;
    public const ushort Topaz = 66;
    public const ushort Amethyst = 67;
    public const ushort Diamond = 68;
    public const ushort JungleThorns = 69;
    public const ushort JunglePlants2 = 74;
    public const ushort Cobalt = 107;
    public const ushort Mythril = 108;
    public const ushort Adamantite = 111;
    public const ushort Timer = 144;
    public const ushort SandstoneBrick = 151;
    public const ushort RichMahogany = 158;
    public const ushort Tin = 166;
    public const ushort Lead = 167;
    public const ushort Tungsten = 168;
    public const ushort Platinum = 169;
    public const ushort ExposedGems = 178;
    public const ushort SmallPiles = 185;
    public const ushort LivingWood = 191;
    public const ushort LeafBlock = 192;
    public const ushort Crimtane = 204;
    public const ushort Chlorophyte = 211;
    public const ushort Palladium = 221;
    public const ushort Orichalcum = 222;
    public const ushort Titanium = 223;
    public const ushort Hive = 225;
    public const ushort LihzahrdBrick = 226;
    public const ushort HoneyBlock = 229;
    public const ushort CrispyHoneyBlock = 230;
    public const ushort Larva = 231;
    public const ushort PlantDetritus = 233;
    public const ushort LivingLoom = 304;
    public const ushort MinecartTrack = 314;
    public const ushort CrimsonThorns = 352;
    public const ushort Sandstone = 396;
    public const ushort HardenedSand = 397;
    public const ushort PressureTrack = 428;
    public const ushort LivingMahogany = 383;
    public const ushort LivingMahoganyLeaves = 384;
    public const ushort BeeHive = 444;
    public const ushort PlanteraThorns = 655;
    public const ushort JunglePlantsEcho = 703;
    public const ushort Containers2 = 467;
}
