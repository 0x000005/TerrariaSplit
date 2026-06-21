namespace TerrariaSplit.Configuration;

internal sealed record SettingsDocument(AppSettings Settings, string Path, bool ShouldSaveDefaults)
{
    public int SchemaVersion => SettingsSchemaVersion.Current;
}
