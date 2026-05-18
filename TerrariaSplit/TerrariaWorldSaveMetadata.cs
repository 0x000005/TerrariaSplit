using System.Text;

namespace TerrariaSplit;

internal enum TerrariaWorldGameMode
{
    Classic = 0,
    Expert = 1,
    Master = 2,
    Journey = 3
}

internal static class TerrariaWorldSaveMetadata
{
    private const uint ModernWorldVersion = 88;
    private const uint DesktopHeaderVersion = 140;
    private const int WorldFileType = 2;
    private const string DesktopHeader = "relogic";
    private const string ChineseHeader = "xindong";

    public static bool TryReadGameMode(string path, out TerrariaWorldGameMode gameMode)
    {
        gameMode = TerrariaWorldGameMode.Classic;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);
            uint version = reader.ReadUInt32();
            if (version < ModernWorldVersion)
            {
                return true;
            }

            if (!TryReadModernHeader(reader, version, out int headerOffset))
            {
                return false;
            }

            reader.BaseStream.Position = headerOffset;
            _ = reader.ReadString();
            if (version >= 179)
            {
                if (version == 179)
                {
                    _ = reader.ReadInt32();
                }
                else
                {
                    _ = reader.ReadString();
                }

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
            _ = reader.ReadInt32();
            _ = reader.ReadInt32();

            int rawGameMode = 0;
            if (version >= 209)
            {
                rawGameMode = reader.ReadInt32();
            }
            else if (version == 208)
            {
                rawGameMode = reader.ReadBoolean() ? 2 : 0;
            }
            else if (version >= 112)
            {
                rawGameMode = reader.ReadBoolean() ? 1 : 0;
            }

            if (!Enum.IsDefined(typeof(TerrariaWorldGameMode), rawGameMode))
            {
                return false;
            }

            gameMode = (TerrariaWorldGameMode)rawGameMode;
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Failed to read Terraria world metadata: {path}");
            return false;
        }
    }

    public static bool HasSameJourneyCompatibility(TerrariaWorldGameMode left, TerrariaWorldGameMode right)
    {
        return IsJourney(left) == IsJourney(right);
    }

    private static bool TryReadModernHeader(BinaryReader reader, uint version, out int headerOffset)
    {
        headerOffset = 0;
        if (version >= DesktopHeaderVersion)
        {
            string magic = Encoding.UTF8.GetString(reader.ReadBytes(7));
            if (!string.Equals(magic, DesktopHeader, StringComparison.Ordinal) &&
                !string.Equals(magic, ChineseHeader, StringComparison.Ordinal))
            {
                return false;
            }

            if (reader.ReadByte() != WorldFileType)
            {
                return false;
            }

            _ = reader.ReadUInt32();
            _ = reader.ReadUInt64();
        }

        short sectionCount = reader.ReadInt16();
        if (sectionCount <= 0)
        {
            return false;
        }

        headerOffset = reader.ReadInt32();
        return headerOffset > 0 && headerOffset < reader.BaseStream.Length;
    }

    private static bool IsJourney(TerrariaWorldGameMode gameMode)
    {
        return gameMode == TerrariaWorldGameMode.Journey;
    }
}
