namespace TerrariaSplit.Configuration;

internal static class SettingsMigrator
{
    public static void Migrate(AppSettings settings)
    {
        MigrateExpandedSplitDetails(settings);
    }

    private static void MigrateExpandedSplitDetails(AppSettings settings)
    {
        if (settings.SplitRoute is null)
        {
            return;
        }

        foreach (SplitRouteEntry entry in settings.SplitRoute)
        {
            if (!entry.ExpandDetails)
            {
                continue;
            }

            settings.Route.ExpandSplitDetails = true;
            entry.ExpandDetails = false;
        }
    }
}
