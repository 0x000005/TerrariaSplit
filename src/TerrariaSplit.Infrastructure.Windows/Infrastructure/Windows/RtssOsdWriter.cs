using System.IO.MemoryMappedFiles;
using System.Text;

namespace TerrariaSplit.Infrastructure.Windows;

public sealed class RtssOsdWriter : IDisposable
{
    private const string DefaultMappingName = "RTSSSharedMemoryV2";
    private const string DefaultOwner = "TerrariaSplit";
    private const uint RtssSignature = 0x52545353;
    private const uint MinimumSupportedVersion = 0x00020000;
    private const uint ExtendedTextVersion = 0x00020007;
    private const uint BusyFlagVersion = 0x0002000E;
    private const int HeaderSignatureOffset = 0;
    private const int HeaderVersionOffset = 4;
    private const int HeaderAppEntrySizeOffset = 8;
    private const int HeaderAppArrayOffsetOffset = 12;
    private const int HeaderAppArraySizeOffset = 16;
    private const int HeaderOsdEntrySizeOffset = 20;
    private const int HeaderOsdArrayOffsetOffset = 24;
    private const int HeaderOsdArraySizeOffset = 28;
    private const int HeaderOsdFrameOffset = 32;
    private const int HeaderBusyOffset = 36;
    private const int OsdTextOffset = 0;
    private const int OsdTextLength = 256;
    private const int OsdOwnerOffset = 256;
    private const int OsdOwnerLength = 256;
    private const int OsdExtendedTextOffset = 512;
    private const int OsdExtendedTextLength = 4096;
    private const int AppProcessIdOffset = 0;
    private const int AppNameOffset = 4;
    private const int AppNameLength = 260;
    private const int AppOsdXOffset = 316;
    private const int AppOsdYOffset = 320;
    private const int AppOsdPixelOffset = 324;
    private const int AppOsdColorOffset = 328;
    private const int AppOsdFrameOffset = 332;
    private const int FirstThirdPartySlotIndex = 1;
    private static readonly Encoding TextEncoding = Encoding.UTF8;
    private static readonly Encoding OwnerEncoding = Encoding.ASCII;

    private readonly string mappingName;
    private readonly string owner;
    private bool disposed;

    public RtssOsdWriter(string mappingName = DefaultMappingName, string owner = DefaultOwner)
    {
        this.mappingName = string.IsNullOrWhiteSpace(mappingName) ? DefaultMappingName : mappingName;
        this.owner = string.IsNullOrWhiteSpace(owner) ? DefaultOwner : owner.Trim();
    }

    public RtssOsdUpdateResult TryUpdate(string text)
    {
        if (disposed)
        {
            return RtssOsdUpdateResult.Failed("RTSS writer has been disposed.");
        }

        return TryWithMemory(accessor =>
        {
            if (!TryReadHeader(accessor, out RtssSharedMemoryHeader header, out RtssOsdUpdateResult error))
            {
                return error;
            }

            if (!TryFindOrCaptureSlot(accessor, header, out long slotOffset))
            {
                return RtssOsdUpdateResult.NoFreeSlot();
            }

            if (!TryBeginWrite(accessor, header))
            {
                return RtssOsdUpdateResult.Busy();
            }

            try
            {
                WriteFixedString(accessor, slotOffset + OsdOwnerOffset, OsdOwnerLength, owner, OwnerEncoding);
                if (header.Version >= ExtendedTextVersion &&
                    header.EntrySize >= OsdExtendedTextOffset + OsdExtendedTextLength)
                {
                    WriteFixedString(accessor, slotOffset + OsdTextOffset, OsdTextLength, string.Empty, TextEncoding);
                    WriteFixedString(accessor, slotOffset + OsdExtendedTextOffset, OsdExtendedTextLength, text, TextEncoding);
                }
                else
                {
                    WriteFixedString(accessor, slotOffset + OsdTextOffset, OsdTextLength, text, TextEncoding);
                }

                IncrementOsdFrame(accessor);
                return RtssOsdUpdateResult.Updated();
            }
            finally
            {
                EndWrite(accessor, header);
            }
        });
    }

    public RtssOsdUpdateResult Clear()
    {
        if (disposed)
        {
            return RtssOsdUpdateResult.Failed("RTSS writer has been disposed.");
        }

        return TryWithMemory(accessor =>
        {
            if (!TryReadHeader(accessor, out RtssSharedMemoryHeader header, out RtssOsdUpdateResult error))
            {
                return error;
            }

            bool cleared = false;
            foreach (long slotOffset in EnumerateOwnedSlots(accessor, header))
            {
                WriteFixedString(accessor, slotOffset + OsdTextOffset, OsdTextLength, string.Empty, TextEncoding);
                WriteFixedString(accessor, slotOffset + OsdOwnerOffset, OsdOwnerLength, string.Empty, OwnerEncoding);
                if (header.Version >= ExtendedTextVersion &&
                    header.EntrySize >= OsdExtendedTextOffset + OsdExtendedTextLength)
                {
                    WriteFixedString(accessor, slotOffset + OsdExtendedTextOffset, OsdExtendedTextLength, string.Empty, TextEncoding);
                }

                cleared = true;
            }

            if (cleared)
            {
                IncrementOsdFrame(accessor);
            }

            return RtssOsdUpdateResult.Updated();
        });
    }

    public RtssTargetProcessResult TryGetTargetProcess(string processName)
    {
        if (disposed)
        {
            return RtssTargetProcessResult.Failed("RTSS writer has been disposed.");
        }

        string normalizedProcessName = NormalizeProcessName(processName);
        return TryWithTargetMemory(accessor =>
        {
            if (!TryReadHeader(accessor, out RtssSharedMemoryHeader header, out RtssOsdUpdateResult error))
            {
                return RtssTargetProcessResult.FromUpdateError(error);
            }

            if (!ValidateAppArray(accessor, header) ||
                header.AppEntrySize < AppNameOffset + AppNameLength)
            {
                return RtssTargetProcessResult.InvalidSharedMemory();
            }

            if (TryFindAppEntryOffset(accessor, header, normalizedProcessName, out long appOffset))
            {
                uint processId = accessor.ReadUInt32(appOffset + AppProcessIdOffset);
                string name = Path.GetFileName(ReadFixedString(
                    accessor,
                    appOffset + AppNameOffset,
                    AppNameLength,
                    OwnerEncoding));
                return RtssTargetProcessResult.Found(unchecked((int)processId), name);
            }

            return RtssTargetProcessResult.NotFound(normalizedProcessName);
        });
    }

    public RtssOsdUpdateResult TryUpdateTargetStyle(string processName, RtssOsdStyle style)
    {
        if (disposed)
        {
            return RtssOsdUpdateResult.Failed("RTSS writer has been disposed.");
        }

        string normalizedProcessName = NormalizeProcessName(processName);
        return TryWithMemory(accessor =>
        {
            if (!TryReadHeader(accessor, out RtssSharedMemoryHeader header, out RtssOsdUpdateResult error))
            {
                return error;
            }

            if (!ValidateAppArray(accessor, header) ||
                header.AppEntrySize < AppOsdFrameOffset + sizeof(uint))
            {
                return RtssOsdUpdateResult.InvalidSharedMemory();
            }

            if (!TryFindAppEntryOffset(accessor, header, normalizedProcessName, out long appOffset))
            {
                return RtssOsdUpdateResult.Updated();
            }

            if (!TryBeginWrite(accessor, header))
            {
                return RtssOsdUpdateResult.Busy();
            }

            try
            {
                accessor.Write(appOffset + AppOsdXOffset, unchecked((uint)style.X));
                accessor.Write(appOffset + AppOsdYOffset, unchecked((uint)style.Y));
                accessor.Write(appOffset + AppOsdPixelOffset, (uint)Math.Clamp(style.PixelZoom, 1, 8));
                accessor.Write(appOffset + AppOsdColorOffset, (uint)(style.RgbColor & 0x00FFFFFF));
                IncrementOsdFrame(accessor);
                return RtssOsdUpdateResult.Updated();
            }
            finally
            {
                EndWrite(accessor, header);
            }
        });
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        TryWithMemory(accessor =>
        {
            if (!TryReadHeader(accessor, out RtssSharedMemoryHeader header, out _))
            {
                return RtssOsdUpdateResult.InvalidSharedMemory();
            }

            foreach (long slotOffset in EnumerateOwnedSlots(accessor, header))
            {
                WriteFixedString(accessor, slotOffset + OsdTextOffset, OsdTextLength, string.Empty, TextEncoding);
                WriteFixedString(accessor, slotOffset + OsdOwnerOffset, OsdOwnerLength, string.Empty, OwnerEncoding);
                if (header.Version >= ExtendedTextVersion &&
                    header.EntrySize >= OsdExtendedTextOffset + OsdExtendedTextLength)
                {
                    WriteFixedString(accessor, slotOffset + OsdExtendedTextOffset, OsdExtendedTextLength, string.Empty, TextEncoding);
                }
            }

            IncrementOsdFrame(accessor);
            return RtssOsdUpdateResult.Updated();
        });
    }

    private RtssOsdUpdateResult TryWithMemory(Func<MemoryMappedViewAccessor, RtssOsdUpdateResult> action)
    {
        try
        {
            using MemoryMappedFile memory = MemoryMappedFile.OpenExisting(mappingName, MemoryMappedFileRights.ReadWrite);
            using MemoryMappedViewAccessor accessor = memory.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
            return action(accessor);
        }
        catch (FileNotFoundException)
        {
            return RtssOsdUpdateResult.MissingSharedMemory();
        }
        catch (UnauthorizedAccessException ex)
        {
            return RtssOsdUpdateResult.AccessDenied(ex.Message);
        }
        catch (IOException ex)
        {
            return RtssOsdUpdateResult.Failed(ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return RtssOsdUpdateResult.Failed(ex.Message);
        }
    }

    private RtssTargetProcessResult TryWithTargetMemory(Func<MemoryMappedViewAccessor, RtssTargetProcessResult> action)
    {
        try
        {
            using MemoryMappedFile memory = MemoryMappedFile.OpenExisting(mappingName, MemoryMappedFileRights.Read);
            using MemoryMappedViewAccessor accessor = memory.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            return action(accessor);
        }
        catch (FileNotFoundException)
        {
            return RtssTargetProcessResult.MissingSharedMemory();
        }
        catch (UnauthorizedAccessException ex)
        {
            return RtssTargetProcessResult.AccessDenied(ex.Message);
        }
        catch (IOException ex)
        {
            return RtssTargetProcessResult.Failed(ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return RtssTargetProcessResult.Failed(ex.Message);
        }
    }

    private static bool TryReadHeader(
        MemoryMappedViewAccessor accessor,
        out RtssSharedMemoryHeader header,
        out RtssOsdUpdateResult error)
    {
        header = default;
        if (accessor.Capacity < HeaderBusyOffset + sizeof(int))
        {
            error = RtssOsdUpdateResult.InvalidSharedMemory();
            return false;
        }

        uint signature = accessor.ReadUInt32(HeaderSignatureOffset);
        uint version = accessor.ReadUInt32(HeaderVersionOffset);
        if (signature != RtssSignature || version < MinimumSupportedVersion)
        {
            error = RtssOsdUpdateResult.InvalidSharedMemory();
            return false;
        }

        int appEntrySize = accessor.ReadInt32(HeaderAppEntrySizeOffset);
        int appArrayOffset = accessor.ReadInt32(HeaderAppArrayOffsetOffset);
        int appArraySize = accessor.ReadInt32(HeaderAppArraySizeOffset);
        int osdEntrySize = accessor.ReadInt32(HeaderOsdEntrySizeOffset);
        int osdArrayOffset = accessor.ReadInt32(HeaderOsdArrayOffsetOffset);
        int osdArraySize = accessor.ReadInt32(HeaderOsdArraySizeOffset);
        if (osdEntrySize < OsdOwnerOffset + OsdOwnerLength ||
            osdArrayOffset < 0 ||
            osdArraySize <= FirstThirdPartySlotIndex ||
            (long)osdArrayOffset + (long)osdArraySize * osdEntrySize > accessor.Capacity)
        {
            error = RtssOsdUpdateResult.InvalidSharedMemory();
            return false;
        }

        header = new RtssSharedMemoryHeader(
            version,
            appEntrySize,
            appArrayOffset,
            appArraySize,
            osdEntrySize,
            osdArrayOffset,
            osdArraySize);
        error = RtssOsdUpdateResult.Updated();
        return true;
    }

    private bool TryFindOrCaptureSlot(
        MemoryMappedViewAccessor accessor,
        RtssSharedMemoryHeader header,
        out long slotOffset)
    {
        slotOffset = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int slot = FirstThirdPartySlotIndex; slot < header.ArraySize; slot++)
            {
                long offset = header.ArrayOffset + (long)slot * header.EntrySize;
                string slotOwner = ReadFixedString(accessor, offset + OsdOwnerOffset, OsdOwnerLength, OwnerEncoding);
                if (pass == 0)
                {
                    if (string.Equals(slotOwner, owner, StringComparison.Ordinal))
                    {
                        slotOffset = offset;
                        return true;
                    }

                    continue;
                }

                if (slotOwner.Length == 0)
                {
                    WriteFixedString(accessor, offset + OsdOwnerOffset, OsdOwnerLength, owner, OwnerEncoding);
                    slotOffset = offset;
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerable<long> EnumerateOwnedSlots(MemoryMappedViewAccessor accessor, RtssSharedMemoryHeader header)
    {
        for (int slot = FirstThirdPartySlotIndex; slot < header.ArraySize; slot++)
        {
            long offset = header.ArrayOffset + (long)slot * header.EntrySize;
            string slotOwner = ReadFixedString(accessor, offset + OsdOwnerOffset, OsdOwnerLength, OwnerEncoding);
            if (string.Equals(slotOwner, owner, StringComparison.Ordinal))
            {
                yield return offset;
            }
        }
    }

    private static bool TryBeginWrite(MemoryMappedViewAccessor accessor, RtssSharedMemoryHeader header)
    {
        if (header.Version < BusyFlagVersion)
        {
            return true;
        }

        int busy = accessor.ReadInt32(HeaderBusyOffset);
        if ((busy & 1) != 0)
        {
            return false;
        }

        accessor.Write(HeaderBusyOffset, busy | 1);
        return true;
    }

    private static void EndWrite(MemoryMappedViewAccessor accessor, RtssSharedMemoryHeader header)
    {
        if (header.Version >= BusyFlagVersion)
        {
            accessor.Write(HeaderBusyOffset, 0);
        }
    }

    private static void IncrementOsdFrame(MemoryMappedViewAccessor accessor)
    {
        uint frame = accessor.ReadUInt32(HeaderOsdFrameOffset);
        accessor.Write(HeaderOsdFrameOffset, unchecked(frame + 1));
    }

    private static string ReadFixedString(
        MemoryMappedViewAccessor accessor,
        long offset,
        int length,
        Encoding encoding)
    {
        byte[] bytes = new byte[length];
        accessor.ReadArray(offset, bytes, 0, bytes.Length);
        int count = Array.IndexOf(bytes, (byte)0);
        if (count < 0)
        {
            count = bytes.Length;
        }

        return encoding.GetString(bytes, 0, count);
    }

    private static bool ValidateAppArray(MemoryMappedViewAccessor accessor, RtssSharedMemoryHeader header)
    {
        return header.AppEntrySize > 0 &&
            header.AppArrayOffset >= 0 &&
            header.AppArraySize > 0 &&
            (long)header.AppArrayOffset + (long)header.AppArraySize * header.AppEntrySize <= accessor.Capacity;
    }

    private static bool TryFindAppEntryOffset(
        MemoryMappedViewAccessor accessor,
        RtssSharedMemoryHeader header,
        string normalizedProcessName,
        out long appOffset)
    {
        appOffset = 0;
        if (header.AppEntrySize < AppNameOffset + AppNameLength)
        {
            return false;
        }

        for (int slot = 0; slot < header.AppArraySize; slot++)
        {
            long offset = header.AppArrayOffset + (long)slot * header.AppEntrySize;
            uint processId = accessor.ReadUInt32(offset + AppProcessIdOffset);
            if (processId == 0)
            {
                continue;
            }

            string name = Path.GetFileName(ReadFixedString(
                accessor,
                offset + AppNameOffset,
                AppNameLength,
                OwnerEncoding));
            if (string.Equals(name, normalizedProcessName, StringComparison.OrdinalIgnoreCase))
            {
                appOffset = offset;
                return true;
            }
        }

        return false;
    }

    private static void WriteFixedString(
        MemoryMappedViewAccessor accessor,
        long offset,
        int length,
        string text,
        Encoding encoding)
    {
        byte[] zeros = new byte[length];
        accessor.WriteArray(offset, zeros, 0, zeros.Length);
        byte[] bytes = GetTruncatedBytes(text, Math.Max(0, length - 1), encoding);
        if (bytes.Length > 0)
        {
            accessor.WriteArray(offset, bytes, 0, bytes.Length);
        }
    }

    private static byte[] GetTruncatedBytes(string text, int maxBytes, Encoding encoding)
    {
        if (maxBytes <= 0 || string.IsNullOrEmpty(text))
        {
            return [];
        }

        var bytes = new List<byte>(Math.Min(maxBytes, text.Length));
        foreach (Rune rune in text.EnumerateRunes())
        {
            string value = rune.ToString();
            int byteCount = encoding.GetByteCount(value);
            if (bytes.Count + byteCount > maxBytes)
            {
                break;
            }

            bytes.AddRange(encoding.GetBytes(value));
        }

        return bytes.ToArray();
    }

    private static string NormalizeProcessName(string processName)
    {
        string value = Path.GetFileName(processName?.Trim() ?? string.Empty);
        return string.IsNullOrWhiteSpace(value) ? "Terraria.exe" : value;
    }

    private readonly record struct RtssSharedMemoryHeader(
        uint Version,
        int AppEntrySize,
        int AppArrayOffset,
        int AppArraySize,
        int EntrySize,
        int ArrayOffset,
        int ArraySize);
}

public readonly record struct RtssOsdStyle(
    int X,
    int Y,
    int PixelZoom,
    int RgbColor);

public enum RtssOsdUpdateStatus
{
    Updated,
    MissingSharedMemory,
    InvalidSharedMemory,
    Busy,
    NoFreeSlot,
    AccessDenied,
    Failed
}

public readonly record struct RtssOsdUpdateResult(
    RtssOsdUpdateStatus Status,
    string Message)
{
    public bool Success => Status == RtssOsdUpdateStatus.Updated;

    public static RtssOsdUpdateResult Updated()
    {
        return new RtssOsdUpdateResult(RtssOsdUpdateStatus.Updated, string.Empty);
    }

    public static RtssOsdUpdateResult MissingSharedMemory()
    {
        return new RtssOsdUpdateResult(RtssOsdUpdateStatus.MissingSharedMemory, "RTSS shared memory is not available.");
    }

    public static RtssOsdUpdateResult InvalidSharedMemory()
    {
        return new RtssOsdUpdateResult(RtssOsdUpdateStatus.InvalidSharedMemory, "RTSS shared memory has an unsupported layout.");
    }

    public static RtssOsdUpdateResult Busy()
    {
        return new RtssOsdUpdateResult(RtssOsdUpdateStatus.Busy, "RTSS OSD is busy.");
    }

    public static RtssOsdUpdateResult NoFreeSlot()
    {
        return new RtssOsdUpdateResult(RtssOsdUpdateStatus.NoFreeSlot, "No free RTSS OSD slot is available.");
    }

    public static RtssOsdUpdateResult AccessDenied(string message)
    {
        return new RtssOsdUpdateResult(RtssOsdUpdateStatus.AccessDenied, message);
    }

    public static RtssOsdUpdateResult Failed(string message)
    {
        return new RtssOsdUpdateResult(RtssOsdUpdateStatus.Failed, message);
    }
}

public enum RtssTargetProcessStatus
{
    Found,
    NotFound,
    MissingSharedMemory,
    InvalidSharedMemory,
    AccessDenied,
    Failed
}

public readonly record struct RtssTargetProcessResult(
    RtssTargetProcessStatus Status,
    int ProcessId,
    string ProcessName,
    string Message)
{
    public bool IsFound => Status == RtssTargetProcessStatus.Found;

    public static RtssTargetProcessResult Found(int processId, string processName)
    {
        return new RtssTargetProcessResult(
            RtssTargetProcessStatus.Found,
            processId,
            processName,
            string.Empty);
    }

    public static RtssTargetProcessResult NotFound(string processName)
    {
        return new RtssTargetProcessResult(
            RtssTargetProcessStatus.NotFound,
            0,
            processName,
            "RTSS has not hooked the target process.");
    }

    public static RtssTargetProcessResult MissingSharedMemory()
    {
        return new RtssTargetProcessResult(
            RtssTargetProcessStatus.MissingSharedMemory,
            0,
            string.Empty,
            "RTSS shared memory is not available.");
    }

    public static RtssTargetProcessResult InvalidSharedMemory()
    {
        return new RtssTargetProcessResult(
            RtssTargetProcessStatus.InvalidSharedMemory,
            0,
            string.Empty,
            "RTSS shared memory has an unsupported layout.");
    }

    public static RtssTargetProcessResult AccessDenied(string message)
    {
        return new RtssTargetProcessResult(
            RtssTargetProcessStatus.AccessDenied,
            0,
            string.Empty,
            message);
    }

    public static RtssTargetProcessResult Failed(string message)
    {
        return new RtssTargetProcessResult(
            RtssTargetProcessStatus.Failed,
            0,
            string.Empty,
            message);
    }

    public static RtssTargetProcessResult FromUpdateError(RtssOsdUpdateResult result)
    {
        return result.Status switch
        {
            RtssOsdUpdateStatus.MissingSharedMemory => MissingSharedMemory(),
            RtssOsdUpdateStatus.InvalidSharedMemory => InvalidSharedMemory(),
            RtssOsdUpdateStatus.AccessDenied => AccessDenied(result.Message),
            _ => Failed(result.Message)
        };
    }
}
