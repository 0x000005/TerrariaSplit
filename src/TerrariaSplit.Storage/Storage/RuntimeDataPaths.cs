namespace TerrariaSplit.Storage;

internal static class RuntimeDataPaths
{
    private static IRuntimeDataPaths Paths => AppContextRuntimeDataPaths.Default;

    public static string DataDirectory => Paths.DataDirectory;

    public static string SettingsDirectory => Paths.SettingsDirectory;

    public static string ReferenceTimesDirectory => Paths.ReferenceTimesDirectory;

    public static string LastRunTimesDirectory => Paths.LastRunTimesDirectory;

    public static string PersonalBestTimesDirectory => Paths.PersonalBestTimesDirectory;

    public static string PersonalBestSegmentsDirectory => Paths.PersonalBestSegmentsDirectory;

    public static string WorldPoolDirectory => Paths.WorldPoolDirectory;

    public static string WorldPoolScratchDirectory => Paths.WorldPoolScratchDirectory;

    public static string LogPath => Paths.LogPath;
}
