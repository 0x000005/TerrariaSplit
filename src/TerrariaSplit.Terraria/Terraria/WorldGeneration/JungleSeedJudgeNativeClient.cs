using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace TerrariaSplit.Terraria.WorldGeneration;

internal sealed class JungleSeedJudgeNativeClient
{
    private const int MaximumResponseBytes = 16 * 1024 * 1024;
    private const int CpuUsagePercent = 80;
    private static readonly int MaximumConcurrentCalls = Math.Max(
        1,
        (int)((long)Math.Max(1, Environment.ProcessorCount) *
            CpuUsagePercent / 100));
    private static readonly SemaphoreSlim NativeCallGate =
        new(MaximumConcurrentCalls, MaximumConcurrentCalls);
    private static readonly ConcurrentDictionary<string, Lazy<NativeApi>>
        LoadedLibraries = new(StringComparer.OrdinalIgnoreCase);

    private readonly NativeApi api;
    private readonly TimeSpan requestTimeout;
    private long nextRequestId;

    public static JungleSeedJudgeNativeClient CreateDefault(
        TimeSpan? requestTimeout = null)
    {
        return new JungleSeedJudgeNativeClient(
            JungleSeedJudgeNativeLibraryLocator.ResolvePath(),
            requestTimeout);
    }

    public JungleSeedJudgeNativeClient(
        string libraryPath,
        TimeSpan? requestTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryPath);
        string fullPath = Path.GetFullPath(libraryPath);
        this.requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(5);
        if (this.requestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestTimeout),
                "The native world-filter timeout must be positive.");
        }

        api = LoadedLibraries.GetOrAdd(
                fullPath,
                static path => new Lazy<NativeApi>(
                    () => NativeApi.Load(path),
                    LazyThreadSafetyMode.ExecutionAndPublication))
            .Value;
    }

    public async Task<JungleSeedJudgeResult> AnalyzeAsync(
        string seedText,
        JungleSeedJudgeGameMode gameMode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seedText);
        await NativeCallGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        string requestId = Interlocked.Increment(ref nextRequestId)
            .ToString(CultureInfo.InvariantCulture);
        Task<JungleSeedJudgeResult> nativeCall = Task.Run(
            () => api.Analyze(seedText, gameMode, requestId),
            CancellationToken.None);
        bool releaseWhenNativeCallCompletes = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(requestTimeout);
            try
            {
                return await nativeCall.WaitAsync(timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Native world-filter call exceeded " +
                    $"{requestTimeout.TotalSeconds:F1} seconds.");
            }
        }
        catch
        {
            if (!nativeCall.IsCompleted)
            {
                releaseWhenNativeCallCompletes = true;
                _ = nativeCall.ContinueWith(
                    static completed =>
                    {
                        _ = completed.Exception;
                        NativeCallGate.Release();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            else
            {
                _ = nativeCall.Exception;
            }
            throw;
        }
        finally
        {
            if (!releaseWhenNativeCallCompletes)
            {
                NativeCallGate.Release();
            }
        }
    }

    private sealed class NativeApi
    {
        private readonly AnalyzeDelegate analyze;
        private readonly FreeDelegate free;

        private NativeApi(
            AnalyzeDelegate analyze,
            FreeDelegate free)
        {
            this.analyze = analyze;
            this.free = free;
        }

        public static NativeApi Load(string libraryPath)
        {
            if (!File.Exists(libraryPath))
            {
                throw new FileNotFoundException(
                    "Terraria World Filter native library was not found.",
                    libraryPath);
            }

            nint libraryHandle;
            try
            {
                libraryHandle = NativeLibrary.Load(libraryPath);
            }
            catch (BadImageFormatException ex)
            {
                throw new InvalidOperationException(
                    "Terraria World Filter must be an x64 DLL.",
                    ex);
            }

            try
            {
                GetAbiVersionDelegate getAbiVersion =
                    GetExport<GetAbiVersionDelegate>(
                        libraryHandle,
                        "TerrariaSplitWorldFilterGetAbiVersion");
                int abiVersion = getAbiVersion();
                if (abiVersion != 1)
                {
                    throw new InvalidDataException(
                        $"Unsupported native world-filter ABI {abiVersion}.");
                }

                return new NativeApi(
                    GetExport<AnalyzeDelegate>(
                        libraryHandle,
                        "TerrariaSplitWorldFilterAnalyze"),
                    GetExport<FreeDelegate>(
                        libraryHandle,
                        "TerrariaSplitWorldFilterFree"));
            }
            catch
            {
                NativeLibrary.Free(libraryHandle);
                throw;
            }
        }

        public JungleSeedJudgeResult Analyze(
            string seedText,
            JungleSeedJudgeGameMode gameMode,
            string requestId)
        {
            byte[] seedUtf8 = Encoding.UTF8.GetBytes(seedText);
            byte[] requestIdUtf8 = Encoding.UTF8.GetBytes(requestId);
            nint responsePointer = 0;
            int responseLength = 0;
            int status = analyze(
                seedUtf8,
                seedUtf8.Length,
                (int)gameMode,
                requestIdUtf8,
                requestIdUtf8.Length,
                out responsePointer,
                out responseLength);
            try
            {
                if (status != 0)
                {
                    throw new InvalidOperationException(
                        $"Native world-filter call failed with status {status}.");
                }
                if (responsePointer == 0 ||
                    responseLength <= 0 ||
                    responseLength > MaximumResponseBytes)
                {
                    throw new InvalidDataException(
                        "Native world-filter returned an invalid response buffer.");
                }

                byte[] responseUtf8 = new byte[responseLength];
                Marshal.Copy(
                    responsePointer,
                    responseUtf8,
                    startIndex: 0,
                    responseLength);
                string responseJson = Encoding.UTF8.GetString(responseUtf8);
                return JungleSeedJudgeProtocolSerializer.DeserializeResponse(
                    responseJson,
                    requestId);
            }
            finally
            {
                if (responsePointer != 0)
                {
                    free(responsePointer);
                }
            }
        }

        private static TDelegate GetExport<TDelegate>(
            nint libraryHandle,
            string exportName)
            where TDelegate : Delegate
        {
            nint address = NativeLibrary.GetExport(libraryHandle, exportName);
            return Marshal.GetDelegateForFunctionPointer<TDelegate>(address);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetAbiVersionDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int AnalyzeDelegate(
        [In] byte[] seedUtf8,
        int seedLength,
        int gameMode,
        [In] byte[] requestIdUtf8,
        int requestIdLength,
        out nint responseUtf8,
        out int responseLength);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeDelegate(nint responseUtf8);
}

internal static class JungleSeedJudgeNativeLibraryLocator
{
    public const string LibraryFileName = "TerrariaSplit.WorldFilter.dll";

    public static string ResolvePath()
    {
        string? configured = Environment.GetEnvironmentVariable(
            "TERRARIA_WORLD_FILTER");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string fullPath = Path.GetFullPath(configured);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            throw new FileNotFoundException(
                "Configured Terraria World Filter library was not found.",
                fullPath);
        }

        foreach (string candidate in EnumerateCandidatePaths())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate {LibraryFileName} next to TerrariaSplit.");
    }

    private static IEnumerable<string> EnumerateCandidatePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, LibraryFileName);

        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        for (int depth = 0; directory is not null && depth < 8; depth++)
        {
            yield return Path.Combine(
                directory.FullName,
                "TerrariaJungleJudge",
                "out",
                "build",
                "x64-release",
                "Release",
                LibraryFileName);
            directory = directory.Parent;
        }
    }
}
