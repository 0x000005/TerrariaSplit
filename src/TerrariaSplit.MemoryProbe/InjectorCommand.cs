using System.ComponentModel;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace TerrariaSplit.MemoryProbe;

internal static class InjectorCommand
{
    private const int MappingCapacity = 65_536;

    internal static int Run(string[] args)
    {
        if (args.Length != 3 ||
            !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out int processId))
        {
            Console.Error.WriteLine("Usage: TerrariaSplit.MemoryBridge inject <Terraria pid> <bootstrap dll> <command>");
            return 2;
        }

        if (Environment.Is64BitProcess)
        {
            Console.Error.WriteLine("The memory bridge must run as x86.");
            return 3;
        }

        string bootstrapPath = Path.GetFullPath(args[1]);
        if (!File.Exists(bootstrapPath))
        {
            Console.Error.WriteLine("Injector bootstrap DLL not found: " + bootstrapPath);
            return 4;
        }

        byte[] commandBytes = Encoding.Unicode.GetBytes(args[2]);
        if (commandBytes.Length == 0 || commandBytes.Length > MappingCapacity - sizeof(int) - sizeof(char))
        {
            Console.Error.WriteLine("Injector command is empty or too large.");
            return 5;
        }

        string prefix = "Local\\TerrariaSplit.WorldGuard";
        string eventName = $"{prefix}.Completed.{processId}";
        string commandMappingName = $"{prefix}.Command.{processId}";
        string resultMappingName = $"{prefix}.Result.{processId}";
        using var completed = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
        using MemoryMappedFile commandMapping = MemoryMappedFile.CreateOrOpen(
            commandMappingName,
            MappingCapacity,
            MemoryMappedFileAccess.ReadWrite);
        using MemoryMappedViewAccessor commandView = commandMapping.CreateViewAccessor(
            0,
            MappingCapacity,
            MemoryMappedFileAccess.ReadWrite);
        commandView.Write(0, commandBytes.Length);
        commandView.WriteArray(sizeof(int), commandBytes, 0, commandBytes.Length);
        commandView.Write(sizeof(int) + commandBytes.Length, (char)0);

        using MemoryMappedFile resultMapping = MemoryMappedFile.CreateOrOpen(
            resultMappingName,
            8,
            MemoryMappedFileAccess.ReadWrite);
        using MemoryMappedViewAccessor resultView = resultMapping.CreateViewAccessor(
            0,
            8,
            MemoryMappedFileAccess.ReadWrite);
        resultView.Write(0, unchecked((int)0xFFFFFFFF));
        resultView.Write(4, unchecked((int)0xFFFFFFFF));

        IntPtr process = InjectorNative.OpenProcess(
            InjectorNative.ProcessAccess.CreateThread |
            InjectorNative.ProcessAccess.QueryInformation |
            InjectorNative.ProcessAccess.VirtualMemoryOperation |
            InjectorNative.ProcessAccess.VirtualMemoryWrite |
            InjectorNative.ProcessAccess.VirtualMemoryRead,
            false,
            processId);
        if (process == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcess failed.");
        }

        IntPtr remotePath = IntPtr.Zero;
        IntPtr remoteThread = IntPtr.Zero;
        bool remoteThreadCompleted = false;
        try
        {
            byte[] pathBytes = Encoding.Unicode.GetBytes(bootstrapPath + '\0');
            remotePath = InjectorNative.VirtualAllocEx(
                process,
                IntPtr.Zero,
                (nuint)pathBytes.Length,
                InjectorNative.AllocationType.Commit | InjectorNative.AllocationType.Reserve,
                InjectorNative.MemoryProtection.ReadWrite);
            if (remotePath == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualAllocEx failed.");
            }

            if (!InjectorNative.WriteProcessMemory(
                    process,
                    remotePath,
                    pathBytes,
                    (nuint)pathBytes.Length,
                    out nuint written) ||
                written != (nuint)pathBytes.Length)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "WriteProcessMemory failed.");
            }

            IntPtr kernel32 = InjectorNative.GetModuleHandleW("kernel32.dll");
            IntPtr loadLibrary = InjectorNative.GetProcAddress(kernel32, "LoadLibraryW");
            if (kernel32 == IntPtr.Zero || loadLibrary == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not locate LoadLibraryW.");
            }

            remoteThread = InjectorNative.CreateRemoteThread(
                process,
                IntPtr.Zero,
                0,
                loadLibrary,
                remotePath,
                0,
                out _);
            if (remoteThread == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateRemoteThread failed.");
            }

            uint wait = InjectorNative.WaitForSingleObject(remoteThread, 10_000);
            if (wait == InjectorNative.WaitTimeout)
            {
                Console.Error.WriteLine(
                    "Loading the injector bootstrap is taking longer than expected; waiting for the remote thread to release its argument.");
                wait = InjectorNative.WaitForSingleObject(remoteThread, InjectorNative.Infinite);
            }

            if (wait != InjectorNative.WaitObject0)
            {
                Console.Error.WriteLine(
                    $"Loading the injector bootstrap did not complete: 0x{wait:X8}. " +
                    "The remote argument remains allocated until Terraria exits to avoid releasing memory still owned by the thread.");
                return 6;
            }

            remoteThreadCompleted = true;

            if (!InjectorNative.GetExitCodeThread(remoteThread, out uint moduleHandle) || moduleHandle == 0)
            {
                Console.Error.WriteLine("Terraria did not load the injector bootstrap DLL.");
                return 7;
            }

            if (!completed.WaitOne(TimeSpan.FromSeconds(15)))
            {
                Console.Error.WriteLine("The injector bootstrap did not return a handshake.");
                return 8;
            }

            uint executeResult = unchecked((uint)resultView.ReadInt32(0));
            uint managedResult = unchecked((uint)resultView.ReadInt32(4));
            if (executeResult != 0 || managedResult != 0)
            {
                Console.Error.WriteLine(
                    $"The injector rejected the command: bootstrap=0x{executeResult:X8}, payload={managedResult}.");
                return 9;
            }

            return 0;
        }
        finally
        {
            if (remoteThread != IntPtr.Zero)
            {
                InjectorNative.CloseHandle(remoteThread);
            }

            if (remotePath != IntPtr.Zero && (remoteThread == IntPtr.Zero || remoteThreadCompleted))
            {
                InjectorNative.VirtualFreeEx(process, remotePath, 0, InjectorNative.FreeType.Release);
            }

            InjectorNative.CloseHandle(process);
        }
    }
}

internal static partial class InjectorNative
{
    internal const uint WaitObject0 = 0;
    internal const uint WaitTimeout = 258;
    internal const uint Infinite = uint.MaxValue;

    [Flags]
    internal enum ProcessAccess : uint
    {
        CreateThread = 0x0002,
        VirtualMemoryOperation = 0x0008,
        VirtualMemoryRead = 0x0010,
        VirtualMemoryWrite = 0x0020,
        QueryInformation = 0x0400
    }

    [Flags]
    internal enum AllocationType : uint
    {
        Commit = 0x1000,
        Reserve = 0x2000
    }

    internal enum MemoryProtection : uint
    {
        ReadWrite = 0x04
    }

    internal enum FreeType : uint
    {
        Release = 0x8000
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial IntPtr OpenProcess(
        ProcessAccess access,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        int processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial IntPtr VirtualAllocEx(
        IntPtr process,
        IntPtr address,
        nuint size,
        AllocationType allocationType,
        MemoryProtection protection);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool VirtualFreeEx(
        IntPtr process,
        IntPtr address,
        nuint size,
        FreeType freeType);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WriteProcessMemory(
        IntPtr process,
        IntPtr address,
        byte[] buffer,
        nuint size,
        out nuint written);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial IntPtr CreateRemoteThread(
        IntPtr process,
        IntPtr threadAttributes,
        nuint stackSize,
        IntPtr startAddress,
        IntPtr parameter,
        uint creationFlags,
        out uint threadId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetExitCodeThread(IntPtr thread, out uint exitCode);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr GetModuleHandleW(string moduleName);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr GetProcAddress(IntPtr module, string procedureName);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(IntPtr handle);
}
