using System.Diagnostics;
using System.Drawing;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class PyramidFilterWorldFileEvaluator
{
    private readonly TerrariaWorldFilePyramidScanner scanner;

    public PyramidFilterWorldFileEvaluator(
        TerrariaWorldFilePyramidScanner? scanner = null)
    {
        this.scanner = scanner ?? new TerrariaWorldFilePyramidScanner();
    }

    public PyramidFilterWorldFileResult Evaluate(string worldPath, AutoCreateWorldSettings settings)
    {
        int requiredItemMask = PyramidFilterItemMatcher.ResolveRequiredMaskOrAll(settings.PyramidFilterItemMask);
        Stopwatch stopwatch = Stopwatch.StartNew();
        bool pyramidEnabled = IsPyramidFilterEnabled(settings);
        bool pyramidScanned = true;
        PyramidChestScanResult candidateChests = PyramidChestScanResult.Empty;
        Rectangle bounds = Rectangle.Empty;
        string pyramidDetail = string.Empty;
        if (pyramidEnabled)
        {
            pyramidScanned = scanner.TryScanCandidateItemChests(
                worldPath,
                settings.WorldSize,
                requiredItemMask,
                out candidateChests,
                out bounds,
                out pyramidDetail);
        }

        stopwatch.Stop();

        bool pyramidKeep = !pyramidEnabled || candidateChests.Chests.Count > 0;
        return new PyramidFilterWorldFileResult(
            pyramidScanned,
            pyramidScanned && pyramidKeep,
            pyramidEnabled,
            pyramidKeep,
            requiredItemMask,
            bounds,
            candidateChests,
            pyramidDetail,
            stopwatch.Elapsed);
    }

    public static bool IsPyramidFilterEnabled(AutoCreateWorldSettings settings)
    {
        return settings.EnableCheats && settings.EnablePyramidFilter;
    }

}

internal readonly record struct PyramidFilterWorldFileResult(
    bool ScanSucceeded,
    bool Keep,
    bool PyramidFilterEnabled,
    bool PyramidKeep,
    int RequiredItemMask,
    Rectangle ScanBounds,
    PyramidChestScanResult CandidateChests,
    string Detail,
    TimeSpan ScanDuration);
