using System.Diagnostics;

namespace TerrariaSplit;

internal sealed class TerrariaVisibleSeedReader : IPyramidVisibleSeedReader, IDisposable
{
    private static readonly TimeSpan SeedReadTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SeedReadRetryDelay = TimeSpan.FromMilliseconds(25);

    private readonly Process process;
    private readonly ProcessMemoryReader memory;
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

    public string? ReadCurrentSeed()
    {
        TerrariaWorldCreationSeedSnapshot snapshot = seedReader.Read(memory);
        return snapshot.Status == TerrariaWorldCreationSeedStatus.Seed &&
            !string.IsNullOrWhiteSpace(snapshot.SeedText)
            ? snapshot.SeedText
            : null;
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
            TerrariaWorldCreationSeedSnapshot snapshot = seedReader.Read(memory);
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
        process.Dispose();
    }
}
