using System.Globalization;

namespace TerrariaSplit.Terraria.WorldGeneration;

internal readonly record struct WorldSeedMetadata(
    string SeedText,
    int SizeCode,
    int DifficultyCode,
    bool HasCrimson,
    int SpecialSeedMask);

internal readonly record struct PyramidChestSet(IReadOnlyList<PyramidChest> Chests)
{
    public static PyramidChestSet Empty => new([]);

    public string FormatTargetClass()
    {
        bool hasFlyingCarpet = ContainsItem(PyramidChestItemNames.FlyingCarpet);
        bool hasSandstorm = ContainsItem(PyramidChestItemNames.SandstormInABottle);
        return (Chests.Count, hasFlyingCarpet, hasSandstorm) switch
        {
            (0, _, _) => "none",
            (_, true, true) => "flying+sandstorm",
            (_, true, _) => "flying",
            (_, _, true) => "sandstorm",
            _ => "other"
        };
    }

    public string FormatLootSummary()
    {
        return Chests.Count == 0
            ? "none"
            : string.Join("; ", Chests
                .Select(static chest => chest.FormatLootSummary())
                .Order(StringComparer.Ordinal));
    }

    public string FormatSummary()
    {
        return Chests.Count == 0
            ? "none"
            : string.Join("; ", Chests.Select(static chest => chest.FormatSummary()));
    }

    private bool ContainsItem(int itemType)
    {
        return Chests.Any(chest => chest.Items.Any(item => item.Type == itemType));
    }
}

internal readonly record struct PyramidChest(
    int X,
    int Y,
    IReadOnlyList<PyramidChestItem> Items,
    IReadOnlyList<PyramidCoinPile> CoinPiles,
    int TunnelTopX,
    int TunnelTopY,
    int TunnelOpeningSide,
    int TunnelSurfaceDistance,
    int CandidateIndex,
    int CandidateSourceIndex,
    int CandidateScanY,
    int CandidateSandDepth,
    int CandidateSandSpan,
    int CandidateActiveDepth)
{
    // The replica records the chest's bottom row, while Terraria stores its top-left tile.
    public int DepthFromSurface => Math.Max(0, Y - CandidateScanY - 1);

    public PyramidCoinPileCounts CoinPileCounts => PyramidCoinPileCounts.From(CoinPiles);

    public string FormatLootSummary()
    {
        return string.Join("|", Items.Select(static item => item.Format()));
    }

    public string FormatSummary()
    {
        return $"({X.ToString(CultureInfo.InvariantCulture)},{Y.ToString(CultureInfo.InvariantCulture)}):{FormatLootSummary()}";
    }
}

internal enum PyramidCoinPileKind
{
    Copper,
    Silver,
    Gold,
}

internal readonly record struct PyramidCoinPile(int X, int Y, PyramidCoinPileKind Kind);

internal readonly record struct PyramidCoinPileCounts(int Copper, int Silver, int Gold)
{
    public static PyramidCoinPileCounts Empty => new(0, 0, 0);

    public int Total => Copper + Silver + Gold;

    public PyramidCoinPileCounts Add(PyramidCoinPileKind kind)
    {
        return kind switch
        {
            PyramidCoinPileKind.Copper => this with { Copper = Copper + 1 },
            PyramidCoinPileKind.Silver => this with { Silver = Silver + 1 },
            PyramidCoinPileKind.Gold => this with { Gold = Gold + 1 },
            _ => this
        };
    }

    public string Format()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"copper={Copper};silver={Silver};gold={Gold};total={Total}");
    }

    public static PyramidCoinPileCounts From(IReadOnlyList<PyramidCoinPile>? piles)
    {
        PyramidCoinPileCounts counts = Empty;
        if (piles is null)
        {
            return counts;
        }

        foreach (PyramidCoinPile pile in piles)
        {
            counts = counts.Add(pile.Kind);
        }

        return counts;
    }
}

internal readonly record struct PyramidFeatureSummary(
    int ChestX,
    int ChestY,
    int DepthFromSurface,
    int TunnelSurfaceDistance,
    PyramidCoinPileCounts CoinPiles)
{
    public static PyramidFeatureSummary Empty => new(-1, -1, 0, 0, PyramidCoinPileCounts.Empty);

    public static PyramidFeatureSummary From(PyramidChest chest)
    {
        return new PyramidFeatureSummary(
            chest.X,
            chest.Y,
            chest.DepthFromSurface,
            chest.TunnelSurfaceDistance,
            chest.CoinPileCounts);
    }
}

internal readonly record struct PyramidChestItem(int Slot, int Type, int Stack, byte Prefix)
{
    public string Format()
    {
        string name = PyramidChestItemNames.NameOf(Type);
        if (Stack > 1)
        {
            name += "x" + Stack.ToString(CultureInfo.InvariantCulture);
        }

        if (Prefix != 0)
        {
            name += "/p" + Prefix.ToString(CultureInfo.InvariantCulture);
        }

        return name;
    }
}

internal static class PyramidChestItemNames
{
    public const int PharaohsMask = 848;
    public const int SandstormInABottle = 857;
    public const int PharaohsRobe = 866;
    public const int FlyingCarpet = 934;

    private static readonly Dictionary<int, string> KnownNames = new()
    {
        [PharaohsMask] = "Pharaoh's Mask",
        [SandstormInABottle] = "Sandstorm in a Bottle",
        [PharaohsRobe] = "Pharaoh's Robe",
        [FlyingCarpet] = "Flying Carpet"
    };

    public static bool IsKnownPyramidItem(PyramidChestItem item)
    {
        return KnownNames.ContainsKey(item.Type);
    }

    public static string NameOf(int itemType)
    {
        return KnownNames.TryGetValue(itemType, out string? name)
            ? name
            : "#" + itemType.ToString(CultureInfo.InvariantCulture);
    }
}

internal readonly record struct Bounds(int Left, int Top, int RightExclusive, int BottomExclusive)
{
    public bool Intersects(int x, int y, int width, int height)
    {
        return x < RightExclusive &&
            x + width > Left &&
            y < BottomExclusive &&
            y + height > Top;
    }

    public static Bounds ForSpeedrunCorridor(int sizeCode)
    {
        (int width, int height) = sizeCode switch
        {
            1 => (4200, 1200),
            3 => (8400, 2400),
            _ => (6400, 1800)
        };

        int left = Math.Max(1, (int)Math.Floor(width * 0.32d));
        int rightExclusive = Math.Min(width - 1, (int)Math.Ceiling(width * 0.68d));
        int top = Math.Max(1, (int)Math.Floor(height * 0.15d));
        int bottomExclusive = Math.Min(height - 1, Math.Max(top + 1, (int)Math.Ceiling(height * 0.35d)));
        return new Bounds(left, top, rightExclusive, bottomExclusive);
    }
}
