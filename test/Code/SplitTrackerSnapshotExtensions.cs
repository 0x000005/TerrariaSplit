namespace TerrariaSplit.Tests;

internal static class SplitTrackerSnapshotExtensions
{
    public static void OnRunStarted(this SplitTracker tracker, TerrariaWatchSnapshot snapshot)
    {
        tracker.OnRunStarted(snapshot.Facts);
    }

    public static SplitRecord? Update(this SplitTracker tracker, TerrariaWatchSnapshot snapshot, TimeSpan elapsed)
    {
        return tracker.Update(snapshot.Facts, snapshot.IsGameMenu, elapsed);
    }
}
