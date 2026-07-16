using System.IO.Compression;
using System.Net;
using System.Text;

namespace TerrariaSplit.Tests;

internal static class UpdateFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Async("update discovery accepts only a newer stable release with the exact asset and digest", TestSuite.Flow, ReleaseDiscovery);
        yield return TestCase.Sync("update package blocks traversal and protected user roots before installation", TestSuite.Flow, PackageSafety);
        yield return TestCase.Sync("update installation replaces managed files, removes obsolete assets and preserves user data", TestSuite.Flow, InstallationJourney);
    }

    private static async Task ReleaseDiscovery(CancellationToken cancellationToken)
    {
        const string digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string valid = $$"""
        { "draft": false, "prerelease": false, "tag_name": "v2.0.0.0", "assets": [{
          "name": "TerrariaSplit-v2.0.0.0-win-x64.zip", "digest": "sha256:{{digest}}", "size": 42,
          "browser_download_url": "https://github.com/0x000005/TerrariaSplit/releases/download/v2.0.0.0/TerrariaSplit-v2.0.0.0-win-x64.zip"
        }] }
        """;
        using (var service = new GitHubApplicationUpdateService(new StaticResponseHandler(valid), new Version(1, 9, 0, 0)))
        {
            ApplicationUpdateCheckResult result = await service.CheckAsync(cancellationToken);
            Check.Equal(ApplicationUpdateCheckKind.UpdateAvailable, result.Kind);
            Check.Equal(new Version(2, 0, 0, 0), result.Release!.Version);
            Check.Equal(digest, result.Release.Sha256);
        }

        string[] rejected =
        [
            valid.Replace("\"draft\": false", "\"draft\": true"),
            valid.Replace("\"prerelease\": false", "\"prerelease\": true"),
            valid.Replace("TerrariaSplit-v2.0.0.0-win-x64.zip", "TerrariaSplit_v2.0.0.0.zip"),
            valid.Replace("sha256:" + digest, "")
        ];
        foreach (string json in rejected)
        {
            using var service = new GitHubApplicationUpdateService(new StaticResponseHandler(json), new Version(1, 9, 0, 0));
            await Check.ThrowsAsync<InvalidDataException>(() => service.CheckAsync(cancellationToken));
        }

        using (var service = new GitHubApplicationUpdateService(
            new StaticResponseHandler(valid.Replace("v2.0.0.0", "v1.8.0.0")), new Version(1, 9, 0, 0)))
        {
            Check.Equal(ApplicationUpdateCheckKind.UpToDate, (await service.CheckAsync(cancellationToken)).Kind);
        }
    }

    private static void PackageSafety()
    {
        using var directory = new TestDirectory();
        string archivePath = directory.Combine("unsafe.zip");
        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            using StreamWriter writer = new(archive.CreateEntry("../outside.txt").Open());
            writer.Write("unsafe");
        }
        Check.Throws<InvalidDataException>(() => ApplicationUpdatePackage.ExtractSafely(archivePath, directory.Combine("package")));
        Check.False(File.Exists(directory.Combine("outside.txt")));

        string package = directory.Combine("protected-package");
        Directory.CreateDirectory(package);
        WriteManifest(package, "TerrariaSplit.exe", "Settings");
        Check.Throws<InvalidDataException>(() => ApplicationUpdatePackage.Validate(package));
    }

    private static void InstallationJourney()
    {
        using var directory = new TestDirectory();
        string target = directory.Combine("target");
        string package = directory.Combine("package");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(package);
        WriteManifest(target, "TerrariaSplit.exe", "Assets", ApplicationUpdatePackage.ManifestDirectoryName);
        File.WriteAllText(Path.Combine(target, "TerrariaSplit.exe"), "old");
        Directory.CreateDirectory(Path.Combine(target, "Assets"));
        File.WriteAllText(Path.Combine(target, "Assets", "obsolete.txt"), "obsolete");
        Directory.CreateDirectory(Path.Combine(target, "Settings"));
        File.WriteAllText(Path.Combine(target, "Settings", "settings.json"), "user-settings");
        Directory.CreateDirectory(Path.Combine(target, "Data"));
        File.WriteAllText(Path.Combine(target, "Data", "pb.json"), "user-data");

        WriteManifest(package, "TerrariaSplit.exe", "Assets", ApplicationUpdatePackage.ManifestDirectoryName);
        File.WriteAllText(Path.Combine(package, "TerrariaSplit.exe"), "new");
        Directory.CreateDirectory(Path.Combine(package, "Assets"));
        File.WriteAllText(Path.Combine(package, "Assets", "current.txt"), "current");

        ApplicationUpdateCommandLine.ApplyPackage(package, target, directory.Combine("backup"));
        Check.Equal("new", File.ReadAllText(Path.Combine(target, "TerrariaSplit.exe")));
        Check.True(File.Exists(Path.Combine(target, "Assets", "current.txt")));
        Check.False(File.Exists(Path.Combine(target, "Assets", "obsolete.txt")));
        Check.Equal("user-settings", File.ReadAllText(Path.Combine(target, "Settings", "settings.json")));
        Check.Equal("user-data", File.ReadAllText(Path.Combine(target, "Data", "pb.json")));
    }

    private static void WriteManifest(string directory, params string[] roots)
    {
        string managed = string.Join(',', roots.Select(root => $"\"{root}\""));
        Directory.CreateDirectory(Path.Combine(directory, ApplicationUpdatePackage.ManifestDirectoryName));
        File.WriteAllText(ApplicationUpdatePackage.ManifestPath(directory),
            $"{{\"schemaVersion\":1,\"managedRoots\":[{managed}]}}", Encoding.UTF8);
    }

    private sealed class StaticResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
