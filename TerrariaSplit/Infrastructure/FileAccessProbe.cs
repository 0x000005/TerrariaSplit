namespace TerrariaSplit;

internal static class FileAccessProbe
{
    public static bool CanOpenForRead(string path)
    {
        return CanOpenForRead(path, FileShare.ReadWrite | FileShare.Delete);
    }

    public static bool CanOpenForExclusiveRead(string path)
    {
        return CanOpenForRead(path, FileShare.None);
    }

    private static bool CanOpenForRead(string path, FileShare share)
    {
        try
        {
            using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                share);
            return stream.Length > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
