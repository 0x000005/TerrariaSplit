using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace TerrariaSplit.Terraria.WorldGeneration;

internal sealed class JungleSeedJudgeNativeClient
{
    private const int MaximumResponseBytes = 16 * 1024 * 1024;
    private const int CpuUsagePercent = 80;
    private static readonly int MaximumConcurrentExecutionThreads = Math.Max(
        1,
        (int)((long)Math.Max(1, Environment.ProcessorCount) *
            CpuUsagePercent / 100));
    private static readonly SemaphoreSlim NativeCallGate =
        new(MaximumConcurrentExecutionThreads, MaximumConcurrentExecutionThreads);
    private static readonly SemaphoreSlim BudgetReservationGate = new(1, 1);
    private static readonly SemaphoreSlim SingleWorldGate = new(1, 1);
    private static readonly ConcurrentDictionary<string, Lazy<NativeApi>>
        LoadedLibraries = new(StringComparer.OrdinalIgnoreCase);

    private readonly Func<string, JungleSeedJudgeGameMode, string, ResourceJudgeRequirements, int, JungleSeedJudgeResult> analyze;
    private readonly SemaphoreSlim nativeCallGate;
    private readonly SemaphoreSlim budgetReservationGate;
    private readonly int maximumLeaseThreads;
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
        this.requestTimeout = ValidateRequestTimeout(requestTimeout);
        NativeApi api = LoadedLibraries.GetOrAdd(
                fullPath,
                static path => new Lazy<NativeApi>(
                    () => NativeApi.Load(path),
                    LazyThreadSafetyMode.ExecutionAndPublication))
            .Value;
        analyze = api.Analyze;
        nativeCallGate = NativeCallGate;
        budgetReservationGate = BudgetReservationGate;
        maximumLeaseThreads = Math.Min(4, Math.Min(Math.Max(1, Environment.ProcessorCount), MaximumConcurrentExecutionThreads));
    }

    internal JungleSeedJudgeNativeClient(
        Func<string, JungleSeedJudgeGameMode, string, ResourceJudgeRequirements, int, JungleSeedJudgeResult> analyze,
        TimeSpan requestTimeout,
        SemaphoreSlim nativeCallGate,
        int maximumLeaseThreads = 1)
    {
        ArgumentNullException.ThrowIfNull(analyze);
        ArgumentNullException.ThrowIfNull(nativeCallGate);
        this.analyze = analyze;
        this.requestTimeout = ValidateRequestTimeout(requestTimeout);
        this.nativeCallGate = nativeCallGate;
        budgetReservationGate = new SemaphoreSlim(1, 1);
        if (maximumLeaseThreads is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(maximumLeaseThreads));
        this.maximumLeaseThreads = maximumLeaseThreads;
    }

    public async Task<JungleSeedJudgeResult> AnalyzeAsync(
        string seedText,
        JungleSeedJudgeGameMode gameMode,
        CancellationToken cancellationToken,
        ResourceJudgeRequirements requirements,
        int threads = 0)
    {
        ArgumentNullException.ThrowIfNull(seedText);
        if (threads is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(threads));
        ArgumentNullException.ThrowIfNull(requirements);
        requirements.Validate();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        deadline.CancelAfter(requestTimeout);
        int leaseThreads;
        bool singleWorldLease = false;
        try
        {
            if (threads != 1)
            {
                await SingleWorldGate.WaitAsync(deadline.Token).ConfigureAwait(false);
                singleWorldLease = true;
            }
            leaseThreads = await AcquireCallBudgetAsync(threads, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            if (singleWorldLease) SingleWorldGate.Release();
            throw CreateTimeoutException();
        }
        catch
        {
            if (singleWorldLease) SingleWorldGate.Release();
            throw;
        }

        string requestId = Interlocked.Increment(ref nextRequestId)
            .ToString(CultureInfo.InvariantCulture);
        Task<JungleSeedJudgeResult> nativeCall = Task.Run(
            () => analyze(seedText, gameMode, requestId, requirements, threads),
            CancellationToken.None);
        bool releaseWhenNativeCallCompletes = false;
        try
        {
            try
            {
                return await nativeCall.WaitAsync(deadline.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                throw CreateTimeoutException();
            }
        }
        catch
        {
            if (!nativeCall.IsCompleted)
            {
                releaseWhenNativeCallCompletes = true;
                SemaphoreSlim gate = nativeCallGate;
                _ = nativeCall.ContinueWith(
                    completed =>
                    {
                        _ = completed.Exception;
                        gate.Release(leaseThreads);
                        if (singleWorldLease) SingleWorldGate.Release();
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
                nativeCallGate.Release(leaseThreads);
                if (singleWorldLease) SingleWorldGate.Release();
            }
        }
    }

    private async Task<int> AcquireCallBudgetAsync(int requestedThreads, CancellationToken cancellationToken)
    {
        int desired = Math.Min(requestedThreads == 0 ? 4 : requestedThreads, maximumLeaseThreads);
        int acquired = 0;
        // Serialize reservations so two callers cannot each hold a partial lease
        // while waiting for the other one's slots. Running calls release directly.
        await budgetReservationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (; acquired < desired; acquired++)
                await nativeCallGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return acquired;
        }
        catch
        {
            if (acquired != 0) nativeCallGate.Release(acquired);
            throw;
        }
        finally { budgetReservationGate.Release(); }
    }

    private static TimeSpan ValidateRequestTimeout(TimeSpan? requestTimeout)
    {
        TimeSpan timeout = requestTimeout ?? TimeSpan.FromSeconds(5);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestTimeout),
                "The native world-filter timeout must be positive.");
        }

        return timeout;
    }

    private TimeoutException CreateTimeoutException()
    {
        return new TimeoutException(
            $"Native world-filter request exceeded " +
            $"{requestTimeout.TotalSeconds:F1} seconds.");
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
                if (abiVersion != 5)
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
            string requestId,
            ResourceJudgeRequirements requirements,
            int threads)
        {
            byte[] seedUtf8 = Encoding.UTF8.GetBytes(seedText);
            byte[] requestIdUtf8 = Encoding.UTF8.GetBytes(requestId);
            nint responsePointer = 0;
            int responseLength = 0;
            var nativeRequirements = new NativeRequirements(requirements);
            int status = analyze(
                seedUtf8,
                seedUtf8.Length,
                (int)gameMode,
                requestIdUtf8,
                requestIdUtf8.Length,
                in nativeRequirements,
                threads,
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
                var result = JungleSeedJudgeProtocolSerializer.DeserializeResponse(
                    responseJson,
                    requestId);
                if (result.Status == JungleSeedJudgeStatus.Complete && (result.PlannedEndPass != requirements.EndPass ||
                    result.ExecutionPath != (requirements.PyramidItemsOnly ? "PyramidFast" : "FullPrefix")))
                    throw new InvalidDataException("World Filter returned a different filter plan.");
                if (result.Status == JungleSeedJudgeStatus.Complete && result.RequestedThreads != threads)
                    throw new InvalidDataException("World Filter returned a different thread request.");
                return result;
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

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRequirements
    {
        public readonly uint Size;
        public readonly int PyramidItemMask, PyramidGoldMinimum, PyramidMaximumDepth, CrimsonMaximumDistance,
            JungleMinimumY, JungleItemMask, LifeCrystalMinimum, SpelunkerPotionMinimum, FeatherfallPotionMinimum,
            StarfuryMaximumDistance, FinchStaffMaximumDistance;
        public NativeRequirements(ResourceJudgeRequirements q)
        {
            Size = (uint)Marshal.SizeOf<NativeRequirements>();
            PyramidItemMask = q.PyramidItemMask; PyramidGoldMinimum = q.PyramidGoldMinimum; PyramidMaximumDepth = q.PyramidMaximumDepth;
            CrimsonMaximumDistance = q.CrimsonMaximumDistance; JungleMinimumY = q.JungleMinimumY; JungleItemMask = q.JungleItemMask;
            LifeCrystalMinimum = q.LifeCrystalMinimum; SpelunkerPotionMinimum = q.SpelunkerPotionMinimum;
            FeatherfallPotionMinimum = q.FeatherfallPotionMinimum; StarfuryMaximumDistance = q.StarfuryMaximumDistance;
            FinchStaffMaximumDistance = q.FinchStaffMaximumDistance;
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
        in NativeRequirements requirements,
        int threads,
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
                "TerrariaResourceJudge",
                "out",
                "resourcejudge-pgo",
                "current",
                LibraryFileName);
            directory = directory.Parent;
        }
    }
}
