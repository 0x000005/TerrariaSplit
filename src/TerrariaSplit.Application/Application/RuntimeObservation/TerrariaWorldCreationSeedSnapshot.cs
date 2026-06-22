namespace TerrariaSplit.Application;

public enum TerrariaWorldCreationSeedStatus
{
    Unknown,
    NotOnWorldCreationPage,
    Empty,
    Seed
}

public readonly record struct TerrariaWorldCreationSeedSnapshot(
    TerrariaWorldCreationSeedStatus Status,
    string? SeedText,
    IntPtr WorldCreationAddress)
{
    public static TerrariaWorldCreationSeedSnapshot Unknown { get; } =
        new(TerrariaWorldCreationSeedStatus.Unknown, null, IntPtr.Zero);

    public static TerrariaWorldCreationSeedSnapshot NotOnWorldCreationPage { get; } =
        new(TerrariaWorldCreationSeedStatus.NotOnWorldCreationPage, null, IntPtr.Zero);

    public static TerrariaWorldCreationSeedSnapshot EmptySeed(IntPtr worldCreationAddress) =>
        new(TerrariaWorldCreationSeedStatus.Empty, string.Empty, worldCreationAddress);

    public static TerrariaWorldCreationSeedSnapshot FromSeed(string seedText, IntPtr worldCreationAddress) =>
        new(TerrariaWorldCreationSeedStatus.Seed, seedText, worldCreationAddress);
}
