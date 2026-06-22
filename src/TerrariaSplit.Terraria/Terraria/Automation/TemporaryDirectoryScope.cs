namespace TerrariaSplit.Terraria.Automation;

internal sealed class TemporaryDirectoryScope : IDisposable
{
    private readonly string directory;
    private readonly bool cleanOnDispose;
    private bool disposed;

    private TemporaryDirectoryScope(string directory, bool cleanOnDispose)
    {
        this.directory = directory;
        this.cleanOnDispose = cleanOnDispose;
    }

    public static TemporaryDirectoryScope Prepare(
        string directory,
        bool cleanExisting = true,
        bool cleanOnDispose = false)
    {
        Directory.CreateDirectory(directory);
        if (cleanExisting)
        {
            CleanDirectory(directory);
        }

        return new TemporaryDirectoryScope(directory, cleanOnDispose);
    }

    public void Clean()
    {
        CleanDirectory(directory);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (cleanOnDispose)
        {
            Clean();
        }
    }

    public static void CleanDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            TryDeleteFile(file);
        }
    }

    public static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
