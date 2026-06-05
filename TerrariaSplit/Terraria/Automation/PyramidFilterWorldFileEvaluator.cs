using System.Diagnostics;
using System.Drawing;

namespace TerrariaSplit;

internal sealed class PyramidFilterWorldFileEvaluator
{
    private readonly TerrariaWorldFilePyramidScanner scanner;

    public PyramidFilterWorldFileEvaluator(TerrariaWorldFilePyramidScanner? scanner = null)
    {
        this.scanner = scanner ?? new TerrariaWorldFilePyramidScanner();
    }

    public PyramidFilterWorldFileResult Evaluate(string worldPath, AutoCreateWorldSettings settings)
    {
        int requiredItemMask = PyramidFilterItemMatcher.ResolveRequiredMaskOrAll(settings.PyramidFilterItemMask);
        Stopwatch stopwatch = Stopwatch.StartNew();
        bool scanned = scanner.TryScanCandidateItemChests(
            worldPath,
            settings.WorldSize,
            requiredItemMask,
            out PyramidChestScanResult candidateChests,
            out Rectangle bounds,
            out string detail);
        stopwatch.Stop();

        return new PyramidFilterWorldFileResult(
            scanned,
            scanned && candidateChests.Chests.Count > 0,
            requiredItemMask,
            bounds,
            candidateChests,
            detail,
            stopwatch.Elapsed);
    }
}

internal readonly record struct PyramidFilterWorldFileResult(
    bool ScanSucceeded,
    bool Keep,
    int RequiredItemMask,
    Rectangle ScanBounds,
    PyramidChestScanResult CandidateChests,
    string Detail,
    TimeSpan ScanDuration);
