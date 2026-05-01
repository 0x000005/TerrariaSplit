using System.Text.Json;

namespace TerrariaSplit;

internal static class JsonFileStore
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
            AppLogger.Error(ex, $"Failed to read {description}: {path}");
            return default;
        }
    }

    public static bool Write<T>(string path, T value, string description)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Failed to write {description}: {path}");
            return false;
        }
    }
}
