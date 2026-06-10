namespace WorldGenSim.Simulation;

internal static class MarblePassReplica
{
    private const int BeachDistance = 380;
    private const ushort MarbleWall = 178;
    private const int Scale = 3;
    private const int SlabColumns = 56;
    private const int SlabRows = 26;

    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Marble replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        int count = random.Next(4, 9);
        double slotWidth = (width - 160) / (double)count;
        int placed = 0;
        int attempts = 0;

        while (placed < count)
        {
            progress.Set(placed / (double)count);
            double slot = placed / (double)count;
            int originX = random.Next((int)(slot * (width - 200)) + 100, (int)(slot * (width - 200)) + 100 + (int)slotWidth);
            int originY = random.Next((int)state.RockLayer + 20, height - ((int)state.RockLayer + 40) - 200 + (int)state.RockLayer + 20);
            while (originX > width * 0.45 && originX < width * 0.55)
            {
                originX = random.Next(BeachDistance, width - BeachDistance);
            }

            if (PlaceBiome(state, random, originX, originY))
            {
                placed++;
            }
            else if (attempts++ > width * 10)
            {
                break;
            }
        }
    }

    private static bool PlaceBiome(WorldGenState state, UnifiedRandom random, int originX, int originY)
    {
        if (BiomeTileCheck(state, originX, originY))
        {
            return false;
        }

        Slab[,] slabs = new Slab[SlabColumns, SlabRows];
        int width = random.Next(80, 150) / Scale;
        int height = random.Next(40, 60) / Scale;
        int innerHalfHeight = (height * Scale - random.Next(20, 30)) / Scale;
        originX -= width * Scale / 2;
        originY -= height * Scale / 2;

        for (int i = -1; i < width + 1; i++)
        {
            double normalizedX = (i - width / 2) / (double)width + 0.5;
            int edgeBias = (int)((0.5 - Math.Abs(normalizedX - 0.5)) * 5.0) - 2;
            for (int j = -1; j < height + 1; j++)
            {
                bool hasWall = true;
                bool solid = false;
                bool groupSolid = IsGroupSolid(state, i * Scale + originX, j * Scale + originY, Scale);
                int verticalDistance = Math.Abs(j - height / 2) - innerHalfHeight / 4 + edgeBias;

                if (verticalDistance > 3)
                {
                    solid = groupSolid;
                    hasWall = false;
                }
                else if (verticalDistance > 0)
                {
                    solid = j - height / 2 > 0 || groupSolid;
                    hasWall = j - height / 2 < 0 || verticalDistance <= 2;
                }
                else if (verticalDistance == 0)
                {
                    solid = random.Next(2) == 0 && (j - height / 2 > 0 || groupSolid);
                }

                if (Math.Abs(normalizedX - 0.5) > 0.35 + random.NextDouble() * 0.1 && !groupSolid)
                {
                    hasWall = false;
                    solid = false;
                }

                slabs[i + 1, j + 1] = new Slab(solid ? SlabShape.Solid : SlabShape.Empty, hasWall);
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                SmoothSlope(slabs, x + 1, y + 1);
            }
        }

        int halfWidth = width / 2;
        int halfHeight = height / 2;
        int halfHeightSquared = (halfHeight + 1) * (halfHeight + 1);
        double driftStart = random.NextDouble() * 2.0 - 1.0;
        double driftMiddle = random.NextDouble() * 2.0 - 1.0;
        double driftEnd = random.NextDouble() * 2.0 - 1.0;
        double drift = 0.0;

        for (int x = 0; x <= width; x++)
        {
            double ellipseX = halfHeight / (double)halfWidth * (x - halfWidth);
            int localHalfHeight = Math.Min(halfHeight, (int)Math.Sqrt(Math.Max(0.0, halfHeightSquared - ellipseX * ellipseX)));
            drift = x >= width / 2
                ? drift + Lerp(driftMiddle, driftEnd, x / (double)(width / 2) - 1.0)
                : drift + Lerp(driftStart, driftMiddle, x / (double)(width / 2));

            for (int y = halfHeight - localHalfHeight; y <= halfHeight + localHalfHeight; y++)
            {
                PlaceSlab(state, random, slabs[x + 1, y + 1], x * Scale + originX, y * Scale + originY + (int)drift, Scale);
            }
        }

        return true;
    }

    private static void SmoothSlope(Slab[,] slabs, int x, int y)
    {
        Slab slab = slabs[x, y];
        if (!slab.IsSolid)
        {
            return;
        }

        bool top = slabs[x, y - 1].IsSolid;
        bool bottom = slabs[x, y + 1].IsSolid;
        bool left = slabs[x - 1, y].IsSolid;
        bool right = slabs[x + 1, y].IsSolid;
        int mask = (top ? 8 : 0) | (bottom ? 4 : 0) | (left ? 2 : 0) | (right ? 1 : 0);
        slabs[x, y] = mask switch
        {
            10 => slab with { Shape = SlabShape.TopLeftFilled },
            9 => slab with { Shape = SlabShape.TopRightFilled },
            6 => slab with { Shape = SlabShape.BottomLeftFilled },
            5 => slab with { Shape = SlabShape.BottomRightFilled },
            4 => slab with { Shape = SlabShape.HalfBrick },
            _ => slab with { Shape = SlabShape.Solid }
        };
    }

    private static void PlaceSlab(WorldGenState state, UnifiedRandom random, Slab slab, int originX, int originY, int scale)
    {
        int minX = -1;
        int maxX = scale + 1;
        int minY = 0;
        int maxY = scale;

        for (int i = minX; i < maxX; i++)
        {
            if ((i == minX || i == maxX - 1) && random.Next(2) == 0)
            {
                continue;
            }

            if (random.Next(2) == 0)
            {
                minY--;
            }

            if (random.Next(2) == 0)
            {
                maxY++;
            }

            for (int j = minY; j < maxY; j++)
            {
                int x = originX + i;
                int y = originY + j;
                if (!InWorld(state, x, y, 1))
                {
                    continue;
                }

                ref TileData tile = ref state.Tiles[x, y];
                tile.Type = TileIds.Marble;
                tile.Active = SlabState(slab.Shape, i, j, scale);
                if (slab.HasWall)
                {
                    tile.Wall = MarbleWall;
                }

                if (IsSolidTile(state, x, y - 1) && random.Next(4) == 0)
                {
                    ConsumePlaceTightRandom(random);
                }

                if (IsSolidTile(state, x, y) && random.Next(4) == 0)
                {
                    ConsumePlaceTightRandom(random);
                }
            }
        }
    }

    private static void ConsumePlaceTightRandom(UnifiedRandom random)
    {
        _ = random.Next(2);
        _ = random.Next(3);
    }

    private static bool IsGroupSolid(WorldGenState state, int x, int y, int scale)
    {
        int count = 0;
        for (int i = 0; i < scale; i++)
        {
            for (int j = 0; j < scale; j++)
            {
                if (InWorld(state, x + i, y + j, 0) && IsSolidTile(state, x + i, y + j))
                {
                    count++;
                }
            }
        }

        return count > scale / 4 * 3;
    }

    private static bool BiomeTileCheck(WorldGenState state, int x, int y)
    {
        for (int scanX = x - 50; scanX < x + 50; scanX++)
        {
            for (int scanY = y - 50; scanY < y + 50; scanY++)
            {
                if (!InWorld(state, scanX, scanY, 0))
                {
                    continue;
                }

                TileData tile = state.Tiles[scanX, scanY];
                if (tile.Active && tile.Type is
                    TileIds.Granite or
                    TileIds.Marble or
                    TileIds.SnowBlock or
                    TileIds.IceBlock or
                    162 or
                    70 or
                    72 or
                    TileIds.Sandstone or
                    TileIds.HardenedSand)
                {
                    return true;
                }

                if (tile.Wall is 187 or 216)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SlabState(SlabShape shape, int x, int y, int scale)
    {
        return shape switch
        {
            SlabShape.Empty => false,
            SlabShape.HalfBrick => y >= scale / 2,
            SlabShape.BottomRightFilled => x >= scale - y,
            SlabShape.BottomLeftFilled => x < y,
            SlabShape.TopRightFilled => x > y,
            SlabShape.TopLeftFilled => x < scale - y,
            _ => true
        };
    }

    private static bool IsSolidTile(WorldGenState state, int x, int y)
    {
        return InWorld(state, x, y, 0) && IsSolidTile(state.Tiles[x, y]);
    }

    private static bool IsSolidTile(TileData tile)
    {
        return tile.Active && tile.Type is
            TileIds.Dirt or
            TileIds.Stone or
            TileIds.Grass or
            TileIds.Clay or
            TileIds.Sand or
            TileIds.Mud or
            60 or
            70 or
            TileIds.SnowBlock or
            TileIds.IceBlock or
            TileIds.SandstoneBrick or
            TileIds.Marble or
            TileIds.Granite or
            TileIds.Sandstone or
            TileIds.HardenedSand or
            TileIds.DesertFossil;
    }

    private static bool InWorld(WorldGenState state, int x, int y, int fluff)
    {
        return x >= fluff &&
            y >= fluff &&
            x < state.Options.Dimensions.Width - fluff &&
            y < state.Options.Dimensions.Height - fluff;
    }

    private static double Lerp(double from, double to, double amount)
    {
        return from + (to - from) * amount;
    }

    private readonly record struct Slab(SlabShape Shape, bool HasWall)
    {
        public bool IsSolid => Shape != SlabShape.Empty;
    }

    private enum SlabShape
    {
        Empty,
        Solid,
        HalfBrick,
        BottomRightFilled,
        BottomLeftFilled,
        TopRightFilled,
        TopLeftFilled
    }
}
