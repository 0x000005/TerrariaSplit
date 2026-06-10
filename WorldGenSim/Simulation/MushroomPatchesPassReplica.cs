namespace WorldGenSim.Simulation;

internal static class MushroomPatchesPassReplica
{
    private const int MushroomGrass = 70;
    private const ushort MushroomWall = 80;
    private const int MaxMushroomBiomes = 50;

    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Mushroom patches replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        double count = width / 700.0;
        if (count > MaxMushroomBiomes)
        {
            count = MaxMushroomBiomes;
        }

        for (int patch = 0; patch < count; patch++)
        {
            SetProgress(progress, patch / count, 0.0, 0.33000001311302185);
            int attempts = 0;
            bool blocked = true;
            while (blocked)
            {
                int x = random.Next((int)(width * 0.2), (int)(width * 0.8));
                if (attempts > width / 4)
                {
                    x = random.Next((int)(width * 0.025), (int)(width * 0.975));
                }

                int y = random.Next((int)state.RockLayer + 50, height - 300);
                blocked = IsBlocked(state, x, y);
                if (!blocked && state.NumMushroomBiomes < MaxMushroomBiomes)
                {
                    ShroomPatch(state, random, x, y);
                    for (int i = 0; i < 5; i++)
                    {
                        int patchX = x + random.Next(-40, 41);
                        int patchY = y + random.Next(-40, 41);
                        ShroomPatch(state, random, patchX, patchY);
                    }

                    state.MushroomBiomesPosition[state.NumMushroomBiomes] = (x, y);
                    state.NumMushroomBiomes++;
                }

                attempts++;
                if (attempts > width / 2)
                {
                    break;
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            SetProgress(progress, (double)x / width, 0.33000001311302185, 0.6600000262260437);
            for (int y = (int)state.WorldSurface; y < height; y++)
            {
                if (InWorld(state, x, y, 50) && state.Tiles[x, y].Active)
                {
                    SpreadMushroomGrass(state, x, y);
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            SetProgress(progress, (double)x / width, 0.6600000262260437, 1.0);
            for (int y = (int)state.WorldSurface; y < height; y++)
            {
                if (!state.Tiles[x, y].Active || state.Tiles[x, y].Type != MushroomGrass)
                {
                    continue;
                }

                CleanMushroomGrassNeighborhood(state, x, y);
                if (random.Next(4) == 0)
                {
                    int patchX = x + random.Next(-20, 21);
                    int patchY = y + random.Next(-20, 21);
                    if (InWorld(state, patchX, patchY, 0) && state.Tiles[patchX, patchY].Type == TileIds.Mud)
                    {
                        state.Tiles[patchX, patchY].Type = MushroomGrass;
                    }
                }
            }
        }
    }

    private static bool IsBlocked(WorldGenState state, int x, int y)
    {
        const int scanRadius = 100;
        for (int scanX = x - scanRadius; scanX < x + scanRadius; scanX += 3)
        {
            for (int scanY = y - scanRadius; scanY < y + scanRadius; scanY += 3)
            {
                if (!InWorld(state, scanX, scanY, 0))
                {
                    return true;
                }

                ref TileData tile = ref state.Tiles[scanX, scanY];
                if (tile.Active && tile.Type is
                    TileIds.SnowBlock or
                    TileIds.IceBlock or
                    162 or
                    60 or
                    368 or
                    367)
                {
                    return true;
                }

                if (state.UndergroundDesertLocation.Contains(scanX, scanY))
                {
                    return true;
                }
            }
        }

        for (int i = 0; i < state.NumMushroomBiomes; i++)
        {
            (int otherX, int otherY) = state.MushroomBiomesPosition[i];
            double dx = otherX - x;
            double dy = otherY - y;
            if (Math.Sqrt(dx * dx + dy * dy) < 500.0)
            {
                return true;
            }
        }

        return false;
    }

    private static void ShroomPatch(WorldGenState state, UnifiedRandom random, int i, int j)
    {
        _ = random.Next(3);
        double strength = random.Next(80, 100);
        double steps = random.Next(20, 26);
        double scale = state.Options.Dimensions.Width / 4200.0;
        strength *= scale;
        steps *= scale;
        double firstStep = steps - 1.0;
        double x = i;
        double y = j - steps * 0.3;
        double velocityX = random.Next(-100, 101) * 0.005;
        double velocityY = random.Next(-200, -100) * 0.005;

        while (strength > 0.0 && steps > 0.0)
        {
            strength -= random.Next(3);
            steps -= 1.0;
            int left = Math.Max(0, (int)(x - strength * 0.5));
            int right = Math.Min(state.Options.Dimensions.Width, (int)(x + strength * 0.5));
            int top = Math.Max(0, (int)(y - strength * 0.5));
            int bottom = Math.Min(state.Options.Dimensions.Height, (int)(y + strength * 0.5));
            double currentStrength = strength * random.Next(80, 120) * 0.01;
            for (int tileX = left; tileX < right; tileX++)
            {
                for (int tileY = top; tileY < bottom; tileY++)
                {
                    double dx = Math.Abs(tileX - x);
                    double dy = Math.Abs((tileY - y) * 2.3);
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    ref TileData tile = ref state.Tiles[tileX, tileY];
                    if (distance < currentStrength * 0.8 && tile.LiquidType == 1)
                    {
                        tile.Liquid = 0;
                        tile.LiquidType = 0;
                    }

                    if (distance < currentStrength * 0.2 && tileY < y)
                    {
                        tile.Active = false;
                        if (tile.Wall > 0)
                        {
                            tile.Wall = MushroomWall;
                        }
                    }
                    else if (distance < currentStrength * 0.4 * (0.95 + random.NextDouble() * 0.1))
                    {
                        tile.Type = TileIds.Mud;
                        if (steps == firstStep && tileY > y)
                        {
                            tile.Active = true;
                        }

                        if (tile.Wall > 0)
                        {
                            tile.Wall = MushroomWall;
                        }
                    }
                }
            }

            x += velocityX;
            y += velocityY;
            x += velocityX;
            velocityX += random.Next(-100, 110) * 0.005;
            velocityY -= random.Next(110) * 0.005;
            if (velocityX > -0.5 && velocityX < 0.5)
            {
                velocityX = velocityX < 0.0 ? -0.5 : 0.5;
            }

            velocityX = Math.Clamp(velocityX, -0.5, 0.5);
            velocityY = Math.Clamp(velocityY, -0.5, 0.5);

            for (int m = 0; m < 2; m++)
            {
                int tileX = (int)x + random.Next(-20, 20);
                int tileY = (int)y + random.Next(0, 20);
                int guard = 0;
                while (InWorld(state, tileX, tileY, 1) &&
                    !state.Tiles[tileX, tileY].Active &&
                    state.Tiles[tileX, tileY].Type != TileIds.Mud &&
                    guard++ < 10000)
                {
                    tileX = (int)x + random.Next(-20, 20);
                    tileY = (int)y + random.Next(0, 20);
                }

                int runnerStrength = random.Next(10, 20);
                int runnerSteps = random.Next(10, 20);
                WorldGenTileRunner.Run(
                    state,
                    random,
                    tileX,
                    tileY,
                    runnerStrength,
                    runnerSteps,
                    TileIds.Mud,
                    addTile: false,
                    speedX: 0.0,
                    speedY: 2.0,
                    noYChange: true);
            }
        }
    }

    private static void SpreadMushroomGrass(WorldGenState state, int x, int y)
    {
        if (!InWorld(state, x, y, 10) ||
            !state.Tiles[x, y].Active ||
            state.Tiles[x, y].Type != TileIds.Mud)
        {
            return;
        }

        bool surrounded = true;
        for (int tileX = x - 1; tileX < x + 2; tileX++)
        {
            for (int tileY = y - 1; tileY < y + 2; tileY++)
            {
                if (!state.Tiles[tileX, tileY].Active || !IsSolid(state.Tiles[tileX, tileY].Type))
                {
                    surrounded = false;
                }
            }
        }

        if (!surrounded)
        {
            state.Tiles[x, y].Type = MushroomGrass;
        }
    }

    private static void CleanMushroomGrassNeighborhood(WorldGenState state, int x, int y)
    {
        for (int tileX = x - 1; tileX <= x + 1; tileX++)
        {
            for (int tileY = y - 1; tileY <= y + 1; tileY++)
            {
                if (!InWorld(state, tileX, tileY, 1))
                {
                    continue;
                }

                if (state.Tiles[tileX, tileY].Active)
                {
                    if (!state.Tiles[tileX - 1, tileY].Active && !state.Tiles[tileX + 1, tileY].Active)
                    {
                        state.Tiles[tileX, tileY].Active = false;
                    }
                    else if (!state.Tiles[tileX, tileY - 1].Active && !state.Tiles[tileX, tileY + 1].Active)
                    {
                        state.Tiles[tileX, tileY].Active = false;
                    }
                }
                else if (state.Tiles[tileX - 1, tileY].Active && state.Tiles[tileX + 1, tileY].Active)
                {
                    state.Tiles[tileX, tileY].SetType(TileIds.Mud);
                    if (state.Tiles[tileX - 1, y].Type == MushroomGrass)
                    {
                        state.Tiles[tileX - 1, y].Type = TileIds.Mud;
                    }

                    if (state.Tiles[tileX + 1, y].Type == MushroomGrass)
                    {
                        state.Tiles[tileX + 1, y].Type = TileIds.Mud;
                    }
                }
                else if (state.Tiles[tileX, tileY - 1].Active && state.Tiles[tileX, tileY + 1].Active)
                {
                    state.Tiles[tileX, tileY].SetType(TileIds.Mud);
                    if (state.Tiles[tileX, y - 1].Type == MushroomGrass)
                    {
                        state.Tiles[tileX, y - 1].Type = TileIds.Mud;
                    }

                    if (state.Tiles[tileX, y + 1].Type == MushroomGrass)
                    {
                        state.Tiles[tileX, y + 1].Type = TileIds.Mud;
                    }
                }
            }
        }
    }

    private static bool IsSolid(int tileType)
    {
        return tileType is
            TileIds.Dirt or
            TileIds.Stone or
            TileIds.Grass or
            TileIds.Clay or
            TileIds.Sand or
            TileIds.Mud or
            60 or
            MushroomGrass or
            TileIds.SnowBlock or
            TileIds.IceBlock or
            TileIds.SandstoneBrick or
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

    private static void SetProgress(GenerationProgress progress, double value, double min, double max)
    {
        progress.Set(min + value * (max - min));
    }
}
