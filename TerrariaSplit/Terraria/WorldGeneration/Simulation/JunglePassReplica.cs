namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal static class JunglePassReplica
{
    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Jungle replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        UnifiedRandom random = context.Random;
        double worldScale = state.Options.Dimensions.Width / 4200.0 * 1.5;
        int x = state.JungleOriginX;
        int y = (int)((state.Options.Dimensions.Height + state.MainRockLayer) / 2.0);
        int averageX = 0;
        int averageY = 0;

        ApplyRandomMovement(state, random, worldScale, ref x, ref y, 100, 100);
        averageX += x;
        averageY += y;
        PlaceFirstPassMud(state, random, worldScale, x, y, 3);
        PlaceGemsAt(state, random, worldScale, x, y, 63, 2);
        progress.Set(0.15);

        ApplyRandomMovement(state, random, worldScale, ref x, ref y, 250, 150);
        averageX += x;
        averageY += y;
        PlaceFirstPassMud(state, random, worldScale, x, y, 0);
        PlaceGemsAt(state, random, worldScale, x, y, 65, 2);
        progress.Set(0.3);

        int oldX = x;
        int oldY = y;
        ApplyRandomMovement(state, random, worldScale, ref x, ref y, 400, 150);
        averageX += x;
        averageY += y;
        PlaceFirstPassMud(state, random, worldScale, x, y, -3);
        PlaceGemsAt(state, random, worldScale, x, y, 67, 2);
        progress.Set(0.45);

        x = averageX / 3;
        y = averageY / 3;
        int mainMudStrength = random.Next((int)(400.0 * worldScale), (int)(600.0 * worldScale));
        int padding = (int)(25.0 * worldScale);
        x = Math.Clamp(
            x,
            state.LeftBeachEnd + mainMudStrength / 2 + padding,
            state.RightBeachStart - mainMudStrength / 2 - padding);

        state.MudWall = true;
        WorldGenTileRunner.Run(
            state,
            random,
            x,
            y,
            mainMudStrength,
            10000,
            TileIds.Mud,
            addTile: false,
            speedX: 0.0,
            speedY: -20.0,
            noYChange: true);
        GenerateTunnelToSurface(state, random, x, y);
        state.MudWall = false;

        progress.Set(0.6);
        // The pyramid simulator only needs the main jungle mud body and surface
        // tunnel because they feed the later Mud Caves To Grass and Crimson
        // surface-range decisions. Deep jungle finishing details do not affect
        // the shallow pyramid candidate scan.
        progress.Set(1.0);
    }

    public static void MarkCandidatesInSkippedJungleMudUncertaintyBand(WorldGenState state)
    {
        int width = state.Options.Dimensions.Width;
        int inwardReach = (int)Math.Ceiling(800.0 * width / 4200.0);
        IReadOnlyList<PyramidCandidate> candidates = state.PyramidCandidates;
        for (int i = 0; i < candidates.Count; i++)
        {
            PyramidCandidate candidate = candidates[i];
            if (!WorldInterestArea.IsInTargetPyramidXRange(state.Options.Dimensions, candidate.X) ||
                !WorldInterestArea.IsPyramidCandidateInBuildableBand(state, candidate.X) ||
                IsInUndergroundDesert(state, candidate.X) ||
                !IsOnJungleInwardSide(state, candidate.X, inwardReach) ||
                !state.TryGetPyramidCandidateScanTile(i, out _, out ushort tileType) ||
                tileType != TileIds.Sand)
            {
                continue;
            }

            state.AddPyramidCandidateRisk(i, PyramidCandidateRisk.JungleMudCoverageUncertain);
        }
    }

    private static bool IsOnJungleInwardSide(WorldGenState state, int x, int inwardReach)
    {
        int width = state.Options.Dimensions.Width;
        if (state.JungleOriginX < width / 2)
        {
            return x >= state.JungleOriginX && x <= state.JungleOriginX + inwardReach;
        }

        return x <= state.JungleOriginX && x >= state.JungleOriginX - inwardReach;
    }

    private static bool IsInUndergroundDesert(WorldGenState state, int x)
    {
        WorldRect desert = state.UndergroundDesertLocation;
        return desert.Width > 0 &&
            x >= desert.Left &&
            x < desert.Right;
    }

    private static void PlaceGemsAt(WorldGenState state, UnifiedRandom random, double worldScale, int x, int y, int baseGem, int gemVariants)
    {
        for (int i = 0; i < 6.0 * worldScale; i++)
        {
            WorldGenTileRunner.Run(
                state,
                random,
                x + random.Next(-(int)(125.0 * worldScale), (int)(125.0 * worldScale)),
                y + random.Next(-(int)(125.0 * worldScale), (int)(125.0 * worldScale)),
                random.Next(3, 7),
                random.Next(3, 8),
                random.Next(baseGem, baseGem + gemVariants));
        }
    }

    private static void PlaceFirstPassMud(WorldGenState state, UnifiedRandom random, double worldScale, int x, int y, int xSpeedScale)
    {
        state.MudWall = true;
        WorldGenTileRunner.Run(
            state,
            random,
            x,
            y,
            random.Next((int)(250.0 * worldScale), (int)(500.0 * worldScale)),
            random.Next(50, 150),
            TileIds.Mud,
            addTile: false,
            speedX: state.DungeonSide * xSpeedScale);
        state.MudWall = false;
    }

    private static void ApplyRandomMovement(
        WorldGenState state,
        UnifiedRandom random,
        double worldScale,
        ref int x,
        ref int y,
        int xRange,
        int yRange)
    {
        x += random.Next((int)(-xRange * worldScale), 1 + (int)(xRange * worldScale));
        y += random.Next((int)(-yRange * worldScale), 1 + (int)(yRange * worldScale));
        y = Math.Clamp(y, (int)state.MainRockLayer, state.Options.Dimensions.Height);
    }

    private static void GenerateTunnelToSurface(WorldGenState state, UnifiedRandom random, int startX, int startY)
    {
        DenseTileGrid tiles = state.Tiles;
        int worldWidth = state.Options.Dimensions.Width;
        int worldHeight = state.Options.Dimensions.Height;
        double strength = random.Next(5, 11);
        double x = startX;
        double y = startY;
        double velocityX = random.Next(-10, 11) * 0.1;
        double velocityY = random.Next(10, 20) * 0.1;
        int branchCounter = 0;
        bool digging = true;
        int guard = 0;

        while (digging && guard++ < 5000)
        {
            if (y < state.MainWorldSurface)
            {
                int tileX = Math.Clamp((int)x, 10, worldWidth - 10);
                int tileY = Math.Clamp((int)y, 10, worldHeight - 10);
                if (tileY < 5)
                {
                    tileY = 5;
                }

                if (IsOpenAirColumn(state, tileX, tileY))
                {
                    digging = false;
                }
            }

            state.JungleX = (int)x;
            strength += random.Next(-20, 21) * 0.1;
            strength = Math.Clamp(strength, 5.0, 10.0);

            int left = Math.Clamp((int)(x - strength * 0.5), 10, worldWidth - 10);
            int right = Math.Clamp((int)(x + strength * 0.5), 10, worldWidth - 10);
            int top = Math.Clamp((int)(y - strength * 0.5), 10, worldHeight - 10);
            int bottom = Math.Clamp((int)(y + strength * 0.5), 10, worldHeight - 10);
            for (int tileX = left; tileX < right; tileX++)
            {
                for (int tileY = top; tileY < bottom; tileY++)
                {
                    if (Math.Abs(tileX - x) + Math.Abs(tileY - y) < strength * 0.5 * (1.0 + random.Next(-10, 11) * 0.015))
                    {
                        tiles.GetUnchecked(tileX, tileY).Active = false;
                    }
                }
            }

            branchCounter++;
            if (branchCounter > 10 && random.Next(50) < branchCounter)
            {
                branchCounter = 0;
                int speedX = random.Next(2) == 0 ? 2 : -2;
                WorldGenTileRunner.Run(
                    state,
                    random,
                    (int)x,
                    (int)y,
                    random.Next(3, 20),
                    random.Next(10, 100),
                    -1,
                    addTile: false,
                    speedX: speedX);
            }

            x += velocityX;
            y += velocityY;
            velocityY += random.Next(-10, 11) * 0.01;
            velocityY = Math.Clamp(velocityY, -2.0, 0.0);
            velocityX += random.Next(-10, 11) * 0.1;
            if (x < startX - 200)
            {
                velocityX += random.Next(5, 21) * 0.1;
            }

            if (x > startX + 200)
            {
                velocityX -= random.Next(5, 21) * 0.1;
            }

            velocityX = Math.Clamp(velocityX, -1.5, 1.5);
        }
    }

    private static bool IsOpenAirColumn(WorldGenState state, int x, int y)
    {
        for (int offset = 0; offset <= 5; offset++)
        {
            ref TileData tile = ref state.Tiles.GetUnchecked(x, y - offset);
            if (tile.Wall != 0 || tile.Active)
            {
                return false;
            }
        }

        return true;
    }

    private static void GenerateHolesInMudWalls(WorldGenState state, UnifiedRandom random)
    {
        int width = state.Options.Dimensions.Width;
        int underworld = state.UnderworldLayer;
        for (int i = 0; i < width / 4; i++)
        {
            int x = random.Next(20, width - 20);
            int y = random.Next((int)state.WorldSurface + 10, underworld);
            int attempts = 0;
            while (state.Tiles.GetUnchecked(x, y).Wall != 64 && state.Tiles.GetUnchecked(x, y).Wall != 15)
            {
                x = random.Next(20, width - 20);
                y = random.Next((int)state.WorldSurface + 10, underworld);
                if (++attempts > 10000)
                {
                    break;
                }
            }

            MudWallRunner(state, random, x, y);
        }
    }

    private static void GenerateFinishingTouches(
        WorldGenState state,
        UnifiedRandom random,
        GenerationProgress progress,
        double worldScale,
        int oldX,
        int oldY)
    {
        int x = oldX;
        int y = oldY;
        for (int i = 0; i <= 20.0 * worldScale; i++)
        {
            progress.Set((60.0 + i / worldScale) * 0.01);
            x += random.Next((int)(-5.0 * worldScale), (int)(6.0 * worldScale));
            y += random.Next((int)(-5.0 * worldScale), (int)(6.0 * worldScale));
            WorldGenTileRunner.Run(state, random, x, y, random.Next(40, 100), random.Next(300, 500), TileIds.Mud);
        }

        for (int j = 0; j <= 10.0 * worldScale; j++)
        {
            progress.Set((80.0 + j / worldScale * 2.0) * 0.01);
            PickMudPointNear(state, random, worldScale, oldX, oldY, out x, out y);
            for (int k = 0; k < 8.0 * worldScale; k++)
            {
                x += random.Next(-30, 31);
                y += random.Next(-30, 31);
                int type = random.Next(7) == 0 ? -2 : -1;
                WorldGenTileRunner.Run(state, random, x, y, random.Next(10, 20), random.Next(30, 70), type);
            }
        }

        for (int l = 0; l <= 300.0 * worldScale; l++)
        {
            PickMudPointNear(state, random, worldScale, oldX, oldY, out x, out y);
            WorldGenTileRunner.Run(state, random, x, y, random.Next(4, 10), random.Next(5, 30), TileIds.Stone);
            if (random.Next(4) == 0)
            {
                int type = random.Next(63, 69);
                WorldGenTileRunner.Run(
                    state,
                    random,
                    x + random.Next(-1, 2),
                    y + random.Next(-1, 2),
                    random.Next(3, 7),
                    random.Next(4, 8),
                    type);
            }
        }
    }

    private static void PickMudPointNear(
        WorldGenState state,
        UnifiedRandom random,
        double worldScale,
        int originX,
        int originY,
        out int x,
        out int y)
    {
        int attempts = 0;
        do
        {
            x = originX + random.Next((int)(-600.0 * worldScale), (int)(600.0 * worldScale));
            y = originY + random.Next((int)(-200.0 * worldScale), (int)(200.0 * worldScale));
            attempts++;
        }
        while (attempts <= 10000 && !IsMudPoint(state, x, y));
    }

    private static bool IsMudPoint(WorldGenState state, int x, int y)
    {
        return x >= 1 &&
            x < state.Options.Dimensions.Width - 1 &&
            y >= 1 &&
            y < state.Options.Dimensions.Height - 1 &&
            state.Tiles.GetUnchecked(x, y).Type == TileIds.Mud;
    }

    private static void MudWallRunner(WorldGenState state, UnifiedRandom random, int i, int j)
    {
        DenseTileGrid tiles = state.Tiles;
        int worldWidth = state.Options.Dimensions.Width;
        int worldHeight = state.Options.Dimensions.Height;
        double strength = random.Next(8, 21);
        double steps = random.Next(8, 33);
        double stepsRemaining = steps;
        double x = i;
        double y = j;
        double velocityX = random.Next(-10, 11) * 0.1;
        double velocityY = random.Next(-10, 11) * 0.1;
        while (strength > 0.0 && stepsRemaining > 0.0)
        {
            double currentStrength = strength * (stepsRemaining / steps);
            stepsRemaining -= 1.0;
            int left = Math.Clamp((int)(x - currentStrength * 0.5), 0, worldWidth);
            int right = Math.Clamp((int)(x + currentStrength * 0.5), 0, worldWidth);
            int top = Math.Clamp((int)(y - currentStrength * 0.5), 0, worldHeight);
            int bottom = Math.Clamp((int)(y + currentStrength * 0.5), 0, worldHeight);

            for (int tileX = left; tileX < right; tileX++)
            {
                for (int tileY = top; tileY < bottom; tileY++)
                {
                    if (Math.Abs(tileX - x) + Math.Abs(tileY - y) < strength * 0.5 * (1.0 + random.Next(-10, 11) * 0.015) &&
                        tileY > state.MainWorldSurface)
                    {
                        tiles.GetUnchecked(tileX, tileY).Wall = 0;
                    }
                }
            }

            x += velocityX;
            y += velocityY;
            velocityX += random.Next(-10, 11) * 0.05;
            velocityX = Math.Clamp(velocityX, -1.0, 1.0);
            velocityY += random.Next(-10, 11) * 0.05;
            velocityY = Math.Clamp(velocityY, -1.0, 1.0);
        }
    }
}
