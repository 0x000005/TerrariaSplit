using System.Drawing;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class WeightedRouteCostField
{
    private const int InternalUnitsPerDisplayedCost = 10;
    private const int OrthogonalCost = 3;
    private const int DiagonalCost = 4;
    private const int DefaultSolidPenalty = 40;
    private const int HivePenalty = 100;
    private const int LiquidPenalty = 10;
    private readonly WorldData world;
    private readonly int maxInternalCost;
    private readonly int[] distance;
    private readonly int[] predecessor;
    private readonly short[] terrainPenalty;
    private readonly int solidPenalty;

    public WeightedRouteCostField(
        WorldData world,
        RouteResult route,
        int maximumDisplayedCost,
        int solidPenalty = DefaultSolidPenalty)
    {
        this.world = world;
        this.solidPenalty = solidPenalty;
        maxInternalCost = checked(maximumDisplayedCost * InternalUnitsPerDisplayedCost);
        int count = checked(world.Tiles.Width * world.Tiles.Height);
        distance = GC.AllocateUninitializedArray<int>(count);
        predecessor = GC.AllocateUninitializedArray<int>(count);
        terrainPenalty = GC.AllocateUninitializedArray<short>(count);
        Array.Fill(distance, int.MaxValue);
        Array.Fill(predecessor, -2);
        BuildTerrainPenalty();
        Build(route);
    }

    public int VisitedTileCount { get; private set; }

    public WeightedAccessResult Measure(int x, int y, int width, int height, bool capturePath)
    {
        int bestIndex = -1;
        int bestCost = int.MaxValue;
        int left = Math.Max(0, x - 1);
        int right = Math.Min(world.Tiles.Width - 1, x + width);
        int top = Math.Max(0, y - 1);
        int bottom = Math.Min(world.Tiles.Height - 1, y + height);
        for (int candidateX = left; candidateX <= right; candidateX++)
        {
            TryCandidate(candidateX, top);
            TryCandidate(candidateX, bottom);
        }
        for (int candidateY = top + 1; candidateY < bottom; candidateY++)
        {
            TryCandidate(left, candidateY);
            TryCandidate(right, candidateY);
        }

        if (bestIndex < 0)
        {
            return WeightedAccessResult.Unreachable;
        }

        List<Point>? path = capturePath ? new List<Point>() : null;
        int current = bestIndex;
        int travelSteps = 0;
        int dugTiles = 0;
        int sourceIndex = bestIndex;
        while (current >= 0)
        {
            int tileX = current / world.Tiles.Height;
            int tileY = current % world.Tiles.Height;
            path?.Add(new Point(tileX, tileY));
            sourceIndex = current;
            int previous = predecessor[current];
            if (previous < 0)
            {
                break;
            }

            travelSteps++;
            if (IsDugTile(current))
            {
                dugTiles++;
            }
            current = previous;
        }

        if (path is not null)
        {
            path.Reverse();
        }
        int sourceX = sourceIndex / world.Tiles.Height;
        int sourceY = sourceIndex % world.Tiles.Height;
        return new WeightedAccessResult(bestCost, travelSteps, dugTiles, sourceX, sourceY, path);

        void TryCandidate(int candidateX, int candidateY)
        {
            int index = world.Tiles.Index(candidateX, candidateY);
            int candidateCost = distance[index];
            if (candidateCost < bestCost)
            {
                bestCost = candidateCost;
                bestIndex = index;
            }
        }
    }

    private void Build(RouteResult route)
    {
        List<int>?[] buckets = new List<int>?[maxInternalCost + 1];
        SeedRoute(route, buckets);
        for (int currentCost = 0; currentCost <= maxInternalCost; currentCost++)
        {
            List<int>? bucket = buckets[currentCost];
            if (bucket is null)
            {
                continue;
            }

            while (bucket.Count > 0)
            {
                int last = bucket.Count - 1;
                int current = bucket[last];
                bucket.RemoveAt(last);
                if (distance[current] != currentCost)
                {
                    continue;
                }

                VisitedTileCount++;
                int x = current / world.Tiles.Height;
                int y = current % world.Tiles.Height;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if ((dx == 0 && dy == 0) || !world.Tiles.Contains(x + dx, y + dy))
                        {
                            continue;
                        }

                        bool diagonal = dx != 0 && dy != 0;
                        if (diagonal &&
                            (terrainPenalty[world.Tiles.Index(x + dx, y)] < 0 ||
                             terrainPenalty[world.Tiles.Index(x, y + dy)] < 0))
                        {
                            continue;
                        }

                        int next = world.Tiles.Index(x + dx, y + dy);
                        int nextTerrainPenalty = terrainPenalty[next];
                        if (nextTerrainPenalty < 0)
                        {
                            continue;
                        }

                        int nextCost = currentCost + (diagonal ? DiagonalCost : OrthogonalCost) + nextTerrainPenalty;
                        if (nextCost > maxInternalCost)
                        {
                            continue;
                        }

                        if (nextCost >= distance[next])
                        {
                            continue;
                        }

                        distance[next] = nextCost;
                        predecessor[next] = current;
                        (buckets[nextCost] ??= new List<int>()).Add(next);
                    }
                }
            }
        }
    }

    private void SeedRoute(RouteResult route, List<int>?[] buckets)
    {
        List<int> sources = buckets[0] = new List<int>();
        for (int cell = 0; cell < route.RouteMask.Length; cell++)
        {
            if (!route.RouteMask[cell])
            {
                continue;
            }

            int cellX = cell % route.GridWidth;
            int cellY = cell / route.GridWidth;
            ushort passableMask = route.PassableMasks is not null && cell < route.PassableMasks.Length
                ? route.PassableMasks[cell]
                : (ushort)0;
            bool seeded = false;
            for (int localY = 0; localY < route.CellSize; localY++)
            {
                for (int localX = 0; localX < route.CellSize; localX++)
                {
                    if (passableMask != 0 && (passableMask & (1 << (localY * route.CellSize + localX))) == 0)
                    {
                        continue;
                    }

                    int x = cellX * route.CellSize + localX;
                    int y = cellY * route.CellSize + localY;
                    if (!world.Tiles.Contains(x, y))
                    {
                        continue;
                    }

                    int index = world.Tiles.Index(x, y);
                    if (terrainPenalty[index] < 0)
                    {
                        continue;
                    }
                    if (distance[index] == 0)
                    {
                        continue;
                    }

                    distance[index] = 0;
                    predecessor[index] = -1;
                    sources.Add(index);
                    seeded = true;
                }
            }

            if (!seeded)
            {
                int x = Math.Clamp(cellX * route.CellSize + route.CellSize / 2, 0, world.Tiles.Width - 1);
                int y = Math.Clamp(cellY * route.CellSize + route.CellSize / 2, 0, world.Tiles.Height - 1);
                int index = world.Tiles.Index(x, y);
                if (terrainPenalty[index] >= 0 && distance[index] != 0)
                {
                    distance[index] = 0;
                    predecessor[index] = -1;
                    sources.Add(index);
                }
            }
        }
    }

    private void BuildTerrainPenalty()
    {
        TileGrid tiles = world.Tiles;
        for (int index = 0; index < terrainPenalty.Length; index++)
        {
            byte liquid = tiles.Liquid[index];
            byte liquidType = tiles.LiquidType[index];
            ushort type = tiles.Type[index];
            if ((liquid > 0 && liquidType is 1 or 3) ||
                tiles.Wall[index] == WallIds.LihzahrdBrick ||
                (tiles.Active[index] && type is TileIds.LihzahrdBrick or TileIds.BlueDungeonBrick or TileIds.GreenDungeonBrick or TileIds.PinkDungeonBrick))
            {
                terrainPenalty[index] = -1;
                continue;
            }

            int penalty = liquid > 0 ? LiquidPenalty : 0;
            if (tiles.Active[index] && type is TileIds.Hive or TileIds.HoneyBlock or TileIds.CrispyHoneyBlock or TileIds.BeeHive)
            {
                terrainPenalty[index] = (short)(penalty + HivePenalty);
            }
            else if (tiles.Active[index] && IsCostSolid(type))
            {
                terrainPenalty[index] = (short)(penalty + solidPenalty);
            }
            else
            {
                terrainPenalty[index] = (short)penalty;
            }
        }
    }

    private bool IsDugTile(int index)
    {
        return world.Tiles.Active[index] && terrainPenalty[index] >= solidPenalty;
    }

    private static bool IsCostSolid(ushort type)
    {
        return type switch
        {
            TileIds.Cobweb or
            TileIds.Plants or
            TileIds.Torches or
            TileIds.Chairs or
            TileIds.ClosedDoor or
            TileIds.OpenDoor or
            TileIds.Heart or
            TileIds.Containers or
            TileIds.Pots or
            TileIds.Vines or
            TileIds.Signs or
            TileIds.JunglePlants or
            TileIds.JungleVines or
            TileIds.JunglePlants2 or
            TileIds.Trees or
            TileIds.LivingWood or
            TileIds.LeafBlock or
            TileIds.CorruptThorns or
            TileIds.JungleThorns or
            TileIds.CrimsonThorns or
            TileIds.PlanteraThorns or
            TileIds.MinecartTrack or
            TileIds.PressureTrack or
            TileIds.LivingLoom or
            TileIds.Larva or
            TileIds.LivingMahoganyLeaves or
            TileIds.Containers2 => false,
            _ => true
        };
    }
}

internal readonly record struct WeightedAccessResult(
    int InternalCost,
    int TravelSteps,
    int DugTiles,
    int? NearestRouteX,
    int? NearestRouteY,
    IReadOnlyList<Point>? Path)
{
    public static WeightedAccessResult Unreachable { get; } =
        new(int.MaxValue, 0, 0, null, null, null);

    public bool Reachable => InternalCost != int.MaxValue;
}
