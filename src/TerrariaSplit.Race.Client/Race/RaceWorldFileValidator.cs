namespace TerrariaSplit.Race.Client;

public static class RaceWorldFileValidator
{
    public static bool IsValidWorldFilePath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
            HasWorldFileExtension(path) &&
            File.Exists(path);
    }

    public static bool HasWorldFileExtension(string? path)
    {
        return string.Equals(
            Path.GetExtension(path?.Trim() ?? string.Empty),
            ".wld",
            StringComparison.OrdinalIgnoreCase);
    }
}
