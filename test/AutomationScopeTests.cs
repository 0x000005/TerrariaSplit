using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class AutomationScopeTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("TemporaryDirectoryScope cleans existing scratch files", TemporaryDirectoryScopeCleansScratchFiles);
    }

    private static void TemporaryDirectoryScopeCleansScratchFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "TerrariaSplit.Tests", "scratch-" + Guid.NewGuid().ToString("N"));
        string nestedDirectory = Path.Combine(directory, "nested");
        Directory.CreateDirectory(nestedDirectory);
        string rootFile = Path.Combine(directory, "root.tmp");
        string nestedFile = Path.Combine(nestedDirectory, "nested.tmp");
        File.WriteAllText(rootFile, "root");
        File.WriteAllText(nestedFile, "nested");

        using TemporaryDirectoryScope scope = TemporaryDirectoryScope.Prepare(directory);

        TestAssert.Equal(true, Directory.Exists(directory));
        TestAssert.Equal(false, File.Exists(rootFile));
        TestAssert.Equal(false, File.Exists(nestedFile));
        File.WriteAllText(rootFile, "new");
        scope.Clean();
        TestAssert.Equal(false, File.Exists(rootFile));

        Directory.Delete(directory, recursive: true);
    }
}
