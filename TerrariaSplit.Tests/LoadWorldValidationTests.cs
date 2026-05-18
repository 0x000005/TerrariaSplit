using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class LoadWorldValidationTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("Load world validation accepts one valid file with a slot name", AcceptsOneValidFileWithSlotName);
        yield return ("Load world validation requires a slot name", RequiresSlotName);
        yield return ("Load world validation requires at least one valid file", RequiresAtLeastOneValidFile);
    }

    private static void AcceptsOneValidFileWithSlotName()
    {
        using TempDirectory temp = new();
        string playerPath = temp.WriteFile("player.plr");
        string worldPath = temp.WriteFile("world.wld");

        TestAssert.Equal(
            true,
            EnterWorldSaveInstaller.TryValidate(
                new PracticeWorldSlot { Name = "Player only", PlayerFilePath = playerPath },
                out _));
        TestAssert.Equal(
            true,
            EnterWorldSaveInstaller.TryValidate(
                new PracticeWorldSlot { Name = "World only", WorldFilePath = worldPath },
                out _));
    }

    private static void RequiresSlotName()
    {
        using TempDirectory temp = new();
        string playerPath = temp.WriteFile("player.plr");

        TestAssert.Equal(
            false,
            EnterWorldSaveInstaller.TryValidate(
                new PracticeWorldSlot { PlayerFilePath = playerPath },
                out _));
    }

    private static void RequiresAtLeastOneValidFile()
    {
        TestAssert.Equal(
            false,
            EnterWorldSaveInstaller.TryValidate(
                new PracticeWorldSlot { Name = "Empty" },
                out _));
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

        public string WriteFile(string fileName)
        {
            string path = System.IO.Path.Combine(Path, fileName);
            File.WriteAllBytes(path, [1]);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
