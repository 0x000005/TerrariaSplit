namespace TerrariaSplit;

internal static class AppSettingsDefaults
{
    private const string DefaultSettingsFileName = "settings.json";
    private static readonly Lazy<AppSettings> Template = new(LoadTemplate);

    public static string TemplatePath => Path.Combine(
        AppContext.BaseDirectory,
        "Assets",
        "DefaultSettings",
        DefaultSettingsFileName);

    public static AppSettings TemplateSettings => Template.Value;

    public static AdvancedSettings Advanced => Template.Value.Advanced;

    public static AutoCreateWorldSettings AutoCreate => Template.Value.AutoCreate;

    public static AppSettings Create()
    {
        return SettingsSerializer.Clone(Template.Value);
    }

    private static AppSettings LoadTemplate()
    {
        return SettingsSerializer.ReadSettings(TemplatePath, "default settings template")
            ?? throw new InvalidOperationException($"Default settings template is missing or invalid: {TemplatePath}");
    }
}
