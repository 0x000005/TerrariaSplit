namespace WorldGenSim.Simulation;

internal static class WorldGenTileRunner
{
    private static readonly double[] ExtraStepThresholds =
    [
        50.0, 100.0, 150.0, 200.0, 250.0, 300.0, 400.0, 500.0, 600.0, 700.0, 800.0, 900.0
    ];

    public static void Run(
        WorldGenState state,
        UnifiedRandom random,
        int i,
        int j,
        double strength,
        int steps,
        int type,
        bool addTile = false,
        double speedX = 0.0,
        double speedY = 0.0,
        bool noYChange = false,
        bool overRide = true,
        int ignoreTileType = -1)
    {
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        double currentStrength = strength;
        double stepsRemaining = steps;
        double x = i;
        double y = j;
        double velocityX = random.Next(-10, 11) * 0.1;
        double velocityY = random.Next(-10, 11) * 0.1;
        if (speedX != 0.0 || speedY != 0.0)
        {
            velocityX = speedX;
            velocityY = speedY;
        }

        _ = random.Next(4);

        while (currentStrength > 0.0 && stepsRemaining > 0.0)
        {
            if (y < 0.0 && stepsRemaining > 0.0 && type == TileIds.Mud)
            {
                stepsRemaining = 0.0;
            }

            currentStrength = strength * (stepsRemaining / steps);
            stepsRemaining -= 1.0;

            int left = Math.Max(1, (int)(x - currentStrength * 0.5));
            int right = Math.Min(state.Options.Dimensions.Width - 1, (int)(x + currentStrength * 0.5));
            int top = Math.Max(1, (int)(y - currentStrength * 0.5));
            int bottom = Math.Min(state.Options.Dimensions.Height - 1, (int)(y + currentStrength * 0.5));

            for (int tileX = left; tileX < right; tileX++)
            {
                for (int tileY = top; tileY < bottom; tileY++)
                {
                    ref TileData tile = ref state.Tiles[tileX, tileY];
                    if (ignoreTileType >= 0 && tile.Active && tile.Type == ignoreTileType)
                    {
                        continue;
                    }

                    double randomizedRadius = strength * 0.5 * (1.0 + random.Next(-10, 11) * 0.015);
                    if (Math.Abs(tileX - x) + Math.Abs(tileY - y) >= randomizedRadius)
                    {
                        continue;
                    }

                    if (state.MudWall &&
                        tileY > state.MainWorldSurface &&
                        state.Tiles[tileX, tileY - 1].Wall != 2 &&
                        tileY < state.Options.Dimensions.Height - 210 - random.Next(3) &&
                        Math.Abs(tileX - x) + Math.Abs(tileY - y) < strength * 0.45 * (1.0 + random.Next(-10, 11) * 0.01))
                    {
                        if (tileY > state.LavaLine - random.Next(0, 4) - 50)
                        {
                            if (state.Tiles[tileX, tileY - 1].Wall != 64 &&
                                state.Tiles[tileX, tileY + 1].Wall != 64 &&
                                state.Tiles[tileX - 1, tileY].Wall != 64 &&
                                state.Tiles[tileX + 1, tileY].Wall != 64)
                            {
                                tile.Wall = 15;
                            }
                        }
                        else if (
                            state.Tiles[tileX, tileY - 1].Wall != 15 &&
                            state.Tiles[tileX, tileY + 1].Wall != 15 &&
                            state.Tiles[tileX - 1, tileY].Wall != 15 &&
                            state.Tiles[tileX + 1, tileY].Wall != 15)
                        {
                            tile.Wall = 64;
                        }
                    }

                    if (type < 0)
                    {
                        if (tile.Active && tile.Type == TileIds.Sand)
                        {
                            continue;
                        }

                        if (type == -2 && tile.Active && (tileY < state.WaterLine || tileY > state.LavaLine))
                        {
                            tile.Liquid = byte.MaxValue;
                            tile.LiquidType = tileY > state.LavaLine ? (byte)1 : (byte)0;
                        }

                        tile.Active = false;
                        continue;
                    }

                    bool skipPlacement = false;
                    if (overRide && tile.Active)
                    {
                        skipPlacement = ShouldSkipPlacement(state, random, tile, tileY, type);
                    }

                    if (!skipPlacement)
                    {
                        tile.Type = checked((ushort)type);
                    }

                    if (addTile)
                    {
                        tile.Active = true;
                        tile.Liquid = 0;
                        tile.LiquidType = 0;
                    }

                    if (noYChange && tileY < state.MainWorldSurface && type != TileIds.Mud)
                    {
                        tile.Wall = 2;
                    }

                    if (type == TileIds.Mud && tileY > state.WaterLine && tile.Liquid > 0)
                    {
                        tile.Liquid = 0;
                        tile.LiquidType = 0;
                    }
                }
            }

            x += velocityX;
            y += velocityY;
            for (int thresholdIndex = 0; thresholdIndex < ExtraStepThresholds.Length; thresholdIndex++)
            {
                if (currentStrength <= ExtraStepThresholds[thresholdIndex])
                {
                    break;
                }

                x += velocityX;
                y += velocityY;
                stepsRemaining -= 1.0;
                velocityY += random.Next(-10, 11) * 0.05;
                velocityX += random.Next(-10, 11) * 0.05;
            }

            velocityX += random.Next(-10, 11) * 0.05;
            velocityX = Math.Clamp(velocityX, -1.0, 1.0);
            if (!noYChange)
            {
                velocityY += random.Next(-10, 11) * 0.05;
                velocityY = Math.Clamp(velocityY, -1.0, 1.0);
            }
            else if (type != TileIds.Mud && currentStrength < 3.0)
            {
                velocityY = Math.Clamp(velocityY, -1.0, 1.0);
            }

            if (type == TileIds.Mud && !noYChange)
            {
                velocityY = Math.Clamp(velocityY, -0.5, 0.5);
                if (y < state.MainRockLayer + 100.0)
                {
                    velocityY = 1.0;
                }

                if (y > state.Options.Dimensions.Height - 300)
                {
                    velocityY = -1.0;
                }
            }
        }
    }

    private static bool ShouldSkipPlacement(
        WorldGenState state,
        UnifiedRandom random,
        TileData tile,
        int tileY,
        int type)
    {
        if (!CanBeClearedDuringGeneration(tile.Type))
        {
            return true;
        }

        if (tile.Type == TileIds.Sand)
        {
            if (type == TileIds.Clay)
            {
                return true;
            }

            if (tileY < state.MainWorldSurface && type != TileIds.Mud)
            {
                return true;
            }
        }

        if (tile.Type == TileIds.Stone && type == TileIds.Mud)
        {
            return tileY < state.MainWorldSurface + random.Next(-50, 50);
        }

        return false;
    }

    private static bool CanBeClearedDuringGeneration(int tileType)
    {
        return tileType is
            TileIds.Dirt or
            TileIds.Stone or
            TileIds.Sand or
            TileIds.Clay or
            TileIds.Mud or
            TileIds.Silt;
    }
}
