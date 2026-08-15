using System.Xml.Linq;

namespace TerrariaSplit.Tests;

internal static class QualityGateTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("project references preserve the declared architecture dependency direction", TestSuite.Core, ProjectDependencyGraph);
        yield return TestCase.Sync("source code preserves platform and side-effect boundaries", TestSuite.Core, SourceBoundaries);
        yield return TestCase.Sync("memory control unit exposes only its documented command surface", TestSuite.Core, MemoryControlUnitCommandSurface);
        yield return TestCase.Sync("portable package contract uses the exact release name and managed-root manifest", TestSuite.Release, PortablePackageContract);
    }

    private static void ProjectDependencyGraph()
    {
        string root = SourceRoot();
        Dictionary<string, HashSet<string>> graph = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path)!, ReadReferences, StringComparer.OrdinalIgnoreCase);
        var allowed = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["TerrariaSplit.Application"] = Set("TerrariaSplit.Configuration", "TerrariaSplit.Domain", "TerrariaSplit.Infrastructure"),
            ["TerrariaSplit.Configuration"] = Set("TerrariaSplit.Domain"),
            ["TerrariaSplit.Domain"] = Set(),
            ["TerrariaSplit.Infrastructure"] = Set(),
            ["TerrariaSplit.Infrastructure.Windows"] = Set("TerrariaSplit.Infrastructure"),
            ["TerrariaSplit.MemoryBridge"] = Set(),
            ["TerrariaSplit.Race.Client"] = Set("TerrariaSplit.Configuration", "TerrariaSplit.Domain", "TerrariaSplit.Race.Contracts"),
            ["TerrariaSplit.Race.Contracts"] = Set("TerrariaSplit.Race.Determinism"),
            ["TerrariaSplit.Race.Determinism"] = Set(),
            ["TerrariaSplit.Race.InGame"] = Set(),
            ["TerrariaSplit.Race.Server"] = Set("TerrariaSplit.Race.Contracts"),
            ["TerrariaSplit.Statistics"] = Set("TerrariaSplit.Configuration", "TerrariaSplit.Domain"),
            ["TerrariaSplit.Storage"] = Set("TerrariaSplit.Application", "TerrariaSplit.Configuration", "TerrariaSplit.Domain", "TerrariaSplit.Infrastructure"),
            ["TerrariaSplit.Terraria"] = Set(
                "TerrariaSplit.Application",
                "TerrariaSplit.Configuration",
                "TerrariaSplit.Domain",
                "TerrariaSplit.Infrastructure",
                "TerrariaSplit.Infrastructure.Windows",
                "TerrariaSplit.Race.Determinism",
                "TerrariaSplit.Race.InGame"),
            ["TerrariaSplit.WinForms"] = Set(
                "TerrariaSplit.Application",
                "TerrariaSplit.Configuration",
                "TerrariaSplit.Domain",
                "TerrariaSplit.Infrastructure",
                "TerrariaSplit.Infrastructure.Windows",
                "TerrariaSplit.MemoryBridge",
                "TerrariaSplit.Race.Client",
                "TerrariaSplit.Race.InGame",
                "TerrariaSplit.Statistics",
                "TerrariaSplit.Storage",
                "TerrariaSplit.Terraria"),
            ["TerrariaSplit.WorldGeneration"] = Set(),
            ["TerrariaSplit.WorldGuard.Payload"] = Set("TerrariaSplit.Race.Determinism", "TerrariaSplit.Race.InGame")
        };
        foreach ((string project, HashSet<string> references) in graph)
        {
            Check.True(allowed.TryGetValue(project, out HashSet<string>? permitted));
            Check.Equal(
                string.Join("|", permitted!.Order(StringComparer.OrdinalIgnoreCase)),
                string.Join("|", references.Order(StringComparer.OrdinalIgnoreCase)));
        }

        Check.False(HasDependencyCycle(graph));
        Check.False(graph["TerrariaSplit.Terraria"].Contains("TerrariaSplit.Storage"));
    }

    private static void SourceBoundaries()
    {
        string source = Path.Combine(SourceRoot(), "src");
        string domain = ReadSourceTree(Path.Combine(source, "TerrariaSplit.Domain"));
        AssertOmits(domain, "System.Drawing", "System.Windows.Forms", "System.IO", "File.", "Directory.", "Process.");

        string application = ReadSourceTree(Path.Combine(source, "TerrariaSplit.Application"));
        AssertOmits(
            application,
            "System.Diagnostics.Process",
            "Process.GetProcess",
            "DllImport(",
            "LibraryImport(",
            "System.Runtime.InteropServices");

        string infrastructure = ReadSourceTree(Path.Combine(source, "TerrariaSplit.Infrastructure"));
        AssertOmits(
            infrastructure,
            "DllImport(",
            "LibraryImport(",
            "winmm.dll",
            "kernel32.dll",
            "CreateWaitableTimer",
            "timeBeginPeriod");

        string rendering = ReadSourceTree(Path.Combine(
            source,
            "TerrariaSplit.WinForms",
            "UI",
            "Rendering"));
        AssertOmits(rendering, "File.", "Directory.", "StaticAppLogger", "IAppLogger");
    }

    private static void MemoryControlUnitCommandSurface()
    {
        string root = SourceRoot();
        string program = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TerrariaSplit.MemoryBridge",
            "Program.cs"));
        foreach (string command in new[] { "\"inject\"", "\"runtime-layout\"", "\"visible-seed\"", "\"random-seed-batch\"" })
        {
            Check.True(program.Contains(command, StringComparison.Ordinal));
        }

        Check.False(program.Contains("\"random-seed-candidates\"", StringComparison.Ordinal));
    }

    private static void PortablePackageContract()
    {
        string root = SourceRoot();
        string targets = File.ReadAllText(Path.Combine(root, "src", "TerrariaSplit.WinForms", "Build", "PortablePackage.targets"));
        Check.True(targets.Contains("TerrariaSplit-v$(FileVersion)-win-x64.zip", StringComparison.Ordinal));
        Check.True(targets.Contains(ApplicationUpdatePackage.ManifestFileName, StringComparison.Ordinal));
        Check.True(targets.Contains("Runtime\\terrariasplit-update-manifest.json", StringComparison.Ordinal));
        foreach (string managedRoot in new[] { "TerrariaSplit.exe", "TerrariaSplit.MemoryBridge.exe", "Runtime", "Assets" })
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

    private static HashSet<string> Set(params string[] values)
    {
        return values.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasDependencyCycle(IReadOnlyDictionary<string, HashSet<string>> graph)
    {
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool Visit(string project)
        {
            if (visiting.Contains(project))
            {
                return true;
            }

            if (!visited.Add(project))
            {
                return false;
            }

            visiting.Add(project);
            foreach (string dependency in graph[project])
            {
                if (graph.ContainsKey(dependency) && Visit(dependency))
                {
                    return true;
                }
            }

            visiting.Remove(project);
            return false;
        }

        return graph.Keys.Any(Visit);
    }

    private static string ReadSourceTree(string directory)
    {
        return string.Join(
            "\n",
            Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));
    }

    private static void AssertOmits(string source, params string[] forbidden)
    {
        foreach (string text in forbidden)
        {
            Check.False(source.Contains(text, StringComparison.Ordinal));
        }
    }

    private static string SourceRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "TerrariaSplit.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate source root.");
    }
}
