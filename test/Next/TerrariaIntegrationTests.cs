using System.Text;
using TerrariaSplit.Terraria.Automation;
using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Tests;

internal static class TerrariaIntegrationTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("pyramid pre-screen evaluates known positive, item mismatch and no-pyramid seeds", TestSuite.Flow, PyramidPredictionJourney, timeoutSeconds: 30);
        yield return TestCase.Async("race world upload validates, hashes, deduplicates, locates and deletes a Terraria world", TestSuite.Flow, WorldFileTransferJourney);
        yield return TestCase.Sync("world automation settings normalize incompatible options and secret seed lists", TestSuite.Core, WorldSettingsNormalization);
    }

    private static void PyramidPredictionJourney()
    {
        var evaluator = new PyramidSeedPreScreenEvaluator();
        var settings = new AutoCreateWorldSettings
        {
            EnablePyramidFilter = true,
            WorldSize = AutoCreateWorldSize.Small,
            WorldDifficulty = AutoCreateWorldDifficulty.Classic,
            WorldEvil = AutoCreateWorldEvil.Crimson,
            PyramidFilterItemMask = AutoCreatePyramidFilterItem.SandstormInABottleMask
        };
        PyramidSeedPreScreenPrediction accepted = evaluator.Evaluate(settings, "540278984", TerrariaWorldGenerationVersion.Modern1456);
        Check.True(accepted.CanUsePrediction);
        Check.True(accepted.AcceptSeed);
        Check.True(accepted.Result.LootSummary.Contains("Sandstorm in a Bottle", StringComparison.Ordinal));

        settings.PyramidFilterItemMask = AutoCreatePyramidFilterItem.FlyingCarpetMask;
        PyramidSeedPreScreenPrediction mismatch = evaluator.Evaluate(settings, "540278984", TerrariaWorldGenerationVersion.Modern1456);
        Check.False(mismatch.AcceptSeed);
        Check.Equal("item mismatch", mismatch.RejectReason);
        PyramidSeedPreScreenPrediction absent = evaluator.Evaluate(settings, "702683177", TerrariaWorldGenerationVersion.Modern1456);
        Check.False(absent.AcceptSeed);
        Check.Equal("no pyramid", absent.RejectReason);
    }

    private static async Task WorldFileTransferJourney(CancellationToken cancellationToken)
    {
        using var directory = new TestDirectory();
        byte[] world = CreateMinimalWorld();
        string path = directory.Combine("source.wld");
        await File.WriteAllBytesAsync(path, world, cancellationToken);
        Check.True(RaceWorldFileValidator.IsValidWorldFilePath(path));
        Check.False(RaceWorldFileValidator.IsValidWorldFilePath(directory.Combine("missing.wld")));

        var store = new RaceWorldFileStore(directory.Combine("server"));
        RaceStoredWorldFile first;
        await using (var stream = new MemoryStream(world))
        {
            first = await store.SaveAsync("a-b-12", " host ", "../race.wld", stream, cancellationToken);
        }
        await using (var stream = new MemoryStream(world))
        {
            RaceStoredWorldFile second = await store.SaveAsync("a-b-12", "host", "race.wld", stream, cancellationToken);
            Check.Equal(first.Path, second.Path);
            Check.Equal(first.Info.Sha256, second.Info.Sha256);
        }
        Check.True(store.TryGetPath("AB12", first.Info, out string storedPath));
        Check.Equal(first.Path, storedPath);
        store.DeleteRoom("AB12");
        Check.False(File.Exists(first.Path));
    }

    private static void WorldSettingsNormalization()
    {
        var settings = new AutoCreateWorldSettings
        {
            WorldSize = "invalid",
            WorldDifficulty = "invalid",
            WorldEvil = "invalid",
            SpecialSeeds = "for the worthy, FOR THE WORTHY, not the bees",
            SecretSeeds = "  first ; second\nfirst  ",
            PyramidFilterItemMask = int.MaxValue
        };
        SettingsNormalizer.Normalize(new AppSettings { Automation = { AutoCreate = settings } });
        Check.Equal(AutoCreateWorldSize.Small, settings.WorldSize);
        Check.Equal(AutoCreateWorldDifficulty.Classic, settings.WorldDifficulty);
        Check.Equal(AutoCreateWorldEvil.Crimson, settings.WorldEvil);
        Check.True((settings.PyramidFilterItemMask & ~AutoCreatePyramidFilterItem.AllMask) == 0);
        Check.Equal(2, AutoCreateSeedList.Parse(settings.SecretSeeds).Count);
    }

    private static byte[] CreateMinimalWorld()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(279);
        writer.Write(0x026369676F6C6572UL);
        writer.Write((uint)0);
        writer.Write((ulong)0);
        writer.Write((short)1);
        long pointerPosition = stream.Position;
        writer.Write(0);
        writer.Write((short)0);
        int headerPosition = checked((int)stream.Position);
        writer.Write("test-world");
        writer.Write(new byte[16]);
        long end = stream.Position;
        stream.Position = pointerPosition;
        writer.Write(headerPosition);
        stream.Position = end;
        return stream.ToArray();
    }
}
