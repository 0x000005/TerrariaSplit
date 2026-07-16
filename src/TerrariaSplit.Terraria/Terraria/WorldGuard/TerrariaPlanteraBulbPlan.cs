using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TerrariaSplit.Race.Determinism;
using TerrariaSplit.Terraria.Automation;
using ResourceTileIds = TerrariaSplit.Terraria.Automation.TileIds;

namespace TerrariaSplit.Terraria;

internal readonly record struct TerrariaPlanteraBulbAnchor(int X, int Y);

internal sealed record TerrariaPlanteraBulbPlan(
    int DoorX,
    int DoorY,
    int MinimumY,
    int UpperOuterRadius,
    int LowerInnerRadius,
    int LowerOuterRadius,
    IReadOnlyList<TerrariaPlanteraBulbAnchor> Anchors)
{
    public const int FormatVersion = 1;

    public static TerrariaPlanteraBulbPlan Empty { get; } =
        new(0, 0, 0, 0, 0, 0, Array.Empty<TerrariaPlanteraBulbAnchor>());

    public string Encode()
    {
        if (Anchors.Count == 0)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes("0"));
        }

        string anchors = string.Join(
            ";",
            Anchors.Select(static anchor => string.Create(
                CultureInfo.InvariantCulture,
                $"{anchor.X},{anchor.Y}")));
        string canonical = string.Join(
            "|",
            FormatVersion.ToString(CultureInfo.InvariantCulture),
            DoorX.ToString(CultureInfo.InvariantCulture),
            DoorY.ToString(CultureInfo.InvariantCulture),
            MinimumY.ToString(CultureInfo.InvariantCulture),
            UpperOuterRadius.ToString(CultureInfo.InvariantCulture),
            LowerInnerRadius.ToString(CultureInfo.InvariantCulture),
            LowerOuterRadius.ToString(CultureInfo.InvariantCulture),
            anchors);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(canonical));
    }

    public string CreateDigest() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Encode())));
}

internal sealed class TerrariaPlanteraBulbPlanner
{
    private const int SmallWorldWidth = 4200;
    private const int SmallUpperOuterRadius = 200;
    private const int SmallLowerInnerRadius = 100;
    private const int SmallLowerOuterRadius = 200;
    private const int TempleDoorSearchRadius = 24;
    private readonly TerrariaResourceWorldReader reader = new();

    public TerrariaPlanteraBulbPlan Create(
        string worldPath,
        int expectedWorldId,
        Guid uniqueId,
        byte[] entropySeed,
        int protocolVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldPath);
        ArgumentNullException.ThrowIfNull(entropySeed);

        WorldData world = reader.Read(worldPath);
        if (world.Header.WorldId != expectedWorldId)
        {
            throw new InvalidDataException("The Plantera bulb plan world id does not match the Race world.");
        }

        (int X, int Y)? door = FindTempleDoor(world.Tiles);
        if (door is null)
        {
            return TerrariaPlanteraBulbPlan.Empty;
        }

        double radiusScale = world.Header.Width / (double)SmallWorldWidth;
        int upperOuterRadius = ScaleRadius(SmallUpperOuterRadius, radiusScale);
        int lowerInnerRadius = ScaleRadius(SmallLowerInnerRadius, radiusScale);
        int lowerOuterRadius = ScaleRadius(SmallLowerOuterRadius, radiusScale);
        int minimumY = (int)Math.Ceiling(world.Header.WorldSurface + 0.5d);
        List<TerrariaPlanteraBulbAnchor> candidates = FindCandidates(
            world,
            door.Value,
            minimumY,
            upperOuterRadius,
            lowerInnerRadius,
            lowerOuterRadius);
        IReadOnlyList<TerrariaPlanteraBulbAnchor> anchors = SelectAnchors(
            candidates,
            expectedWorldId,
            uniqueId,
            entropySeed,
            protocolVersion);
        return new TerrariaPlanteraBulbPlan(
            door.Value.X,
            door.Value.Y,
            minimumY,
            upperOuterRadius,
            lowerInnerRadius,
            lowerOuterRadius,
            anchors);
    }

    private static int ScaleRadius(int smallWorldRadius, double scale) =>
        Math.Max(1, (int)Math.Round(smallWorldRadius * scale, MidpointRounding.AwayFromZero));

    private static (int X, int Y)? FindTempleDoor(TileGrid tiles)
    {
        (int X, int Y, int Score)? best = null;
        for (int x = 0; x < tiles.Width; x++)
        {
            for (int y = 0; y < tiles.Height; y++)
            {
                int index = tiles.Index(x, y);
                if (!tiles.Active[index] || tiles.Type[index] is not (ResourceTileIds.ClosedDoor or ResourceTileIds.OpenDoor))
                {
                    continue;
                }

                int score = CountTempleBrick(tiles, x, y);
                if (best is null || score > best.Value.Score)
                {
                    best = (x, y, score);
                }
            }
        }

        if (best is null || best.Value.Score == 0)
        {
            return null;
        }

        List<int> rows = new();
        for (int y = Math.Max(0, best.Value.Y - 3); y <= Math.Min(tiles.Height - 1, best.Value.Y + 3); y++)
        {
            int index = tiles.Index(best.Value.X, y);
            if (tiles.Active[index] && tiles.Type[index] is ResourceTileIds.ClosedDoor or ResourceTileIds.OpenDoor)
            {
                rows.Add(y);
            }
        }

        int centerY = rows.Count == 0
            ? best.Value.Y
            : (int)Math.Round(rows.Average(), MidpointRounding.AwayFromZero);
        return (best.Value.X, centerY);
    }

    private static int CountTempleBrick(TileGrid tiles, int centerX, int centerY)
    {
        int count = 0;
        for (int x = Math.Max(0, centerX - TempleDoorSearchRadius); x <= Math.Min(tiles.Width - 1, centerX + TempleDoorSearchRadius); x++)
        {
            for (int y = Math.Max(0, centerY - TempleDoorSearchRadius); y <= Math.Min(tiles.Height - 1, centerY + TempleDoorSearchRadius); y++)
            {
                int index = tiles.Index(x, y);
                count += tiles.Active[index] && tiles.Type[index] == ResourceTileIds.LihzahrdBrick ? 1 : 0;
            }
        }

        return count;
    }

    private static List<TerrariaPlanteraBulbAnchor> FindCandidates(
        WorldData world,
        (int X, int Y) door,
        int minimumY,
        int upperOuterRadius,
        int lowerInnerRadius,
        int lowerOuterRadius)
    {
        TileGrid tiles = world.Tiles;
        int maximumRadius = Math.Max(upperOuterRadius, lowerOuterRadius);
        long upperOuterSquared = (long)upperOuterRadius * upperOuterRadius * 4;
        long lowerInnerSquared = (long)lowerInnerRadius * lowerInnerRadius * 4;
        long lowerOuterSquared = (long)lowerOuterRadius * lowerOuterRadius * 4;
        List<TerrariaPlanteraBulbAnchor> candidates = new();
        for (int x = Math.Max(5, door.X - maximumRadius); x <= Math.Min(tiles.Width - 6, door.X + maximumRadius); x++)
        {
            for (int y = Math.Max(5, door.Y - upperOuterRadius); y <= Math.Min(tiles.Height - 6, door.Y + lowerOuterRadius); y++)
            {
                if (y < minimumY || !IsInRequestedArea(x, y, door, upperOuterSquared, lowerInnerSquared, lowerOuterSquared))
                {
                    continue;
                }

                if (CanPlaceBulb(tiles, x, y))
                {
                    candidates.Add(new TerrariaPlanteraBulbAnchor(x, y));
                }
            }
        }

        return candidates;
    }

    private static bool IsInRequestedArea(
        int x,
        int y,
        (int X, int Y) door,
        long upperOuterSquared,
        long lowerInnerSquared,
        long lowerOuterSquared)
    {
        long dx = (long)x * 2 - 1 - (long)door.X * 2;
        long dy = (long)y * 2 - 1 - (long)door.Y * 2;
        long distanceSquared = dx * dx + dy * dy;
        return dy <= 0
            ? distanceSquared <= upperOuterSquared
            : distanceSquared >= lowerInnerSquared && distanceSquared <= lowerOuterSquared;
    }

    private static bool CanPlaceBulb(TileGrid tiles, int x, int y)
    {
        for (int tileX = x - 1; tileX <= x; tileX++)
        {
            for (int tileY = y - 1; tileY <= y; tileY++)
            {
                int index = tiles.Index(tileX, tileY);
                if (tiles.Active[index] && !CanReplaceWithBulb(tiles.Type[index], tiles.FrameY[index]))
                {
                    return false;
                }
            }

            int support = tiles.Index(tileX, y + 1);
            if (!tiles.Active[support] || tiles.Type[support] != ResourceTileIds.JungleGrass)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanReplaceWithBulb(ushort type, short frameY) =>
        type is ResourceTileIds.JunglePlants or
            ResourceTileIds.JunglePlantsEcho or
            ResourceTileIds.JungleVines or
            ResourceTileIds.PlanteraThorns or
            ResourceTileIds.JungleThorns or
            ResourceTileIds.JunglePlants2 or
            ResourceTileIds.PlantDetritus ||
        type == ResourceTileIds.SmallPiles && frameY == 0;

    private static IReadOnlyList<TerrariaPlanteraBulbAnchor> SelectAnchors(
        IReadOnlyList<TerrariaPlanteraBulbAnchor> candidates,
        int worldId,
        Guid uniqueId,
        byte[] entropySeed,
        int protocolVersion)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<TerrariaPlanteraBulbAnchor>();
        }

        return candidates
            .Select(anchor => new OrderedAnchor(
                anchor,
                DeterministicDomainSeed.Derive(
                    entropySeed,
                    protocolVersion,
                    "first-plantera-bulb/anchor-order",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{worldId}|{uniqueId:N}|{anchor.X}|{anchor.Y}"))))
            .OrderBy(static entry => entry.Key, ByteArrayComparer.Instance)
            .ThenBy(static entry => entry.Anchor.X)
            .ThenBy(static entry => entry.Anchor.Y)
            .Select(static entry => entry.Anchor)
            .ToArray();
    }

    private sealed record OrderedAnchor(TerrariaPlanteraBulbAnchor Anchor, byte[] Key);

    private sealed class ByteArrayComparer : IComparer<byte[]>
    {
        public static ByteArrayComparer Instance { get; } = new();

        public int Compare(byte[]? left, byte[]? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }
            if (left is null)
            {
                return -1;
            }
            if (right is null)
            {
                return 1;
            }

            int length = Math.Min(left.Length, right.Length);
            for (int index = 0; index < length; index++)
            {
                int comparison = left[index].CompareTo(right[index]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return left.Length.CompareTo(right.Length);
        }
    }
}
