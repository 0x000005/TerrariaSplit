namespace WorldGenSim.Simulation;

internal static class FullDesertPassReplica
{
    private const ushort SandstoneWall = 187;
    private const ushort HardenedSandWall = 216;

    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Full desert replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int attemptsOnBothSides = 0;
        int side = state.DungeonSide;
        int halfWorld = width / 2;
        int offset = random.Next(halfWorld) / 8;
        offset += halfWorld / 8;
        int x = halfWorld + offset * -side;
        int retryCount = 0;

        while (!PlaceDesert(state, random, x, (int)state.WorldSurfaceHigh + 25, progress))
        {
            offset = random.Next(halfWorld) / 2;
            offset += halfWorld / 8;
            offset += random.Next(retryCount / 12);
            x = halfWorld + offset * -side;
            if (++retryCount > width / 4)
            {
                side *= -1;
                retryCount = 0;
                attemptsOnBothSides++;
                if (attemptsOnBothSides >= 2)
                {
                    state.SkipDesertTileCheck = true;
                }
            }
        }
    }

    private static bool PlaceDesert(
        WorldGenState state,
        UnifiedRandom random,
        int originX,
        int originY,
        GenerationProgress progress)
    {
        if (!TryCreateDescription(state, random, originX, originY, out DesertDescription description))
        {
            return false;
        }

        ExportDescriptionToState(state, description);
        if (!WorldInterestArea.IntersectsTargetPyramidArea(
            state.Options.Dimensions,
            description.CombinedArea,
            horizontalPadding: 30,
            verticalPadding: 30))
        {
            return true;
        }

        PlaceSandMound(state, random, description, progress, 0.0, 0.1);
        description = description.WithUpdatedSurface(state);

        if (random.NextDouble() <= 0.5)
        {
            int entrance = random.Next(4);
            PlaceEntranceApproximation(state, random, description, entrance, progress, 0.1, 0.2);
        }

        PlaceDesertHive(state, random, description, progress, 0.2, 0.75);
        CleanupArea(state, description.Hive, progress, 0.75, 1.0);
        return true;
    }

    private static bool TryCreateDescription(
        WorldGenState state,
        UnifiedRandom random,
        int originX,
        int originY,
        out DesertDescription description)
    {
        double worldScale = state.Options.Dimensions.Width / 4200.0;
        int blockColumns = (int)(80.0 * worldScale);
        int blockRows = (int)((random.NextDouble() * 0.5 + 1.5) * 170.0 * worldScale);
        int width = blockColumns * 4;
        int height = blockRows * 2;
        originX -= width / 2;
        SurfaceMap surface = SurfaceMap.FromArea(state, originX - 5, width + 10);
        if (RowHasInvalidTiles(state, originX, surface.Bottom, width))
        {
            description = default;
            return false;
        }

        int topY = (int)(surface.Average + surface.Bottom) / 2;
        originY = topY + random.Next(40, 60);
        description = new DesertDescription(
            CombinedArea: new WorldRect(originX, topY, width, originY + height - topY),
            Desert: new WorldRect(originX, topY, width, originY + height / 2 - topY),
            Hive: new WorldRect(originX, originY, width, height),
            BlockColumnCount: blockColumns,
            BlockRowCount: blockRows,
            Surface: surface);
        return true;
    }

    private static bool RowHasInvalidTiles(WorldGenState state, int startX, int startY, int width)
    {
        if (state.SkipDesertTileCheck)
        {
            return false;
        }

        for (int x = startX; x < startX + width; x++)
        {
            if (!InWorld(state, x, startY, 0))
            {
                return true;
            }

            ushort type = state.Tiles[x, startY].Type;
            if (type is TileIds.Mud or 60 or TileIds.IceBlock or TileIds.SnowBlock)
            {
                return true;
            }
        }

        return false;
    }

    private static void ExportDescriptionToState(WorldGenState state, DesertDescription description)
    {
        state.UndergroundDesertLocation = description.CombinedArea.Inflated(10, 10);
        state.UndergroundDesertHiveLocation = description.Hive;
    }

    private static void PlaceSandMound(
        WorldGenState state,
        UnifiedRandom random,
        DesertDescription description,
        GenerationProgress progress,
        double progressMin,
        double progressMax)
    {
        WorldRect desert = description.Desert with
        {
            Height = Math.Min(description.Desert.Height, description.Hive.Height / 2)
        };
        WorldRect lowerDesert = description.Desert with
        {
            Y = desert.Bottom,
            Height = Math.Max(0, description.Desert.Bottom - desert.Bottom)
        };
        int verticalNoise = 0;
        int surfaceNoise = 0;
        progress.Set(progressMin);
        int scanWidth = desert.Width + 5;
        for (int i = -5; i < scanWidth; i++)
        {
            double normalized = Math.Abs((double)(i + 5) / (desert.Width + 10)) * 2.0 - 1.0;
            normalized = Math.Clamp(normalized, -1.0, 1.0);
            SetProgress(progress, (i + 5.0) / (scanWidth + 5.0), progressMin, progressMax);
            if (i % 3 == 0)
            {
                verticalNoise += random.Next(-1, 2);
                verticalNoise = Math.Clamp(verticalNoise, -10, 10);
            }

            surfaceNoise += random.Next(-1, 2);
            surfaceNoise = Math.Clamp(surfaceNoise, -10, 10);
            double mound = Math.Sqrt(1.0 - normalized * normalized * normalized * normalized);
            int startY = desert.Bottom - (int)(mound * desert.Height) + verticalNoise;
            int x = i + desert.X;
            if (Math.Abs(normalized) < 1.0)
            {
                double smoothing = UnclampedSmoothStep(0.5, 0.8, Math.Abs(normalized));
                smoothing = smoothing * smoothing * smoothing;
                int clearBottom = 10 + (int)(desert.Top - smoothing * 20.0) + surfaceNoise;
                clearBottom = Math.Min(clearBottom, startY);
                for (int y = description.Surface[x] - 1; y < clearBottom; y++)
                {
                    if (InWorld(state, x, y, 0))
                    {
                        state.Tiles[x, y].Active = false;
                        state.Tiles[x, y].Wall = 0;
                    }
                }
            }

            PlaceSandColumn(state, x, startY, lowerDesert.Bottom - startY);
        }
    }

    private static void PlaceSandColumn(WorldGenState state, int x, int startY, int height)
    {
        for (int y = startY + height - 1; y >= startY; y--)
        {
            if (!InWorld(state, x, y, 0))
            {
                continue;
            }

            ref TileData tile = ref state.Tiles[x, y];
            tile.Liquid = 0;
            tile.LiquidType = 0;
            tile.Type = TileIds.Sand;
            tile.Active = true;
        }
    }

    private static void PlaceEntranceApproximation(
        WorldGenState state,
        UnifiedRandom random,
        DesertDescription description,
        int entranceKind,
        GenerationProgress progress,
        double progressMin,
        double progressMax)
    {
        switch (entranceKind)
        {
            case 0:
                ConsumeAndCarveChambersEntrance(state, random, description, progress, progressMin, progressMax);
                break;
            case 1:
                ConsumeAndCarveAnthillEntrance(state, random, description, progress, progressMin, progressMax);
                break;
            case 2:
                ConsumeAndCarveLarvaHoleEntrance(state, random, description, progress, progressMin, progressMax);
                break;
            default:
                ConsumeAndCarvePitEntrance(state, random, description, progress, progressMin, progressMax);
                break;
        }
    }

    private static void ConsumeAndCarveChambersEntrance(
        WorldGenState state,
        UnifiedRandom random,
        DesertDescription description,
        GenerationProgress progress,
        double progressMin,
        double progressMax)
    {
        int x = description.Desert.CenterX + random.Next(-40, 41);
        int y = description.Surface[x];
        int height = description.Hive.Top - y;
        int direction = random.Next(2) != 0 ? 1 : -1;
        int chamberCount = random.Next(2, 4);
        CarveEllipse(state, x, y + 2, 24, 12, TileIds.Sand, SandstoneWall, fill: true);
        int previousX = x + -direction * 26;
        int previousY = y - 8;
        for (int i = 0; i < chamberCount; i++)
        {
            SetProgress(progress, (i + 1.0) / chamberCount, progressMin, progressMax);
            int offsetY = (int)((double)(i + 1) / chamberCount * height) + random.Next(-8, 9);
            int offsetX = direction * random.Next(20, 41);
            int chamberWidth = random.Next(18, 29);
            int chamberX = x + offsetX;
            int chamberY = y + offsetY;
            CarveEllipse(state, chamberX, chamberY, chamberWidth / 2, 3, 0, SandstoneWall, fill: false);
            CarveLine(state, previousX, previousY, chamberX + chamberWidth / 2 * -direction, chamberY, 2, SandstoneWall);
            previousX = chamberX + chamberWidth / 2 * -direction;
            previousY = chamberY;
            direction *= -1;
        }
    }

    private static void ConsumeAndCarveAnthillEntrance(
        WorldGenState state,
        UnifiedRandom random,
        DesertDescription description,
        GenerationProgress progress,
        double progressMin,
        double progressMax)
    {
        int count = random.Next(2, 4);
        for (int i = 0; i < count; i++)
        {
            SetProgress(progress, (double)i / count, progressMin, progressMax);
            int radius = random.Next(15, 18);
            int x = (int)((double)(i + 1) / (count + 1) * description.Surface.Width) + description.Desert.Left;
            int y = description.Surface[x];
            int currentX = x;
            for (int tileY = y - radius - 3; tileY < description.Hive.Top + (y - description.Desert.Top) * 2 + 12; tileY++)
            {
                CarveEllipse(state, currentX, tileY, 2, 3, TileIds.HardenedSand, SandstoneWall, fill: tileY < y);
                if (tileY % 3 == 0 && tileY >= y)
                {
                    currentX += random.Next(-1, 2);
                    CarveEllipse(state, currentX, tileY, radius, 3, TileIds.Sand, SandstoneWall, fill: tileY < y + 5);
                }
            }
        }
    }

    private static void ConsumeAndCarveLarvaHoleEntrance(
        WorldGenState state,
        UnifiedRandom random,
        DesertDescription description,
        GenerationProgress progress,
        double progressMin,
        double progressMax)
    {
        int count = random.Next(2, 4);
        for (int i = 0; i < count; i++)
        {
            SetProgress(progress, (double)i / count, progressMin, progressMax);
            int radius = random.Next(13, 16);
            int x = (int)((double)(i + 1) / (count + 1) * description.Surface.Width) + description.Desert.Left;
            int y = description.Surface[x];
            CarveEllipse(state, x, y, radius, radius * 2, 0, SandstoneWall, fill: false);
            int currentX = x;
            for (int tileY = y + (int)(radius * 1.5); tileY < description.Hive.Top + (y - description.Desert.Top) * 2 + 12; tileY++)
            {
                CarveEllipse(state, currentX, tileY, 2, 3, TileIds.HardenedSand, SandstoneWall, fill: false);
                if (tileY % 3 == 0)
                {
                    currentX += random.Next(-1, 2);
                    CarveEllipse(state, currentX, tileY, 2, 3, TileIds.HardenedSand, SandstoneWall, fill: false);
                }
            }
        }
    }

    private static void ConsumeAndCarvePitEntrance(
        WorldGenState state,
        UnifiedRandom random,
        DesertDescription description,
        GenerationProgress progress,
        double progressMin,
        double progressMax)
    {
        int radius = random.Next(6, 9);
        int x = description.CombinedArea.CenterX;
        int y = description.Surface[x];
        int progressOffset = radius + 3;
        int progressWidth = progressOffset + radius + 3;
        for (int dx = -radius - 3; dx < radius + 3; dx++)
        {
            SetProgress(progress, (dx + progressOffset) / (double)progressWidth, progressMin, progressMax);
            int columnX = x + dx;
            for (int tileY = description.Surface[columnX]; tileY <= description.Hive.Top + 10; tileY++)
            {
                double yProgress = (double)(tileY - description.Surface[columnX]) /
                    (description.Hive.Top - description.Desert.Top);
                yProgress = Math.Clamp(yProgress, 0.0, 1.0);
                int currentRadius = (int)(GetPitHoleRadiusScaleAt(yProgress) * radius);
                if (Math.Abs(dx) < currentRadius)
                {
                    state.Tiles[columnX, tileY].Clear();
                }
                else if (Math.Abs(dx) < currentRadius + 3 && yProgress > 0.35)
                {
                    state.Tiles[columnX, tileY].SetType(TileIds.HardenedSand);
                }

                double edge = Math.Abs((double)dx / radius);
                edge *= edge;
                if (Math.Abs(dx) < currentRadius + 3 && tileY - y > 15.0 - 3.0 * edge)
                {
                    state.Tiles[columnX, tileY].Wall = SandstoneWall;
                }
            }
        }
    }

    private static void PlaceDesertHive(
        WorldGenState state,
        UnifiedRandom random,
        DesertDescription description,
        GenerationProgress progress,
        double progressMin,
        double progressMax)
    {
        ClusterGroup clusters = ClusterGroup.FromDescription(description, random);
        WorldRect area = description.Hive.Inflated(20, 20);
        area = ClipToTargetPyramidArea(state, area, horizontalPadding: 16, verticalPadding: 16);
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        PlaceClustersArea(state, description, clusters, area, progress, progressMin, progressMax);
        AddTileVariance(state, random, description);
    }

    private static void PlaceClustersArea(
        WorldGenState state,
        DesertDescription description,
        ClusterGroup clusters,
        WorldRect area,
        GenerationProgress progress,
        double progressMin,
        double progressMax)
    {
        FastRandomReplica fastRandom = new((ulong)state.Options.Seed);
        fastRandom = fastRandom.WithModifier(57005UL);
        ClusterData[] clusterData = ClusterData.From(clusters);
        double hiveWidth = description.Hive.Width;
        double hiveHeight = description.Hive.Height;
        double clusterWidth = clusters.Width;
        double clusterHeight = clusters.Height;

        for (int x = area.Left; x < area.Right; x++)
        {
            SetProgress(progress, (double)(x - area.Left) / (area.Right - area.Left), progressMin, progressMax);
            for (int y = area.Top; y < area.Bottom; y++)
            {
                if (!InWorld(state, x, y, 1))
                {
                    continue;
                }

                double best = 0.0;
                int bestIndex = -1;
                double secondBest = 0.0;
                ushort type = fastRandom.Next(3) == 0
                    ? (ushort)TileIds.HardenedSand
                    : (ushort)TileIds.Sand;

                if (!IsPyramidCandidateScanTile(state, x, y))
                {
                    continue;
                }

                int relativeX = x - description.Hive.X;
                int relativeY = y - description.Hive.Y;
                double clusterPointX = (relativeX - 2.0) / hiveWidth * clusterWidth;
                double clusterPointY = (relativeY - 1.0) / hiveHeight * clusterHeight;
                for (int i = 0; i < clusterData.Length; i++)
                {
                    ClusterData cluster = clusterData[i];
                    if (Math.Abs(cluster.FirstX - clusterPointX) > 10.0 ||
                        Math.Abs(cluster.FirstY - clusterPointY) > 10.0)
                    {
                        continue;
                    }

                    double influence = 0.0;
                    foreach (Block block in cluster.Blocks)
                    {
                        double dx = block.Position.X - clusterPointX;
                        double dy = block.Position.Y - clusterPointY;
                        double distance = dx * dx + dy * dy;
                        influence += 1.0 / (distance == 0.0 ? double.Epsilon : distance);
                    }

                    if (influence > best)
                    {
                        if (best > secondBest)
                        {
                            secondBest = best;
                        }

                        best = influence;
                        bestIndex = cluster.Index;
                    }
                    else if (influence > secondBest)
                    {
                        secondBest = influence;
                    }
                }

                double combinedInfluence = best + secondBest;
                double normalizedX = (relativeX - 2.0) / hiveWidth * 2.0 - 1.0;
                double normalizedY = (relativeY - 1.0) / hiveHeight * 2.0 - 1.0;
                bool outsideCore = Math.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY) >= 0.8;
                bool touchedHive = true;
                if (combinedInfluence > 3.5)
                {
                    state.Tiles[x, y].Clear();
                    state.Tiles[x, y].Wall = SandstoneWall;
                    if (bestIndex % 15 == 2)
                    {
                        state.Tiles[x, y].SetType(TileIds.DesertFossil);
                    }
                }
                else if (combinedInfluence > 1.8)
                {
                    state.Tiles[x, y].Wall = SandstoneWall;
                    if (y < state.WorldSurface)
                    {
                        state.Tiles[x, y].Liquid = 0;
                        state.Tiles[x, y].LiquidType = 0;
                    }
                    else
                    {
                        state.Tiles[x, y].LiquidType = 1;
                    }

                    if (!outsideCore || state.Tiles[x, y].Active)
                    {
                        state.Tiles[x, y].SetType(TileIds.Sandstone);
                    }
                }
                else if (combinedInfluence > 0.7 || !outsideCore)
                {
                    state.Tiles[x, y].Wall = HardenedSandWall;
                    state.Tiles[x, y].Liquid = 0;
                    state.Tiles[x, y].LiquidType = 0;
                    if (!outsideCore || state.Tiles[x, y].Active)
                    {
                        state.Tiles[x, y].SetType(type);
                    }
                }
                else if (combinedInfluence > 0.25)
                {
                    FastRandomReplica localRandom = fastRandom.WithModifier(relativeX, relativeY);
                    double threshold = (combinedInfluence - 0.25) / 0.45;
                    if (localRandom.NextDouble() < threshold)
                    {
                        state.Tiles[x, y].Wall = SandstoneWall;
                        if (y < state.WorldSurface)
                        {
                            state.Tiles[x, y].Liquid = 0;
                            state.Tiles[x, y].LiquidType = 0;
                        }
                        else
                        {
                            state.Tiles[x, y].LiquidType = 1;
                        }

                        if (state.Tiles[x, y].Active)
                        {
                            state.Tiles[x, y].SetType(type);
                        }
                    }
                }
                else
                {
                    touchedHive = false;
                }

                if (touchedHive)
                {
                    UpdateDesertHiveBounds(state, x, y);
                }
            }
        }
    }

    private static void AddTileVariance(WorldGenState state, UnifiedRandom random, DesertDescription description)
    {
        foreach (PyramidCandidate candidate in state.PyramidCandidates)
        {
            if (!WorldInterestArea.IsInTargetPyramidXRange(state.Options.Dimensions, candidate.X))
            {
                continue;
            }

            if (candidate.X < description.Hive.Left - 20 || candidate.X >= description.Hive.Right + 20)
            {
                continue;
            }

            int startY = Math.Max(candidate.Y, description.Hive.Top - 20);
            int endY = Math.Min((int)state.MainWorldSurface, description.Hive.Bottom + 20);
            for (int y = startY; y < endY; y++)
            {
                if (InWorld(state, candidate.X, y + 2, 1))
                {
                    ref TileData tile = ref state.Tiles[candidate.X, y];
                    if (tile.Type == TileIds.Sand &&
                        (!IsSolid(state.Tiles[candidate.X, y + 1]) || !IsSolid(state.Tiles[candidate.X, y + 2])))
                    {
                        tile.Type = TileIds.HardenedSand;
                    }
                }
            }
        }

        foreach (PyramidCandidate candidate in state.PyramidCandidates)
        {
            if (!WorldInterestArea.IsInTargetPyramidXRange(state.Options.Dimensions, candidate.X))
            {
                continue;
            }

            if (candidate.X < description.Hive.Left - 20 || candidate.X >= description.Hive.Right + 20)
            {
                continue;
            }

            int startY = Math.Max(candidate.Y, description.Hive.Top - 20);
            int endY = Math.Min((int)state.MainWorldSurface, description.Hive.Bottom + 20);
            for (int y = startY; y < endY; y++)
            {
                if (!InWorld(state, candidate.X, y, 5) ||
                    !state.Tiles[candidate.X, y].Active ||
                    state.Tiles[candidate.X, y].Type != TileIds.Sandstone)
                {
                    continue;
                }

                bool openAbove = true;
                for (int offset = -1; offset >= -3; offset--)
                {
                    if (state.Tiles[candidate.X, y + offset].Active || state.Tiles[candidate.X + 1, y + offset].Active)
                    {
                        openAbove = false;
                        break;
                    }
                }

                bool openBelow = true;
                for (int offset = 1; offset <= 3; offset++)
                {
                    if (state.Tiles[candidate.X, y + offset].Active || state.Tiles[candidate.X + 1, y + offset].Active)
                    {
                        openBelow = false;
                        break;
                    }
                }

                if (openAbove && random.Next(20) == 0)
                {
                    int type = 485;
                    int style = random.Next(4);
                    if (random.Next(30) == 0)
                    {
                        type = 751;
                        style = 0;
                    }

                    _ = style;
                    PlaceDecorativeTile(state, candidate.X, y - 1, type);
                }
                else if (openAbove && random.Next(5) == 0)
                {
                    PlaceDecorativeTile(state, candidate.X, y - 1, 484);
                }
                else if ((openAbove ^ openBelow) && random.Next(5) == 0)
                {
                    PlaceDecorativeTile(state, candidate.X, y + (openAbove ? -1 : 1), 165);
                }
                else if (openAbove && random.Next(5) == 0)
                {
                    int style = 29 + random.Next(6);
                    _ = style;
                    PlaceDecorativeTile(state, candidate.X, y - 1, 187);
                }
            }
        }
    }

    private static void PlaceDecorativeTile(WorldGenState state, int x, int y, int type)
    {
        if (InWorld(state, x, y, 1) && !state.Tiles[x, y].Active)
        {
            state.Tiles[x, y].SetType(type);
        }
    }

    private static void CleanupArea(
        WorldGenState state,
        WorldRect area,
        GenerationProgress progress,
        double progressMin,
        double progressMax)
    {
        int offset = 20 - area.Left;
        int total = offset + area.Right + 20;
        for (int x = -20 + area.Left; x < area.Right + 20; x++)
        {
            SetProgress(progress, (double)(x + offset) / total, progressMin, progressMax);
        }
    }

    private static void UpdateDesertHiveBounds(WorldGenState state, int x, int y)
    {
        if (state.DesertHiveHigh > y)
        {
            state.DesertHiveHigh = y;
        }

        if (state.DesertHiveLow < y)
        {
            state.DesertHiveLow = y;
        }

        if (state.DesertHiveLeft > x)
        {
            state.DesertHiveLeft = x;
        }

        if (state.DesertHiveRight < x)
        {
            state.DesertHiveRight = x;
        }
    }

    private static bool IsPyramidCandidateScanTile(WorldGenState state, int x, int y)
    {
        if (y >= state.MainWorldSurface)
        {
            return false;
        }

        foreach (PyramidCandidate candidate in state.PyramidCandidates)
        {
            if (WorldInterestArea.IsInTargetPyramidXRange(state.Options.Dimensions, candidate.X) &&
                candidate.X == x &&
                y >= candidate.Y)
            {
                return true;
            }
        }

        return false;
    }

    private static WorldRect ClipToTargetPyramidArea(
        WorldGenState state,
        WorldRect area,
        int horizontalPadding,
        int verticalPadding)
    {
        (int targetLeft, int targetRight) = WorldInterestArea.TargetPyramidXRange(state.Options.Dimensions);
        (int targetTop, int targetBottom) = WorldInterestArea.TargetPyramidYRange(state.Options.Dimensions);
        int left = Math.Max(area.Left, targetLeft - horizontalPadding);
        int right = Math.Min(area.Right, targetRight + horizontalPadding);
        int top = Math.Max(area.Top, targetTop - verticalPadding);
        int bottom = Math.Min(area.Bottom, targetBottom + verticalPadding);
        if (right <= left || bottom <= top)
        {
            return WorldRect.Empty;
        }

        return new WorldRect(left, top, right - left, bottom - top);
    }

    private static void CarveEllipse(
        WorldGenState state,
        int centerX,
        int centerY,
        int radiusX,
        int radiusY,
        int fillType,
        ushort wall,
        bool fill)
    {
        for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
        {
            for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
            {
                if (!InWorld(state, x, y, 1))
                {
                    continue;
                }

                double dx = radiusX == 0 ? 0.0 : (x - centerX) / (double)radiusX;
                double dy = radiusY == 0 ? 0.0 : (y - centerY) / (double)radiusY;
                if (dx * dx + dy * dy > 1.0)
                {
                    continue;
                }

                if (fill)
                {
                    state.Tiles[x, y].SetType(fillType);
                }
                else
                {
                    state.Tiles[x, y].Active = false;
                }

                state.Tiles[x, y].Wall = wall;
            }
        }
    }

    private static void CarveLine(
        WorldGenState state,
        int fromX,
        int fromY,
        int toX,
        int toY,
        int radius,
        ushort wall)
    {
        int steps = Math.Max(Math.Abs(toX - fromX), Math.Abs(toY - fromY));
        if (steps <= 0)
        {
            return;
        }

        for (int i = 0; i <= steps; i++)
        {
            double t = i / (double)steps;
            int x = (int)Math.Round(fromX + (toX - fromX) * t);
            int y = (int)Math.Round(fromY + (toY - fromY) * t);
            CarveEllipse(state, x, y, radius, radius * 2, 0, wall, fill: false);
        }
    }

    private static double GetPitHoleRadiusScaleAt(double yProgress)
    {
        if (yProgress < 0.6)
        {
            return 1.0;
        }

        double delta = Math.Clamp((yProgress - 0.6) / 0.4, 0.0, 1.0);
        return (1.0 - Math.Cos(delta * Math.PI) * 0.5 - 0.5) * -0.5 + 1.0;
    }

    private static bool InWorld(WorldGenState state, int x, int y, int fluff)
    {
        return x >= fluff &&
            y >= fluff &&
            x < state.Options.Dimensions.Width - fluff &&
            y < state.Options.Dimensions.Height - fluff;
    }

    private static bool IsSolid(TileData tile)
    {
        return tile.Active && tile.Type is
            TileIds.Dirt or
            TileIds.Stone or
            TileIds.Grass or
            TileIds.Clay or
            TileIds.Sand or
            TileIds.Mud or
            60 or
            TileIds.SnowBlock or
            TileIds.IceBlock or
            TileIds.SandstoneBrick or
            TileIds.Sandstone or
            TileIds.HardenedSand or
            TileIds.DesertFossil;
    }

    private static double UnclampedSmoothStep(double min, double max, double value)
    {
        double amount = (value - min) / (max - min);
        return amount * amount * (3.0 - 2.0 * amount);
    }

    private static void SetProgress(GenerationProgress progress, double value, double min, double max)
    {
        progress.Set(min + value * (max - min));
    }

    private readonly record struct DesertDescription(
        WorldRect CombinedArea,
        WorldRect Desert,
        WorldRect Hive,
        int BlockColumnCount,
        int BlockRowCount,
        SurfaceMap Surface)
    {
        public DesertDescription WithUpdatedSurface(WorldGenState state)
        {
            return this with
            {
                Surface = SurfaceMap.FromArea(state, CombinedArea.Left - 5, CombinedArea.Width + 10)
            };
        }
    }

    private sealed class SurfaceMap
    {
        private readonly short[] heights;

        private SurfaceMap(short[] heights, int x)
        {
            this.heights = heights;
            X = x;
            int bottom = 0;
            int top = int.MaxValue;
            int sum = 0;
            foreach (short height in heights)
            {
                sum += height;
                bottom = Math.Max(bottom, height);
                top = Math.Min(top, height);
            }

            if (bottom > WorldSurfaceLimit)
            {
                bottom = WorldSurfaceLimit;
            }

            Bottom = bottom;
            Top = top;
            Average = (double)sum / heights.Length;
        }

        public int X { get; }

        public int Width => heights.Length;

        public int Top { get; }

        public int Bottom { get; }

        public double Average { get; }

        private static int WorldSurfaceLimit { get; set; }

        public short this[int absoluteX] => heights[absoluteX - X];

        public static SurfaceMap FromArea(WorldGenState state, int startX, int width)
        {
            int scanHeight = state.Options.Dimensions.Height / 2;
            short[] heights = new short[width];
            for (int x = startX; x < startX + width; x++)
            {
                bool found = false;
                int surface = 0;
                for (int y = 50; y < 50 + scanHeight; y++)
                {
                    if (InWorld(state, x, y, 0) && state.Tiles[x, y].Active)
                    {
                        if (!found)
                        {
                            surface = y;
                            found = true;
                        }
                    }

                    if (!found)
                    {
                        surface = scanHeight + 50;
                    }
                }

                heights[x - startX] = checked((short)surface);
            }

            WorldSurfaceLimit = (int)state.MainWorldSurface - 10;
            return new SurfaceMap(heights, startX);
        }
    }

    private readonly record struct Vec2(double X, double Y)
    {
        public static Vec2 One => new(1.0, 1.0);

        public static Vec2 operator +(Vec2 left, Vec2 right) => new(left.X + right.X, left.Y + right.Y);

        public static Vec2 operator -(Vec2 left, Vec2 right) => new(left.X - right.X, left.Y - right.Y);

        public static Vec2 operator *(Vec2 left, Vec2 right) => new(left.X * right.X, left.Y * right.Y);

        public static Vec2 operator *(Vec2 left, double value) => new(left.X * value, left.Y * value);

        public static Vec2 operator /(Vec2 left, Vec2 right) => new(left.X / right.X, left.Y / right.Y);

        public double Length()
        {
            return Math.Sqrt(X * X + Y * Y);
        }

        public static double DistanceSquared(Vec2 left, Vec2 right)
        {
            double dx = left.X - right.X;
            double dy = left.Y - right.Y;
            double distance = dx * dx + dy * dy;
            return distance == 0.0 ? double.Epsilon : distance;
        }
    }

    private readonly record struct Block(Vec2 Position);

    private readonly record struct ClusterData(int Index, double FirstX, double FirstY, Block[] Blocks)
    {
        public static ClusterData[] From(ClusterGroup clusters)
        {
            ClusterData[] data = new ClusterData[clusters.Count];
            for (int i = 0; i < clusters.Count; i++)
            {
                Cluster cluster = clusters[i];
                Vec2 first = cluster[0].Position;
                data[i] = new ClusterData(i, first.X, first.Y, cluster.ToArray());
            }

            return data;
        }
    }

    private sealed class Cluster : List<Block>
    {
    }

    private sealed class ClusterGroup : List<Cluster>
    {
        private ClusterGroup(int width, int height, UnifiedRandom random)
        {
            Width = width;
            Height = height;
            Generate(random);
        }

        public int Width { get; }

        public int Height { get; }

        public static ClusterGroup FromDescription(DesertDescription description, UnifiedRandom random)
        {
            return new ClusterGroup(description.BlockColumnCount, description.BlockRowCount, random);
        }

        private static void SearchForCluster(bool[,] blockMap, List<(int X, int Y)> pointCluster, int x, int y, int level = 2)
        {
            pointCluster.Add((x, y));
            blockMap[x, y] = false;
            level--;
            if (level == -1)
            {
                return;
            }

            if (x > 0 && blockMap[x - 1, y])
            {
                SearchForCluster(blockMap, pointCluster, x - 1, y, level);
            }

            if (x < blockMap.GetLength(0) - 1 && blockMap[x + 1, y])
            {
                SearchForCluster(blockMap, pointCluster, x + 1, y, level);
            }

            if (y > 0 && blockMap[x, y - 1])
            {
                SearchForCluster(blockMap, pointCluster, x, y - 1, level);
            }

            if (y < blockMap.GetLength(1) - 1 && blockMap[x, y + 1])
            {
                SearchForCluster(blockMap, pointCluster, x, y + 1, level);
            }
        }

        private static void AttemptClaim(
            UnifiedRandom random,
            int x,
            int y,
            int[,] clusterIndexMap,
            List<List<(int X, int Y)>> pointClusters,
            int index)
        {
            int currentIndex = clusterIndexMap[x, y];
            if (currentIndex == -1 || currentIndex == index)
            {
                return;
            }

            int replacement = random.Next(2) == 0 ? -1 : index;
            foreach ((int pointX, int pointY) in pointClusters[currentIndex])
            {
                clusterIndexMap[pointX, pointY] = replacement;
            }
        }

        private void Generate(UnifiedRandom random)
        {
            Clear();
            bool[,] blocks = new bool[Width, Height];
            int radiusX = Width / 2 - 1;
            int radiusY = Height / 2 - 1;
            int radiusSquared = (radiusX + 1) * (radiusX + 1);
            (int X, int Y) center = (radiusX, radiusY);
            for (int y = center.Y - radiusY; y <= center.Y + radiusY; y++)
            {
                double scaledY = (double)radiusX / radiusY * (y - center.Y);
                int localRadius = Math.Min(radiusX, (int)Math.Sqrt(radiusSquared - scaledY * scaledY));
                for (int x = center.X - localRadius; x <= center.X + localRadius; x++)
                {
                    blocks[x, y] = random.Next(2) == 0;
                }
            }

            List<List<(int X, int Y)>> pointClusters = [];
            for (int x = 0; x < blocks.GetLength(0); x++)
            {
                for (int y = 0; y < blocks.GetLength(1); y++)
                {
                    if (blocks[x, y] && random.Next(2) == 0)
                    {
                        List<(int X, int Y)> cluster = [];
                        SearchForCluster(blocks, cluster, x, y);
                        if (cluster.Count > 2)
                        {
                            pointClusters.Add(cluster);
                        }
                    }
                }
            }

            int[,] clusterIndexMap = new int[blocks.GetLength(0), blocks.GetLength(1)];
            for (int x = 0; x < clusterIndexMap.GetLength(0); x++)
            {
                for (int y = 0; y < clusterIndexMap.GetLength(1); y++)
                {
                    clusterIndexMap[x, y] = -1;
                }
            }

            for (int index = 0; index < pointClusters.Count; index++)
            {
                foreach ((int x, int y) in pointClusters[index])
                {
                    clusterIndexMap[x, y] = index;
                }
            }

            for (int index = 0; index < pointClusters.Count; index++)
            {
                foreach ((int x, int y) in pointClusters[index])
                {
                    if (clusterIndexMap[x, y] == -1)
                    {
                        break;
                    }

                    int currentIndex = clusterIndexMap[x, y];
                    if (x > 0)
                    {
                        AttemptClaim(random, x - 1, y, clusterIndexMap, pointClusters, currentIndex);
                    }

                    if (x < clusterIndexMap.GetLength(0) - 1)
                    {
                        AttemptClaim(random, x + 1, y, clusterIndexMap, pointClusters, currentIndex);
                    }

                    if (y > 0)
                    {
                        AttemptClaim(random, x, y - 1, clusterIndexMap, pointClusters, currentIndex);
                    }

                    if (y < clusterIndexMap.GetLength(1) - 1)
                    {
                        AttemptClaim(random, x, y + 1, clusterIndexMap, pointClusters, currentIndex);
                    }
                }
            }

            foreach (List<(int X, int Y)> cluster in pointClusters)
            {
                cluster.Clear();
            }

            for (int x = 0; x < clusterIndexMap.GetLength(0); x++)
            {
                for (int y = 0; y < clusterIndexMap.GetLength(1); y++)
                {
                    if (clusterIndexMap[x, y] != -1)
                    {
                        pointClusters[clusterIndexMap[x, y]].Add((x, y));
                    }
                }
            }

            foreach (List<(int X, int Y)> pointCluster in pointClusters)
            {
                if (pointCluster.Count < 4)
                {
                    pointCluster.Clear();
                }
            }

            foreach (List<(int X, int Y)> pointCluster in pointClusters)
            {
                Cluster cluster = [];
                if (pointCluster.Count <= 0)
                {
                    continue;
                }

                foreach ((int x, int y) in pointCluster)
                {
                    cluster.Add(new Block(new Vec2(
                        x + (random.NextDouble() - 0.5) * 0.5,
                        y + (random.NextDouble() - 0.5) * 0.5)));
                }

                Add(cluster);
            }
        }
    }
}
