using System.Diagnostics;
using System.Drawing;

namespace TerrariaSplit.Terraria.Automation;

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
        bool pyramidEnabled = settings.EnablePyramidFilter;
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

        bool crimsonCorridorEnabled = IsCrimsonCorridorFilterEnabled(settings);
        bool crimsonScanned = true;
        CrimsonCorridorScanResult crimsonCorridor = default;
        string crimsonDetail = string.Empty;
        if (crimsonCorridorEnabled)
        {
            crimsonScanned = scanner.TryScanCrimsonBetweenDungeonAndSpawn(
                worldPath,
                out crimsonCorridor,
                out crimsonDetail);
        }

        stopwatch.Stop();

        bool pyramidKeep = !pyramidEnabled || candidateChests.Chests.Count > 0;
        bool crimsonCorridorKeep = !crimsonCorridorEnabled || crimsonCorridor.HasCrimson;
        string detail = string.Join(
            "; ",
            new[]
            {
                string.IsNullOrWhiteSpace(pyramidDetail) ? string.Empty : "pyramid: " + pyramidDetail,
                string.IsNullOrWhiteSpace(crimsonDetail) ? string.Empty : "crimson corridor: " + crimsonDetail
            }.Where(value => value.Length > 0));

        return new PyramidFilterWorldFileResult(
            pyramidScanned && crimsonScanned,
            pyramidScanned && crimsonScanned && pyramidKeep && crimsonCorridorKeep,
            pyramidEnabled,
            pyramidKeep,
            requiredItemMask,
            bounds,
            candidateChests,
            crimsonCorridorEnabled,
            crimsonCorridorKeep,
            crimsonCorridor,
            detail,
            stopwatch.Elapsed);
    }

    public static bool IsCrimsonCorridorFilterEnabled(AutoCreateWorldSettings settings)
    {
        return settings.RequireCrimsonBetweenDungeonAndSpawn &&
            string.Equals(AutoCreateWorldEvil.Normalize(settings.WorldEvil), AutoCreateWorldEvil.Crimson, StringComparison.Ordinal);
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
    bool CrimsonCorridorFilterEnabled,
    bool CrimsonCorridorKeep,
    CrimsonCorridorScanResult CrimsonCorridor,
    string Detail,
    TimeSpan ScanDuration);
