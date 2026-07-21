using TerrariaSplit.Configuration;
using TerrariaSplit.Models;
using TerrariaSplit.Race.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace TerrariaSplit.Race.Client;

public sealed class RaceRouteOverrideController
{
    public const string AlreadyAppliedDetail = "Race route is already active.";
    private const int MaximumEmbeddedIconBytes = 2 * 1024 * 1024;

    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly string iconCacheDirectory;
    private string? appliedRouteKey;

    public RaceRouteOverrideController(
        ISettingsSnapshotFactory settingsSnapshots,
        string? iconCacheDirectory = null)
    {
        this.settingsSnapshots = settingsSnapshots;
        this.iconCacheDirectory = string.IsNullOrWhiteSpace(iconCacheDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "Data", "RaceIcons")
            : iconCacheDirectory;
    }

    public bool HasOverride => appliedRouteKey is not null;

    public string? ActiveKey => appliedRouteKey;

    public bool TryApply(
        AppSettings currentSettings,
        RaceRoutePayload payload,
        out AppSettings nextSettings,
        out string detail)
    {
        nextSettings = currentSettings;
        string routeKey = CreateRouteKey(payload);
        if (string.Equals(appliedRouteKey, routeKey, StringComparison.Ordinal))
        {
            detail = AlreadyAppliedDetail;
            return false;
        }

        if (!TryCreatePackage(payload, out SettingsRouteOverridePackage package, out detail))
        {
            return false;
        }

        nextSettings = SettingsRouteOverrideService.Apply(currentSettings, package, settingsSnapshots);
        SetActiveRouteKey(routeKey);
        return true;
    }

    public bool TryCreatePackage(
        RaceRoutePayload payload,
        out SettingsRouteOverridePackage package,
        out string detail)
    {
        package = new SettingsRouteOverridePackage();
        string routeKey = CreateRouteKey(payload);
        if (!RaceRoutePayloadFactory.TryDeserializeSyncedSettings(
                payload,
                out RaceRoutePayloadFactory.RaceRouteSyncSettings syncedSettings,
                out detail))
        {
            return false;
        }

        if (!TryMaterializeCustomIconOverrides(
                syncedSettings.SplitRoute,
                payload,
                routeKey,
                out List<SplitRouteEntry> materializedRoute,
                out detail))
        {
            return false;
        }

        package = new SettingsRouteOverridePackage
        {
            Key = routeKey,
            SplitRoute = materializedRoute,
            ReferenceSet = SettingsRouteOverrideService.CloneReferenceSet(syncedSettings.ReferenceSet)
        };
        return true;
    }

    public bool MarkApplied(SettingsRouteOverridePackage package)
    {
        string key = package.Key ?? string.Empty;
        if (string.IsNullOrWhiteSpace(key) ||
            string.Equals(appliedRouteKey, key, StringComparison.Ordinal))
        {
            return false;
        }

        SetActiveRouteKey(key);
        return true;
    }

    public bool Clear()
    {
        if (appliedRouteKey is null)
        {
            return false;
        }

        string clearedRouteKey = appliedRouteKey;
        appliedRouteKey = null;
        DeleteIconCache(clearedRouteKey);
        return true;
    }

    private void SetActiveRouteKey(string routeKey)
    {
        string? previousRouteKey = appliedRouteKey;
        appliedRouteKey = routeKey;
        if (!string.IsNullOrWhiteSpace(previousRouteKey) &&
            !string.Equals(previousRouteKey, routeKey, StringComparison.Ordinal))
        {
            DeleteIconCache(previousRouteKey);
        }
    }

    private void DeleteIconCache(string routeKey)
    {
        try
        {
            string routeDirectory = GetRouteDirectory(routeKey);
            if (Directory.Exists(routeDirectory))
            {
                Directory.Delete(routeDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            ScheduleIconCacheDeletion(routeKey);
        }
        catch (UnauthorizedAccessException)
        {
            ScheduleIconCacheDeletion(routeKey);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
        }
    }

    private void ScheduleIconCacheDeletion(string routeKey)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(500).ConfigureAwait(false);
            try
            {
                if (string.Equals(appliedRouteKey, routeKey, StringComparison.Ordinal))
                {
                    return;
                }

                string routeDirectory = GetRouteDirectory(routeKey);
                if (Directory.Exists(routeDirectory))
                {
                    Directory.Delete(routeDirectory, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
            }
        });
    }

    private static string CreateRouteKey(RaceRoutePayload payload)
    {
        var builder = new StringBuilder();
        builder
            .Append(payload.RouteHash?.Trim() ?? string.Empty)
            .Append('\n')
            .Append(payload.SerializedRouteJson?.Trim() ?? string.Empty);

        foreach (RaceRouteIconPayload icon in (payload.Icons ?? []).OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder
                .Append('\n')
                .Append(icon.Key?.Trim() ?? string.Empty)
                .Append('|')
                .Append(icon.FileName?.Trim() ?? string.Empty)
                .Append('|')
                .Append(icon.DataBase64?.Trim() ?? string.Empty);
        }

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return "payload:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private bool TryMaterializeCustomIconOverrides(
        IReadOnlyList<SplitRouteEntry> splitRoute,
        RaceRoutePayload payload,
        string routeKey,
        out List<SplitRouteEntry> route,
        out string detail)
    {
        route = new List<SplitRouteEntry>(splitRoute.Count);
        detail = string.Empty;
        foreach (SplitRouteEntry entry in splitRoute)
        {
            SplitRouteEntry nextEntry = SettingsRouteOverrideService.CloneEntry(entry);
            if (SplitIconOverrideSource.Normalize(nextEntry.IconOverride.Source) == SplitIconOverrideSource.CustomFile)
            {
                if (!TryMaterializeCustomIcon(nextEntry, payload, routeKey, out string localPath))
                {
                    detail = $"Race route custom icon is unavailable for split '{nextEntry.Id}'.";
                    route.Clear();
                    return false;
                }

                nextEntry.IconOverride.FilePath = localPath;
            }
            else if (SplitIconOverrideSource.Normalize(nextEntry.IconOverride.Source) == SplitIconOverrideSource.All &&
                !TryMaterializeAllIconOverrides(nextEntry, payload, routeKey, out detail))
            {
                route.Clear();
                return false;
            }

            route.Add(nextEntry);
        }

        return true;
    }

    private bool TryMaterializeCustomIcon(
        SplitRouteEntry entry,
        RaceRoutePayload payload,
        string routeKey,
        out string localPath)
    {
        localPath = string.Empty;
        RaceRouteIconPayload? icon = FindCustomIconPayload(entry, payload);
        return TryMaterializeIcon(icon, routeKey, out localPath);
    }

    private bool TryMaterializeAllIconOverrides(
        SplitRouteEntry entry,
        RaceRoutePayload payload,
        string routeKey,
        out string detail)
    {
        detail = string.Empty;
        var materializedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string targetId, string fileName) in entry.IconOverride.AllIconFilePaths)
        {
            RaceRouteIconPayload? icon = FindIconPayload(payload, targetId, fileName);
            if (!TryMaterializeIcon(icon, routeKey, out string localPath))
            {
                detail = $"Race route custom icon is unavailable for target '{targetId}' in split '{entry.Id}'.";
                return false;
            }

            materializedPaths[targetId] = localPath;
        }

        entry.IconOverride.AllIconFilePaths = materializedPaths;
        return true;
    }

    private bool TryMaterializeIcon(
        RaceRouteIconPayload? icon,
        string routeKey,
        out string localPath)
    {
        localPath = string.Empty;
        if (icon?.DataBase64 is not string dataBase64 || string.IsNullOrWhiteSpace(dataBase64))
        {
            return false;
        }

        byte[] data;
        try
        {
            data = Convert.FromBase64String(dataBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        if (data.Length == 0 || data.Length > MaximumEmbeddedIconBytes)
        {
            return false;
        }

        try
        {
            string routeDirectory = GetRouteDirectory(routeKey);
            Directory.CreateDirectory(routeDirectory);
            string extension = Path.GetExtension(icon.FileName).ToLowerInvariant();
            if (extension is not (".png" or ".gif" or ".jpg" or ".jpeg" or ".bmp"))
            {
                extension = ".png";
            }

            string fileStem = CreateSafeFileName(icon.Key);
            string dataHash = Convert.ToHexString(SHA256.HashData(data))[..16].ToLowerInvariant();
            localPath = Path.Combine(routeDirectory, $"{fileStem}-{dataHash}{extension}");
            if (!File.Exists(localPath))
            {
                File.WriteAllBytes(localPath, data);
            }

            return true;
        }
        catch (IOException)
        {
            localPath = string.Empty;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            localPath = string.Empty;
            return false;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            localPath = string.Empty;
            return false;
        }
    }

    private string GetRouteDirectory(string routeKey)
    {
        return Path.Combine(iconCacheDirectory, CreateSafeFileName(routeKey));
    }

    private static RaceRouteIconPayload? FindCustomIconPayload(
        SplitRouteEntry entry,
        RaceRoutePayload payload)
    {
        IReadOnlyList<RaceRouteIconPayload> icons = payload.Icons ?? [];
        if (icons.Count == 0)
        {
            return null;
        }

        string customKey = "custom-icon:" + entry.Id.Trim();
        RaceRouteIconPayload? keyMatch = icons.FirstOrDefault(icon =>
            string.Equals(icon.Key, customKey, StringComparison.OrdinalIgnoreCase));
        if (keyMatch is not null)
        {
            return keyMatch;
        }

        string customPath = entry.IconOverride.FilePath?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(customPath)
            ? null
            : icons.FirstOrDefault(icon => string.Equals(icon.FileName, customPath, StringComparison.OrdinalIgnoreCase));
    }

    private static RaceRouteIconPayload? FindIconPayload(
        RaceRoutePayload payload,
        string key,
        string fileName)
    {
        string normalizedKey = key?.Trim() ?? string.Empty;
        string normalizedFileName;
        try
        {
            normalizedFileName = Path.GetFileName(fileName?.Trim() ?? string.Empty);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(normalizedKey) || string.IsNullOrWhiteSpace(normalizedFileName))
        {
            return null;
        }

        return (payload.Icons ?? []).FirstOrDefault(icon =>
            string.Equals(icon.Key, normalizedKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(icon.FileName, normalizedFileName, StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateSafeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new((value ?? string.Empty)
            .Trim()
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "icon" : safe;
    }
}
