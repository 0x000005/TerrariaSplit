using System.Globalization;
using System.Text;
using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Diagnostics;

internal static class PyramidDepthReport
{
    public static bool TryRun(string[] args)
    {
        if (args.Length == 0 ||
            !string.Equals(args[0], "pyramid-depths", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: pyramid-depths <output.csv> <world-root> [world-root ...]");
            Environment.ExitCode = 2;
            return true;
        }

        string outputPath = Path.GetFullPath(args[1]);
        string[] worldRoots = args.Skip(2).Select(Path.GetFullPath).ToArray();
        foreach (string worldRoot in worldRoots)
        {
            if (!Directory.Exists(worldRoot))
            {
                Console.Error.WriteLine("World root not found: " + worldRoot);
                Environment.ExitCode = 2;
                return true;
            }
        }

        WriteReport(outputPath, worldRoots);
        return true;
    }

    private static void WriteReport(string outputPath, IReadOnlyList<string> worldRoots)
    {
        var scanner = new TerrariaWorldFilePyramidScanner();
        var rows = new List<PyramidDepthRow>();
        foreach (string worldRoot in worldRoots)
        {
            string batch = Path.GetFileName(Path.GetDirectoryName(worldRoot)) ?? Path.GetFileName(worldRoot);
            foreach (string worldPath in Directory.EnumerateFiles(worldRoot, "*.wld", SearchOption.AllDirectories)
                         .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                string ordinal = ParseOrdinal(Path.GetFileNameWithoutExtension(worldPath));
                if (!scanner.TryReadWorldSeedMetadata(
                        worldPath,
                        out TerrariaWorldSeedMetadata metadata,
                        out string metadataDetail))
                {
                    rows.Add(new PyramidDepthRow(
                        batch,
                        ordinal,
                        string.Empty,
                        "unreadable",
                        0,
                        string.Empty,
                        string.Empty,
                        metadataDetail,
                        worldPath));
                    continue;
                }

                if (!scanner.TryScanCandidateItemChests(
                        worldPath,
                        SizeText(metadata.SizeCode),
                        AutoCreatePyramidFilterItem.AllMask,
                        out PyramidChestScanResult scan,
                        out _,
                        out string scanDetail))
                {
                    rows.Add(new PyramidDepthRow(
                        batch,
                        ordinal,
                        metadata.SeedText,
                        "unreadable",
                        0,
                        string.Empty,
                        string.Empty,
                        scanDetail,
                        worldPath));
                    continue;
                }

                IReadOnlyList<PyramidChestInfo> chests = scan.Chests ?? [];
                bool measured = chests.Count > 0 && chests.All(static chest => chest.TunnelSurfaceDistance >= 0);
                string status = chests.Count == 0
                    ? "no-pyramid"
                    : measured ? "measured" : "depth-unknown";
                string depthDetail = string.Join(
                    " | ",
                    chests.Select(static chest => chest.TunnelDepthDetail)
                        .Where(static detail => !string.IsNullOrWhiteSpace(detail)));
                rows.Add(new PyramidDepthRow(
                    batch,
                    ordinal,
                    metadata.SeedText,
                    status,
                    chests.Count,
                    string.Join('|', chests.Select(static chest => chest.TunnelSurfaceDistance.ToString(CultureInfo.InvariantCulture))),
                    string.Join('|', chests.Select(static chest => $"{chest.TunnelTopX}:{chest.TunnelTopY}:{chest.TunnelOpeningSide}")),
                    string.IsNullOrWhiteSpace(depthDetail) ? scanDetail : depthDetail,
                    worldPath));
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var writer = new StreamWriter(outputPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine("batch,ordinal,seed,status,pyramidCount,depths,tunnelPoints,detail,worldFile");
        foreach (PyramidDepthRow row in rows
                     .OrderBy(static row => row.Batch, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(static row => int.TryParse(row.Ordinal, out int ordinal) ? ordinal : int.MaxValue))
        {
            writer.WriteLine(string.Join(',',
                Csv(row.Batch),
                Csv(row.Ordinal),
                Csv(row.Seed),
                Csv(row.Status),
                row.PyramidCount.ToString(CultureInfo.InvariantCulture),
                Csv(row.Depths),
                Csv(row.TunnelPoints),
                Csv(row.Detail),
                Csv(row.WorldFile)));
        }

        Console.Error.WriteLine(
            $"wrote {rows.Count} worlds; measured={rows.Count(static row => row.Status == "measured")}, " +
            $"noPyramid={rows.Count(static row => row.Status == "no-pyramid")}, " +
            $"unknown={rows.Count(static row => row.Status == "depth-unknown")}, " +
            $"unreadable={rows.Count(static row => row.Status == "unreadable")}; output={outputPath}");
    }

    private static string ParseOrdinal(string stem)
    {
        int separator = stem.IndexOf('-');
        return separator > 0 && int.TryParse(stem[..separator], out int ordinal)
            ? ordinal.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string SizeText(int sizeCode) => sizeCode switch
    {
        1 => AutoCreateWorldSize.Small,
        3 => AutoCreateWorldSize.Large,
        _ => AutoCreateWorldSize.Medium
    };

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';

    private readonly record struct PyramidDepthRow(
        string Batch,
        string Ordinal,
        string Seed,
        string Status,
        int PyramidCount,
        string Depths,
        string TunnelPoints,
        string Detail,
        string WorldFile);
}
