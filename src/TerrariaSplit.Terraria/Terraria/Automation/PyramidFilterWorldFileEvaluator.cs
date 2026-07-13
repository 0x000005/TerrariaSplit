using System.Diagnostics;
using System.Drawing;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class PyramidFilterWorldFileEvaluator
{
    private readonly TerrariaWorldFilePyramidScanner scanner;
    private readonly WorldResourceFilterScanner resourceScanner;

    public PyramidFilterWorldFileEvaluator(
        TerrariaWorldFilePyramidScanner? scanner = null,
        WorldResourceFilterScanner? resourceScanner = null)
    {
        this.scanner = scanner ?? new TerrariaWorldFilePyramidScanner();
        this.resourceScanner = resourceScanner ?? new WorldResourceFilterScanner();
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

        bool crimsonCorridorEnabled = IsCrimsonCorridorFilterEnabled(settings);
        bool crimsonScanned = true;
        CrimsonCorridorScanResult crimsonCorridor = default;
        string crimsonDetail = string.Empty;
        if (crimsonCorridorEnabled)
        {
            crimsonScanned = scanner.TryScanCrimsonBetweenDungeonAndSpawn(
                worldPath,
                out crimsonCorridor,
                out crimsonDetail,
                settings.CrimsonDistance);
        }

        bool resourceFilterEnabled = IsResourceFilterEnabled(settings);
        bool resourcesScanned = true;
        WorldResourceFilterResult resources = WorldResourceFilterResult.Empty;
        string resourceDetail = string.Empty;
        if (resourceFilterEnabled)
        {
            resourcesScanned = resourceScanner.TryScan(
                worldPath,
                settings,
                out resources,
                out resourceDetail);
        }

        stopwatch.Stop();

        bool pyramidKeep = !pyramidEnabled || candidateChests.Chests.Count > 0;
        bool crimsonCorridorKeep = !crimsonCorridorEnabled || crimsonCorridor.HasCrimson;
        bool resourceFilterKeep = !resourceFilterEnabled || resources.Keep;
        string detail = string.Join(
            "; ",
            new[]
            {
                string.IsNullOrWhiteSpace(pyramidDetail) ? string.Empty : "pyramid: " + pyramidDetail,
                string.IsNullOrWhiteSpace(crimsonDetail) ? string.Empty : "crimson corridor: " + crimsonDetail,
                string.IsNullOrWhiteSpace(resourceDetail) ? string.Empty : "resources: " + resourceDetail
            }.Where(value => value.Length > 0));

        return new PyramidFilterWorldFileResult(
            pyramidScanned && crimsonScanned && resourcesScanned,
            pyramidScanned && crimsonScanned && resourcesScanned && pyramidKeep && crimsonCorridorKeep && resourceFilterKeep,
            pyramidEnabled,
            pyramidKeep,
            requiredItemMask,
            bounds,
            candidateChests,
            crimsonCorridorEnabled,
            crimsonCorridorKeep,
            crimsonCorridor,
            resourceFilterEnabled,
            resourceFilterKeep,
            resources,
            detail,
            stopwatch.Elapsed);
    }

    public static bool IsPyramidFilterEnabled(AutoCreateWorldSettings settings)
    {
        return settings.EnableCheats && settings.EnablePyramidFilter;
    }

    public static bool IsCrimsonCorridorFilterEnabled(AutoCreateWorldSettings settings)
    {
        return settings.EnableCheats && settings.RequireCrimsonBetweenDungeonAndSpawn &&
            string.Equals(AutoCreateWorldEvil.Normalize(settings.WorldEvil), AutoCreateWorldEvil.Crimson, StringComparison.Ordinal);
    }

    public static bool IsResourceFilterEnabled(AutoCreateWorldSettings settings)
    {
        return settings.EnableCheats && AutoCreateResourceFilter.HasRequirements(settings) &&
            string.Equals(AutoCreateWorldSize.Normalize(settings.WorldSize), AutoCreateWorldSize.Small, StringComparison.Ordinal) &&
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
    bool ResourceFilterEnabled,
    bool ResourceFilterKeep,
    WorldResourceFilterResult Resources,
    string Detail,
    TimeSpan ScanDuration);
