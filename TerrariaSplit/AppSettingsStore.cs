using System.Text.Json;

namespace TerrariaSplit;

internal static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string SettingsPath
    {
        get
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TerrariaSplit");
            return Path.Combine(directory, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions)
                    ?? AppSettings.CreateDefault()
                : AppSettings.CreateDefault();
        }
        catch (Exception)
        {
            settings = AppSettings.CreateDefault();
        }

        foreach (BossSplitDefinition definition in BossSplitDefinitions.All)
        {
            settings.WorldRecordSplits.TryAdd(definition.Name.ToString(), string.Empty);
        }

        settings.Colors ??= new UiColorSettings();
        return settings;
    }

    public static void Save(AppSettings settings)
    {
        string directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static AppSettings Clone(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.CreateDefault();
    }
}
