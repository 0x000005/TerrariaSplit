using System.Text;
using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class WorldSaveMetadataTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("Terraria world metadata reads Journey game mode", TerrariaWorldMetadataReadsJourneyGameMode);
        yield return ("Favorite world count ignores incompatible Journey group", FavoriteWorldCountIgnoresIncompatibleJourneyGroup);
    }

    private static void TerrariaWorldMetadataReadsJourneyGameMode()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "Journey.wld");
        WriteWorldFile(path, TerrariaWorldGameMode.Journey);

        TestAssert.Equal(true, TerrariaWorldSaveMetadata.TryReadGameMode(path, out TerrariaWorldGameMode gameMode));
        TestAssert.Equal(TerrariaWorldGameMode.Journey, gameMode);
    }

    private static void FavoriteWorldCountIgnoresIncompatibleJourneyGroup()
    {
        using TempDirectory temp = new();
        string journeyPath = Path.Combine(temp.Path, "Journey.wld");
        string classicPath = Path.Combine(temp.Path, "Classic.wld");
        string masterPath = Path.Combine(temp.Path, "Master.wld");
        WriteWorldFile(journeyPath, TerrariaWorldGameMode.Journey);
        WriteWorldFile(classicPath, TerrariaWorldGameMode.Classic);
        WriteWorldFile(masterPath, TerrariaWorldGameMode.Master);

        var favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFileName(journeyPath),
            Path.GetFileName(classicPath),
            Path.GetFileName(masterPath)
        };

        TestAssert.Equal(
            1,
            TerrariaSaveFileCleaner.CountCompatibleFavoriteWorldFiles(
                temp.Path,
                favorites,
                TerrariaWorldGameMode.Journey));
        TestAssert.Equal(
            2,
            TerrariaSaveFileCleaner.CountCompatibleFavoriteWorldFiles(
                temp.Path,
                favorites,
                TerrariaWorldGameMode.Classic));
    }

    private static void WriteWorldFile(string path, TerrariaWorldGameMode gameMode)
    {
        const uint version = 317;
        const short sectionCount = 11;

        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: false);
        writer.Write(version);
        writer.Write(Encoding.UTF8.GetBytes("relogic"));
        writer.Write((byte)2);
        writer.Write(0);
        writer.Write(0UL);
        writer.Write(sectionCount);

        long pointerOffset = stream.Position;
        for (int i = 0; i < sectionCount; i++)
        {
            writer.Write(0);
        }

        int headerOffset = (int)stream.Position;
        writer.Write("Test World");
        writer.Write("test-seed");
        writer.Write(0UL);
        writer.Write(Guid.Empty.ToByteArray());
        writer.Write(1);
        writer.Write(0);
        writer.Write(8400);
        writer.Write(0);
        writer.Write(2400);
        writer.Write(1200);
        writer.Write(4200);
        writer.Write((int)gameMode);

        int endOffset = (int)stream.Position;
        stream.Position = pointerOffset;
        writer.Write(headerOffset);
        for (int i = 1; i < sectionCount; i++)
        {
            writer.Write(endOffset);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "TerrariaSplitTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
