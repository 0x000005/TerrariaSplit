using System.Diagnostics;

namespace TerrariaSplit;

// Resolves the path to TerrariaServer.exe, which ships alongside Terraria.exe in the
// game install. The seed pool runs it headlessly to generate worlds in the background,
// so the lookup must work even when the game itself is not running.
internal static class TerrariaServerLocator
{
    private const string ServerExeName = "TerrariaServer.exe";

    private static readonly string[] DefaultInstallDirectories =
    {
        @"C:\Program Files (x86)\Steam\steamapps\common\Terraria",
        @"C:\Program Files\Steam\steamapps\common\Terraria"
    };

    private static string? cachedPath;

    public static string? TryResolve()
    {
        if (cachedPath is not null && File.Exists(cachedPath))
        {
            return cachedPath;
        }

        cachedPath = null;
        foreach (string? directory in CandidateDirectories())
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            try
            {
                string candidate = Path.Combine(directory, ServerExeName);
                if (File.Exists(candidate))
                {
                    cachedPath = candidate;
                    return candidate;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
            }
        }

        return null;
    }

    private static IEnumerable<string?> CandidateDirectories()
    {
        yield return TryGetRunningGameDirectory();
        foreach (string directory in DefaultInstallDirectories)
        {
            yield return directory;
        }
    }

    private static string? TryGetRunningGameDirectory()
    {
        try
        {
            using Process? process = TerrariaProcessFinder.FindNewest();
            string? fileName = process?.MainModule?.FileName;
            return string.IsNullOrWhiteSpace(fileName) ? null : Path.GetDirectoryName(fileName);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }
}
