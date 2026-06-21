namespace TerrariaSplit.Configuration;

internal static class AppSettingsDefaults
{
    private static readonly Lazy<AppSettings> Template = new(LoadTemplate);

    public static AppSettings TemplateSettings => Template.Value;

    public static string TemplateJson => EmbeddedDefaults.SettingsJson;

    public static AdvancedSettings Advanced => Template.Value.Advanced;

    public static AutoCreateWorldSettings AutoCreate => Template.Value.AutoCreate;

    public static AppSettings Create()
    {
        return SettingsSerializer.Clone(Template.Value);
    }

    private static AppSettings LoadTemplate()
    {
        return SettingsSerializer.ReadSettingsFromJson(TemplateJson, "default settings template")
            ?? throw new InvalidOperationException("Embedded default settings template is invalid.");
    }
}
