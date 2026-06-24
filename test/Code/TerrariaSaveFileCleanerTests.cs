using System.Reflection;
using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class TerrariaSaveFileCleanerTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("Terraria save cleaner moves player second backups", MovesPlayerSecondBackups);
        yield return ("Terraria save cleaner moves world second backups", MovesWorldSecondBackups);
    }

    private static void MovesPlayerSecondBackups()
    {
        string directory = TestTempDirectory("SaveCleaner");
        try
        {
            string root = Path.Combine(directory, "Terraria");
            string backupRoot = Path.Combine(directory, "Deleted");
            string playersPath = Path.Combine(root, "Players");
            Directory.CreateDirectory(playersPath);

            WriteFile(Path.Combine(playersPath, "throwaway.plr"));
            WriteFile(Path.Combine(playersPath, "throwaway.plr.bak"));
            WriteFile(Path.Combine(playersPath, "throwaway.plr.bak2"));
            WriteFile(Path.Combine(playersPath, "throwaway", "map.dat"));
            WriteFile(Path.Combine(playersPath, "legacy.plr.bak2"));
            WriteFile(Path.Combine(playersPath, "legacy", "map.dat"));
            WriteFile(Path.Combine(playersPath, "keeper.plr"));
            WriteFile(Path.Combine(playersPath, "keeper.plr.bak2"));
            WriteFile(Path.Combine(playersPath, "keptLegacy.plr.bak2"));

            int moved = InvokeMoveNonFavoritePlayers(
                root,
                backupRoot,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "keeper.plr", "keptLegacy.plr" });

            TestAssert.Equal(2, moved);
            AssertMoved(playersPath, backupRoot, "Players", "throwaway.plr");
            AssertMoved(playersPath, backupRoot, "Players", "throwaway.plr.bak");
            AssertMoved(playersPath, backupRoot, "Players", "throwaway.plr.bak2");
            AssertDirectoryMoved(playersPath, backupRoot, "Players", "throwaway");
            AssertMoved(playersPath, backupRoot, "Players", "legacy.plr.bak2");
            AssertDirectoryMoved(playersPath, backupRoot, "Players", "legacy");
            TestAssert.Equal(true, File.Exists(Path.Combine(playersPath, "keeper.plr")));
            TestAssert.Equal(true, File.Exists(Path.Combine(playersPath, "keeper.plr.bak2")));
            TestAssert.Equal(false, File.Exists(Path.Combine(backupRoot, "Players", "keeper.plr.bak2")));
            TestAssert.Equal(true, File.Exists(Path.Combine(playersPath, "keptLegacy.plr.bak2")));
            TestAssert.Equal(false, File.Exists(Path.Combine(backupRoot, "Players", "keptLegacy.plr.bak2")));
        }
        finally
        {
            DeleteDirectoryIfExists(directory);
        }
    }

    private static void MovesWorldSecondBackups()
    {
        string directory = TestTempDirectory("SaveCleaner");
        try
        {
            string root = Path.Combine(directory, "Terraria");
            string backupRoot = Path.Combine(directory, "Deleted");
            string worldsPath = Path.Combine(root, "Worlds");
            Directory.CreateDirectory(worldsPath);

            WriteFile(Path.Combine(worldsPath, "throwaway.wld"));
            WriteFile(Path.Combine(worldsPath, "throwaway.wld.bak"));
            WriteFile(Path.Combine(worldsPath, "throwaway.wld.bak2"));
            WriteFile(Path.Combine(worldsPath, "throwaway.twld"));
            WriteFile(Path.Combine(worldsPath, "throwaway.twld.bak"));
            WriteFile(Path.Combine(worldsPath, "throwaway.twld.bak2"));
            WriteFile(Path.Combine(worldsPath, "legacy.wld.bak2"));
            WriteFile(Path.Combine(worldsPath, "legacy.twld.bak2"));
            WriteFile(Path.Combine(worldsPath, "keeper.wld"));
            WriteFile(Path.Combine(worldsPath, "keeper.wld.bak2"));
            WriteFile(Path.Combine(worldsPath, "keptLegacy.wld.bak2"));

            int moved = InvokeMoveNonFavoriteWorlds(
                root,
                backupRoot,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "keeper.wld", "keptLegacy.wld" });

            TestAssert.Equal(2, moved);
            AssertMoved(worldsPath, backupRoot, "Worlds", "throwaway.wld");
            AssertMoved(worldsPath, backupRoot, "Worlds", "throwaway.wld.bak");
            AssertMoved(worldsPath, backupRoot, "Worlds", "throwaway.wld.bak2");
            AssertMoved(worldsPath, backupRoot, "Worlds", "throwaway.twld");
            AssertMoved(worldsPath, backupRoot, "Worlds", "throwaway.twld.bak");
            AssertMoved(worldsPath, backupRoot, "Worlds", "throwaway.twld.bak2");
            AssertMoved(worldsPath, backupRoot, "Worlds", "legacy.wld.bak2");
            AssertMoved(worldsPath, backupRoot, "Worlds", "legacy.twld.bak2");
            TestAssert.Equal(true, File.Exists(Path.Combine(worldsPath, "keeper.wld")));
            TestAssert.Equal(true, File.Exists(Path.Combine(worldsPath, "keeper.wld.bak2")));
            TestAssert.Equal(false, File.Exists(Path.Combine(backupRoot, "Worlds", "keeper.wld.bak2")));
            TestAssert.Equal(true, File.Exists(Path.Combine(worldsPath, "keptLegacy.wld.bak2")));
            TestAssert.Equal(false, File.Exists(Path.Combine(backupRoot, "Worlds", "keptLegacy.wld.bak2")));
        }
        finally
        {
            DeleteDirectoryIfExists(directory);
        }
    }

    private static int InvokeMoveNonFavoritePlayers(string root, string backupRoot, HashSet<string> favorites)
    {
        MethodInfo method = typeof(TerrariaSaveFileCleaner).GetMethod(
                "MoveNonFavoritePlayers",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MoveNonFavoritePlayers method was not found.");

        return (int)(method.Invoke(null, [root, backupRoot, favorites])
            ?? throw new InvalidOperationException("MoveNonFavoritePlayers returned null."));
    }

    private static int InvokeMoveNonFavoriteWorlds(string root, string backupRoot, HashSet<string> favorites)
    {
        MethodInfo method = typeof(TerrariaSaveFileCleaner).GetMethod(
                "MoveNonFavoriteWorlds",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MoveNonFavoriteWorlds method was not found.");

        return (int)(method.Invoke(null, [root, backupRoot, favorites])
            ?? throw new InvalidOperationException("MoveNonFavoriteWorlds returned null."));
    }

    private static void AssertMoved(string savePath, string backupRoot, string category, string fileName)
    {
        TestAssert.Equal(false, File.Exists(Path.Combine(savePath, fileName)));
        TestAssert.Equal(true, File.Exists(Path.Combine(backupRoot, category, fileName)));
    }

    private static void AssertDirectoryMoved(string savePath, string backupRoot, string category, string directoryName)
    {
        TestAssert.Equal(false, Directory.Exists(Path.Combine(savePath, directoryName)));
        TestAssert.Equal(true, Directory.Exists(Path.Combine(backupRoot, category, directoryName)));
    }

    private static void WriteFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1]);
    }

    private static string TestTempDirectory(string name)
    {
        string path = Path.Combine(
            FindSourceRoot(),
            "test",
            "Temp",
            name,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindSourceRoot()
    {
        string directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "TerrariaSplit.slnx")))
            {
                return directory;
            }

            string siblingSourceRoot = Path.Combine(directory, "TerrariaSplit");
            if (File.Exists(Path.Combine(siblingSourceRoot, "TerrariaSplit.slnx")))
            {
                return siblingSourceRoot;
            }

            string? parent = Directory.GetParent(directory)?.FullName;
            if (string.Equals(parent, directory, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            directory = parent ?? string.Empty;
        }

        throw new DirectoryNotFoundException("TerrariaSplit source root was not found.");
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
