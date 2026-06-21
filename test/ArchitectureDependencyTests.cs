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
        yield return ("Architecture keeps UI settings pages from starting automation", UiSettingsDoesNotStartAutomation);
        yield return ("Architecture static dependency debt does not grow", StaticDependencyDebtDoesNotGrow);
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

    private static void StaticDependencyDebtDoesNotGrow()
    {
        AssertOnlyAllowedFilesReference(
            Path.Combine("TerrariaSplit", "Application"),
            "AppSettingsStore",
            [
                Path.Combine("TerrariaSplit", "Application", "ApplicationController.cs"),
                Path.Combine("TerrariaSplit", "Application", "WorldPoolFillService.cs")
            ]);

        AssertOnlyAllowedFilesReference(
            Path.Combine("TerrariaSplit", "Application"),
            "AppLogger",
            [
                Path.Combine("TerrariaSplit", "Application", "AutomationRunner.cs"),
                Path.Combine("TerrariaSplit", "Application", "TerrariaMonitorCoordinator.cs"),
                Path.Combine("TerrariaSplit", "Application", "WorldPoolFillService.cs")
            ]);
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
