namespace TerrariaSplit.Configuration;

public sealed class SettingsDocument
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

public sealed record LoadedSettingsDocument(AppSettings Settings, string Path, bool ShouldSaveDefaults);
