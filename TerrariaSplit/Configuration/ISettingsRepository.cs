namespace TerrariaSplit.Configuration;

internal interface ISettingsRepository
{
    string SettingsDirectory { get; }

    string SettingsPath { get; }

    string SettingsFileName { get; }

    AppSettings Load();

    AppSettings Load(string path);

    IReadOnlyList<string> GetSettingsFiles();

    void Save(AppSettings settings);

    AppSettings Clone(AppSettings settings);

    void Normalize(AppSettings settings);
}
