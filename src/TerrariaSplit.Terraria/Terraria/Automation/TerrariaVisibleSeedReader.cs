using System.Diagnostics;
using Process = System.Diagnostics.Process;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class TerrariaVisibleSeedReader : IPyramidVisibleSeedReader, IDisposable
{
    private static readonly TimeSpan SeedReadTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SeedReadRetryDelay = TimeSpan.FromMilliseconds(25);

    private readonly Process process;
    private readonly ProcessMemoryReader memory;
    private readonly TerrariaMemoryResolver resolver = new();
    private readonly TerrariaWorldCreationSeedReader seedReader = new();
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;

    private TerrariaVisibleSeedReader(
        Process process,
        ProcessMemoryReader memory,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        this.process = process;
        this.memory = memory;
        this.delayAsync = delayAsync;
        resolver.SetProcess(process);
        seedReader.Reset();
    }

    public static bool TryCreate(
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        out TerrariaVisibleSeedReader? reader,
        out string detail)
    {
        reader = null;
        using Process? candidateProcess = TerrariaProcessFinder.FindNewest();
        if (candidateProcess is null)
        {
            detail = "Terraria.exe was not found.";
            return false;
        }

        try
        {
            Process process = Process.GetProcessById(candidateProcess.Id);
            reader = new TerrariaVisibleSeedReader(process, new ProcessMemoryReader(process), delayAsync);
            detail = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            detail = ex.Message;
            return false;
        }
    }

    public static TerrariaVisibleSeedReaderPreparation Prepare(
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (!TryCreate(delayAsync, out TerrariaVisibleSeedReader? reader, out string detail) ||
            reader is null)
        {
            return new TerrariaVisibleSeedReaderPreparation(
                null,
                detail,
                stopwatch.Elapsed);
        }

        try
        {
            _ = reader.ReadCurrentSeed();
            bool complete = reader.resolver.Resolution.HasSeedUiLayout;
            if (!complete)
            {
                reader.resolver.ResetResolvedAddresses();
            }

            return new TerrariaVisibleSeedReaderPreparation(
                reader,
                complete
                    ? "MemoryBridge seed UI layout prewarmed."
                    : "MemoryBridge prewarm was partial; seed UI layout will be retried.",
                stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            reader.Dispose();
            return new TerrariaVisibleSeedReaderPreparation(
                null,
                ex.Message,
                stopwatch.Elapsed);
        }
    }

    public string? ReadCurrentSeed()
    {
        TerrariaWorldCreationSeedSnapshot snapshot = ReadSeedSnapshot();
        return snapshot.Status == TerrariaWorldCreationSeedStatus.Seed &&
            !string.IsNullOrWhiteSpace(snapshot.SeedText)
            ? snapshot.SeedText
            : null;
    }

    public bool TryPredictNextSeedBatch(
        int count,
        out IReadOnlyList<string> seedTexts,
        out string detail)
    {
        _ = count;
        seedTexts = Array.Empty<string>();
        detail =
            "Terraria.Main.rand is shared with menu animation and advances every frame; " +
            "future random-button seeds cannot be reserved exactly without mutating or hooking the game RNG.";
        return false;
    }

    public async Task<PyramidVisibleSeedReadResult> WaitForSeedAfterRandomizeAsync(
        string? previousSeedText,
        CancellationToken cancellationToken)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        TerrariaWorldCreationSeedStatus lastStatus = TerrariaWorldCreationSeedStatus.Unknown;
        int readAttempts = 0;
        string lastSeedText = string.Empty;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            readAttempts++;
            TerrariaWorldCreationSeedSnapshot snapshot = ReadSeedSnapshot();
            lastStatus = snapshot.Status;
            if (snapshot.Status == TerrariaWorldCreationSeedStatus.Seed &&
                !string.IsNullOrWhiteSpace(snapshot.SeedText))
            {
                lastSeedText = snapshot.SeedText;
                if (previousSeedText is null ||
                    !string.Equals(snapshot.SeedText, previousSeedText, StringComparison.Ordinal))
                {
                    return PyramidVisibleSeedReadResult.FromSeed(snapshot.SeedText, readAttempts);
                }
            }

            TimeSpan remaining = SeedReadTimeout - timeout.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return PyramidVisibleSeedReadResult.Failed(lastStatus, readAttempts, lastSeedText);
            }

            TimeSpan delay = remaining < SeedReadRetryDelay ? remaining : SeedReadRetryDelay;
            await delayAsync(delay, cancellationToken);
        }
    }

    public void Dispose()
    {
        resolver.SetProcess(null);
        process.Dispose();
    }

    private TerrariaWorldCreationSeedSnapshot ReadSeedSnapshot()
    {
        _ = resolver.Resolve(memory);
        return seedReader.Read(memory, resolver.SeedUiLayout);
    }
}

internal readonly record struct TerrariaVisibleSeedReaderPreparation(
    TerrariaVisibleSeedReader? Reader,
    string Detail,
    TimeSpan Duration);
