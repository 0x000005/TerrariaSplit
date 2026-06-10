using System.Globalization;

namespace WorldGenSim;

internal sealed class WorldFileMetadataReader
{
    private const ulong ReLogicMagic = 27981915666277746UL;
    private const byte WorldFileType = 2;

    public bool TryRead(string worldPath, out WorldSeedMetadata metadata, out string detail)
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
            metadata = ReadHeaderMetadata(reader, version);
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

    private static bool TryReadSectionPointers(
        BinaryReader reader,
        out int version,
        out int[] sectionPointers,
        out string detail)
    {
        sectionPointers = [];
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

    private static WorldSeedMetadata ReadHeaderMetadata(BinaryReader reader, int version)
    {
        _ = reader.ReadString();
        string seedText = string.Empty;
        if (version >= 179)
        {
            seedText = version == 179
                ? reader.ReadInt32().ToString(CultureInfo.InvariantCulture)
                : reader.ReadString();

            _ = reader.ReadUInt64();
        }

        if (version >= 181)
        {
            _ = reader.ReadBytes(16);
        }

        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
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
            if (version >= 112) expert = reader.ReadBoolean();
            if (version == 208) master = reader.ReadBoolean();
            gameMode = master ? 2 : expert ? 1 : 0;
        }

        if (version >= 141) _ = reader.ReadInt64();
        if (version >= 284) _ = reader.ReadInt64();

        _ = reader.ReadByte();
        for (int i = 0; i < 19; i++)
        {
            _ = reader.ReadInt32();
        }

        _ = reader.ReadDouble();
        _ = reader.ReadDouble();
        _ = reader.ReadDouble();
        _ = reader.ReadBoolean();
        _ = reader.ReadInt32();
        _ = reader.ReadBoolean();
        _ = reader.ReadBoolean();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        bool hasCrimson = reader.ReadBoolean();

        return new WorldSeedMetadata(
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
}

internal readonly record struct WorldSeedMetadata(
    string SeedText,
    int SizeCode,
    int DifficultyCode,
    bool HasCrimson,
    int SpecialSeedMask);
