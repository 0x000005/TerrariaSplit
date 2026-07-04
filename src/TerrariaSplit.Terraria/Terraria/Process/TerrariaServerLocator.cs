using System.Diagnostics;

namespace TerrariaSplit.Terraria.Processes;

internal readonly record struct TerrariaServerTarget(string ExePath, string? FileVersion)
{
    public bool IsLegacy1449 => TerrariaMenuProfile.IsLegacy1449Version(FileVersion);
}

// Resolves the path to TerrariaServer.exe, which ships alongside Terraria.exe in the
// game install. The world pool runs it headlessly to generate worlds in the background,
// so the lookup must work even when the game itself is not running.
internal static class TerrariaServerLocator
{
    private const string ServerExeName = "TerrariaServer.exe";

    private static readonly string[] DefaultInstallDirectories =
    {
        @"C:\Program Files (x86)\Steam\steamapps\common\Terraria",
        @"C:\Program Files\Steam\steamapps\common\Terraria"
    };

    private static TerrariaServerTarget? cachedTarget;

    public static string? TryResolve()
    {
        return TryResolveTarget()?.ExePath;
    }

    public static TerrariaServerTarget? TryResolveTarget()
    {
        if (TryResolveInDirectory(TryGetRunningGameDirectory(), out TerrariaServerTarget runningTarget))
        {
            cachedTarget = runningTarget;
            return runningTarget;
        }

        if (cachedTarget is TerrariaServerTarget cached && File.Exists(cached.ExePath))
        {
            cachedTarget = CreateTarget(cached.ExePath);
            return cachedTarget;
        }

        foreach (string directory in DefaultInstallDirectories)
        {
            if (TryResolveInDirectory(directory, out TerrariaServerTarget defaultTarget))
            {
                cachedTarget = defaultTarget;
                return defaultTarget;
            }
        }

        cachedTarget = null;
        return null;
    }

    private static bool TryResolveInDirectory(string? directory, out TerrariaServerTarget target)
    {
        target = default;
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            string candidate = Path.Combine(directory, ServerExeName);
            if (!File.Exists(candidate))
            {
                return false;
            }

            target = CreateTarget(candidate);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
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

    private static TerrariaServerTarget CreateTarget(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            fullPath = path;
        }

        return new TerrariaServerTarget(fullPath, TryGetFileVersion(fullPath));
    }

    private static string? TryGetFileVersion(string path)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            return string.IsNullOrWhiteSpace(info.FileVersion)
                ? info.ProductVersion
                : info.FileVersion;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
