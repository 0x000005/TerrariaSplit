using System.Text.Json.Serialization;

namespace TerrariaSplit.Configuration;

public sealed class RoutePreset
{
    public string Name { get; set; } = string.Empty;
    public List<SplitRouteEntry> SplitRoute { get; set; } = new();

    [JsonIgnore]
    public string LoadedFingerprint { get; set; } = string.Empty;

    [JsonIgnore]
    public Guid StorageSessionId { get; set; } = Guid.NewGuid();
}

public static class RoutePresetService
{
    public const string DefaultName = "Default";

    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 80 &&
        !name.EndsWith('.') && name == name.Trim() &&
        !name.Any(character => char.IsControl(character) || "<>:\"/\\|?*".Contains(character)) &&
        !IsReservedFileName(name);

    private static bool IsReservedFileName(string name)
    {
        string stem = name.Split('.')[0];
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (stem.Length == 4 && stem[3] is >= '1' and <= '9' &&
                (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)));
    }

    public static RoutePreset CaptureActive(RouteSettings route)
    {
        route.ActiveRouteName = string.IsNullOrWhiteSpace(route.ActiveRouteName)
            ? DefaultName : route.ActiveRouteName.Trim();
        RoutePreset? active = route.Presets.FirstOrDefault(preset =>
            string.Equals(preset.Name, route.ActiveRouteName, StringComparison.OrdinalIgnoreCase));
        if (active is null)
        {
            active = new RoutePreset { Name = route.ActiveRouteName };
            route.Presets.Add(active);
        }

        active.SplitRoute = AppSettingsCloner.CloneRouteEntries(route.SplitRoute);
        return active;
    }

    public static bool Select(RouteSettings route, string name)
    {
        RoutePreset? selected = route.Presets.FirstOrDefault(preset =>
            string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            return false;
        }

        CaptureActive(route);
        route.ActiveRouteName = selected.Name;
        route.SplitRoute = AppSettingsCloner.CloneRouteEntries(selected.SplitRoute);
        return true;
    }

    public static bool Add(RouteSettings route, string name)
    {
        name = name.Trim();
        if (!IsValidName(name) || route.Presets.Any(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        CaptureActive(route);
        route.Presets.Add(new RoutePreset { Name = name });
        route.ActiveRouteName = name;
        route.SplitRoute = [];
        return true;
    }
}
