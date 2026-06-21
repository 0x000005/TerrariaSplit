namespace TerrariaSplit.Infrastructure;

internal sealed class AppContextRuntimeDataPaths : IRuntimeDataPaths
{
    public static AppContextRuntimeDataPaths Default { get; } = new();

    private readonly string baseDirectory;

    public AppContextRuntimeDataPaths(string? baseDirectory = null)
    {
        this.baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
    }

    public string DataDirectory => Path.Combine(baseDirectory, "Data");

    public string SettingsDirectory => Path.Combine(baseDirectory, "Settings");

    public string ReferenceTimesDirectory => Path.Combine(DataDirectory, "reference-times");

    public string LastRunTimesDirectory => Path.Combine(DataDirectory, "last-times");

    public string PersonalBestTimesDirectory => Path.Combine(DataDirectory, "personal-best-times");

    public string PersonalBestSegmentsDirectory => Path.Combine(DataDirectory, "personal-best-segments");

    public string WorldPoolDirectory => Path.Combine(baseDirectory, "Worlds");

    public string WorldPoolScratchDirectory => Path.Combine(WorldPoolDirectory, "scratch");

    public string LogPath => Path.Combine(baseDirectory, "terrariasplit.log");
}
