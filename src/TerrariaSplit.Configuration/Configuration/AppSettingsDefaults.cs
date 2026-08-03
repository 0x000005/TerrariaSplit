using System.Text.Json;

namespace TerrariaSplit.Configuration;

public static class AppSettingsDefaults
{
    private static readonly Lazy<AppSettings> Template = new(LoadTemplate);

    public static string TemplateJson => EmbeddedDefaults.SettingsJson;

    public static AdvancedSettings Advanced =>
        AppSettingsCloner.CloneAdvancedSettings(Template.Value.Advanced);

    public static AutomationSettings Automation =>
        AppSettingsCloner.CloneAutomationSettings(Template.Value.Automation);

    public static AutoCreateWorldSettings AutoCreate =>
        AppSettingsCloner.CloneAutoCreateWorldSettings(Template.Value.Automation.AutoCreate);

    public static AppSettings Create()
    {
        return AppSettingsCloner.Clone(Template.Value);
    }

    private static AppSettings LoadTemplate()
    {
        return JsonSerializer.Deserialize(TemplateJson, AppSettingsJsonContext.Default.AppSettings)
            ?? throw new InvalidOperationException("Embedded default settings template is invalid.");
    }
}
