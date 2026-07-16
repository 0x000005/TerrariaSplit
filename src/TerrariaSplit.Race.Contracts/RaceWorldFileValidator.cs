namespace TerrariaSplit.Race.Contracts;

public static class RaceWorldFileValidator
{
    private const ulong ReLogicMagic = 27981915666277746UL;
    private const byte WorldFileType = 2;
    private const int MinimumSupportedVersion = 135;
    private const int MaximumReasonableVersion = 1_000;
    private const int MaximumSectionCount = 1_024;
    private const int MaximumWorldNameBytes = 4_096;

    public static bool IsValidWorldFilePath(string? path)
    {
        return TryValidateWorldFile(path, out _);
    }

    public static bool HasWorldFileExtension(string? path)
    {
        return string.Equals(
            Path.GetExtension(path?.Trim() ?? string.Empty),
            ".wld",
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryValidateWorldFile(string? path, out string detail)
    {
        detail = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !HasWorldFileExtension(path) || !File.Exists(path))
        {
            detail = "A valid .wld file is required.";
            return false;
        }

        try
        {
            using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return TryValidateWorldStream(stream, out detail);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            detail = ex.Message;
            return false;
        }
    }

    public static bool TryValidateWorldStream(Stream stream, out string detail)
    {
        detail = string.Empty;
        if (!stream.CanRead || !stream.CanSeek)
        {
            detail = "World stream must be readable and seekable.";
            return false;
        }

        long originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            int version = reader.ReadInt32();
            if (version < MinimumSupportedVersion || version > MaximumReasonableVersion)
            {
                detail = $"Unsupported Terraria world version {version}.";
                return false;
            }

            ulong metadata = reader.ReadUInt64();
            if ((metadata & 0x00FFFFFFFFFFFFFFUL) != ReLogicMagic || (byte)(metadata >> 56) != WorldFileType)
            {
                detail = "The file is not a Terraria world file.";
                return false;
            }

            _ = reader.ReadUInt32();
            _ = reader.ReadUInt64();
            short sectionCount = reader.ReadInt16();
            if (sectionCount < 1 || sectionCount > MaximumSectionCount)
            {
                detail = $"Invalid Terraria world section count {sectionCount}.";
                return false;
            }

            var sectionPointers = new int[sectionCount];
            int previousPointer = 0;
            for (int index = 0; index < sectionPointers.Length; index++)
            {
                int pointer = reader.ReadInt32();
                if (pointer <= 0 || pointer > stream.Length || pointer < previousPointer)
                {
                    detail = "Invalid Terraria world section table.";
                    return false;
                }

                sectionPointers[index] = pointer;
                previousPointer = pointer;
            }

            short frameImportanceCount = reader.ReadInt16();
            if (frameImportanceCount < 0)
            {
                detail = "Invalid Terraria tile frame table.";
                return false;
            }

            int frameImportanceBytes = (frameImportanceCount + 7) / 8;
            if (stream.Position + frameImportanceBytes > stream.Length)
            {
                detail = "Truncated Terraria tile frame table.";
                return false;
            }

            stream.Position += frameImportanceBytes;
            if (sectionPointers[0] < stream.Position || sectionPointers[0] >= stream.Length)
            {
                detail = "Invalid Terraria world header offset.";
                return false;
            }

            stream.Position = sectionPointers[0];
            if (!TryReadBoundedString(reader, MaximumWorldNameBytes, out string worldName) ||
                string.IsNullOrWhiteSpace(worldName))
            {
                detail = "Invalid Terraria world name.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or ArgumentException or InvalidDataException)
        {
            detail = ex.Message;
            return false;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    public static bool TryReadWorldIdentity(
        string? path,
        out RaceWorldIdentity? identity,
        out string detail)
    {
        identity = null;
        detail = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !HasWorldFileExtension(path) || !File.Exists(path))
        {
            detail = "A valid .wld file is required.";
            return false;
        }

        try
        {
            using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return TryReadWorldIdentity(stream, out identity, out detail);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            detail = ex.Message;
            return false;
        }
    }

    public static bool TryReadWorldIdentity(
        Stream stream,
        out RaceWorldIdentity? identity,
        out string detail)
    {
        identity = null;
        detail = string.Empty;
        if (!stream.CanRead || !stream.CanSeek)
        {
            detail = "World stream must be readable and seekable.";
            return false;
        }

        long originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            if (!TrySeekWorldHeader(reader, stream, out int version, out detail))
            {
                return false;
            }

            if (!TryReadBoundedString(reader, MaximumWorldNameBytes, out string worldName) ||
                string.IsNullOrWhiteSpace(worldName))
            {
                detail = "Invalid Terraria world name.";
                return false;
            }

            if (version >= 179)
            {
                if (version == 179)
                {
                    _ = reader.ReadInt32();
                }
                else if (!TryReadBoundedString(reader, MaximumWorldNameBytes, out _))
                {
                    detail = "Invalid Terraria world seed.";
                    return false;
                }

                _ = reader.ReadUInt64();
            }

            if (version < 181)
            {
                detail = $"Terraria world version {version} has no stable unique id.";
                return false;
            }

            byte[] uniqueIdBytes = reader.ReadBytes(16);
            if (uniqueIdBytes.Length != 16)
            {
                detail = "Truncated Terraria world unique id.";
                return false;
            }

            Guid uniqueId = new(uniqueIdBytes);
            int worldId = reader.ReadInt32();
            identity = new RaceWorldIdentity(worldName, worldId, uniqueId);
            return true;
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or ArgumentException or InvalidDataException)
        {
            detail = ex.Message;
            return false;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static bool TrySeekWorldHeader(
        BinaryReader reader,
        Stream stream,
        out int version,
        out string detail)
    {
        detail = string.Empty;
        version = reader.ReadInt32();
        if (version < MinimumSupportedVersion || version > MaximumReasonableVersion)
        {
            detail = $"Unsupported Terraria world version {version}.";
            return false;
        }

        ulong metadata = reader.ReadUInt64();
        if ((metadata & 0x00FFFFFFFFFFFFFFUL) != ReLogicMagic || (byte)(metadata >> 56) != WorldFileType)
        {
            detail = "The file is not a Terraria world file.";
            return false;
        }

        _ = reader.ReadUInt32();
        _ = reader.ReadUInt64();
        short sectionCount = reader.ReadInt16();
        if (sectionCount < 1 || sectionCount > MaximumSectionCount)
        {
            detail = $"Invalid Terraria world section count {sectionCount}.";
            return false;
        }

        int firstSectionPointer = 0;
        int previousPointer = 0;
        for (int index = 0; index < sectionCount; index++)
        {
            int pointer = reader.ReadInt32();
            if (pointer <= 0 || pointer > stream.Length || pointer < previousPointer)
            {
                detail = "Invalid Terraria world section table.";
                return false;
            }

            if (index == 0)
            {
                firstSectionPointer = pointer;
            }

            previousPointer = pointer;
        }

        short frameImportanceCount = reader.ReadInt16();
        if (frameImportanceCount < 0)
        {
            detail = "Invalid Terraria tile frame table.";
            return false;
        }

        int frameImportanceBytes = (frameImportanceCount + 7) / 8;
        if (stream.Position + frameImportanceBytes > stream.Length)
        {
            detail = "Truncated Terraria tile frame table.";
            return false;
        }

        stream.Position += frameImportanceBytes;
        if (firstSectionPointer < stream.Position || firstSectionPointer >= stream.Length)
        {
            detail = "Invalid Terraria world header offset.";
            return false;
        }

        stream.Position = firstSectionPointer;
        return true;
    }

    private static bool TryReadBoundedString(BinaryReader reader, int maximumBytes, out string value)
    {
        value = string.Empty;
        int byteCount = 0;
        int shift = 0;
        for (int index = 0; index < 5; index++)
        {
            byte current = reader.ReadByte();
            byteCount |= (current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                if (byteCount < 0 || byteCount > maximumBytes || reader.BaseStream.Position + byteCount > reader.BaseStream.Length)
                {
                    return false;
                }

                byte[] bytes = reader.ReadBytes(byteCount);
                if (bytes.Length != byteCount)
                {
                    return false;
                }

                value = System.Text.Encoding.UTF8.GetString(bytes);
                return true;
            }

            shift += 7;
        }

        return false;
    }
}
