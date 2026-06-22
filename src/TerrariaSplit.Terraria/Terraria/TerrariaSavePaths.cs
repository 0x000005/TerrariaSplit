namespace TerrariaSplit.Terraria;

public static class TerrariaSavePaths
{
    public const string DeletedSavesDirectoryName = "TerrariaSplitDeleted";

    public static string SaveRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "Terraria");
    }

    public static string DeletedSavesRoot()
    {
        return Path.Combine(SaveRoot(), DeletedSavesDirectoryName);
    }
}
