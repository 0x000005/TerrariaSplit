using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using TerrariaSplit.Domain;
using TerrariaSplit.Models;
using TerrariaSplit.Terraria.Memory;

namespace TerrariaSplit.Tests;

internal static class PerformanceRegressionTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("Process memory primitive reads avoid managed allocations", ProcessMemoryPrimitiveReadsAvoidManagedAllocations);
        yield return ("Process memory primitive failures preserve default outputs", ProcessMemoryPrimitiveFailuresPreserveDefaultOutputs);
        yield return ("Process memory 32-bit pointers are zero extended", ProcessMemory32BitPointersAreZeroExtended);
        yield return ("SplitStatus completed fact merge stays linear", SplitStatusCompletedFactMergeStaysLinear);
    }

    private static void ProcessMemoryPrimitiveReadsAvoidManagedAllocations()
    {
        using Process process = Process.GetCurrentProcess();
        var reader = new ProcessMemoryReader(process);
        IntPtr memory = Marshal.AllocHGlobal(32);
        try
        {
            Marshal.WriteByte(memory, 1);
            Marshal.WriteInt32(memory, 4, 123456789);
            Marshal.Copy(BitConverter.GetBytes(1234.5d), 0, IntPtr.Add(memory, 8), sizeof(double));
            Marshal.WriteIntPtr(IntPtr.Add(memory, 16), new IntPtr(0x123456));

            AssertReads(reader, memory);
            for (int i = 0; i < 32; i++)
            {
                AssertReads(reader, memory);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2_000; i++)
            {
                if (!reader.TryReadBool(memory, out _) ||
                    !reader.TryReadInt32(IntPtr.Add(memory, 4), out _) ||
                    !reader.TryReadDouble(IntPtr.Add(memory, 8), out _) ||
                    !reader.TryReadPointerValue(IntPtr.Add(memory, 16), out _))
                {
                    throw new InvalidOperationException("Primitive process-memory read failed during allocation measurement.");
                }
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (allocated > 32_768)
            {
                throw new InvalidOperationException(
                    $"Primitive process-memory reads allocated {allocated:N0} bytes; expected at most 32,768 bytes.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static void AssertReads(ProcessMemoryReader reader, IntPtr memory)
    {
        if (!reader.TryReadBool(memory, out bool boolValue) || !boolValue)
        {
            throw new InvalidOperationException("Boolean process-memory read did not preserve its value.");
        }

        if (!reader.TryReadInt32(IntPtr.Add(memory, 4), out int intValue) || intValue != 123456789)
        {
            throw new InvalidOperationException("Int32 process-memory read did not preserve its value.");
        }

        if (!reader.TryReadDouble(IntPtr.Add(memory, 8), out double doubleValue) || doubleValue != 1234.5d)
        {
            throw new InvalidOperationException("Double process-memory read did not preserve its value.");
        }

        if (!reader.TryReadPointerValue(IntPtr.Add(memory, 16), out IntPtr pointerValue) ||
            pointerValue != new IntPtr(0x123456))
        {
            throw new InvalidOperationException("Pointer process-memory read did not preserve its value.");
        }
    }

    private static void ProcessMemoryPrimitiveFailuresPreserveDefaultOutputs()
    {
        using Process process = Process.GetCurrentProcess();
        var reader = new ProcessMemoryReader(process);

        if (reader.TryReadBool(IntPtr.Zero, out bool boolValue) || boolValue ||
            reader.TryReadInt32(IntPtr.Zero, out int intValue) || intValue != 0 ||
            reader.TryReadDouble(IntPtr.Zero, out double doubleValue) || doubleValue != 0d ||
            reader.TryReadPointerValue(IntPtr.Zero, out IntPtr pointerValue) || pointerValue != IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed primitive reads must leave their outputs at default values.");
        }

        int pageSize = Environment.SystemPageSize;
        IntPtr region = VirtualAlloc(
            IntPtr.Zero,
            (UIntPtr)(pageSize * 2),
            AllocationTypeCommit | AllocationTypeReserve,
            PageReadWrite);
        if (region == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not allocate guarded pages for partial-read validation.");
        }

        IntPtr secondPage = IntPtr.Add(region, pageSize);
        try
        {
            IntPtr partialValueAddress = IntPtr.Add(region, pageSize - 2);
            Marshal.WriteByte(partialValueAddress, 0, 0x34);
            Marshal.WriteByte(partialValueAddress, 1, 0x12);
            if (!VirtualProtect(secondPage, (UIntPtr)pageSize, PageNoAccess, out uint oldProtection))
            {
                throw new InvalidOperationException("Could not protect the second page for partial-read validation.");
            }

            try
            {
                if (reader.TryReadInt32(partialValueAddress, out int partialValue) || partialValue != 0)
                {
                    throw new InvalidOperationException("A partial Int32 read must fail without exposing partially written output.");
                }
            }
            finally
            {
                _ = VirtualProtect(secondPage, (UIntPtr)pageSize, oldProtection, out _);
            }
        }
        finally
        {
            _ = VirtualFree(region, UIntPtr.Zero, FreeTypeRelease);
        }
    }

    private static void ProcessMemory32BitPointersAreZeroExtended()
    {
        using Process process = Process.GetCurrentProcess();
        var reader = new ProcessMemoryReader(process, is64Bit: false);
        IntPtr memory = Marshal.AllocHGlobal(sizeof(uint));
        try
        {
            const uint expected = 0xF1234567u;
            Marshal.WriteInt32(memory, unchecked((int)expected));
            if (!reader.TryReadPointerValue(memory, out IntPtr pointer) ||
                unchecked((ulong)pointer.ToInt64()) != expected)
            {
                throw new InvalidOperationException("A 32-bit process pointer must be read as an unsigned, zero-extended value.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static void SplitStatusCompletedFactMergeStaysLinear()
    {
        MethodInfo merge = typeof(SplitStatus).GetMethod(
                "MergeCompletedFactKeys",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing SplitStatus completed-fact merge method.");
        var definition = new SplitDefinition(
            "split:fact-merge-performance",
            "Fact merge performance",
            SplitCondition.Fact("fact:placeholder"),
            [],
            [],
            []);
        var semanticStatus = new SplitStatus(definition);
        semanticStatus.ApplyState(new SplitStatusState(
            TimeSpan.Zero,
            IsSkipped: false,
            ["fact:a", "FACT:A", string.Empty, "   ", "fact:b"]));
        merge.Invoke(semanticStatus, [new[] { "FACT:B", "fact:c", "FACT:C" }]);
        string[] expected = ["fact:a", "fact:b", "fact:c"];
        if (!semanticStatus.CompletedFactKeys.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Completed fact keys did not preserve first-seen order and case-insensitive uniqueness.");
        }

        semanticStatus.ApplyState(new SplitStatusState(
            TimeSpan.Zero,
            IsSkipped: false,
            semanticStatus.CompletedFactKeys));
        if (!semanticStatus.CompletedFactKeys.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Applying state from the current completed-fact view must preserve its keys.");
        }

        string[] keys = Enumerable.Range(0, 5_000).Select(index => $"fact:{index}").ToArray();
        var performanceStatus = new SplitStatus(definition);
        performanceStatus.ApplyState(new SplitStatusState(TimeSpan.Zero, IsSkipped: false, keys));
        merge.Invoke(performanceStatus, [keys]);

        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int iteration = 0; iteration < 20; iteration++)
        {
            merge.Invoke(performanceStatus, [keys]);
        }

        stopwatch.Stop();
        if (stopwatch.Elapsed > TimeSpan.FromSeconds(2))
        {
            throw new InvalidOperationException(
                $"Merging 5,000 existing fact keys took {stopwatch.Elapsed.TotalMilliseconds:N1}ms; expected linear-time lookup.");
        }

        string[][] incrementalBatches = Enumerable.Range(0, 5_000)
            .Select(index => new[] { $"incremental:{index}" })
            .ToArray();
        var incrementalStatus = new SplitStatus(definition);
        merge.Invoke(incrementalStatus, [Array.Empty<string>()]);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        foreach (string[] batch in incrementalBatches)
        {
            merge.Invoke(incrementalStatus, [batch]);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        if (incrementalStatus.CompletedFactKeys.Count != incrementalBatches.Length)
        {
            throw new InvalidOperationException("Incremental completed fact keys were not all retained.");
        }

        if (allocated > 16 * 1024 * 1024)
        {
            throw new InvalidOperationException(
                $"Incrementally merging 5,000 fact keys allocated {allocated:N0} bytes; expected linear storage growth.");
        }
    }

    private const uint AllocationTypeCommit = 0x1000;
    private const uint AllocationTypeReserve = 0x2000;
    private const uint FreeTypeRelease = 0x8000;
    private const uint PageNoAccess = 0x01;
    private const uint PageReadWrite = 0x04;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(
        IntPtr address,
        UIntPtr size,
        uint allocationType,
        uint protection);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualProtect(
        IntPtr address,
        UIntPtr size,
        uint newProtection,
        out uint oldProtection);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);
}
