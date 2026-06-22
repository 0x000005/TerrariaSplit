using System.Text.Json;

namespace TerrariaSplit.Configuration;

public static class AppSettingsDefaults
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly Lazy<AppSettings> Template = new(LoadTemplate);

    public static AppSettings TemplateSettings => Template.Value;

    public static string TemplateJson => EmbeddedDefaults.SettingsJson;

    public static AdvancedSettings Advanced => Template.Value.Advanced;

    public static AutomationSettings Automation => Template.Value.Automation;

    public static AutoCreateWorldSettings AutoCreate => Template.Value.Automation.AutoCreate;

    public static AppSettings Create()
    {
        string json = JsonSerializer.Serialize(Template.Value, JsonOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    private static AppSettings LoadTemplate()
    {
        return SettingsJsonSectionMigrator.Deserialize(TemplateJson, JsonOptions)
            ?? throw new InvalidOperationException("Embedded default settings template is invalid.");
    }
}
