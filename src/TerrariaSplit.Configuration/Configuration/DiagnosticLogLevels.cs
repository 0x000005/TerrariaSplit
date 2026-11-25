namespace TerrariaSplit.Configuration;

public static class DiagnosticLogLevels
{
    public const string Default = "ERROR";
    public const string None = "NONE";
    public static IReadOnlyList<string> All { get; } = ["DEBUG", "INFO", "WARN", Default, None];
    public static string Normalize(string? value)
    {
        string normalized = value?.Trim().ToUpperInvariant() ?? Default;
        return All.Contains(normalized) ? normalized : Default;
    }
}
