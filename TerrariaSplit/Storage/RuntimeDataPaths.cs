namespace TerrariaSplit;

internal static class RuntimeDataPaths
{
    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Data");

    public static string SettingsDirectory => Path.Combine(AppContext.BaseDirectory, "Settings");

    public static string ReferenceTimesDirectory => Path.Combine(DataDirectory, "reference-times");

    public static string LastRunTimesDirectory => Path.Combine(DataDirectory, "last-times");

    public static string PersonalBestTimesDirectory => Path.Combine(DataDirectory, "personal-best-times");

    public static string PersonalBestSegmentsDirectory => Path.Combine(DataDirectory, "personal-best-segments");

    public static string WorldPoolDirectory => Path.Combine(AppContext.BaseDirectory, "Worlds");

    public static string WorldPoolScratchDirectory => Path.Combine(WorldPoolDirectory, "scratch");

    public static string LogPath => Path.Combine(AppContext.BaseDirectory, "terrariasplit.log");
}
