namespace TerrariaSplit.Storage;

internal static class AppSettingsStore
{
    private static readonly AppSettingsRepository Repository = new();

    public static string SettingsDirectory => Repository.SettingsDirectory;

    public static string SettingsPath
    {
        get => Repository.SettingsPath;
    }

    public static string SettingsFileName => Repository.SettingsFileName;

    public static AppSettings Load()
    {
        return Repository.Load();
    }

    public static AppSettings Load(string path)
    {
        return Repository.Load(path);
    }

    public static IReadOnlyList<string> GetSettingsFiles()
    {
        return Repository.GetSettingsFiles();
    }

    public static void Save(AppSettings settings)
    {
        Repository.Save(settings);
    }

    public static OperationResult TrySave(AppSettings settings)
    {
        return Repository.TrySave(settings);
    }

    public static AppSettings Clone(AppSettings settings)
    {
        return Repository.Clone(settings);
    }

    public static void Normalize(AppSettings settings)
    {
        Repository.Normalize(settings);
    }
}
