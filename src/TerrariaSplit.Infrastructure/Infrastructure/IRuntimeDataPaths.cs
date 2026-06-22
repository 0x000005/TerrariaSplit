namespace TerrariaSplit.Infrastructure;

public interface IRuntimeDataPaths
{
    string DataDirectory { get; }

    string SettingsDirectory { get; }

    string ReferenceTimesDirectory { get; }

    string LastRunTimesDirectory { get; }

    string PersonalBestTimesDirectory { get; }

    string PersonalBestSegmentsDirectory { get; }

    string WorldPoolDirectory { get; }

    string WorldPoolScratchDirectory { get; }

    string LogPath { get; }
}
