using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace TerrariaSplit;

internal sealed class TerrariaUiScalePatch
{
    public const int TargetScalePercent = 300;
    private const int ModuleReadChunkSize = 64 * 1024;

    private static readonly UIntPtr MemoryBasicInformationSize =
        (UIntPtr)Marshal.SizeOf<MemoryBasicInformation>();

    private static readonly PatchOperation[] Operations =
    [
        new(
            "mouse slider display range",
            [
                0x7E, 0x14, 0x0D, 0x00, 0x04,
                0x22, 0x00, 0x00, 0x00, 0x3F,
                0x59,
                0x22, 0x00, 0x00, 0xC0, 0x3F,
                0x5B,
                0x22, 0x00, 0x00, 0x00, 0x00,
                0x22, 0x00, 0x00, 0x80, 0x3F,
                0x28, 0xCE, 0x00, 0x00, 0x0A,
                0x16,
                0x14,
                0x28, 0xCC, 0x01, 0x00, 0x06
            ],
            [
                0x7E, 0x14, 0x0D, 0x00, 0x04,
                0x22, 0x00, 0x00, 0x00, 0x3F,
                0x59,
                0x22, 0x00, 0x00, 0x20, 0x40,
                0x5B,
                0x22, 0x00, 0x00, 0x00, 0x00,
                0x22, 0x00, 0x00, 0x80, 0x3F,
                0x28, 0xCE, 0x00, 0x00, 0x0A,
                0x16,
                0x14,
                0x28, 0xCC, 0x01, 0x00, 0x06
            ],
            ReplacementOffset: 12,
            Replacement: [0x00, 0x00, 0x20, 0x40]),
        new(
            "mouse slider assignment range",
            [
                0x11, 0x2C,
                0x22, 0x00, 0x00, 0xC0, 0x3F,
                0x5A,
                0x22, 0x00, 0x00, 0x00, 0x3F,
                0x58,
                0x80, 0x14, 0x0D, 0x00, 0x04
            ],
            [
                0x11, 0x2C,
                0x22, 0x00, 0x00, 0x20, 0x40,
                0x5A,
                0x22, 0x00, 0x00, 0x00, 0x3F,
                0x58,
                0x80, 0x14, 0x0D, 0x00, 0x04
            ],
            ReplacementOffset: 3,
            Replacement: [0x00, 0x00, 0x20, 0x40]),
        new(
            "gamepad slider range",
            [
                0x28, 0xCD, 0x0B, 0x00, 0x06,
                0x22, 0x00, 0x00, 0x00, 0x3F,
                0x22, 0x00, 0x00, 0x00, 0x40,
                0x28, 0x92, 0x16, 0x00, 0x06,
                0x7B, 0x2D, 0x11, 0x00, 0x04,
                0x22, 0x33, 0x33, 0xB3, 0x3E,
                0x28, 0xAE, 0x15, 0x00, 0x06,
                0x28, 0xCF, 0x0B, 0x00, 0x06
            ],
            [
                0x28, 0xCD, 0x0B, 0x00, 0x06,
                0x22, 0x00, 0x00, 0x00, 0x3F,
                0x22, 0x00, 0x00, 0x40, 0x40,
                0x28, 0x92, 0x16, 0x00, 0x06,
                0x7B, 0x2D, 0x11, 0x00, 0x04,
                0x22, 0x33, 0x33, 0xB3, 0x3E,
                0x28, 0xAE, 0x15, 0x00, 0x06,
                0x28, 0xCF, 0x0B, 0x00, 0x06
            ],
            ReplacementOffset: 11,
            Replacement: [0x00, 0x00, 0x40, 0x40])
    ];

    public TerrariaUiScalePatchResult TryApply()
    {
        using Process? process = TerrariaProcessFinder.FindNewest();
        if (process is null)
        {
            return TerrariaUiScalePatchResult.NoProcess();
        }

        try
        {
            using ProcessPatchHandle handle = ProcessPatchHandle.Open(process.Id);
            if (Is64BitProcess(handle.Value))
            {
                return TerrariaUiScalePatchResult.Unsupported(process.Id, "Terraria UI scale patch currently supports x86 Terraria only.");
            }

            ProcessModule? mainModule = process.MainModule;
            if (mainModule is null)
            {
                return TerrariaUiScalePatchResult.Failed(process.Id, "Terraria main module is unavailable.");
            }

            if (!ReadModuleBytes(handle.Value, mainModule, out byte[] moduleBytes, out string failure))
            {
                return TerrariaUiScalePatchResult.Failed(process.Id, failure);
            }

            TerrariaUiScalePatchPlan plan = CreatePlan(moduleBytes, mainModule.BaseAddress);
            if (!plan.CanApply)
            {
                return TerrariaUiScalePatchResult.Failed(process.Id, plan.Message);
            }

            if (plan.AlreadyApplied)
            {
                return TerrariaUiScalePatchResult.AlreadyApplied(process.Id, plan.Message);
            }

            ApplyWrites(handle.Value, plan.Writes);

            return TerrariaUiScalePatchResult.Applied(process.Id, plan.Message);
        }
        catch (Win32Exception ex)
        {
            return TerrariaUiScalePatchResult.Failed(process.Id, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return TerrariaUiScalePatchResult.Failed(process.Id, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return TerrariaUiScalePatchResult.Failed(process.Id, ex.Message);
        }
    }

    internal static TerrariaUiScalePatchPlan CreatePlan(byte[] moduleBytes, IntPtr moduleBaseAddress)
    {
        var writes = new List<PatchWrite>();
        var statuses = new List<string>();
        bool anyAlreadyApplied = false;

        foreach (PatchOperation operation in Operations)
        {
            List<int> originalMatches = FindAll(moduleBytes, operation.OriginalPattern);
            List<int> patchedMatches = FindAll(moduleBytes, operation.PatchedPattern);

            if (originalMatches.Count == 1 && patchedMatches.Count == 0)
            {
                int replacementStart = originalMatches[0] + operation.ReplacementOffset;
                IntPtr address = IntPtr.Add(moduleBaseAddress, replacementStart);
                byte[] originalBytes = moduleBytes
                    .AsSpan(replacementStart, operation.Replacement.Length)
                    .ToArray();
                writes.Add(new PatchWrite(address, operation.Replacement, originalBytes));
                statuses.Add($"{operation.Name}: pending");
                continue;
            }

            if (originalMatches.Count == 0 && patchedMatches.Count == 1)
            {
                anyAlreadyApplied = true;
                statuses.Add($"{operation.Name}: already applied");
                continue;
            }

            string detail = string.Create(
                CultureInfo.InvariantCulture,
                $"{operation.Name}: expected one original or patched signature, found original {originalMatches.Count}, patched {patchedMatches.Count}");
            return TerrariaUiScalePatchPlan.Failed(detail);
        }

        if (writes.Count == 0 && anyAlreadyApplied)
        {
            return TerrariaUiScalePatchPlan.AlreadyAppliedPlan(
                $"Terraria UI scale enhancement is already active up to {TargetScalePercent}%.");
        }

        return TerrariaUiScalePatchPlan.Apply(
            writes,
            $"Terraria UI scale enhancement will be patched to {TargetScalePercent}% ({string.Join("; ", statuses)}).");
    }

    internal static byte[] ApplyToBufferForTest(byte[] moduleBytes)
    {
        byte[] copy = (byte[])moduleBytes.Clone();
        TerrariaUiScalePatchPlan plan = CreatePlan(copy, IntPtr.Zero);
        if (!plan.CanApply)
        {
            throw new InvalidOperationException(plan.Message);
        }

        foreach (PatchWrite write in plan.Writes)
        {
            int offset = write.Address.ToInt32();
            Array.Copy(write.Bytes, 0, copy, offset, write.Bytes.Length);
        }

        return copy;
    }

    private static bool ReadModuleBytes(
        IntPtr processHandle,
        ProcessModule module,
        out byte[] bytes,
        out string failure)
    {
        bytes = [];
        failure = string.Empty;

        if (module.ModuleMemorySize <= 0)
        {
            failure = "Terraria main module has an invalid size.";
            return false;
        }

        bytes = new byte[module.ModuleMemorySize];
        int successfulReads = 0;
        int failedReads = 0;
        long moduleStart = module.BaseAddress.ToInt64();
        long moduleEnd = moduleStart + module.ModuleMemorySize;
        long address = moduleStart;

        while (address < moduleEnd)
        {
            if (NativeMethods.VirtualQueryEx(
                    processHandle,
                    new IntPtr(address),
                    out MemoryBasicInformation info,
                    MemoryBasicInformationSize) == UIntPtr.Zero)
            {
                failedReads++;
                address += Environment.SystemPageSize;
                continue;
            }

            long regionStart = info.BaseAddress.ToInt64();
            long regionSize = unchecked((long)info.RegionSize.ToUInt64());
            if (regionSize <= 0)
            {
                failedReads++;
                address += Environment.SystemPageSize;
                continue;
            }

            long regionEnd = regionStart + regionSize;
            long readStart = Math.Max(moduleStart, regionStart);
            long readEnd = Math.Min(moduleEnd, regionEnd);
            if (readStart < readEnd && IsReadableCommittedPage(info))
            {
                ReadModuleRegion(
                    processHandle,
                    bytes,
                    moduleStart,
                    readStart,
                    readEnd,
                    ref successfulReads,
                    ref failedReads);
            }

            address = Math.Max(address + Environment.SystemPageSize, regionEnd);
        }

        if (successfulReads == 0)
        {
            failure = failedReads == 0
                ? "Terraria main module did not expose readable memory pages."
                : $"Terraria main module read failed across {failedReads} page range(s).";
            return false;
        }

        return true;
    }

    private static void ReadModuleRegion(
        IntPtr processHandle,
        byte[] moduleBytes,
        long moduleStart,
        long readStart,
        long readEnd,
        ref int successfulReads,
        ref int failedReads)
    {
        for (long chunkStart = readStart; chunkStart < readEnd;)
        {
            int chunkLength = (int)Math.Min(ModuleReadChunkSize, readEnd - chunkStart);
            byte[] chunk = new byte[chunkLength];
            if (NativeMethods.ReadProcessMemory(
                    processHandle,
                    new IntPtr(chunkStart),
                    chunk,
                    (UIntPtr)chunk.Length,
                    out UIntPtr bytesRead) &&
                bytesRead == (UIntPtr)chunk.Length)
            {
                Buffer.BlockCopy(chunk, 0, moduleBytes, (int)(chunkStart - moduleStart), chunk.Length);
                successfulReads++;
            }
            else
            {
                failedReads++;
            }

            chunkStart += chunkLength;
        }
    }

    private static bool IsReadableCommittedPage(MemoryBasicInformation info)
    {
        if (info.State != MemoryPageState.Commit)
        {
            return false;
        }

        if ((info.Protect & MemoryPageProtect.PageGuard) != 0 ||
            (info.Protect & MemoryPageProtect.PageNoAccess) != 0)
        {
            return false;
        }

        const MemoryPageProtect readable =
            MemoryPageProtect.PageReadOnly |
            MemoryPageProtect.PageReadWrite |
            MemoryPageProtect.PageWriteCopy |
            MemoryPageProtect.PageExecuteRead |
            MemoryPageProtect.PageExecuteReadWrite |
            MemoryPageProtect.PageExecuteWriteCopy;

        return (info.Protect & readable) != 0;
    }

    private static void ApplyWrites(IntPtr processHandle, IReadOnlyList<PatchWrite> writes)
    {
        var appliedWrites = new List<PatchWrite>();
        try
        {
            foreach (PatchWrite write in writes)
            {
                WriteProtectedBytes(processHandle, write.Address, write.Bytes);
                appliedWrites.Add(write);
            }
        }
        catch
        {
            RollBackWrites(processHandle, appliedWrites);
            throw;
        }
    }

    private static void RollBackWrites(IntPtr processHandle, IReadOnlyList<PatchWrite> appliedWrites)
    {
        for (int index = appliedWrites.Count - 1; index >= 0; index--)
        {
            PatchWrite write = appliedWrites[index];
            try
            {
                WriteProtectedBytes(processHandle, write.Address, write.OriginalBytes);
            }
            catch (Win32Exception ex)
            {
                AppLogger.Error(ex, "Failed to roll back Terraria UI scale patch bytes.");
            }
        }
    }

    private static bool Is64BitProcess(IntPtr processHandle)
    {
        if (!Environment.Is64BitOperatingSystem)
        {
            return false;
        }

        if (!NativeMethods.IsWow64Process(processHandle, out bool isWow64))
        {
            throw new Win32Exception();
        }

        return !isWow64;
    }

    private static void WriteProtectedBytes(IntPtr processHandle, IntPtr address, byte[] bytes)
    {
        UIntPtr size = (UIntPtr)bytes.Length;
        if (!NativeMethods.VirtualProtectEx(
                processHandle,
                address,
                size,
                MemoryPageProtect.PageExecuteReadWrite,
                out MemoryPageProtect oldProtect))
        {
            throw new Win32Exception();
        }

        try
        {
            if (!NativeMethods.WriteProcessMemory(
                    processHandle,
                    address,
                    bytes,
                    size,
                    out UIntPtr bytesWritten) ||
                bytesWritten != size)
            {
                throw new Win32Exception();
            }

            if (!NativeMethods.FlushInstructionCache(processHandle, address, size))
            {
                throw new Win32Exception();
            }
        }
        finally
        {
            NativeMethods.VirtualProtectEx(processHandle, address, size, oldProtect, out _);
        }
    }

    private static List<int> FindAll(byte[] buffer, byte[] pattern)
    {
        var matches = new List<int>();
        if (pattern.Length == 0 || buffer.Length < pattern.Length)
        {
            return matches;
        }

        int lastStart = buffer.Length - pattern.Length;
        byte first = pattern[0];
        for (int start = 0; start <= lastStart; start++)
        {
            if (buffer[start] != first)
            {
                continue;
            }

            if (MatchesAt(buffer, pattern, start))
            {
                matches.Add(start);
            }
        }

        return matches;
    }

    private static bool MatchesAt(byte[] buffer, byte[] pattern, int start)
    {
        for (int index = 0; index < pattern.Length; index++)
        {
            if (buffer[start + index] != pattern[index])
            {
                return false;
            }
        }

        return true;
    }

    private sealed record PatchOperation(
        string Name,
        byte[] OriginalPattern,
        byte[] PatchedPattern,
        int ReplacementOffset,
        byte[] Replacement);

    private sealed class ProcessPatchHandle : IDisposable
    {
        private ProcessPatchHandle(IntPtr value)
        {
            Value = value;
        }

        public IntPtr Value { get; private set; }

        public static ProcessPatchHandle Open(int processId)
        {
            const ProcessAccessRights access =
                ProcessAccessRights.QueryInformation |
                ProcessAccessRights.VirtualMemoryOperation |
                ProcessAccessRights.VirtualMemoryRead |
                ProcessAccessRights.VirtualMemoryWrite;

            IntPtr handle = NativeMethods.OpenProcess(access, inheritHandle: false, processId);
            if (handle == IntPtr.Zero)
            {
                throw new Win32Exception();
            }

            return new ProcessPatchHandle(handle);
        }

        public void Dispose()
        {
            IntPtr handle = Value;
            Value = IntPtr.Zero;
            if (handle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(handle);
            }
        }
    }
}

internal readonly record struct TerrariaUiScalePatchPlan(
    bool CanApply,
    bool AlreadyApplied,
    IReadOnlyList<PatchWrite> Writes,
    string Message)
{
    public static TerrariaUiScalePatchPlan Failed(string message)
    {
        return new TerrariaUiScalePatchPlan(false, false, [], message);
    }

    public static TerrariaUiScalePatchPlan AlreadyAppliedPlan(string message)
    {
        return new TerrariaUiScalePatchPlan(true, true, [], message);
    }

    public static TerrariaUiScalePatchPlan Apply(IReadOnlyList<PatchWrite> writes, string message)
    {
        return new TerrariaUiScalePatchPlan(true, false, writes, message);
    }
}

internal readonly record struct PatchWrite(IntPtr Address, byte[] Bytes, byte[] OriginalBytes);

internal readonly record struct TerrariaUiScalePatchResult(
    TerrariaUiScalePatchStatus Status,
    int? ProcessId,
    string Message)
{
    public bool IsSuccess => Status is TerrariaUiScalePatchStatus.Applied or TerrariaUiScalePatchStatus.AlreadyApplied;

    public static TerrariaUiScalePatchResult NoProcess()
    {
        return new TerrariaUiScalePatchResult(
            TerrariaUiScalePatchStatus.NoProcess,
            null,
            "Terraria process is not running.");
    }

    public static TerrariaUiScalePatchResult Unsupported(int processId, string message)
    {
        return new TerrariaUiScalePatchResult(TerrariaUiScalePatchStatus.Unsupported, processId, message);
    }

    public static TerrariaUiScalePatchResult Failed(int processId, string message)
    {
        return new TerrariaUiScalePatchResult(TerrariaUiScalePatchStatus.Failed, processId, message);
    }

    public static TerrariaUiScalePatchResult Applied(int processId, string message)
    {
        return new TerrariaUiScalePatchResult(TerrariaUiScalePatchStatus.Applied, processId, message);
    }

    public static TerrariaUiScalePatchResult AlreadyApplied(int processId, string message)
    {
        return new TerrariaUiScalePatchResult(TerrariaUiScalePatchStatus.AlreadyApplied, processId, message);
    }
}

internal enum TerrariaUiScalePatchStatus
{
    NoProcess,
    Unsupported,
    Failed,
    Applied,
    AlreadyApplied
}
