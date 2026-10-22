using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerrariaSplit.Diagnostics;

internal static class LegacyPreScreenLoss
{
    public static bool TryRun(string[] args)
    {
        if (args.Length == 0 || args[0] != "legacy-prescreen-loss") return false;
        try
        {
            var seeds = JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[1]))!;
            var rows = new List<object>(); int rejected = 0, errors = 0, passed = 0, other = 0;
            var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() } };
            foreach (var seed in seeds)
            {
                // Jungle depth is evaluated by the DLL; the old pre-screen only sees pyramid requirements.
                var result = PyramidSeedPreScreen.EvaluateSmallCrimson(seed, difficultyCode: 0,
                    requiredItemMask: 3, version: TerrariaWorldGenerationVersion.Modern1458, coinPileMinimum: 0);
                string category;
                if(result.Status == PyramidSeedPreScreenStatus.Complete)
                {
                    if(result.MatchesRequirements){passed++;category="Passed";}
                    else {rejected++;category=result.HasTargetPyramid?"ItemMismatch":"NoPyramid";}
                }
                else if(result.Status == PyramidSeedPreScreenStatus.Error){errors++;category="SimulationError";}
                else {other++;category="Unavailable";}
                rows.Add(new { seed, category, result });
                Console.WriteLine($"{rows.Count}/{seeds.Length} seed={seed} {category}");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
            File.WriteAllText(args[2],JsonSerializer.Serialize(new { dllSatisfied=seeds.Length,passed,
                falseRejected=rejected,errorSkipped=errors,unavailable=other,rows },options));
        }
        catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}
        return true;
    }
}
