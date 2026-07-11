using System.Xml.Linq;

namespace TerrariaSplit.Tests;

internal static class QualityGateTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("project references preserve the declared architecture dependency direction", TestSuite.Core, ProjectDependencyGraph);
        yield return TestCase.Sync("portable package contract uses the exact release name and managed-root manifest", TestSuite.Release, PortablePackageContract);
    }

    private static void ProjectDependencyGraph()
    {
        string root = SourceRoot();
        Dictionary<string, HashSet<string>> graph = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path)!, ReadReferences, StringComparer.OrdinalIgnoreCase);
        Check.Equal(0, graph["TerrariaSplit.Domain"].Count);
        Check.False(graph["TerrariaSplit.Application"].Contains("TerrariaSplit.WinForms"));
        Check.False(graph["TerrariaSplit.Terraria"].Contains("TerrariaSplit.WinForms"));
        Check.True(graph["TerrariaSplit.WinForms"].Contains("TerrariaSplit.Application"));
        Check.True(graph["TerrariaSplit.WinForms"].Contains("TerrariaSplit.Terraria"));
    }

    private static void PortablePackageContract()
    {
        string root = SourceRoot();
        string targets = File.ReadAllText(Path.Combine(root, "src", "TerrariaSplit.WinForms", "Build", "PortablePackage.targets"));
        Check.True(targets.Contains("TerrariaSplit-v$(FileVersion)-win-x64.zip", StringComparison.Ordinal));
        Check.True(targets.Contains(ApplicationUpdatePackage.ManifestFileName, StringComparison.Ordinal));
        foreach (string managedRoot in new[] { "TerrariaSplit.exe", "TerrariaSplit.MemoryProbe.exe", "Assets" })
        {
            Check.True(targets.Contains(managedRoot, StringComparison.OrdinalIgnoreCase));
        }
        foreach (string protectedRoot in new[] { "Settings", "Data", "Worlds", "terrariasplit.log" })
        {
            Check.False(targets.Contains($"&quot;{protectedRoot}&quot;", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static HashSet<string> ReadReferences(string projectPath)
    {
        XDocument document = XDocument.Load(projectPath);
        return document.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension((string?)element.Attribute("Include") ?? string.Empty))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string SourceRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "TerrariaSplit.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate source root.");
    }
}
