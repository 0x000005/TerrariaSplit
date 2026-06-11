namespace WorldGenSim.Simulation;

internal static class CrimsonPassReplica
{
    private const int CrimstoneWall = 83;
    private const int JungleGrass = 60;
    private const int CrimsonJungleGrass = TileIds.CrimsonJungleGrass;
    private const int CrimsonAltar = 26;
    private const int ShadowOrbTile = 31;

    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Crimson replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        if (!state.Crimson)
        {
            throw new InvalidOperationException("Stage 1 only supports crimson worlds.");
        }

        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        double surfaceLow = state.WorldSurfaceLow;
        double worldSurface = state.MainWorldSurface;

        ScanSurfaceBiomeExtents(
            state,
            worldSurface,
            out int jungleLeft,
            out int jungleRight,
            out int snowLeft,
            out int snowRight);

        const int beachAvoidance = 500;
        const int dungeonAvoidance = 100;
        double infectionCount = width * 0.00045d;
        var heartPositions = new List<(int X, int Y)>();

        for (int biomeIndex = 0; biomeIndex < infectionCount; biomeIndex++)
        {
            progress.Set(biomeIndex / infectionCount);

            int localSnowLeft = snowLeft;
            int localSnowRight = snowRight;
            int localJungleLeft = jungleLeft;
            int localJungleRight = jungleRight;

            RollCrimsonRange(
                state,
                random,
                beachAvoidance,
                dungeonAvoidance,
                ref localJungleLeft,
                ref localJungleRight,
                ref localSnowLeft,
                ref localSnowRight,
                out int center,
                out int left,
                out int right);

            CrimStart(state, random, center, (int)surfaceLow - 10, heartPositions);
            ConvertSurfaceJungleGrass(state, random, left, right, surfaceLow, worldSurface);
            ConvertSurfaceCrimsonTiles(state, random, left, right, surfaceLow, worldSurface);
            PlaceCrimsonAltarsNearSurface(state, random, left, right, worldSurface);
        }

        PlaceCrimsonHearts(state, random, heartPositions);
    }

    private static void ScanSurfaceBiomeExtents(
        WorldGenState state,
        double worldSurface,
        out int jungleLeft,
        out int jungleRight,
        out int snowLeft,
        out int snowRight)
    {
        int width = state.Options.Dimensions.Width;
        jungleLeft = width;
        jungleRight = 0;
        snowLeft = width;
        snowRight = 0;

        int scanBottom = Math.Clamp((int)worldSurface, 0, state.Options.Dimensions.Height);
        (int jungleScanLeft, int jungleScanRight) = JungleSurfaceScanRange(state, width);
        for (int x = jungleScanLeft; x < jungleScanRight; x++)
        {
            for (int y = 0; y < scanBottom; y++)
            {
                TileData tile = state.Tiles[x, y];
                if (tile.Active && tile.Type == JungleGrass)
                {
                    jungleLeft = Math.Min(jungleLeft, x);
                    jungleRight = Math.Max(jungleRight, x);
                }
            }
        }

        (int snowScanLeft, int snowScanRight) = SnowSurfaceScanRange(state, width, scanBottom);
        for (int x = snowScanLeft; x < snowScanRight; x++)
        {
            for (int y = 0; y < scanBottom; y++)
            {
                TileData tile = state.Tiles[x, y];
                if (tile.Active && tile.Type is TileIds.SnowBlock or TileIds.IceBlock)
                {
                    snowLeft = Math.Min(snowLeft, x);
                    snowRight = Math.Max(snowRight, x);
                }
            }
        }

        const int padding = 10;
        jungleLeft -= padding;
        jungleRight += padding;
        snowLeft -= padding;
        snowRight += padding;
    }

    private static (int Left, int Right) JungleSurfaceScanRange(WorldGenState state, int width)
    {
        if (state.JungleMinX < 0 || state.JungleMaxX <= state.JungleMinX)
        {
            return (0, width);
        }

        return (Math.Max(0, state.JungleMinX - 20), Math.Min(width, state.JungleMaxX + 20));
    }

    private static (int Left, int Right) SnowSurfaceScanRange(WorldGenState state, int width, int scanBottom)
    {
        if (state.SnowMinX.Length == 0 || state.SnowMaxX.Length == 0)
        {
            return (0, width);
        }

        int left = width;
        int right = 0;
        int bottom = Math.Min(scanBottom, Math.Min(state.SnowMinX.Length, state.SnowMaxX.Length));
        for (int y = 0; y < bottom; y++)
        {
            int rowLeft = state.SnowMinX[y];
            int rowRight = state.SnowMaxX[y];
            if (rowRight <= rowLeft)
            {
                continue;
            }

            left = Math.Min(left, rowLeft);
            right = Math.Max(right, rowRight);
        }

        if (right <= left)
        {
            return (0, width);
        }

        return (Math.Max(0, left - 20), Math.Min(width, right + 20));
    }

    private static void RollCrimsonRange(
        WorldGenState state,
        UnifiedRandom random,
        int beachAvoidance,
        int dungeonAvoidance,
        ref int jungleLeft,
        ref int jungleRight,
        ref int snowLeft,
        ref int snowRight,
        out int center,
        out int left,
        out int right)
    {
        int width = state.Options.Dimensions.Width;
        bool accepted = false;
        center = 0;
        left = 0;
        right = 0;

        while (!accepted)
        {
            accepted = true;
            int middle = width / 2;
            const int middleAvoidance = 200;

            center = random.Next(beachAvoidance, width - beachAvoidance);
            left = center - random.Next(200) - 100;
            right = center + random.Next(200) + 100;

            if (left < state.EvilBiomeBeachAvoidance)
            {
                left = state.EvilBiomeBeachAvoidance;
            }

            if (right > width - state.EvilBiomeBeachAvoidance)
            {
                right = width - state.EvilBiomeBeachAvoidance;
            }

            if (center < left + state.EvilBiomeAvoidanceMidFixer)
            {
                center = left + state.EvilBiomeAvoidanceMidFixer;
            }

            if (center > right - state.EvilBiomeAvoidanceMidFixer)
            {
                center = right - state.EvilBiomeAvoidanceMidFixer;
            }

            if (state.DungeonSide <= -1 && left < 400)
            {
                left = 400;
            }
            else if (state.DungeonSide >= 1 && left > width - 400)
            {
                left = width - 400;
            }

            if (left < state.DungeonLocation + dungeonAvoidance &&
                right > state.DungeonLocation - dungeonAvoidance)
            {
                accepted = false;
            }

            if (center > middle - middleAvoidance && center < middle + middleAvoidance)
            {
                accepted = false;
            }

            if (left > middle - middleAvoidance && left < middle + middleAvoidance)
            {
                accepted = false;
            }

            if (right > middle - middleAvoidance && right < middle + middleAvoidance)
            {
                accepted = false;
            }

            WorldRect desert = state.UndergroundDesertLocation;
            if (center > desert.Left && center < desert.Right)
            {
                accepted = false;
            }

            if (left > desert.Left && left < desert.Right)
            {
                accepted = false;
            }

            if (right > desert.Left && right < desert.Right)
            {
                accepted = false;
            }

            if (left < snowRight && right > snowLeft)
            {
                snowLeft++;
                snowRight--;
                accepted = false;
            }

            if (left < jungleRight && right > jungleLeft)
            {
                jungleLeft++;
                jungleRight--;
                accepted = false;
            }
        }
    }

    private static void ConvertSurfaceJungleGrass(
        WorldGenState state,
        UnifiedRandom random,
        int left,
        int right,
        double surfaceLow,
        double worldSurface)
    {
        for (int x = left; x < right; x++)
        {
            for (int y = (int)surfaceLow; y < worldSurface - 1.0; y++)
            {
                if (!InWorld(state, x, y))
                {
                    continue;
                }

                if (!state.Tiles[x, y].Active)
                {
                    continue;
                }

                int bottom = y + random.Next(10, 14);
                for (int tileY = y; tileY < bottom; tileY++)
                {
                    if (!InWorld(state, x, tileY))
                    {
                        continue;
                    }

                    if (state.Tiles[x, tileY].Active &&
                        state.Tiles[x, tileY].Type == JungleGrass &&
                        x >= left + random.Next(5) &&
                        x < right - random.Next(5))
                    {
                        state.Tiles[x, tileY].Type = CrimsonJungleGrass;
                    }
                }

                break;
            }
        }
    }

    private static void ConvertSurfaceCrimsonTiles(
        WorldGenState state,
        UnifiedRandom random,
        int left,
        int right,
        double surfaceLow,
        double worldSurface)
    {
        double lowerLimit = worldSurface + 40.0;
        for (int x = left; x < right; x++)
        {
            lowerLimit += random.Next(-2, 3);
            if (lowerLimit < worldSurface + 30.0)
            {
                lowerLimit = worldSurface + 30.0;
            }

            if (lowerLimit > worldSurface + 50.0)
            {
                lowerLimit = worldSurface + 50.0;
            }

            bool foundActive = false;
            for (int y = (int)surfaceLow; y < lowerLimit; y++)
            {
                bool insideRangeOrRandomEdge = (x > left + 1 && x < right - 2) || random.Next(2) != 0;
                bool insideDepthOrRandomEdge = (y > surfaceLow + 1.0 && y < lowerLimit - 2.0) || random.Next(2) != 0;
                if (!insideRangeOrRandomEdge || !insideDepthOrRandomEdge || !InWorld(state, x, y))
                {
                    continue;
                }

                ref TileData tile = ref state.Tiles[x, y];
                if (!tile.Active)
                {
                    continue;
                }

                if (tile.Type == TileIds.Sand)
                {
                    bool shouldConvertSand = x >= left + random.Next(5) &&
                        x <= right - random.Next(5);
                    if (shouldConvertSand && !IsPyramidCandidateScanColumn(state, x, y))
                    {
                        tile.Type = TileIds.Crimsand;
                    }
                }

                if (y < worldSurface - 1.0 && !foundActive)
                {
                    if (tile.Type == TileIds.Dirt)
                    {
                        SpreadGrass(state, x, y, TileIds.Dirt, TileIds.CrimsonGrass);
                    }
                    else if (tile.Type == TileIds.Mud)
                    {
                        SpreadGrass(state, x, y, TileIds.Mud, CrimsonJungleGrass);
                    }
                }

                foundActive = true;
                if (tile.Wall == 216)
                {
                    tile.Wall = 218;
                }
                else if (tile.Wall == 187)
                {
                    tile.Wall = 221;
                }

                if (tile.Type == TileIds.Stone)
                {
                    if (x >= left + random.Next(5) && x <= right - random.Next(5))
                    {
                        tile.Type = TileIds.Crimstone;
                    }
                }
                else if (tile.Type == TileIds.Grass)
                {
                    tile.Type = TileIds.CrimsonGrass;
                }
                else if (tile.Type == JungleGrass)
                {
                    tile.Type = CrimsonJungleGrass;
                }
                else if (tile.Type == TileIds.IceBlock)
                {
                    tile.Type = TileIds.FleshIce;
                }
                else if (tile.Type == TileIds.Sandstone)
                {
                    tile.Type = TileIds.CrimsonSandstone;
                }
                else if (tile.Type == TileIds.HardenedSand)
                {
                    tile.Type = TileIds.CrimsonHardenedSand;
                }
            }
        }
    }

    private static void PlaceCrimsonAltarsNearSurface(
        WorldGenState state,
        UnifiedRandom random,
        int left,
        int right,
        double worldSurface)
    {
        int count = random.Next(10, 15);
        for (int i = 0; i < count; i++)
        {
            int attempts = 0;
            bool placed = false;
            int expansion = 0;
            while (!placed)
            {
                attempts++;
                int x = random.Next(left - expansion, right + expansion);
                int y = random.Next((int)(worldSurface - expansion / 2.0), (int)(worldSurface + 100.0 + expansion));
                while (OceanDepths(state, x, y))
                {
                    x = random.Next(left - expansion, right + expansion);
                    y = random.Next((int)(worldSurface - expansion / 2.0), (int)(worldSurface + 100.0 + expansion));
                }

                if (attempts > 100)
                {
                    expansion++;
                    attempts = 0;
                }

                x = Math.Clamp(x, 1, state.Options.Dimensions.Width - 2);
                y = Math.Clamp(y, 1, state.Options.Dimensions.Height - 3);

                if (!state.Tiles[x, y].Active)
                {
                    while (InWorld(state, x, y) && !state.Tiles[x, y].Active)
                    {
                        y++;
                    }

                    y--;
                }
                else
                {
                    while (InWorld(state, x, y) && state.Tiles[x, y].Active && y > worldSurface)
                    {
                        y--;
                    }
                }

                y = Math.Clamp(y, 1, state.Options.Dimensions.Height - 3);
                if ((expansion > 10 || state.Tiles[x, y + 1].IsActiveType(TileIds.Crimstone)) &&
                    !IsTileNearby(state, x, y, CrimsonAltar, radius: 3))
                {
                    PlaceCrimsonAltar(state, x, y);
                    if (state.Tiles[x, y].Type == CrimsonAltar)
                    {
                        placed = true;
                    }
                }

                if (expansion > 100)
                {
                    placed = true;
                }
            }
        }
    }

    private static void CrimStart(
        WorldGenState state,
        UnifiedRandom random,
        int i,
        int j,
        List<(int X, int Y)> heartPositions)
    {
        double worldSurface = state.MainWorldSurface;
        int crimDir = 1;
        int k = j;
        if (k > worldSurface)
        {
            k = (int)worldSurface;
        }

        while (InWorld(state, i, k) && !SolidTile(state, i, k))
        {
            k++;
        }

        int surfaceY = k;
        var position = new Vec2(i, k);
        var velocity = new Vec2(random.Next(-20, 21) * 0.1, random.Next(20, 201) * 0.01);
        if (velocity.X < 0.0)
        {
            crimDir = -1;
        }

        double radius = random.Next(15, 26);
        bool running = true;
        int xBias = 0;
        while (running)
        {
            radius += random.Next(-50, 51) * 0.01;
            radius = Math.Clamp(radius, 15.0, 25.0);
            for (int x = (int)(position.X - radius / 2.0); x < position.X + radius / 2.0; x++)
            {
                for (int y = (int)(position.Y - radius / 2.0); y < position.Y + radius / 2.0; y++)
                {
                    if (!CanEvilReplace(state, x, y))
                    {
                        continue;
                    }

                    ref TileData tile = ref state.Tiles[x, y];
                    double manhattan = Math.Abs(x - position.X) + Math.Abs(y - position.Y);
                    if (y > surfaceY)
                    {
                        if (manhattan < radius * 0.3)
                        {
                            tile.Active = false;
                            tile.Wall = CrimstoneWall;
                        }
                        else if (manhattan < radius * 0.8 && tile.Wall != CrimstoneWall)
                        {
                            tile.SetType(TileIds.Crimstone);
                            if (manhattan < radius * 0.6)
                            {
                                tile.Wall = CrimstoneWall;
                            }
                        }
                    }
                    else if (manhattan < radius * 0.3 && tile.Active)
                    {
                        tile.Active = false;
                        tile.Wall = CrimstoneWall;
                    }
                }
            }

            if (position.X > i + 50)
            {
                xBias = -100;
            }

            if (position.X < i - 50)
            {
                xBias = 100;
            }

            if (xBias < 0)
            {
                velocity.X -= random.Next(20, 51) * 0.01;
            }
            else if (xBias > 0)
            {
                velocity.X += random.Next(20, 51) * 0.01;
            }
            else
            {
                velocity.X += random.Next(-50, 51) * 0.01;
            }

            velocity.Y += random.Next(-50, 51) * 0.01;
            velocity.Y = Math.Clamp(velocity.Y, 0.25, 2.0);
            velocity.X = Math.Clamp(velocity.X, -2.0, 2.0);
            position += velocity;
            if (position.Y > worldSurface + 100.0)
            {
                running = false;
            }
        }

        radius = random.Next(40, 55);
        for (int n = 0; n < 50; n++)
        {
            int centerX = (int)position.X + random.Next(-20, 21);
            int centerY = (int)position.Y + random.Next(-20, 21);
            for (int x = (int)(centerX - radius / 2.0); x < centerX + radius / 2.0; x++)
            {
                for (int y = (int)(centerY - radius / 2.0); y < centerY + radius / 2.0; y++)
                {
                    if (!CanEvilReplace(state, x, y))
                    {
                        continue;
                    }

                    double dx = Math.Abs(x - centerX);
                    double dy = Math.Abs(y - centerY);
                    double scaledX = dx * (1.0 + random.Next(-20, 21) * 0.01);
                    dy *= 1.0 + random.Next(-20, 21) * 0.01;
                    double distanceSquared = scaledX * scaledX + dy * dy;
                    ref TileData tile = ref state.Tiles[x, y];
                    double clearRadius = radius * 0.25;
                    double stoneRadius = radius * 0.4;
                    if (distanceSquared < clearRadius * clearRadius)
                    {
                        tile.Active = false;
                        tile.Wall = CrimstoneWall;
                    }
                    else if (distanceSquared < stoneRadius * stoneRadius && tile.Wall != CrimstoneWall)
                    {
                        tile.SetType(TileIds.Crimstone);
                        double wallRadius = radius * 0.35;
                        if (distanceSquared < wallRadius * wallRadius)
                        {
                            tile.Wall = CrimstoneWall;
                        }
                    }
                }
            }
        }

        int branchCount = random.Next(5, 9);
        var branchVelocities = new Vec2[branchCount];
        for (int branch = 0; branch < branchCount; branch++)
        {
            int branchX = (int)position.X;
            int branchY = (int)position.Y;
            int attempts = 0;
            bool retry = true;
            var branchVelocity = new Vec2(random.Next(-20, 21) * 0.15, random.Next(0, 21) * 0.15);
            while (retry)
            {
                branchVelocity = new Vec2(random.Next(-20, 21) * 0.15, random.Next(0, 21) * 0.15);
                while (Math.Abs(branchVelocity.X) + Math.Abs(branchVelocity.Y) < 1.5)
                {
                    branchVelocity = new Vec2(random.Next(-20, 21) * 0.15, random.Next(0, 21) * 0.15);
                }

                retry = false;
                for (int previous = 0; previous < branch; previous++)
                {
                    if (velocity.X > branchVelocities[previous].X - 0.75 &&
                        velocity.X < branchVelocities[previous].X + 0.75 &&
                        velocity.Y > branchVelocities[previous].Y - 0.75 &&
                        velocity.Y < branchVelocities[previous].Y + 0.75)
                    {
                        retry = true;
                        attempts++;
                        break;
                    }
                }

                if (attempts > 10000)
                {
                    break;
                }
            }

            branchVelocities[branch] = branchVelocity;
            CrimVein(state, random, new Vec2(branchX, branchY), branchVelocity, heartPositions);
        }

        int left = state.Options.Dimensions.Width;
        int right = 0;
        position = new Vec2(i, surfaceY);
        radius = random.Next(25, 35);
        double lift = random.Next(0, 6);
        for (int n = 0; n < 50; n++)
        {
            if (lift > 0.0)
            {
                double delta = random.Next(10, 30) * 0.01;
                lift -= delta;
                position.Y -= delta;
            }

            int centerX = (int)position.X + random.Next(-2, 3);
            int centerY = (int)position.Y + random.Next(-2, 3);
            for (int x = (int)(centerX - radius / 2.0); x < centerX + radius / 2.0; x++)
            {
                for (int y = (int)(centerY - radius / 2.0); y < centerY + radius / 2.0; y++)
                {
                    if (!CanEvilReplace(state, x, y))
                    {
                        continue;
                    }

                    double dx = Math.Abs(x - centerX);
                    double dy = Math.Abs(y - centerY);
                    double scaledX = dx * (1.0 + random.Next(-20, 21) * 0.005);
                    dy *= 1.0 + random.Next(-20, 21) * 0.005;
                    double distanceSquared = scaledX * scaledX + dy * dy;
                    ref TileData tile = ref state.Tiles[x, y];
                    double clearRadius = radius * 0.2 * (random.Next(90, 111) * 0.01);
                    if (distanceSquared < clearRadius * clearRadius)
                    {
                        tile.Active = false;
                        tile.Wall = CrimstoneWall;
                    }
                    else if (distanceSquared < radius * radius * 0.45 * 0.45)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        if (tile.Wall != CrimstoneWall)
                        {
                            tile.SetType(TileIds.Crimstone);
                            double wallRadius = radius * 0.35;
                            if (distanceSquared < wallRadius * wallRadius)
                            {
                                tile.Wall = CrimstoneWall;
                            }
                        }
                    }
                }
            }
        }

        for (int x = left; x <= right; x++)
        {
            if (!InWorld(state, x, surfaceY))
            {
                continue;
            }

            int y = surfaceY;
            while (InWorld(state, x, y) &&
                ((state.Tiles[x, y].Active && state.Tiles[x, y].Type == TileIds.Crimstone) ||
                state.Tiles[x, y].Wall == CrimstoneWall))
            {
                y++;
            }

            int fill = random.Next(15, 20);
            while (InWorld(state, x, y) &&
                !state.Tiles[x, y].Active &&
                fill > 0 &&
                state.Tiles[x, y].Wall != CrimstoneWall)
            {
                if (CanEvilReplace(state, x, y))
                {
                    fill--;
                    state.Tiles[x, y].SetType(TileIds.Crimstone);
                    y++;
                }
            }
        }

        CrimEnt(state, random, position, crimDir);
    }

    private static void CrimVein(
        WorldGenState state,
        UnifiedRandom random,
        Vec2 position,
        Vec2 velocity,
        List<(int X, int Y)> heartPositions)
    {
        double radius = random.Next(15, 26);
        bool running = true;
        Vec2 baseVelocity = velocity;
        Vec2 start = position;
        int maxDistance = random.Next(100, 150);
        if (velocity.Y < 0.0)
        {
            maxDistance -= 25;
        }

        while (running)
        {
            radius += random.Next(-50, 51) * 0.02;
            radius = Math.Clamp(radius, 15.0, 25.0);
            for (int x = (int)(position.X - radius / 2.0); x < position.X + radius / 2.0; x++)
            {
                for (int y = (int)(position.Y - radius / 2.0); y < position.Y + radius / 2.0; y++)
                {
                    if (!CanEvilReplace(state, x, y))
                    {
                        continue;
                    }

                    double dx = Math.Abs(x - position.X);
                    double dy = Math.Abs(y - position.Y);
                    double distanceSquared = dx * dx + dy * dy;
                    ref TileData tile = ref state.Tiles[x, y];
                    double clearRadius = radius * 0.2;
                    double stoneRadius = radius * 0.5;
                    if (distanceSquared < clearRadius * clearRadius)
                    {
                        tile.Active = false;
                        tile.Wall = CrimstoneWall;
                    }
                    else if (distanceSquared < stoneRadius * stoneRadius && tile.Wall != CrimstoneWall)
                    {
                        tile.SetType(TileIds.Crimstone);
                        double wallRadius = radius * 0.4;
                        if (distanceSquared < wallRadius * wallRadius)
                        {
                            tile.Wall = CrimstoneWall;
                        }
                    }
                }
            }

            velocity.X += random.Next(-50, 51) * 0.05;
            velocity.Y += random.Next(-50, 51) * 0.05;
            velocity.Y = Math.Clamp(velocity.Y, baseVelocity.Y - 0.75, baseVelocity.Y + 0.75);
            velocity.X = Math.Clamp(velocity.X, baseVelocity.X - 0.75, baseVelocity.X + 0.75);
            position += velocity;
            if (Math.Abs(position.X - start.X) + Math.Abs(position.Y - start.Y) > maxDistance)
            {
                running = false;
            }
        }

        heartPositions.Add(((int)position.X, (int)position.Y));
    }

    private static void CrimEnt(WorldGenState state, UnifiedRandom random, Vec2 position, int crimDir)
    {
        double idleSteps = 0.0;
        double radius = random.Next(6, 11);
        bool running = true;
        var velocity = new Vec2(2.0, random.Next(-20, 0) * 0.01);
        velocity.X *= -crimDir;
        while (running)
        {
            idleSteps += 1.0;
            if (idleSteps >= 20.0)
            {
                running = false;
            }

            radius += random.Next(-10, 11) * 0.02;
            radius = Math.Clamp(radius, 6.0, 10.0);
            for (int x = (int)(position.X - radius / 2.0); x < position.X + radius / 2.0; x++)
            {
                for (int y = (int)(position.Y - radius / 2.0); y < position.Y + radius / 2.0; y++)
                {
                    if (!CanEvilReplace(state, x, y))
                    {
                        continue;
                    }

                    double dx = Math.Abs(x - position.X);
                    double dy = Math.Abs(y - position.Y);
                    double entRadius = radius * 0.5;
                    if (dx * dx + dy * dy < entRadius * entRadius &&
                        state.Tiles[x, y].Active &&
                        state.Tiles[x, y].Type == TileIds.Crimstone)
                    {
                        state.Tiles[x, y].Active = false;
                        running = true;
                        idleSteps = 0.0;
                    }
                }
            }

            position += velocity;
        }
    }

    private static void PlaceCrimsonHearts(
        WorldGenState state,
        UnifiedRandom random,
        IReadOnlyList<(int X, int Y)> heartPositions)
    {
        foreach ((int x, int y) in heartPositions)
        {
            int radius = random.Next(16, 21);
            for (int tileX = x - radius / 2; tileX < x + radius / 2; tileX++)
            {
                for (int tileY = y - radius / 2; tileY < y + radius / 2; tileY++)
                {
                    if (!InWorld(state, tileX, tileY))
                    {
                        continue;
                    }

                    double dx = Math.Abs(tileX - x);
                    double dy = Math.Abs(tileY - y);
                    double heartRadius = radius * 0.4;
                    if (dx * dx + dy * dy < heartRadius * heartRadius)
                    {
                        state.Tiles[tileX, tileY].SetType(TileIds.Crimstone);
                        state.Tiles[tileX, tileY].Wall = CrimstoneWall;
                    }
                }
            }
        }

        foreach ((int x, int y) in heartPositions)
        {
            int radius = random.Next(10, 14);
            for (int tileX = x - radius / 2; tileX < x + radius / 2; tileX++)
            {
                for (int tileY = y - radius / 2; tileY < y + radius / 2; tileY++)
                {
                    if (!InWorld(state, tileX, tileY))
                    {
                        continue;
                    }

                    double dx = Math.Abs(tileX - x);
                    double dy = Math.Abs(tileY - y);
                    double clearRadius = radius * 0.3;
                    if (dx * dx + dy * dy < clearRadius * clearRadius)
                    {
                        state.Tiles[tileX, tileY].Active = false;
                        state.Tiles[tileX, tileY].Wall = CrimstoneWall;
                    }
                }
            }
        }

        foreach ((int x, int y) in heartPositions)
        {
            AddShadowOrb(state, x, y);
        }
    }

    private static void AddShadowOrb(WorldGenState state, int x, int y)
    {
        if (x < 10 || x > state.Options.Dimensions.Width - 10 ||
            y < 10 || y > state.Options.Dimensions.Height - 10)
        {
            return;
        }

        for (int tileX = x - 1; tileX < x + 1; tileX++)
        {
            for (int tileY = y - 1; tileY < y + 1; tileY++)
            {
                if (state.Tiles[tileX, tileY].IsActiveType(ShadowOrbTile))
                {
                    return;
                }
            }
        }

        state.Tiles[x - 1, y - 1].SetType(ShadowOrbTile);
        state.Tiles[x, y - 1].SetType(ShadowOrbTile);
        state.Tiles[x - 1, y].SetType(ShadowOrbTile);
        state.Tiles[x, y].SetType(ShadowOrbTile);
    }

    private static void SpreadGrass(WorldGenState state, int startX, int startY, int dirt, int grass)
    {
        var stack = new Stack<(int X, int Y, int Depth)>();
        stack.Push((startX, startY, 0));
        while (stack.Count > 0)
        {
            (int x, int y, int depth) = stack.Pop();
            if (!InWorld(state, x, y, 10) ||
                !state.Tiles[x, y].Active ||
                state.Tiles[x, y].Type != dirt ||
                depth > 1000)
            {
                continue;
            }

            if (grass == TileIds.CrimsonGrass &&
                x > state.Options.Dimensions.Width * 0.45 &&
                x <= state.Options.Dimensions.Width * 0.55)
            {
                continue;
            }

            if (IsFullySurroundedBySolid(state, x, y) || !CanBeClearedDuringGeneration(state.Tiles[x, y].Type))
            {
                continue;
            }

            state.Tiles[x, y].Type = checked((ushort)grass);
            for (int tileX = x - 1; tileX < x + 2; tileX++)
            {
                for (int tileY = y - 1; tileY < y + 2; tileY++)
                {
                    if (InWorld(state, tileX, tileY) &&
                        state.Tiles[tileX, tileY].Active &&
                        state.Tiles[tileX, tileY].Type == dirt)
                    {
                        stack.Push((tileX, tileY, depth + 1));
                    }
                }
            }
        }
    }

    private static bool IsFullySurroundedBySolid(WorldGenState state, int x, int y)
    {
        for (int tileX = Math.Max(0, x - 1); tileX < Math.Min(state.Options.Dimensions.Width, x + 2); tileX++)
        {
            for (int tileY = Math.Max(0, y - 1); tileY < Math.Min(state.Options.Dimensions.Height, y + 2); tileY++)
            {
                TileData tile = state.Tiles[tileX, tileY];
                if (!tile.Active || !IsSolidTileType(tile.Type))
                {
                    return false;
                }

                if (tile.Liquid > 0 && tile.LiquidType == 1)
                {
                    return true;
                }
            }
        }

        return true;
    }

    private static bool OceanDepths(WorldGenState state, int x, int y)
    {
        _ = state;
        _ = x;
        _ = y;
        return false;
    }

    private static bool IsTileNearby(WorldGenState state, int x, int y, int type, int radius)
    {
        for (int tileX = x - radius; tileX <= x + radius; tileX++)
        {
            for (int tileY = y - radius; tileY <= y + radius; tileY++)
            {
                if (InWorld(state, tileX, tileY) && state.Tiles[tileX, tileY].IsActiveType(type))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void PlaceCrimsonAltar(WorldGenState state, int x, int y)
    {
        if (!InWorld(state, x + 2, y + 1))
        {
            return;
        }

        if (!state.Tiles[x, y + 1].Active ||
            !state.Tiles[x + 1, y + 1].Active ||
            !state.Tiles[x + 2, y + 1].Active)
        {
            return;
        }

        for (int tileX = x; tileX < x + 3; tileX++)
        {
            for (int tileY = y; tileY < y + 2; tileY++)
            {
                state.Tiles[tileX, tileY].SetType(CrimsonAltar);
            }
        }
    }

    private static bool SolidTile(WorldGenState state, int x, int y)
    {
        return InWorld(state, x, y) && state.Tiles[x, y].Active && IsSolidTileType(state.Tiles[x, y].Type);
    }

    private static bool IsSolidTileType(int tileType)
    {
        return tileType is
            TileIds.Dirt or
            TileIds.Stone or
            TileIds.Grass or
            TileIds.Clay or
            TileIds.Sand or
            TileIds.Mud or
            JungleGrass or
            TileIds.Silt or
            TileIds.SnowBlock or
            TileIds.IceBlock or
            TileIds.SandstoneBrick or
            TileIds.CrimsonGrass or
            TileIds.FleshIce or
            TileIds.Crimstone or
            TileIds.Crimtane or
            TileIds.Crimsand or
            CrimsonJungleGrass or
            TileIds.Marble or
            TileIds.Granite or
            TileIds.Sandstone or
            TileIds.HardenedSand or
            TileIds.CrimsonHardenedSand or
            TileIds.CrimsonSandstone or
            TileIds.DesertFossil;
    }

    private static bool CanBeClearedDuringGeneration(int tileType)
    {
        return tileType is
            TileIds.Dirt or
            TileIds.Stone or
            TileIds.Grass or
            TileIds.Clay or
            TileIds.Sand or
            TileIds.Mud or
            JungleGrass or
            TileIds.Silt or
            TileIds.SnowBlock or
            TileIds.IceBlock or
            TileIds.Sandstone or
            TileIds.HardenedSand;
    }

    private static bool CanEvilReplace(WorldGenState state, int x, int y)
    {
        return InWorld(state, x, y);
    }

    private static bool IsPyramidCandidateScanColumn(WorldGenState state, int x, int y)
    {
        if (y >= state.MainWorldSurface)
        {
            return false;
        }

        foreach (PyramidCandidate candidate in state.PyramidCandidates)
        {
            if (candidate.X == x && y >= candidate.Y)
            {
                return true;
            }
        }

        return false;
    }

    private static bool InWorld(WorldGenState state, int x, int y, int fluff = 0)
    {
        return x >= fluff &&
            y >= fluff &&
            x < state.Options.Dimensions.Width - fluff &&
            y < state.Options.Dimensions.Height - fluff;
    }

    private struct Vec2
    {
        public Vec2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X;

        public double Y;

        public static Vec2 operator +(Vec2 left, Vec2 right)
        {
            return new Vec2(left.X + right.X, left.Y + right.Y);
        }
    }
}
