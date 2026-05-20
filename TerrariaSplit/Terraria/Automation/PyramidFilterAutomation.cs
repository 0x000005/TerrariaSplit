using System.Diagnostics;
using System.Drawing;

namespace TerrariaSplit;

internal sealed class PyramidFilterAutomation
{
    private const int PyramidWallThreshold = 1;
    private const int PyramidTileThreshold = 1;
    private static readonly TimeSpan WorldFileTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan StableFileDuration = TimeSpan.FromMilliseconds(400);

    private readonly TerrariaAutomationContext automation;
    private readonly TerrariaWorldFilePyramidScanner scanner;

    public PyramidFilterAutomation(
        TerrariaAutomationContext automation,
        TerrariaWorldFilePyramidScanner? scanner = null)
    {
        this.automation = automation;
        this.scanner = scanner ?? new TerrariaWorldFilePyramidScanner();
    }

    public async Task<PyramidFilterOutcome> RunAsync(
        AutoCreateWorldSettings settings,
        IReadOnlyDictionary<string, DateTime> worldsBefore,
        CancellationToken cancellationToken)
    {
        if (!settings.EnablePyramidFilter)
        {
            return PyramidFilterOutcome.Disabled;
        }

        string? worldPath = await WaitForStableCreatedWorldFileAsync(worldsBefore, cancellationToken);
        if (string.IsNullOrWhiteSpace(worldPath))
        {
            AppLogger.Info("Pyramid filter kept the world because no completed world file was observed before timeout.");
            return PyramidFilterOutcome.KeptWithoutVerification;
        }

        AppLogger.Info($"Pyramid filter will scan world file '{Path.GetFileName(worldPath)}'.");

        Stopwatch stopwatch = Stopwatch.StartNew();
        bool scanned = scanner.TryScanSpeedrunCorridor(
            worldPath,
            settings.WorldSize,
            PyramidWallThreshold,
            PyramidTileThreshold,
            out PyramidEvidenceScanResult evidence,
            out Rectangle bounds,
            out string detail);
        stopwatch.Stop();

        if (!scanned || evidence.ScanFailed)
        {
            AppLogger.Info(
                $"Pyramid filter kept '{Path.GetFileName(worldPath)}' to avoid a false delete. " +
                $"scanFailed={evidence.ScanFailed}, detail={detail}, scanMs={stopwatch.ElapsedMilliseconds}");
            return PyramidFilterOutcome.KeptWithoutVerification;
        }

        bool keep = evidence.MeetsThreshold(PyramidWallThreshold, PyramidTileThreshold);
        AppLogger.Info(
            $"Pyramid filter scanned '{Path.GetFileName(worldPath)}': keep={keep}, " +
            $"corridor={bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom}, " +
            $"wall34={evidence.Wall34Count}, activeTile151={evidence.ActiveTile151Count}, " +
            $"scanMs={stopwatch.ElapsedMilliseconds}");

        return keep
            ? PyramidFilterOutcome.Kept
            : PyramidFilterOutcome.Rejected;
    }

    private async Task<string?> WaitForStableCreatedWorldFileAsync(
        IReadOnlyDictionary<string, DateTime> worldsBefore,
        CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + WorldFileTimeout;
        string? stablePath = null;
        long stableLength = -1;
        DateTime stableWriteTime = DateTime.MinValue;
        DateTime stableSince = DateTime.MinValue;

        while (DateTime.UtcNow <= deadline)
        {
            automation.ThrowIfCancellationRequested(cancellationToken);
            if (TryFindNewestCreatedWorldFile(worldsBefore, out string? candidatePath, out long length, out DateTime writeTime) &&
                candidatePath is not null)
            {
                if (string.Equals(stablePath, candidatePath, StringComparison.OrdinalIgnoreCase) &&
                    stableLength == length &&
                    stableWriteTime == writeTime &&
                    CanOpenForRead(candidatePath))
                {
                    if (stableSince == DateTime.MinValue)
                    {
                        stableSince = DateTime.UtcNow;
                    }
                    else if (DateTime.UtcNow - stableSince >= StableFileDuration)
                    {
                        return candidatePath;
                    }
                }
                else
                {
                    stablePath = candidatePath;
                    stableLength = length;
                    stableWriteTime = writeTime;
                    stableSince = DateTime.MinValue;
                }
            }

            await automation.DelayAsync(PollInterval, cancellationToken);
        }

        return null;
    }

    private static bool TryFindNewestCreatedWorldFile(
        IReadOnlyDictionary<string, DateTime> worldsBefore,
        out string? path,
        out long length,
        out DateTime writeTimeUtc)
    {
        path = null;
        length = -1;
        writeTimeUtc = DateTime.MinValue;
        string worldsPath = Path.Combine(TerrariaSavePaths.SaveRoot(), "Worlds");
        if (!Directory.Exists(worldsPath))
        {
            return false;
        }

        foreach (string worldFile in Directory.EnumerateFiles(worldsPath, "*.wld", SearchOption.TopDirectoryOnly))
        {
            FileInfo info;
            try
            {
                info = new FileInfo(worldFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            string fileName = info.Name;
            bool createdOrChanged = !worldsBefore.TryGetValue(fileName, out DateTime previousWriteTime) ||
                info.LastWriteTimeUtc > previousWriteTime;
            if (!createdOrChanged || info.LastWriteTimeUtc < writeTimeUtc)
            {
                continue;
            }

            path = info.FullName;
            length = info.Length;
            writeTimeUtc = info.LastWriteTimeUtc;
        }

        return path is not null;
    }

    private static bool CanOpenForRead(string path)
    {
        try
        {
            using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return stream.Length > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

internal enum PyramidFilterOutcome
{
    Disabled,
    Kept,
    KeptWithoutVerification,
    Rejected
}
