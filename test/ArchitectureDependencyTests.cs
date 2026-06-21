using System.Text.RegularExpressions;

namespace TerrariaSplit.Tests;

internal static class ArchitectureDependencyTests
{
    private static readonly Regex WinFormsPattern = new(
        @"System\.Windows\.Forms|\bForm\b|\bControl\b",
        RegexOptions.Compiled);

    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("Architecture keeps Application free of WinForms", ApplicationDoesNotReferenceWinForms);
        yield return ("Architecture keeps Domain free of outer layers", DomainDoesNotReferenceOuterLayers);
        yield return ("Architecture keeps Terraria free of UI shell references", TerrariaDoesNotReferenceUiShell);
        yield return ("Architecture keeps Storage free of outer layers", StorageDoesNotReferenceOuterLayers);
        yield return ("Architecture keeps UI settings pages from starting automation", UiSettingsDoesNotStartAutomation);
        yield return ("Architecture keeps Application project free of Terraria and Storage", ApplicationProjectDoesNotReferenceTerrariaOrStorage);
        yield return ("Architecture keeps Configuration platform neutral", ConfigurationProjectDoesNotUseWindowsFormsOrDrawing);
        yield return ("Architecture has repository build safety props", RepositoryBuildSafetyPropsExist);
        yield return ("Architecture uses typed application effects", ApplicationEffectsAreTypedRecords);
        yield return ("Architecture uses typed application commands", ApplicationCommandsAreTypedRecords);
        yield return ("Architecture keeps AppSettings section-only", AppSettingsHasNoCompatibilityFacade);
        yield return ("Architecture keeps settings normalization current-schema only", SettingsNormalizerDoesNotRunObjectMigrations);
        yield return ("Architecture has no root namespace source files", RootNamespaceIsEmpty);
        yield return ("Architecture static dependency debt does not grow", StaticDependencyDebtDoesNotGrow);
        yield return ("Architecture reports next phase transition debt", NextPhaseTransitionDebtReport);
    }

    private static void ApplicationDoesNotReferenceWinForms()
    {
        AssertNoMatches(
            Path.Combine("TerrariaSplit", "Application"),
            WinFormsPattern,
            "Application must not reference WinForms.");
    }

    private static void DomainDoesNotReferenceOuterLayers()
    {
        AssertNoMatches(
            Path.Combine("TerrariaSplit", "Domain"),
            new Regex(
                @"System\.Windows\.Forms|TerrariaSplit\.UI|TerrariaSplit\.Storage|TerrariaSplit\.Terraria|AppSettingsStore|AppLogger",
                RegexOptions.Compiled),
            "Domain must stay pure and independent of outer layers.");
    }

    private static void TerrariaDoesNotReferenceUiShell()
    {
        AssertNoMatches(
            Path.Combine("TerrariaSplit", "Terraria"),
            new Regex(
                @"MainForm|SettingsPage|SettingsForm|OverlayWindow|TimerOverlay|ApplicationShellEffectExecutor",
                RegexOptions.Compiled),
            "Terraria integration must not reference WinForms shell implementations.");
    }

    private static void UiSettingsDoesNotStartAutomation()
    {
        AssertNoMatches(
            Path.Combine("TerrariaSplit", "UI", "Settings"),
            new Regex(
                @"StartCreateWorld|StartEnterWorld|TerrariaWorldAutomation|TerrariaMonitorCoordinator|WorldPoolFillService|GlobalHotkeyManager",
                RegexOptions.Compiled),
            "Settings pages edit data and must not start shell/runtime side effects directly.");
    }

    private static void StorageDoesNotReferenceOuterLayers()
    {
        AssertNoMatches(
            Path.Combine("TerrariaSplit", "Storage"),
            new Regex(
                @"TerrariaSplit\.UI|TerrariaSplit\.Application|TerrariaSplit\.Terraria",
                RegexOptions.Compiled),
            "Storage must not reference UI, Application, or Terraria integration layers.");
    }

    private static void ApplicationProjectDoesNotReferenceTerrariaOrStorage()
    {
        string root = FindRepositoryRoot();
        string projectPath = Path.Combine(root, "src", "TerrariaSplit.Application", "TerrariaSplit.Application.csproj");
        string project = File.ReadAllText(projectPath);
        string[] forbidden =
        [
            "TerrariaSplit.Storage",
            "TerrariaSplit.Terraria"
        ];
        string[] offenders = forbidden
            .Where(token => project.Contains(token, StringComparison.Ordinal))
            .ToArray();
        if (offenders.Length > 0)
        {
            throw new InvalidOperationException(
                "Application project must not reference concrete Storage or Terraria projects:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, offenders));
        }
    }

    private static void ConfigurationProjectDoesNotUseWindowsFormsOrDrawing()
    {
        string root = FindRepositoryRoot();
        string projectPath = Path.Combine(root, "src", "TerrariaSplit.Configuration", "TerrariaSplit.Configuration.csproj");
        string project = File.ReadAllText(projectPath);
        string[] forbiddenProjectTokens =
        [
            "net10.0-windows",
            "UseWindowsForms"
        ];
        string[] projectOffenders = forbiddenProjectTokens
            .Where(token => project.Contains(token, StringComparison.Ordinal))
            .ToArray();
        if (projectOffenders.Length > 0)
        {
            throw new InvalidOperationException(
                "Configuration project must stay platform neutral:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, projectOffenders));
        }

        AssertNoMatches(
            Path.Combine("TerrariaSplit", "Configuration"),
            new Regex(@"System\.Drawing|System\.Windows\.Forms|InstalledFontCollection", RegexOptions.Compiled),
            "Configuration source must not reference UI or Windows font APIs.");
    }

    private static void RootNamespaceIsEmpty()
    {
        AssertNoMatches(
            "TerrariaSplit",
            new Regex(@"^namespace TerrariaSplit;$", RegexOptions.Compiled),
            "Root namespace source files must be moved into layer namespaces.");
    }

    private static void StaticDependencyDebtDoesNotGrow()
    {
        AssertOnlyAllowedFilesReference(
            Path.Combine("TerrariaSplit", "Application"),
            "AppSettingsStore",
            []);

        AssertOnlyAllowedFilesReference(
            Path.Combine("TerrariaSplit", "Application"),
            "AppLogger",
            []);
    }

    private static void RepositoryBuildSafetyPropsExist()
    {
        string root = FindRepositoryRoot();
        string propsPath = Path.Combine(root, "Directory.Build.props");
        string props = File.Exists(propsPath)
            ? File.ReadAllText(propsPath)
            : throw new InvalidOperationException("Directory.Build.props is missing.");

        string[] requiredProperties =
        [
            "<Nullable>enable</Nullable>",
            "<ImplicitUsings>enable</ImplicitUsings>",
            "<AnalysisLevel>latest</AnalysisLevel>",
            "<TreatWarningsAsErrors>false</TreatWarningsAsErrors>",
            "<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>"
        ];
        string[] missing = requiredProperties
            .Where(property => !props.Contains(property, StringComparison.Ordinal))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "Directory.Build.props is missing required safety properties:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, missing));
        }
    }

    private static void NextPhaseTransitionDebtReport()
    {
        string root = FindRepositoryRoot();
        var report = new List<string>();
        AddProjectTokenDebt(
            root,
            report,
            "Application project concrete implementation references",
            Path.Combine("src", "TerrariaSplit.Application", "TerrariaSplit.Application.csproj"),
            "TerrariaSplit.Storage",
            "TerrariaSplit.Terraria");
        AddProjectTokenDebt(
            root,
            report,
            "Configuration project Windows dependency",
            Path.Combine("src", "TerrariaSplit.Configuration", "TerrariaSplit.Configuration.csproj"),
            "net10.0-windows",
            "UseWindowsForms");
        AddLinkedSourceDebt(root, report);
        AddRuntimeTokenDebt(root, report, "AppSettingsStore.", "AppSettingsStore static calls");
        AddRuntimeTokenDebt(root, report, "RuntimeDataPaths", "RuntimeDataPaths static references");
        AddRuntimeTokenDebt(root, report, "AppLogger.", "AppLogger static calls");
        AddInternalsVisibleToDebt(root, report);

        Console.WriteLine("Architecture transition debt report:");
        if (report.Count == 0)
        {
            Console.WriteLine("  none");
            return;
        }

        foreach (string line in report)
        {
            Console.WriteLine("  " + line);
        }
    }

    private static void ApplicationEffectsAreTypedRecords()
    {
        AssertNoMatches(
            Path.Combine("TerrariaSplit", "Application"),
            new Regex(@"\bApplicationEffectKind\b|ApplicationEffect\.", RegexOptions.Compiled),
            "Application effects must be expressed as concrete typed records, not Kind/factory combinations.");
    }

    private static void ApplicationCommandsAreTypedRecords()
    {
        AssertNoMatches(
            Path.Combine("TerrariaSplit", "Application"),
            new Regex(@"\bAppCommandKind\b", RegexOptions.Compiled),
            "Application commands must be expressed as concrete typed records, not Kind/factory combinations.");
    }

    private static void AppSettingsHasNoCompatibilityFacade()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, "TerrariaSplit", "Configuration", "AppSettings.cs");
        string appSettingsClass = File.ReadAllText(path)
            .Split("internal sealed class GeneralSettings", StringSplitOptions.None)[0];

        if (appSettingsClass.Contains("[JsonIgnore]", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AppSettings must expose persisted sections only; legacy facade properties belong in migrations.");
        }
    }

    private static void SettingsNormalizerDoesNotRunObjectMigrations()
    {
        AssertNoMatches(
            Path.Combine("TerrariaSplit", "Configuration", "SettingsNormalizer.cs"),
            new Regex(@"\bSettingsMigrator\b|\.Migrate\(settings\)", RegexOptions.Compiled),
            "SettingsNormalizer must only normalize the current schema; JSON/object compatibility belongs before normalization.");
    }

    private static void AssertNoMatches(string relativeDirectory, Regex pattern, string message)
    {
        string root = FindRepositoryRoot();
        string directory = Path.Combine(root, relativeDirectory);
        string[] matches = EnumerateSourceFiles(directory)
            .SelectMany(file => FindMatches(root, file, pattern))
            .ToArray();

        if (matches.Length > 0)
        {
            throw new InvalidOperationException(message + Environment.NewLine + string.Join(Environment.NewLine, matches));
        }
    }

    private static void AssertOnlyAllowedFilesReference(
        string relativeDirectory,
        string token,
        IReadOnlyCollection<string> allowedRelativeFiles)
    {
        string root = FindRepositoryRoot();
        string directory = Path.Combine(root, relativeDirectory);
        Regex pattern = new(@"\b" + Regex.Escape(token) + @"\b", RegexOptions.Compiled);
        string[] unexpectedFiles = EnumerateSourceFiles(directory)
            .Where(file => pattern.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(root, file))
            .Where(relativePath => !allowedRelativeFiles.Contains(relativePath, StringComparer.OrdinalIgnoreCase))
            .OrderBy(relativePath => relativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (unexpectedFiles.Length > 0)
        {
            throw new InvalidOperationException(
                $"{token} architecture debt grew unexpectedly:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, unexpectedFiles));
        }
    }

    private static void AddProjectTokenDebt(
        string root,
        List<string> report,
        string title,
        string relativeProjectPath,
        params string[] tokens)
    {
        string path = Path.Combine(root, relativeProjectPath);
        if (!File.Exists(path))
        {
            report.Add($"{title}: missing {relativeProjectPath}");
            return;
        }

        string content = File.ReadAllText(path);
        string[] offenders = tokens
            .Where(token => content.Contains(token, StringComparison.Ordinal))
            .ToArray();
        if (offenders.Length > 0)
        {
            report.Add($"{title}: {relativeProjectPath} contains {string.Join(", ", offenders)}");
        }
    }

    private static void AddLinkedSourceDebt(string root, List<string> report)
    {
        string srcRoot = Path.Combine(root, "src");
        string[] linkedProjects = Directory.EnumerateFiles(srcRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => File.ReadLines(path).Any(line => line.Contains(@"..\..\TerrariaSplit\", StringComparison.Ordinal)))
            .Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (linkedProjects.Length > 0)
        {
            report.Add($"linked source projects ({linkedProjects.Length}): {string.Join(", ", linkedProjects)}");
        }
    }

    private static void AddRuntimeTokenDebt(
        string root,
        List<string> report,
        string token,
        string title)
    {
        string[] files = EnumerateRuntimeSourceFiles(root)
            .Where(file => File.ReadAllText(file).Contains(token, StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(root, file))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length > 0)
        {
            report.Add($"{title} ({files.Length} files): {FormatSample(files)}");
        }
    }

    private static void AddInternalsVisibleToDebt(string root, List<string> report)
    {
        string[] files = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadLines(file).Any(line =>
                line.Contains("InternalsVisibleTo", StringComparison.Ordinal) &&
                !line.Contains("TerrariaSplit.Tests", StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(root, file))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length > 0)
        {
            report.Add($"runtime InternalsVisibleTo debt ({files.Length} files): {FormatSample(files)}");
        }
    }

    private static IEnumerable<string> EnumerateRuntimeSourceFiles(string root)
    {
        return EnumerateSourceFiles(Path.Combine(root, "TerrariaSplit"))
            .Concat(EnumerateSourceFiles(Path.Combine(root, "src")));
    }

    private static string FormatSample(IReadOnlyList<string> values)
    {
        const int Maximum = 8;
        string sample = string.Join(", ", values.Take(Maximum));
        return values.Count <= Maximum
            ? sample
            : sample + $", ... +{values.Count - Maximum} more";
    }

    private static IEnumerable<string> FindMatches(string root, string file, Regex pattern)
    {
        int lineNumber = 0;
        foreach (string line in File.ReadLines(file))
        {
            lineNumber++;
            if (pattern.IsMatch(line))
            {
                yield return $"{Path.GetRelativePath(root, file)}:{lineNumber}: {line.Trim()}";
            }
        }
    }

    private static IEnumerable<string> EnumerateSourceFiles(string directory)
    {
        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TerrariaSplit.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate TerrariaSplit.slnx from test output directory.");
    }
}
