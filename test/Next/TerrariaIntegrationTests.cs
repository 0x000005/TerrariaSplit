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
        yield return TestCase.Sync("post-generation filter accepts Crimson between dungeon and spawn and rejects other placement", TestSuite.Flow, CrimsonCorridorPostFilter);
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

    private static void CrimsonCorridorPostFilter()
    {
        using var directory = new TestDirectory();
        string betweenPath = directory.Combine("between.wld");
        string outsidePath = directory.Combine("outside.wld");
        File.WriteAllBytes(betweenPath, CreatePostFilterWorld(crimsonTileX: 1000));
        File.WriteAllBytes(outsidePath, CreatePostFilterWorld(crimsonTileX: 3000));

        var scanner = new TerrariaWorldFilePyramidScanner();
        Check.True(scanner.TryScanCrimsonBetweenDungeonAndSpawn(betweenPath, out CrimsonCorridorScanResult between, out string betweenDetail));
        Check.Equal(string.Empty, betweenDetail);
        Check.True(between.HasCrimson);
        Check.True(between.CrimsonTileCount >= 300);
        Check.Equal(201, between.Bounds.Left);
        Check.Equal(2100, between.Bounds.Right);

        Check.True(scanner.TryScanCrimsonBetweenDungeonAndSpawn(outsidePath, out CrimsonCorridorScanResult outside, out string outsideDetail));
        Check.Equal(string.Empty, outsideDetail);
        Check.False(outside.HasCrimson);
        Check.Equal(0, outside.CrimsonTileCount);

        var settings = new AutoCreateWorldSettings
        {
            WorldSize = AutoCreateWorldSize.Small,
            WorldEvil = AutoCreateWorldEvil.Crimson,
            EnablePyramidFilter = false,
            RequireCrimsonBetweenDungeonAndSpawn = true
        };
        var evaluator = new PyramidFilterWorldFileEvaluator(scanner);
        PyramidFilterWorldFileResult kept = evaluator.Evaluate(betweenPath, settings);
        PyramidFilterWorldFileResult rejected = evaluator.Evaluate(outsidePath, settings);
        Check.True(kept.Keep);
        Check.True(kept.CrimsonCorridorFilterEnabled);
        Check.False(rejected.Keep);

        string enabledSignature = WorldPoolSignature.From(settings);
        settings.RequireCrimsonBetweenDungeonAndSpawn = false;
        Check.False(string.Equals(enabledSignature, WorldPoolSignature.From(settings), StringComparison.Ordinal));
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

    private static byte[] CreatePostFilterWorld(int crimsonTileX)
    {
        const int version = 279;
        const int width = 4200;
        const int height = 1200;
        const int importanceCount = 753;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(version);
        writer.Write(0x026369676F6C6572UL);
        writer.Write((uint)0);
        writer.Write((ulong)0);
        writer.Write((short)3);
        long pointersPosition = stream.Position;
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write((ushort)importanceCount);
        writer.Write(new byte[(importanceCount + 7) / 8]);

        int headerOffset = checked((int)stream.Position);
        writer.Write("post-filter-world");
        writer.Write("12345");
        writer.Write((ulong)version);
        writer.Write(Guid.Empty.ToByteArray());
        writer.Write(12345);
        writer.Write(0);
        writer.Write(width * 16);
        writer.Write(0);
        writer.Write(height * 16);
        writer.Write(height);
        writer.Write(width);
        writer.Write(0); // classic
        for (int index = 0; index < 8; index++) writer.Write(false);
        writer.Write(DateTime.UnixEpoch.ToBinary());
        writer.Write((byte)0);
        for (int index = 0; index < 17; index++) writer.Write(0);
        writer.Write(2100); // spawn x
        writer.Write(250);  // spawn y
        writer.Write(300d);
        writer.Write(500d);
        writer.Write(0d);
        writer.Write(true);
        writer.Write(0);
        writer.Write(false);
        writer.Write(false);
        writer.Write(200); // dungeon x
        writer.Write(250); // dungeon y
        writer.Write(true);

        int tileOffset = checked((int)stream.Position);
        for (int x = 0; x < width; x++)
        {
            if (x == crimsonTileX)
            {
                writer.Write((byte)0x82); // active tile, Int16 RLE
                writer.Write((byte)203);  // Crimstone
                writer.Write((short)(height - 1));
            }
            else
            {
                writer.Write((byte)0x80); // empty tile, Int16 RLE
                writer.Write((short)(height - 1));
            }
        }

        int chestOffset = checked((int)stream.Position);
        writer.Write((short)0);
        writer.Write((short)40);
        long end = stream.Position;
        stream.Position = pointersPosition;
        writer.Write(headerOffset);
        writer.Write(tileOffset);
        writer.Write(chestOffset);
        stream.Position = end;
        return stream.ToArray();
    }
}
