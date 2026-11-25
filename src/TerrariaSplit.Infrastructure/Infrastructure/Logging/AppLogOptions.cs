using Microsoft.Extensions.Logging;

namespace TerrariaSplit.Infrastructure;

public sealed record AppLogOptions
{
    public string FilePath { get; init; } = Path.Combine(AppContext.BaseDirectory, "terrariasplit.log");
    public string FallbackDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TerrariaSplit");
    public LogLevel MinimumLevel { get; init; } = LogLevel.Error;
    public TimeSpan RecentWindow { get; init; } = TimeSpan.FromSeconds(60);
    public int RecentByteLimit { get; init; } = 8 * 1024 * 1024;
    public int PendingByteLimit { get; init; } = 16 * 1024 * 1024;
    public long FileByteLimit { get; init; } = 10 * 1024 * 1024;
    public int FileCount { get; init; } = 5;
    public bool ConsoleOutput { get; init; }
    public TimeProvider Clock { get; init; } = TimeProvider.System;
}
