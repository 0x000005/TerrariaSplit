using System.Text.Json;

namespace TerrariaSplit.Infrastructure;

public static class JsonFileStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static T? Read<T>(string path, string description)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
                : default;
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to read {description}: {path}");
            return default;
        }
    }

    public static bool Write<T>(string path, T value, string description)
    {
        return TryWrite(path, value, description).Succeeded;
    }

    public static OperationResult TryWrite<T>(string path, T value, string description)
    {
        try
        {
            WriteTextAtomically(path, JsonSerializer.Serialize(value, JsonOptions));
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to write {description}: {path}");
            return OperationResult.Failure($"Failed to save {description}.", ex);
        }
    }

    public static bool WriteText(string path, string value, string description)
    {
        return TryWriteText(path, value, description).Succeeded;
    }

    public static OperationResult TryWriteText(string path, string value, string description)
    {
        try
        {
            WriteTextAtomically(path, value);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to write {description}: {path}");
            return OperationResult.Failure($"Failed to save {description}.", ex);
        }
    }

    private static void WriteTextAtomically(string path, string value)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(directory, $"{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(value);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            ReplaceOrMove(tempPath, fullPath);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    private static void ReplaceOrMove(string tempPath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.Replace(tempPath, targetPath, null, true);
            return;
        }

        try
        {
            File.Move(tempPath, targetPath);
        }
        catch (IOException) when (File.Exists(targetPath) && File.Exists(tempPath))
        {
            File.Replace(tempPath, targetPath, null, true);
        }
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to delete temporary file: {tempPath}");
        }
    }
}
