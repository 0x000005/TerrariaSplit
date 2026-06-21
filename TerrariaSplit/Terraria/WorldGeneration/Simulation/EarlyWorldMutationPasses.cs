using static TerrariaSplit.Terraria.WorldGeneration.Simulation.WorldGenBounds;

namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal static class EarlyWorldMutationPasses
{
    public static void ApplyTunnels(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = RequireTargetState(context, "Tunnels");
        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int tunnelCount = (int)(width * 0.0015);

        for (int i = 0; i < tunnelCount; i++)
        {
            progress.Set((double)i / tunnelCount);
            if (state.NumTunnels >= state.TunnelX.Length - 1)
            {
                break;
            }

            int[] xs = new int[10];
            int[] ys = new int[10];
            int x = random.Next(450, width - 450);
            while (x > width * 0.4 && x < width * 0.6)
            {
                x = random.Next(450, width - 450);
            }

            int y = 0;
            bool touchedSand;
            do
            {
                touchedSand = false;
                for (int k = 0; k < 10; k++)
                {
                    x %= width;
                    while (!state.Tiles[x, y].Active)
                    {
                        y++;
                    }

                    if (state.Tiles[x, y].Type == TileIds.Sand)
                    {
                        touchedSand = true;
                    }

                    xs[k] = x;
                    ys[k] = y - random.Next(11, 16);
                    x += random.Next(5, 11);
                }
            }
            while (touchedSand);

            state.TunnelX[state.NumTunnels] = xs[5];
            state.NumTunnels++;

            for (int l = 0; l < 10; l++)
            {
                WorldGenTileRunner.Run(state, random, xs[l], ys[l], random.Next(5, 8), random.Next(6, 9), TileIds.Dirt, addTile: true, speedX: -2.0, speedY: -0.3);
                WorldGenTileRunner.Run(state, random, xs[l], ys[l], random.Next(5, 8), random.Next(6, 9), TileIds.Dirt, addTile: true, speedX: 2.0, speedY: -0.3);
            }
        }
    }

    public static void ApplyMountCaves(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = RequireTargetState(context, "Mount Caves");
        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int caveCount = (int)(width * 0.001);

        for (int i = 0; i < caveCount; i++)
        {
            progress.Set((double)i / caveCount);
            int retryCount = 0;
            bool giveUp = false;
            bool accepted = false;
            int x = random.Next((int)(width * 0.25), (int)(width * 0.75));
            while (!accepted)
            {
                accepted = true;
                while (x > width / 2 - 90 && x < width / 2 + 90)
                {
                    x = random.Next((int)(width * 0.25), (int)(width * 0.75));
                }

                for (int j = 0; j < state.NumMountainCaves; j++)
                {
                    if (Math.Abs(x - state.MountainCaveX[j]) < 100)
                    {
                        retryCount++;
                        accepted = false;
                        break;
                    }
                }

                if (retryCount >= width / 5)
                {
                    giveUp = true;
                    break;
                }
            }

            if (giveUp)
            {
                continue;
            }

            bool blocked = false;
            for (int y = 0; y < state.MainWorldSurface; y++)
            {
                if (!state.Tiles[x, y].Active)
                {
                    continue;
                }

                for (int scanX = x - 50; scanX < x + 50; scanX++)
                {
                    for (int scanY = y - 25; scanY < y + 25; scanY++)
                    {
                        if (IsInWorld(state, scanX, scanY) && state.Tiles[scanX, scanY].Active)
                        {
                            int type = state.Tiles[scanX, scanY].Type;
                            if (type is TileIds.Sand or TileIds.SandstoneBrick or TileIds.HardenedSand)
                            {
                                blocked = true;
                            }
                        }
                    }
                }

                if (!blocked)
                {
                    Mountinater(state, random, x, y);
                    if (state.NumMountainCaves < state.MountainCaveX.Length)
                    {
                        state.MountainCaveX[state.NumMountainCaves] = x;
                        state.MountainCaveY[state.NumMountainCaves] = y;
                        state.NumMountainCaves++;
                    }
                }

                break;
            }
        }
    }

    public static void ApplyDirtWallBackgrounds(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = RequireTargetState(context, "Dirt Wall Backgrounds");
        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        for (int i = 1; i < width - 1; i++)
        {
            progress.Set((double)i / width);
            _ = random.Next(-1, 2);
        }
    }

    public static void ApplyRocksInDirt(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = RequireTargetState(context, "Rocks In Dirt");
        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;

        double count = width * height * 0.00015;
        for (int i = 0; i < count; i++)
        {
            WorldGenTileRunner.Run(state, random, random.Next(0, width), random.Next(0, (int)state.WorldSurfaceLow + 1), random.Next(4, 15), random.Next(5, 40), TileIds.Stone);
        }

        count = width * height * 0.0002;
        for (int i = 0; i < count; i++)
        {
            int x = random.Next(0, width);
            int y = random.Next((int)state.WorldSurfaceLow, (int)state.WorldSurfaceHigh + 1);
            if (!state.Tiles[x, y - 10].Active)
            {
                y = random.Next((int)state.WorldSurfaceLow, (int)state.WorldSurfaceHigh + 1);
            }

            WorldGenTileRunner.Run(state, random, x, y, random.Next(4, 10), random.Next(5, 30), TileIds.Stone);
        }

        count = width * height * 0.0045;
        for (int i = 0; i < count; i++)
        {
            WorldGenTileRunner.Run(state, random, random.Next(0, width), random.Next((int)state.WorldSurfaceHigh, (int)state.RockLayerHigh + 1), random.Next(2, 7), random.Next(2, 23), TileIds.Stone);
        }
    }

    public static void ApplyDirtInRocks(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = RequireTargetState(context, "Dirt In Rocks");
        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        double count = width * height * 0.005;
        for (int i = 0; i < count; i++)
        {
            WorldGenTileRunner.Run(state, random, random.Next(0, width), random.Next((int)state.RockLayerLow, height), random.Next(2, 6), random.Next(2, 40), TileIds.Dirt);
        }
    }

    public static void ApplyClay(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = RequireTargetState(context, "Clay");
        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;

        for (int i = 0; i < (int)(width * height * 2E-05); i++)
        {
            WorldGenTileRunner.Run(state, random, random.Next(0, width), random.Next(0, (int)state.WorldSurfaceLow), random.Next(4, 14), random.Next(10, 50), TileIds.Clay);
        }

        for (int i = 0; i < (int)(width * height * 5E-05); i++)
        {
            WorldGenTileRunner.Run(state, random, random.Next(0, width), random.Next((int)state.WorldSurfaceLow, (int)state.WorldSurfaceHigh + 1), random.Next(8, 14), random.Next(15, 45), TileIds.Clay);
        }

        for (int i = 0; i < (int)(width * height * 2E-05); i++)
        {
            WorldGenTileRunner.Run(state, random, random.Next(0, width), random.Next((int)state.WorldSurfaceHigh, (int)state.RockLayerHigh + 1), random.Next(8, 15), random.Next(5, 50), TileIds.Clay);
        }

        for (int x = 5; x < width - 5; x++)
        {
            for (int y = 1; y < state.MainWorldSurface - 1.0 && y < height; y++)
            {
                if (!state.Tiles[x, y].Active)
                {
                    continue;
                }

                for (int clayY = y; clayY < y + 5 && clayY < height; clayY++)
                {
                    if (state.Tiles[x, clayY].Type == TileIds.Clay)
                    {
                        state.Tiles[x, clayY].Type = TileIds.Dirt;
                    }
                }

                break;
            }
        }
    }

    public static void ApplySmallHoles(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = RequireTargetState(context, "Small Holes");
        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        int beachAvoidance = state.BeachSandRandomCenter + 20;
        int count = (int)(width * height * 0.0015);

        for (int i = 0; i < count; i++)
        {
            progress.Set((double)i / count);
            int type = random.Next(5) == 0 ? -2 : -1;
            RunSmallHole(state, random, width, height, beachAvoidance, type, 2, 5, 2, 20);
            RunSmallHole(state, random, width, height, beachAvoidance, type, 8, 15, 7, 30);
        }
    }

    public static void ApplyDirtLayerCaves(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = RequireTargetState(context, "Dirt Layer Caves");
        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int beachAvoidance = state.BeachSandRandomCenter + 20;
        int count = (int)(width * state.Options.Dimensions.Height * 3E-05);

        for (int i = 0; i < count; i++)
        {
            progress.Set((double)i / count);
            if (state.RockLayerHigh > state.Options.Dimensions.Height)
            {
                continue;
            }

            int type = random.Next(6) == 0 ? -2 : -1;
            int x = random.Next(0, width);
            int y = random.Next((int)state.WorldSurfaceLow, (int)state.RockLayerHigh + 1);
            while (((x < beachAvoidance || x > width - beachAvoidance) && y < state.WorldSurfaceHigh) ||
                (x >= width * 0.45 && x <= width * 0.55 && y < state.MainWorldSurface))
            {
                x = random.Next(0, width);
                y = random.Next((int)state.WorldSurfaceLow, (int)state.RockLayerHigh + 1);
            }

            WorldGenTileRunner.Run(state, random, x, y, random.Next(5, 15), random.Next(30, 200), type);
        }
    }

    public static void ApplyRockLayerCaves(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = RequireTargetState(context, "Rock Layer Caves");
        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        int count = (int)(width * height * 0.00013);

        for (int i = 0; i < count; i++)
        {
            progress.Set((double)i / count);
            if (state.RockLayerHigh > height)
            {
                continue;
            }

            int type = random.Next(10) == 0 ? -2 : -1;
            int strength = random.Next(6, 20);
            int steps = random.Next(50, 300);
            WorldGenTileRunner.Run(state, random, random.Next(0, width), random.Next((int)state.RockLayerHigh, height), strength, steps, type);
        }
    }

    public static void ApplySurfaceCaves(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = RequireTargetState(context, "Surface Caves");
        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;

        int small = (int)(width * 0.002);
        int medium = (int)(width * 0.0007);
        int large = (int)(width * 0.0003);

        for (int i = 0; i < small; i++)
        {
            int x = PickSurfaceCaveX(state, random, 0.45, 0.55);
            int? y = FirstActiveY(state, x, 0, (int)state.WorldSurfaceHigh);
            if (y.HasValue)
            {
                WorldGenTileRunner.Run(state, random, x, y.Value, random.Next(3, 6), random.Next(5, 50), -1, addTile: false, speedX: random.Next(-10, 11) * 0.1, speedY: 1.0);
            }
        }

        for (int i = 0; i < medium; i++)
        {
            int x = PickSurfaceCaveX(state, random, 0.43, 0.5700000000000001);
            int? y = FirstActiveY(state, x, 0, (int)state.WorldSurfaceHigh);
            if (y.HasValue)
            {
                WorldGenTileRunner.Run(state, random, x, y.Value, random.Next(10, 15), random.Next(50, 130), -1, addTile: false, speedX: random.Next(-10, 11) * 0.1, speedY: 2.0);
            }
        }

        for (int i = 0; i < large; i++)
        {
            int x = PickSurfaceCaveX(state, random, 0.4, 0.6);
            int? y = FirstActiveY(state, x, 0, (int)state.WorldSurfaceHigh);
            if (y.HasValue)
            {
                WorldGenTileRunner.Run(state, random, x, y.Value, random.Next(12, 25), random.Next(150, 500), -1, addTile: false, speedX: random.Next(-10, 11) * 0.1, speedY: 4.0);
                WorldGenTileRunner.Run(state, random, x, y.Value, random.Next(8, 17), random.Next(60, 200), -1, addTile: false, speedX: random.Next(-10, 11) * 0.1, speedY: 2.0);
                WorldGenTileRunner.Run(state, random, x, y.Value, random.Next(5, 13), random.Next(40, 170), -1, addTile: false, speedX: random.Next(-10, 11) * 0.1, speedY: 2.0);
            }
        }

        int vertical = (int)(width * 0.0004);
        for (int i = 0; i < vertical; i++)
        {
            int x = PickSurfaceCaveX(state, random, 0.4, 0.6);
            int? y = FirstActiveY(state, x, 0, (int)state.WorldSurfaceHigh);
            if (y.HasValue)
            {
                WorldGenTileRunner.Run(state, random, x, y.Value, random.Next(7, 12), random.Next(150, 250), -1, addTile: false, speedX: 0.0, speedY: 1.0, noYChange: true);
            }
        }

        int cavererCount = (int)(5.0 * (width / 4200.0));
        for (int i = 0; i < cavererCount; i++)
        {
            int minY = (int)state.MainRockLayer;
            int maxY = height - 400;
            if (minY >= maxY)
            {
                minY = maxY - 1;
            }

            Caverer(state, random, random.Next(state.SurfaceCavesBeachAvoidance2, width - state.SurfaceCavesBeachAvoidance2), random.Next(minY, maxY));
        }
    }

    private static void RunSmallHole(
        WorldGenState state,
        UnifiedRandom random,
        int width,
        int height,
        int beachAvoidance,
        int type,
        int strengthMin,
        int strengthMax,
        int stepsMin,
        int stepsMax)
    {
        int x = random.Next(0, width);
        int y = random.Next((int)state.WorldSurfaceHigh, height);
        while (((x < beachAvoidance || x > width - beachAvoidance) && y < state.WorldSurfaceHigh) ||
            (x > width * 0.45 && x < width * 0.55 && y < state.MainWorldSurface))
        {
            x = random.Next(0, width);
            y = random.Next((int)state.WorldSurfaceHigh, height);
        }

        int strength = random.Next(strengthMin, strengthMax);
        int steps = random.Next(stepsMin, stepsMax);
        WorldGenTileRunner.Run(state, random, x, y, strength, steps, type);
    }

    private static int PickSurfaceCaveX(
        WorldGenState state,
        UnifiedRandom random,
        double centerLeft,
        double centerRight)
    {
        int width = state.Options.Dimensions.Width;
        int x = random.Next(0, width);
        while ((x > width * centerLeft && x < width * centerRight) ||
            x < state.LeftBeachEnd + 20 ||
            x > state.RightBeachStart - 20)
        {
            x = random.Next(0, width);
        }

        return x;
    }

    private static int? FirstActiveY(WorldGenState state, int x, int minY, int maxExclusive)
    {
        int maxY = Math.Min(maxExclusive, state.Options.Dimensions.Height);
        for (int y = Math.Max(0, minY); y < maxY; y++)
        {
            if (state.Tiles[x, y].Active)
            {
                return y;
            }
        }

        return null;
    }

    private static void Mountinater(WorldGenState state, UnifiedRandom random, int i, int j)
    {
        double strength = random.Next(80, 120);
        double steps = random.Next(40, 55);
        double x = i;
        double y = j + steps / 2.0;
        double velocityX = random.Next(-10, 11) * 0.1;
        double velocityY = random.Next(-20, -10) * 0.1;

        while (strength > 0.0 && steps > 0.0)
        {
            strength -= random.Next(4);
            steps -= 1.0;
            int left = Math.Max(0, (int)(x - strength * 0.5));
            int right = Math.Min(state.Options.Dimensions.Width, (int)(x + strength * 0.5));
            int top = Math.Max(0, (int)(y - strength * 0.5));
            int bottom = Math.Min(state.Options.Dimensions.Height, (int)(y + strength * 0.5));
            double randomizedStrength = strength * random.Next(80, 120) * 0.01;

            for (int tileX = left; tileX < right; tileX++)
            {
                for (int tileY = top; tileY < bottom; tileY++)
                {
                    double dx = Math.Abs(tileX - x);
                    double dy = Math.Abs(tileY - y);
                    if (Math.Sqrt(dx * dx + dy * dy) < randomizedStrength * 0.4 &&
                        !state.Tiles[tileX, tileY].Active)
                    {
                        state.Tiles[tileX, tileY].SetType(TileIds.Dirt);
                    }
                }
            }

            x += velocityX;
            y += velocityY;
            velocityX += random.Next(-10, 11) * 0.05;
            velocityY += random.Next(-10, 11) * 0.05;
            velocityX = Math.Clamp(velocityX, -0.5, 0.5);
            velocityY = Math.Clamp(velocityY, -1.5, -0.5);
        }
    }

    private static void Caverer(WorldGenState state, UnifiedRandom random, int x, int y)
    {
        switch (random.Next(2))
        {
            case 0:
            {
                int segments = random.Next(7, 9);
                double dx = random.Next(100) * 0.01;
                double dy = 1.0 - dx;
                if (random.Next(2) == 0)
                {
                    dx = -dx;
                }

                if (random.Next(2) == 0)
                {
                    dy = -dy;
                }

                double currentX = x;
                double currentY = y;
                for (int i = 0; i < segments; i++)
                {
                    (currentX, currentY) = DigTunnel(state, random, currentX, currentY, dx, dy, random.Next(6, 20), random.Next(4, 9));
                    dx += random.Next(-20, 21) * 0.1;
                    dy += random.Next(-20, 21) * 0.1;
                    dx = Math.Clamp(dx, -1.5, 1.5);
                    dy = Math.Clamp(dy, -1.5, 1.5);

                    double branchDx = random.Next(100) * 0.01;
                    double branchDy = 1.0 - branchDx;
                    if (random.Next(2) == 0)
                    {
                        branchDx = -branchDx;
                    }

                    if (random.Next(2) == 0)
                    {
                        branchDy = -branchDy;
                    }

                    (double branchX, double branchY) = DigTunnel(state, random, currentX, currentY, branchDx, branchDy, random.Next(30, 50), random.Next(3, 6));
                    WorldGenTileRunner.Run(state, random, (int)branchX, (int)branchY, random.Next(10, 20), random.Next(5, 10), -1);
                }

                break;
            }

            case 1:
            {
                int segments = random.Next(15, 30);
                double dx = random.Next(100) * 0.01;
                double dy = 1.0 - dx;
                if (random.Next(2) == 0)
                {
                    dx = -dx;
                }

                if (random.Next(2) == 0)
                {
                    dy = -dy;
                }

                double currentX = x;
                double currentY = y;
                for (int i = 0; i < segments; i++)
                {
                    (currentX, currentY) = DigTunnel(state, random, currentX, currentY, dx, dy, random.Next(5, 15), random.Next(2, 6));
                    dx += random.Next(-20, 21) * 0.1;
                    dy += random.Next(-20, 21) * 0.1;
                    dx = Math.Clamp(dx, -1.5, 1.5);
                    dy = Math.Clamp(dy, -1.5, 1.5);
                }

                break;
            }
        }
    }

    private static (double X, double Y) DigTunnel(
        WorldGenState state,
        UnifiedRandom random,
        double x,
        double y,
        double directionX,
        double directionY,
        int steps,
        int size)
    {
        double driftX = 0.0;
        double driftY = 0.0;
        double radius = size;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        x = Math.Clamp(x, radius + 1.0, width - radius - 1.0);
        y = Math.Clamp(y, radius + 1.0, height - radius - 1.0);

        for (int i = 0; i < steps; i++)
        {
            for (int tileX = (int)(x - radius); tileX <= x + radius; tileX++)
            {
                for (int tileY = (int)(y - radius); tileY <= y + radius; tileY++)
                {
                    if (tileX >= 0 && tileX < width && tileY >= 0 && tileY < height &&
                        Math.Abs(tileX - x) + Math.Abs(tileY - y) < radius * (1.0 + random.Next(-10, 11) * 0.005))
                    {
                        state.Tiles[tileX, tileY].Active = false;
                    }
                }
            }

            radius += random.Next(-50, 51) * 0.03;
            radius = Math.Clamp(radius, size * 0.6, size * 2);
            driftX += random.Next(-20, 21) * 0.01;
            driftY += random.Next(-20, 21) * 0.01;
            driftX = Math.Clamp(driftX, -1.0, 1.0);
            driftY = Math.Clamp(driftY, -1.0, 1.0);
            x += (directionX + driftX) * 0.6;
            y += (directionY + driftY) * 0.6;
        }

        return (x, y);
    }

    private static WorldGenState RequireTargetState(WorldGenContext context, string passName)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException(passName + " replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        return state;
    }

}
