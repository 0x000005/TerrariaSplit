using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TerrariaSplit.Storage;

internal sealed class RoutePresetRepository(IRuntimeDataPaths paths)
{
    // Settings effects carry cloned snapshots. Remember each loaded catalog's last
    // successful revision without changing application-owned settings snapshots.
    private readonly Dictionary<Guid, string> savedRevisions = new();

    public bool LoadInto(AppSettings settings, string profilePath, bool newProfile)
    {
        RouteSettings route = settings.Route;
        route.Presets = Load();
        bool embeddedRoute = string.IsNullOrWhiteSpace(route.ActiveRouteName);
        bool shouldSave = embeddedRoute;
        if (embeddedRoute)
        {
            string preferredName = newProfile || Path.GetFileName(profilePath).Equals("settings.json", StringComparison.OrdinalIgnoreCase)
                ? RoutePresetService.DefaultName : Path.GetFileNameWithoutExtension(profilePath);
            if (!RoutePresetService.IsValidName(preferredName))
            {
                preferredName = RoutePresetService.DefaultName;
            }

            RoutePreset? selected = newProfile
                ? route.Presets.FirstOrDefault(preset => preset.Name.Equals(RoutePresetService.DefaultName, StringComparison.OrdinalIgnoreCase))
                : route.Presets.FirstOrDefault(preset => EntriesMatch(preset.SplitRoute, route.SplitRoute) &&
                    (preset.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase) ||
                     preset.Name.StartsWith(preferredName + " (", StringComparison.OrdinalIgnoreCase)));
            if (selected is null)
            {
                selected = new RoutePreset
                {
                    Name = GetAvailableName(preferredName, route.Presets),
                    SplitRoute = AppSettingsCloner.CloneRouteEntries(route.SplitRoute)
                };
                route.Presets.Add(selected);
            }

            route.ActiveRouteName = selected.Name;
        }

        RoutePreset? active = route.Presets.FirstOrDefault(preset =>
            preset.Name.Equals(route.ActiveRouteName, StringComparison.OrdinalIgnoreCase));
        if (active is null)
        {
            shouldSave = true;
            AppLog.ForSource().LogWarning("Selected route is unavailable. Route={Route}; Profile={Profile}", route.ActiveRouteName, profilePath);
            active = route.Presets.FirstOrDefault() ?? new RoutePreset
            {
                Name = GetAvailableName(RoutePresetService.DefaultName, route.Presets),
                SplitRoute = SplitCatalog.CreateDefaultRoute()
            };
            if (!route.Presets.Contains(active))
            {
                route.Presets.Add(active);
            }

            route.ActiveRouteName = active.Name;
        }

        route.SplitRoute = AppSettingsCloner.CloneRouteEntries(active.SplitRoute);
        return shouldSave;
    }

    public void PrepareSave(RouteSettings route)
    {
        if (route.Presets.Count == 0)
        {
            route.Presets = Load();
        }

        RoutePresetService.CaptureActive(route);
    }

    public OperationResult Save(RouteSettings route)
    {
        try
        {
            // Validate every changed file before writing any of them.
            var pending = new List<(RoutePreset Preset, string Path, string Fingerprint)>();
            foreach (RoutePreset preset in route.Presets)
            {
                if (!RoutePresetService.IsValidName(preset.Name) || preset.SplitRoute is null)
                {
                    return OperationResult.Failure("Invalid route name or missing route entries.");
                }

                string desired = Fingerprint(preset.SplitRoute);
                string baseline = savedRevisions.GetValueOrDefault(preset.StorageSessionId, preset.LoadedFingerprint);
                if (desired == baseline)
                {
                    continue;
                }

                string path = Path.Combine(paths.RoutesDirectory, preset.Name + ".json");
                if (File.Exists(path))
                {
                    RoutePreset? current = JsonFileStore.Read<RoutePreset>(path, "route");
                    string actual = current?.SplitRoute is { } entries ? Fingerprint(entries) : string.Empty;
                    if (actual != desired && (string.IsNullOrEmpty(baseline) || actual != baseline))
                    {
                        return OperationResult.Failure($"Route '{preset.Name}' changed on disk. Reopen settings before editing it.");
                    }

                    if (actual == desired)
                    {
                        savedRevisions[preset.StorageSessionId] = desired;
                        continue;
                    }
                }
                else if (!string.IsNullOrEmpty(baseline))
                {
                    return OperationResult.Failure($"Route '{preset.Name}' was removed from disk. Reopen settings before editing it.");
                }

                pending.Add((preset, path, desired));
            }

            foreach (var item in pending)
            {
                OperationResult result = JsonFileStore.TryWrite(item.Path, item.Preset, "route");
                if (result.Failed)
                {
                    return result;
                }

                savedRevisions[item.Preset.StorageSessionId] = item.Fingerprint;
            }

            return OperationResult.Success();
        }
        catch (Exception exception)
        {
            return OperationResult.Failure("Failed to save routes.", exception);
        }
    }

    private List<RoutePreset> Load()
    {
        Directory.CreateDirectory(paths.RoutesDirectory);
        var presets = new List<RoutePreset>();
        foreach (string path in Directory.EnumerateFiles(paths.RoutesDirectory, "*.json")
            .OrderBy(path => Path.GetFileNameWithoutExtension(path).Equals(RoutePresetService.DefaultName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            RoutePreset? preset = JsonFileStore.Read<RoutePreset>(path, "route");
            if (!RoutePresetService.IsValidName(name) || preset?.SplitRoute is null ||
                preset.SplitRoute.Any(entry => entry is null) ||
                presets.Any(existing => existing.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                AppLog.ForSource().LogWarning("Ignoring invalid route file. Path={Path}", path);
                continue;
            }

            preset.Name = name;
            preset.LoadedFingerprint = Fingerprint(preset.SplitRoute);
            presets.Add(preset);
        }

        return presets;
    }

    private string GetAvailableName(string name, IReadOnlyList<RoutePreset> presets)
    {
        string candidate = name;
        for (int suffix = 2; presets.Any(preset => preset.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)) ||
             File.Exists(Path.Combine(paths.RoutesDirectory, candidate + ".json")); suffix++)
        {
            string ending = $" ({suffix})";
            candidate = name[..Math.Min(name.Length, 80 - ending.Length)] + ending;
        }

        return candidate;
    }

    private static bool EntriesMatch(List<SplitRouteEntry> left, List<SplitRouteEntry> right) => Fingerprint(left) == Fingerprint(right);

    private static string Fingerprint(List<SplitRouteEntry> entries) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entries, JsonFileStore.JsonOptions))));
}
