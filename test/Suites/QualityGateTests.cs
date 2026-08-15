using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using TerrariaSplit.MemoryBridge.Protocol;

namespace TerrariaSplit.Tests;

internal static class QualityGateTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("project references preserve the declared architecture dependency direction", TestSuite.Core, ProjectDependencyGraph);
        yield return TestCase.Sync("memory control unit exposes only its documented command surface", TestSuite.Core, MemoryControlUnitCommandSurface);
        yield return TestCase.Sync("build outputs use one SDK artifacts root", TestSuite.Core, CentralBuildOutputContract);
        yield return TestCase.Sync("release layout publishes a directory and managed-root manifest", TestSuite.Release, ReleaseLayoutContract);
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
                "TerrariaSplit.MemoryBridge.Payload",
                "TerrariaSplit.Race.Client",
                "TerrariaSplit.Race.Contracts",
                "TerrariaSplit.Race.InGame",
                "TerrariaSplit.Statistics",
                "TerrariaSplit.Storage",
                "TerrariaSplit.Terraria"),
            ["TerrariaSplit.MemoryBridge.Payload"] = Set("TerrariaSplit.Race.Determinism", "TerrariaSplit.Race.InGame")
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
        Check.Equal("net10.0", ReadTargetFramework(Path.Combine(root, "src", "TerrariaSplit.Storage", "TerrariaSplit.Storage.csproj")));
        Check.Equal("net10.0", ReadTargetFramework(Path.Combine(root, "src", "TerrariaSplit.Statistics", "TerrariaSplit.Statistics.csproj")));
    }

    private static void MemoryControlUnitCommandSurface()
    {
        string root = SourceRoot();
        string[] commands = typeof(MemoryBridgeCommands)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && !field.IsInitOnly)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Check.Sequence(
            new[] { "inject", "random-seed-batch", "runtime-layout" },
            commands);

        XDocument memoryBridgeProject = ReadXml(Path.Combine(
            root,
            "src",
            "TerrariaSplit.MemoryBridge",
            "TerrariaSplit.MemoryBridge.csproj"));
        Check.Equal("win-x86", RequiredProperty(memoryBridgeProject, "RuntimeIdentifier"));
        Check.Equal("x86", RequiredProperty(memoryBridgeProject, "PlatformTarget"));

        XDocument terrariaProject = ReadXml(Path.Combine(
            root,
            "src",
            "TerrariaSplit.Terraria",
            "TerrariaSplit.Terraria.csproj"));
        Check.True(terrariaProject.Descendants()
            .Where(element => element.Name.LocalName == "Compile")
            .Select(element => (string?)element.Attribute("Include"))
            .Any(include => include?.EndsWith(
                @"TerrariaSplit.MemoryBridge\Protocol\MemoryBridgeProtocol.cs",
                StringComparison.OrdinalIgnoreCase) == true));
    }

    private static void CentralBuildOutputContract()
    {
        string root = SourceRoot();
        XDocument buildProps = ReadXml(Path.Combine(root, "Directory.Build.props"));
        Check.Equal("true", RequiredProperty(buildProps, "UseArtifactsOutput"));
        Check.True(RequiredProperty(buildProps, "ArtifactsPath").Contains(".build", StringComparison.Ordinal));
        Check.True(Version.TryParse(
            RequiredProperty(buildProps, "TerrariaSplitProductVersion"),
            out _));
        Check.False(DeclaresProperty(buildProps, "DisableFastUpToDateCheck"));
        Check.False(DeclaresProperty(buildProps, "TerrariaSplitIsolationRoot"));

        foreach (string project in new[] { "TerrariaSplit.Tests.csproj", "TerrariaSplit.Diagnostics.csproj" })
        {
            XDocument testProject = ReadXml(Path.Combine(root, "test", project));
            Check.False(DeclaresProperty(testProject, "BaseOutputPath"));
            Check.False(DeclaresProperty(testProject, "BaseIntermediateOutputPath"));
        }
    }

    private static void ReleaseLayoutContract()
    {
        string root = SourceRoot();
        XDocument releaseLayout = ReadXml(Path.Combine(root, "src", "TerrariaSplit.WinForms", "Build", "ReleaseLayout.targets"));
        XElement cleanTarget = RequiredTarget(releaseLayout, "TerrariaSplitCleanFinalPublishDirectory");
        Check.Equal("PrepareForPublish", (string?)cleanTarget.Attribute("BeforeTargets"));
        XElement finalizeTarget = RequiredTarget(releaseLayout, "TerrariaSplitFinalizeReleaseLayout");
        Check.Equal("Publish", (string?)finalizeTarget.Attribute("AfterTargets"));
        XElement manifestWriter = finalizeTarget.Descendants()
            .Single(element => element.Name.LocalName == "WriteLinesToFile");
        Check.Equal(
            "$(TerrariaSplitUpdateManifestPath)",
            (string?)manifestWriter.Attribute("File"));
        string manifestPath = RequiredProperty(
            releaseLayout,
            "TerrariaSplitUpdateManifestPath");
        Check.True(manifestPath.EndsWith(
            Path.Combine("Runtime", ApplicationUpdatePackage.ManifestFileName),
            StringComparison.OrdinalIgnoreCase));
        using JsonDocument manifest = JsonDocument.Parse(
            (string?)manifestWriter.Attribute("Lines") ?? string.Empty);
        HashSet<string> managedRoots = manifest.RootElement
            .GetProperty("managedRoots")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Check.True(new[] { "TerrariaSplit.exe", "TerrariaSplit.MemoryBridge.exe", "TerrariaSplit.WorldFilter.dll", "Runtime", "Assets" }
            .All(managedRoots.Contains));
        Check.False(new[] { "Settings", "Data", "Worlds", "terrariasplit.log" }
            .Any(managedRoots.Contains));
        Check.False(releaseLayout.Descendants().Any(element =>
            element.Name.LocalName.Contains("Zip", StringComparison.OrdinalIgnoreCase) ||
            element.Attributes().Any(attribute => attribute.Value.Contains(".zip", StringComparison.OrdinalIgnoreCase))));
        Check.False(File.Exists(Path.Combine(root, "src", "TerrariaSplit.WinForms", "Build", "PortablePackage.targets")));
        Check.False(File.Exists(Path.Combine(root, "eng", "Validate-PortablePackage.ps1")));

        XDocument clientProject = ReadXml(Path.Combine(root, "src", "TerrariaSplit.WinForms", "TerrariaSplit.WinForms.csproj"));
        XDocument serverProject = ReadXml(Path.Combine(root, "src", "TerrariaSplit.Race.Server", "TerrariaSplit.Race.Server.csproj"));
        Check.True(RequiredProperty(clientProject, "PublishDir").Contains(
            @"publish\TerrariaSplit-v$(FileVersion)-$(RuntimeIdentifier)",
            StringComparison.Ordinal));
        Check.True(RequiredProperty(serverProject, "PublishDir").Contains(
            @"publish\TerrariaSplit.Race.Server-v$(FileVersion)-$(RuntimeIdentifier)",
            StringComparison.Ordinal));
        Check.Sequence(
            new[] { "linux-x64", "win-x64" },
            RequiredProperty(serverProject, "RuntimeIdentifiers")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Order(StringComparer.Ordinal));
        Check.Equal(
            "PrepareForPublish",
            (string?)RequiredTarget(serverProject, "TerrariaSplitCleanServerFinalPublishDirectory")
                .Attribute("BeforeTargets"));
    }

    private static HashSet<string> ReadReferences(string projectPath)
    {
        XDocument document = XDocument.Load(projectPath);
        return document.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension((string?)element.Attribute("Include") ?? string.Empty))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string ReadTargetFramework(string projectPath)
    {
        return RequiredProperty(ReadXml(projectPath), "TargetFramework");
    }

    private static XDocument ReadXml(string path) => XDocument.Load(path);

    private static string RequiredProperty(XDocument document, string name)
    {
        return document.Descendants()
            .Single(element => element.Name.LocalName == name)
            .Value.Trim();
    }

    private static bool DeclaresProperty(XDocument document, string name)
    {
        return document.Descendants().Any(element => element.Name.LocalName == name);
    }

    private static XElement RequiredTarget(XDocument document, string name)
    {
        return document.Descendants()
            .Single(element =>
                element.Name.LocalName == "Target" &&
                string.Equals((string?)element.Attribute("Name"), name, StringComparison.Ordinal));
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

    private static string SourceRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "TerrariaSplit.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate source root.");
    }
}
