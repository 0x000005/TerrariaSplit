using System.Runtime.InteropServices;
using System.Text.Json;
using TerrariaSplit.Compatibility;
using TerrariaSplit.Localization;
using TerrariaSplit.Terraria.Automation;
using TerrariaSplit.Terraria.Processes;
using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Tests;

internal static class ResourceJudgeVersionTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("ABI7 resource filtering maps game groups and isolates generation fingerprints in world pools", TestSuite.Core, VersionAndLayoutJourney);
        yield return TestCase.Sync("ABI7 resource responses reject cross-version stale and incomplete metadata", TestSuite.Core, ResponseJourney);
        yield return TestCase.Async("ABI7 foreground and background filters forward the selected backend independently", TestSuite.Core, PipelineJourney);
        yield return TestCase.Async("native ABI7 switches both versions and preserves metadata under concurrent full-prefix calls", TestSuite.Native, NativeJourney, timeoutSeconds: 180);
    }

    private static void VersionAndLayoutJourney()
    {
        Check.Equal(60, Marshal.SizeOf<JungleSeedJudgeNativeClient.NativeRequirements>());
        string[] fields = ["Size", "PyramidItemMask", "PyramidGoldMinimum", "PyramidMaximumDepth", "CrimsonMaximumDistance",
            "JungleMinimumY", "JungleItemMask", "LifeCrystalMinimum", "SpelunkerPotionMinimum", "FeatherfallPotionMinimum",
            "StarfuryMaximumDistance", "FinchStaffMaximumDistance", "WorldSize", "Evil", "TerrariaVersion"];
        for (int index = 0; index < fields.Length; index++)
            Check.Equal(index * 4, Marshal.OffsetOf<JungleSeedJudgeNativeClient.NativeRequirements>(fields[index]).ToInt32());
        Check.Equal(TerrariaResourceJudgeVersion.Version1461, new ResourceJudgeRequirements(PyramidItemMask: 7).TargetBuild.Version);
        Check.Equal(TerrariaResourceJudgeVersion.Version1461, new ResourceJudgeRequirements(PyramidItemMask: 7,
            TerrariaVersion: TerrariaResourceJudgeVersion.Default).TargetBuild.Version);
        Check.Throws<ArgumentOutOfRangeException>(() => new ResourceJudgeRequirements(PyramidItemMask: 7,
            TerrariaVersion: (TerrariaResourceJudgeVersion)1460).Validate());

        AutoCreateWorldSettings settings = Settings();
        var signatures = new HashSet<string>();
        foreach ((string game, TerrariaResourceJudgeVersion expected) in Mappings())
        {
            TerrariaResourceJudgeBuild build = TerrariaResourceJudgeBuilds.ForGameVersion(game);
            Check.Equal(expected, build.Version);
            var q = WorldSeedFilterEvaluator.RequestedRequirements(settings, expected);
            var native = new JungleSeedJudgeNativeClient.NativeRequirements(q);
            Check.Equal(60u, native.Size);
            Check.Equal((int)expected, native.TerrariaVersion);
            string signature = WorldPoolSignature.From(settings, LanguageNames.English, game);
            Check.True(signature.Contains(build.GenerationCompatibilityId, StringComparison.Ordinal));
            Check.True(signature.Contains("resourceFilterV7", StringComparison.Ordinal));
            Check.True(signatures.Add(signature));
        }
        Check.Throws<InvalidOperationException>(() => TerrariaResourceJudgeBuilds.ForGameVersion("unreadable"));
        Check.True(WorldPoolSignature.From(settings, LanguageNames.English, "unreadable").Contains("generation=unavailable", StringComparison.Ordinal));
        string original = WorldPoolSignature.From(settings, LanguageNames.English, "1.4.6.1");
        foreach (Action<AutoCreateWorldSettings> change in new Action<AutoCreateWorldSettings>[]
        {
            s => s.WorldSize = AutoCreateWorldSize.Large, s => s.WorldEvil = AutoCreateWorldEvil.Corruption,
            s => s.WorldDifficulty = AutoCreateWorldDifficulty.Master, s => s.PyramidFilterItemMask = 1,
            s => s.PyramidFilterCoinPileMinimum = 3, s => s.FixedSeed = "123"
        })
        {
            AutoCreateWorldSettings changed = Settings(); change(changed);
            Check.False(original == WorldPoolSignature.From(changed, LanguageNames.English, "1.4.6.1"));
        }
    }

    private static void ResponseJourney()
    {
        string Serialize(JungleSeedJudgeResult result) => JsonSerializer.Serialize(result,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        foreach (TerrariaResourceJudgeVersion version in new[] { TerrariaResourceJudgeVersion.Version1458, TerrariaResourceJudgeVersion.Version1461 })
        {
            var q = new ResourceJudgeRequirements(PyramidItemMask: 7, TerrariaVersion: version);
            JungleSeedJudgeResult response = Result("0", "test", q, 1);
            Check.True(JungleSeedJudgeProtocolSerializer.DeserializeResponse(Serialize(response), "test", version).Complete);
            TerrariaResourceJudgeBuild other = TerrariaResourceJudgeBuilds.ForVersion(version == TerrariaResourceJudgeVersion.Version1458
                ? TerrariaResourceJudgeVersion.Version1461 : TerrariaResourceJudgeVersion.Version1458);
            foreach (JungleSeedJudgeResult wrong in new[]
            {
                response with { ProtocolVersion = 6 }, response with { CompatibilityId = other.CompatibilityId },
                response with { TerrariaVersion = other.TerrariaVersion }, response with { GenerationCompatibilityId = other.GenerationCompatibilityId },
                response with { TerrariaVersion = null }, response with { GenerationCompatibilityId = null }
            })
                Check.Throws<InvalidDataException>(() => JungleSeedJudgeProtocolSerializer.DeserializeResponse(Serialize(wrong), "test", version));
            Check.Throws<InvalidDataException>(() => JungleSeedJudgeProtocolSerializer.DeserializeResponse(Serialize(response), "different-id", version));
            JungleSeedJudgeResult failure = response with { Status = JungleSeedJudgeStatus.GenerationFailed,
                Decision = null, TerrariaVersion = null, GenerationCompatibilityId = null, Detail = "native generation failed" };
            Check.Equal(JungleSeedJudgeStatus.GenerationFailed,
                JungleSeedJudgeProtocolSerializer.DeserializeResponse(Serialize(failure), "test", version).Status);
            Check.True(JungleSeedJudgeProtocolSerializer.DeserializeResponse(Serialize(response with
                { RequestedThreads = 8, AvailableThreads = 16, Threads = 6 }), "test", version).Complete);
        }
    }

    private static async Task PipelineJourney(CancellationToken token)
    {
        var recorded = new List<TerrariaResourceJudgeVersion>();
        var client = new JungleSeedJudgeNativeClient((seed, _, id, q, threads) =>
        {
            recorded.Add(q.TerrariaVersion);
            return Result(seed, id, q, threads);
        }, TimeSpan.FromSeconds(5), new SemaphoreSlim(1, 1));
        using var evaluator = new WorldSeedFilterEvaluator(client);
        foreach ((string game, TerrariaResourceJudgeVersion expected) in Mappings())
        {
            var loop = new PyramidSeedPreScreenLoop(evaluator, _ => { });
            var ui = new SeedUi();
            Check.True((await loop.RunAsync(Settings(), TerrariaMenuProfile.FromVersion(game), ui, ui, token)).Accepted);
            Check.Equal(expected, recorded[^1]);
            // A foreground 1461 process must not choose the backend for a 1449/1458 server.
            var server = new TerrariaServerTarget("TerrariaServer.exe", game);
            using var background = new WorldPoolSeedFilter(1, client, () => "11");
            Check.Equal(1, (await background.FilterBatchAsync(Settings(), server.FilteringBuild.Version, token)).AcceptedSeeds.Count);
            Check.Equal(expected, recorded[^1]);
        }
        Check.Equal(Mappings().Count() * 2, recorded.Count);
    }

    private static async Task NativeJourney(CancellationToken token)
    {
        var client = new JungleSeedJudgeNativeClient(JungleSeedJudgeNativeLibraryLocator.ResolvePath(), TimeSpan.FromSeconds(60));
        foreach (TerrariaResourceJudgeVersion version in new[] { TerrariaResourceJudgeVersion.Version1458, TerrariaResourceJudgeVersion.Version1461 })
        {
            for (int size = 1; size <= 3; size++) for (int evil = 1; evil <= 2; evil++)
                await CheckNative(client, new(PyramidItemMask: 7, WorldSize: size, Evil: evil, TerrariaVersion: version), JungleSeedJudgeGameMode.Classic, token);
            foreach (JungleSeedJudgeGameMode mode in new[] { JungleSeedJudgeGameMode.Expert, JungleSeedJudgeGameMode.Master, JungleSeedJudgeGameMode.Journey })
                await CheckNative(client, new(PyramidItemMask: 7, TerrariaVersion: version), mode, token);
        }
        await CheckNative(client, new(PyramidItemMask: 7, TerrariaVersion: TerrariaResourceJudgeVersion.Default), JungleSeedJudgeGameMode.Classic, token);
        await CheckNative(client, new(PyramidItemMask: 7, TerrariaVersion: TerrariaResourceJudgeVersion.Version1458), JungleSeedJudgeGameMode.Classic, token);
        JungleSeedJudgeResult[] full = await Task.WhenAll(new[] { TerrariaResourceJudgeVersion.Version1458, TerrariaResourceJudgeVersion.Version1461 }
            .Select(version => CheckNative(client, new(LifeCrystalMinimum: 1, TerrariaVersion: version), JungleSeedJudgeGameMode.Classic, token)));
        Check.True(full.All(result => result.CheckpointPassIndex == 97));
        Check.False(full[0].GenerationCompatibilityId == full[1].GenerationCompatibilityId);
    }

    private static async Task<JungleSeedJudgeResult> CheckNative(JungleSeedJudgeNativeClient client, ResourceJudgeRequirements q,
        JungleSeedJudgeGameMode mode, CancellationToken token)
    {
        JungleSeedJudgeResult result = await client.AnalyzeAsync("0", mode, token, q, threads: 1);
        Check.True(result.Complete);
        Check.Equal(q.TargetBuild.TerrariaVersion, result.TerrariaVersion);
        Check.Equal(q.TargetBuild.GenerationCompatibilityId, result.GenerationCompatibilityId);
        Check.Equal(q.TargetBuild.CompatibilityId, result.CompatibilityId);
        Check.Equal(q.EndPass, result.PlannedEndPass);
        Check.Equal(1, result.Threads);
        return result;
    }

    private static JungleSeedJudgeResult Result(string seed, string id, ResourceJudgeRequirements q, int threads) =>
        new(JungleSeedJudgeProtocol.Version, id, q.TargetBuild.CompatibilityId, JungleSeedJudgeStatus.Complete,
            seed, JungleSeedJudgeDecision.Accepted, "RequirementsSatisfied", q.EndPass, q.EndPass,
            q.PyramidFastEligible ? "PyramidFast" : "FullPrefix", false, threads, 16, threads, 1, 1,
            TerrariaVersion: q.TargetBuild.TerrariaVersion, GenerationCompatibilityId: q.TargetBuild.GenerationCompatibilityId);

    private static AutoCreateWorldSettings Settings() => new()
    {
        EnableCheats = true, EnablePyramidFilter = true, PyramidFilterItemMask = 7, PyramidMaximumDepth = 0,
        WorldSize = AutoCreateWorldSize.Small, WorldEvil = AutoCreateWorldEvil.Crimson,
        WorldDifficulty = AutoCreateWorldDifficulty.Classic, JungleRouteDepth = AutoCreateJungleRouteDepth.None
    };

    private static IEnumerable<(string Game, TerrariaResourceJudgeVersion Version)> Mappings()
    {
        yield return ("1.4.4.9", TerrariaResourceJudgeVersion.Version1458);
        yield return ("1.4.5.8", TerrariaResourceJudgeVersion.Version1458);
        yield return ("1.4.6", TerrariaResourceJudgeVersion.Version1461);
        yield return ("1.4.6.1", TerrariaResourceJudgeVersion.Version1461);
    }

    private sealed class SeedUi : IPyramidSeedRandomizer, IPyramidVisibleSeedReader
    {
        private int seed = 10;
        public string ReadCurrentSeed() => seed.ToString();
        public bool TryPredictNextSeedBatch(int count, out IReadOnlyList<string> seeds, out string detail)
        {
            seeds = Enumerable.Range(seed + 1, count).Select(value => value.ToString()).ToArray(); detail = "predicted"; return true;
        }
        public Task<bool> RandomizeVisibleSeedAsync(int attempt, CancellationToken token) { seed++; return Task.FromResult(true); }
        public Task<PyramidVisibleSeedReadResult> WaitForSeedAfterRandomizeAsync(string? previous, CancellationToken token) =>
            Task.FromResult(PyramidVisibleSeedReadResult.FromSeed(seed.ToString(), 1));
    }
}
