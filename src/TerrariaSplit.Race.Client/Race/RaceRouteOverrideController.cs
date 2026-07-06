using TerrariaSplit.Configuration;
using TerrariaSplit.Models;
using TerrariaSplit.Race.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace TerrariaSplit.Race.Client;

public sealed class RaceRouteOverrideController
{
    public const string AlreadyAppliedDetail = "Race route is already active.";

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
        appliedRouteKey = routeKey;
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

        package = new SettingsRouteOverridePackage
        {
            Key = routeKey,
            SplitRoute = MaterializeCustomIconOverrides(
                syncedSettings.SplitRoute,
                payload,
                routeKey),
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

        appliedRouteKey = key;
        return true;
    }

    public bool Clear()
    {
        if (appliedRouteKey is null)
        {
            return false;
        }

        appliedRouteKey = null;
        return true;
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

    private List<SplitRouteEntry> MaterializeCustomIconOverrides(
        IReadOnlyList<SplitRouteEntry> splitRoute,
        RaceRoutePayload payload,
        string routeKey)
    {
        var route = new List<SplitRouteEntry>(splitRoute.Count);
        foreach (SplitRouteEntry entry in splitRoute)
        {
            SplitRouteEntry nextEntry = SettingsRouteOverrideService.CloneEntry(entry);
            if (SplitIconOverrideSource.Normalize(nextEntry.IconOverride.Source) == SplitIconOverrideSource.CustomFile &&
                TryMaterializeCustomIcon(nextEntry, payload, routeKey, out string localPath))
            {
                nextEntry.IconOverride.FilePath = localPath;
            }

            route.Add(nextEntry);
        }

        return route;
    }

    private bool TryMaterializeCustomIcon(
        SplitRouteEntry entry,
        RaceRoutePayload payload,
        string routeKey,
        out string localPath)
    {
        localPath = string.Empty;
        RaceRouteIconPayload? icon = FindCustomIconPayload(entry, payload);
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

        if (data.Length == 0)
        {
            return false;
        }

        try
        {
            string routeDirectory = Path.Combine(iconCacheDirectory, CreateSafeFileName(routeKey));
            Directory.CreateDirectory(routeDirectory);
            string extension = Path.GetExtension(icon.FileName);
            if (string.IsNullOrWhiteSpace(extension))
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
