namespace TerrariaSplit.Configuration;

internal sealed class SettingsDocument
{
    public int SchemaVersion { get; set; } = SettingsSchemaVersion.Current;

    public AppSettings Settings { get; set; } = new();

    public SettingsDocument()
    {
    }

    public SettingsDocument(AppSettings settings)
    {
        Settings = settings;
    }
}

internal sealed record LoadedSettingsDocument(AppSettings Settings, string Path, bool ShouldSaveDefaults);
