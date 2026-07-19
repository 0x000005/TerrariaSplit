using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Updating;

internal enum ApplicationUpdateCheckKind
{
    UpToDate,
    UpdateAvailable
}

internal sealed record ApplicationUpdateRelease(
    Version Version,
    Uri DownloadUri,
    string Sha256,
    long Size);

internal sealed record ApplicationUpdateCheckResult(
    ApplicationUpdateCheckKind Kind,
    Version CurrentVersion,
    ApplicationUpdateRelease? Release);

internal readonly record struct ApplicationUpdateProgress(long BytesReceived, long? TotalBytes, bool Verifying = false);

internal sealed record PreparedApplicationUpdate(
    ApplicationUpdateRelease Release,
    string WorkDirectory,
    string PackageDirectory)
{
    public void Discard()
    {
        try
        {
            if (Directory.Exists(WorkDirectory))
            {
                Directory.Delete(WorkDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }
}

internal interface IApplicationUpdateService : IDisposable
{
    Version CurrentVersion { get; }

    Task<ApplicationUpdateCheckResult> CheckAsync(CancellationToken cancellationToken);

    Task<PreparedApplicationUpdate> PrepareAsync(
        ApplicationUpdateRelease release,
        IProgress<ApplicationUpdateProgress>? progress,
        CancellationToken cancellationToken);
}

internal sealed class GitHubApplicationUpdateService : IApplicationUpdateService
{
    internal const string RepositoryOwner = "0x000005";
    internal const string RepositoryName = "TerrariaSplit";
    internal const long MaximumPackageBytes = 512L * 1024 * 1024;
    private static readonly Uri LatestReleaseUri = new(
        $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest");
    private static readonly Regex StableTagPattern = new(
        "^v(?<version>\\d+\\.\\d+\\.\\d+\\.\\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly HttpClient httpClient;

    public GitHubApplicationUpdateService(HttpMessageHandler? handler = null, Version? currentVersion = null)
    {
        httpClient = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        httpClient.Timeout = Timeout.InfiniteTimeSpan;
        httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("TerrariaSplit", ApplicationVersion.Current.ToString(4)));
        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        CurrentVersion = currentVersion ?? ApplicationVersion.Current;
    }

    public Version CurrentVersion { get; }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    public async Task<ApplicationUpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using HttpResponseMessage response = await httpClient.GetAsync(LatestReleaseUri, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
        JsonElement root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
        {
            throw new InvalidDataException("GitHub latest release is not a stable release.");
        }

        string tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        Match match = StableTagPattern.Match(tag);
        if (!match.Success || !Version.TryParse(match.Groups["version"].Value, out Version? version) || version.Revision < 0)
        {
            throw new InvalidDataException($"Unsupported GitHub release tag: {tag}");
        }

        if (version <= CurrentVersion)
        {
            return new ApplicationUpdateCheckResult(ApplicationUpdateCheckKind.UpToDate, CurrentVersion, null);
        }

        string expectedName = ApplicationUpdatePackage.AssetName(version);
        JsonElement? matchingAsset = root.GetProperty("assets")
            .EnumerateArray()
            .FirstOrDefault(asset => string.Equals(
                asset.GetProperty("name").GetString(),
                expectedName,
                StringComparison.Ordinal));
        if (!matchingAsset.HasValue || matchingAsset.Value.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidDataException($"Release asset {expectedName} is missing.");
        }

        JsonElement asset = matchingAsset.Value;
        string digest = asset.TryGetProperty("digest", out JsonElement digestElement)
            ? digestElement.GetString() ?? string.Empty
            : string.Empty;
        if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || digest.Length != 71)
        {
            throw new InvalidDataException("Release asset has no valid SHA-256 digest.");
        }

        long size = asset.GetProperty("size").GetInt64();
        if (size <= 0 || size > MaximumPackageBytes)
        {
            throw new InvalidDataException("Release asset size is outside the allowed range.");
        }

        string url = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? downloadUri) || downloadUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException("Release asset download URL is invalid.");
        }

        var release = new ApplicationUpdateRelease(
            version,
            downloadUri,
            digest[7..].ToLowerInvariant(),
            size);
        return new ApplicationUpdateCheckResult(ApplicationUpdateCheckKind.UpdateAvailable, CurrentVersion, release);
    }

    public async Task<PreparedApplicationUpdate> PrepareAsync(
        ApplicationUpdateRelease release,
        IProgress<ApplicationUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        string workDirectory = Path.Combine(
            Path.GetTempPath(),
            "TerrariaSplit",
            "updates",
            $"{release.Version.ToString(4)}-{Guid.NewGuid():N}");
        string archivePath = Path.Combine(workDirectory, ApplicationUpdatePackage.AssetName(release.Version));
        string packageDirectory = Path.Combine(workDirectory, "package");
        Directory.CreateDirectory(workDirectory);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(15));
            using HttpResponseMessage response = await httpClient.GetAsync(
                release.DownloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            response.EnsureSuccessStatusCode();
            long? contentLength = response.Content.Headers.ContentLength;
            if (contentLength is > MaximumPackageBytes)
            {
                throw new InvalidDataException("Downloaded update is too large.");
            }

            await using Stream source = await response.Content.ReadAsStreamAsync(timeout.Token);
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[1024 * 128];
            long received = 0;
            await using (FileStream destination = new(
                archivePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 128,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                while (true)
                {
                    int read = await source.ReadAsync(buffer, timeout.Token);
                    if (read == 0)
                    {
                        break;
                    }

                    received += read;
                    if (received > MaximumPackageBytes)
                    {
                        throw new InvalidDataException("Downloaded update is too large.");
                    }

                    hash.AppendData(buffer, 0, read);
                    await destination.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                    progress?.Report(new ApplicationUpdateProgress(received, contentLength ?? release.Size));
                }

                await destination.FlushAsync(timeout.Token);
            }

            progress?.Report(new ApplicationUpdateProgress(received, contentLength ?? release.Size, Verifying: true));
            string actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(actualHash),
                    Convert.FromHexString(release.Sha256)))
            {
                throw new InvalidDataException("Downloaded update SHA-256 digest does not match GitHub.");
            }

            ApplicationUpdatePackage.ExtractSafely(archivePath, packageDirectory);
            ApplicationUpdatePackage.Validate(packageDirectory, release.Version);
            return new PreparedApplicationUpdate(release, workDirectory, packageDirectory);
        }
        catch
        {
            TryDeleteDirectory(workDirectory);
            throw;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}

internal static class ApplicationVersion
{
    public static Version Current { get; } = ReadCurrent();

    private static Version ReadCurrent()
    {
        string? executablePath = Environment.ProcessPath;
        string? value = executablePath is null
            ? null
            : FileVersionInfo.GetVersionInfo(executablePath).FileVersion;
        return Version.TryParse(value, out Version? version) && version.Revision >= 0
            ? version
            : new Version(0, 0, 0, 0);
    }
}

internal sealed record ApplicationUpdateManifest(int SchemaVersion, string[] ManagedRoots);

internal static class ApplicationUpdatePackage
{
    public const string ManifestDirectoryName = "Runtime";
    public const string ManifestFileName = "terrariasplit-update-manifest.json";
    public const string MainExecutableName = "TerrariaSplit.exe";
    private static readonly HashSet<string> ProtectedRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "Settings", "Data", "Worlds", "terrariasplit.log"
    };

    public static string AssetName(Version version) => $"TerrariaSplit-v{version.ToString(4)}-win-x64.zip";

    public static void ExtractSafely(string archivePath, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        string root = Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar;
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string target = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Update archive contains an unsafe path.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: false);
        }
    }

    public static ApplicationUpdateManifest Validate(string packageDirectory, Version? expectedVersion = null)
    {
        ApplicationUpdateManifest manifest = ReadManifest(packageDirectory);
        if (manifest.SchemaVersion != 1 || manifest.ManagedRoots.Length == 0)
        {
            throw new InvalidDataException("Update manifest schema is unsupported.");
        }

        foreach (string root in manifest.ManagedRoots)
        {
            ValidateManagedRoot(root);
            string path = Path.Combine(packageDirectory, root);
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new InvalidDataException($"Managed update path is missing: {root}");
            }
        }

        if (!manifest.ManagedRoots.Contains(MainExecutableName, StringComparer.OrdinalIgnoreCase) ||
            !manifest.ManagedRoots.Contains(ManifestDirectoryName, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Update manifest does not manage required application files.");
        }

        if (expectedVersion is not null)
        {
            string executablePath = Path.Combine(packageDirectory, MainExecutableName);
            string? fileVersion = FileVersionInfo.GetVersionInfo(executablePath).FileVersion;
            if (!Version.TryParse(fileVersion, out Version? packageVersion) || packageVersion != expectedVersion)
            {
                throw new InvalidDataException("Update package version does not match the GitHub release.");
            }
        }

        return manifest;
    }

    public static ApplicationUpdateManifest ReadManifest(string directory)
    {
        string path = ManifestPath(directory);
        if (!File.Exists(path))
        {
            throw new InvalidDataException("Update manifest is missing.");
        }

        ApplicationUpdateManifest? manifest = JsonSerializer.Deserialize<ApplicationUpdateManifest>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return manifest ?? throw new InvalidDataException("Update manifest is invalid.");
    }

    public static string ManifestPath(string directory) =>
        Path.Combine(directory, ManifestDirectoryName, ManifestFileName);

    public static void ValidateManagedRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root) ||
            Path.IsPathRooted(root) ||
            root.Contains(Path.DirectorySeparatorChar) ||
            root.Contains(Path.AltDirectorySeparatorChar) ||
            root is "." or ".." ||
            ProtectedRoots.Contains(root))
        {
            throw new InvalidDataException($"Unsafe managed update root: {root}");
        }
    }
}

internal static class ApplicationUpdateLauncher
{
    public static void Launch(PreparedApplicationUpdate update, int parentProcessId, string targetDirectory)
    {
        VerifyTargetWritable(targetDirectory);
        string currentExecutable = Path.Combine(targetDirectory, ApplicationUpdatePackage.MainExecutableName);
        if (!File.Exists(currentExecutable))
        {
            throw new InvalidOperationException("The installed TerrariaSplit executable is unavailable.");
        }

        string helperPath = Path.Combine(update.WorkDirectory, "TerrariaSplit.Update.exe");
        File.Copy(currentExecutable, helperPath, overwrite: true);

        var startInfo = new ProcessStartInfo
        {
            FileName = helperPath,
            WorkingDirectory = update.WorkDirectory,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--apply-update");
        startInfo.ArgumentList.Add("--parent-pid");
        startInfo.ArgumentList.Add(parentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--package");
        startInfo.ArgumentList.Add(update.PackageDirectory);
        startInfo.ArgumentList.Add("--target");
        startInfo.ArgumentList.Add(targetDirectory);
        startInfo.ArgumentList.Add("--work");
        startInfo.ArgumentList.Add(update.WorkDirectory);
        _ = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the update helper.");
    }

    private static void VerifyTargetWritable(string targetDirectory)
    {
        string probe = Path.Combine(targetDirectory, $".terrariasplit-update-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probe, string.Empty);
        }
        finally
        {
            if (File.Exists(probe))
            {
                File.Delete(probe);
            }
        }
    }
}

internal static class ApplicationUpdateCommandLine
{
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Contains("--apply-update", StringComparer.Ordinal))
        {
            exitCode = RunApply(args);
            return true;
        }

        if (TryValue(args, "--cleanup-update", out string? work) &&
            TryValue(args, "--updater-pid", out string? updaterPidText) &&
            int.TryParse(updaterPidText, out int updaterPid))
        {
            WaitForProcess(updaterPid, TimeSpan.FromSeconds(30));
            TryDeleteWithRetries(work!, 10);
        }

        return false;
    }

    private static int RunApply(string[] args)
    {
        if (!TryValue(args, "--parent-pid", out string? parentText) ||
            !int.TryParse(parentText, out int parentPid) ||
            !TryValue(args, "--package", out string? package) ||
            !TryValue(args, "--target", out string? target) ||
            !TryValue(args, "--work", out string? work))
        {
            return 2;
        }

        try
        {
            if (!WaitForProcess(parentPid, TimeSpan.FromMinutes(2)))
            {
                throw new TimeoutException("TerrariaSplit did not exit before the update timeout.");
            }

            ApplyPackage(package!, target!, Path.Combine(work!, "backup"));
            string executable = Path.Combine(target!, ApplicationUpdatePackage.MainExecutableName);
            var restart = new ProcessStartInfo { FileName = executable, WorkingDirectory = target!, UseShellExecute = false };
            restart.ArgumentList.Add("--cleanup-update");
            restart.ArgumentList.Add(work!);
            restart.ArgumentList.Add("--updater-pid");
            restart.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _ = Process.Start(restart) ?? throw new InvalidOperationException("Could not restart TerrariaSplit.");
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(Path.Combine(work!, "update-error.log"), ex.ToString());
            }
            catch
            {
            }

            SettingsMessageDialog.ShowThemed(
                null,
                "TerrariaSplit Update",
                "TerrariaSplit update failed and the previous version was restored.\n\n" + ex.Message +
                "\n\nTerrariaSplit 更新失败，已尝试恢复原版本。",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error,
                static key => key);
            string executable = Path.Combine(target!, ApplicationUpdatePackage.MainExecutableName);
            if (File.Exists(executable))
            {
                Process.Start(new ProcessStartInfo { FileName = executable, WorkingDirectory = target!, UseShellExecute = false });
            }

            return 1;
        }
    }

    internal static void ApplyPackage(string packageDirectory, string targetDirectory, string backupDirectory)
    {
        ApplicationUpdateManifest next = ApplicationUpdatePackage.Validate(packageDirectory);
        ApplicationUpdateManifest current = File.Exists(ApplicationUpdatePackage.ManifestPath(targetDirectory))
            ? ApplicationUpdatePackage.ReadManifest(targetDirectory)
            : next;
        string[] roots = current.ManagedRoots
            .Concat(next.ManagedRoots)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (string root in roots)
        {
            ApplicationUpdatePackage.ValidateManagedRoot(root);
        }

        Directory.CreateDirectory(backupDirectory);
        var moved = new List<string>();
        var installed = new List<string>();
        try
        {
            foreach (string root in roots)
            {
                string existing = Path.Combine(targetDirectory, root);
                if (!File.Exists(existing) && !Directory.Exists(existing))
                {
                    continue;
                }

                Move(existing, Path.Combine(backupDirectory, root));
                moved.Add(root);
            }

            foreach (string root in next.ManagedRoots)
            {
                installed.Add(root);
                Copy(Path.Combine(packageDirectory, root), Path.Combine(targetDirectory, root));
            }
        }
        catch
        {
            foreach (string root in installed.AsEnumerable().Reverse())
            {
                Delete(Path.Combine(targetDirectory, root));
            }

            foreach (string root in moved.AsEnumerable().Reverse())
            {
                Move(Path.Combine(backupDirectory, root), Path.Combine(targetDirectory, root));
            }

            throw;
        }
    }

    private static bool TryValue(string[] args, string key, out string? value)
    {
        int index = Array.IndexOf(args, key);
        value = index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        return value is not null;
    }

    private static bool WaitForProcess(int processId, TimeSpan timeout)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return process.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static void Copy(string source, string destination)
    {
        if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: false);
            return;
        }

        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static void Move(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (!string.Equals(
                Path.GetPathRoot(source),
                Path.GetPathRoot(destination),
                StringComparison.OrdinalIgnoreCase))
        {
            Copy(source, destination);
            Delete(source);
            return;
        }

        if (File.Exists(source))
        {
            File.Move(source, destination);
        }
        else
        {
            Directory.Move(source, destination);
        }
    }

    private static void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void TryDeleteWithRetries(string path, int attempts)
    {
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                return;
            }
            catch when (i + 1 < attempts)
            {
                Thread.Sleep(200);
            }
        }
    }
}
