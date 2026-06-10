namespace WorldGenSim.Simulation;

internal static class PyramidsPassReplica
{
    private const ushort PyramidWall = 34;

    public static IReadOnlyList<PyramidCandidateAnalysis> AnalyzeCandidates(WorldGenState state)
    {
        int width = state.Options.Dimensions.Width;
        IReadOnlyList<PyramidCandidate> candidates = state.PyramidCandidates;
        var analyses = new List<PyramidCandidateAnalysis>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            PyramidCandidate candidate = candidates[i];
            int x = candidate.X;
            int y = candidate.Y;
            bool buildable = IsCandidateInBuildableBand(state, x);
            if (buildable)
            {
                while (InWorld(state, x, y) &&
                    !state.Tiles[x, y].Active &&
                    y < state.MainWorldSurface)
                {
                    y++;
                }
            }

            bool inWorld = InWorld(state, x, y);
            bool active = inWorld && state.Tiles[x, y].Active;
            int tileType = inWorld ? state.Tiles[x, y].Type : -1;
            bool sandOk = buildable &&
                inWorld &&
                y < state.MainWorldSurface &&
                tileType == TileIds.Sand;

            int minDistance = width;
            for (int previous = 0; previous < i; previous++)
            {
                minDistance = Math.Min(minDistance, Math.Abs(x - candidates[previous].X));
            }

            string fate = buildable switch
            {
                false => "buildable-band",
                true when !sandOk => "sand",
                true when minDistance < 220 => "spacing",
                _ => "alive"
            };

            analyses.Add(new PyramidCandidateAnalysis(
                i,
                candidate,
                buildable,
                y,
                active,
                tileType,
                minDistance,
                fate));
        }

        return analyses;
    }

    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Pyramids replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        int width = state.Options.Dimensions.Width;
        UnifiedRandom random = context.Random;
        IReadOnlyList<PyramidCandidate> candidates = state.PyramidCandidates;
        for (int i = 0; i < candidates.Count; i++)
        {
            progress.Set(candidates.Count == 0 ? 1.0 : (double)i / candidates.Count);
            PyramidCandidate candidate = candidates[i];
            int x = candidate.X;
            int y = candidate.Y;
            if (!IsCandidateInBuildableBand(state, x))
            {
                continue;
            }

            while (!state.Tiles[x, y].Active && y < state.MainWorldSurface)
            {
                y++;
            }

            if (y >= state.MainWorldSurface || state.Tiles[x, y].Type != TileIds.Sand)
            {
                continue;
            }

            int minDistance = width;
            for (int previous = 0; previous < i; previous++)
            {
                minDistance = Math.Min(minDistance, Math.Abs(x - candidates[previous].X));
            }

            if (minDistance < 220)
            {
                continue;
            }

            SimulatePyramid(state, random, x, y - 1, pyramidMinDepth: 75, pyramidMaxDepth: 125);
        }
    }

    private static bool IsCandidateInBuildableBand(WorldGenState state, int x)
    {
        int width = state.Options.Dimensions.Width;
        if (x <= 300 || x >= width - 300)
        {
            return false;
        }

        double dungeonShadow = width * 0.15;
        if (state.DungeonSide <= -1 && x < state.DungeonLocation + dungeonShadow)
        {
            return false;
        }

        if (state.DungeonSide >= 1 && x > state.DungeonLocation - dungeonShadow)
        {
            return false;
        }

        return true;
    }

    private static void SimulatePyramid(
        WorldGenState state,
        UnifiedRandom random,
        int x,
        int y,
        int pyramidMinDepth,
        int pyramidMaxDepth)
    {
        if (state.Tiles[x, y].Active && state.Tiles[x, y].Type == TileIds.SandstoneBrick)
        {
            return;
        }

        int top = y - random.Next(0, 7);
        int diagonalOffset = random.Next(9, 13);
        int bodyHalfWidth = 1;
        int bottom = y + random.Next(pyramidMinDepth, pyramidMaxDepth);
        for (int row = top; row < bottom; row++)
        {
            for (int column = x - bodyHalfWidth; column < x + bodyHalfWidth - 1; column++)
            {
                SetActiveType(state, column, row, TileIds.SandstoneBrick);
            }

            bodyHalfWidth++;
        }

        FillPyramidWalls(state, random, x, y, bottom, bodyHalfWidth);

        int direction = random.Next(2) == 0 ? -1 : 1;
        int hallX = x - diagonalOffset * direction;
        int hallY = y + diagonalOffset;
        int hallHeight = random.Next(5, 8);

        bool clearingEntrance = true;
        while (clearingEntrance)
        {
            clearingEntrance = false;
            bool reachedSand = false;
            for (int row = hallY; row <= hallY + hallHeight; row++)
            {
                if (state.Tiles[hallX, row - 1].IsActiveType(TileIds.Sand))
                {
                    reachedSand = true;
                }

                if (state.Tiles[hallX, row].IsActiveType(TileIds.SandstoneBrick))
                {
                    ClearTile(state, hallX, row);
                    clearingEntrance = true;
                }

                if (reachedSand)
                {
                    SetActiveType(state, hallX, row, TileIds.Sand);
                }
            }

            hallX -= direction;
        }

        hallX = x - diagonalOffset * direction;
        int turnCountdown = random.Next(20, 30);
        bool firstTurn = true;
        bool roomPlaced = false;
        bool digging = true;

        while (digging)
        {
            for (int row = hallY; row <= hallY + hallHeight; row++)
            {
                ClearTile(state, hallX, row);
            }

            hallX += direction;
            hallY++;
            turnCountdown--;
            if (hallY >= bottom - hallHeight * 2)
            {
                turnCountdown = 10;
            }

            if (turnCountdown <= 0)
            {
                bool placedThisIteration = false;
                if (!firstTurn && !roomPlaced)
                {
                    roomPlaced = true;
                    placedThisIteration = true;
                    int roomHeight = random.Next(7, 13);
                    int roomWidthRemaining = random.Next(23, 28);
                    int originalRoomWidth = roomWidthRemaining;
                    int roomStartX = hallX;
                    while (roomWidthRemaining > 0)
                    {
                        int topRoomY = hallY - roomHeight + hallHeight;
                        for (int roomY = topRoomY; roomY <= hallY + hallHeight; roomY++)
                        {
                            if (roomWidthRemaining == originalRoomWidth || roomWidthRemaining == 1)
                            {
                                if (roomY >= topRoomY + 2)
                                {
                                    ClearTile(state, hallX, roomY);
                                }
                            }
                            else if (
                                roomWidthRemaining == originalRoomWidth - 1 ||
                                roomWidthRemaining == 2 ||
                                roomWidthRemaining == originalRoomWidth - 2 ||
                                roomWidthRemaining == 3)
                            {
                                if (roomY >= topRoomY + 1)
                                {
                                    ClearTile(state, hallX, roomY);
                                }
                            }
                            else
                            {
                                ClearTile(state, hallX, roomY);
                            }
                        }

                        roomWidthRemaining--;
                        hallX += direction;
                    }

                    int roomEndX = hallX - direction;
                    int left = Math.Min(roomEndX, roomStartX);
                    int right = Math.Max(roomEndX, roomStartX);
                    int chestMainItem = RollPyramidMainItem(random);
                    int chestX = (left + right) / 2;
                    int chestGroundY = FindChestGroundY(state, chestX, hallY);
                    state.AddPyramidChest(chestX - 1, chestGroundY - 1, RollPyramidChestItems(state, random, chestMainItem));

                    int pileCount = random.Next(1, 10);
                    for (int i = 0; i < pileCount; i++)
                    {
                        _ = random.Next(left, right);
                        _ = hallY + hallHeight;
                        _ = random.Next(16, 19);
                    }

                    _ = random.Next(4, 7);
                    _ = random.Next(4, 7);
                    _ = random.Next(4, 7);
                    _ = random.Next(4, 7);
                    for (int potX = left; potX <= right; potX++)
                    {
                        _ = random.Next(25, 28);
                    }
                }

                if (firstTurn)
                {
                    firstTurn = false;
                    direction *= -1;
                    turnCountdown = random.Next(15, 20);
                }
                else if (placedThisIteration)
                {
                    turnCountdown = random.Next(10, 15);
                }
                else
                {
                    direction *= -1;
                    turnCountdown = random.Next(20, 40);
                }
            }

            if (hallY >= bottom - hallHeight)
            {
                digging = false;
            }
        }

        SimulateLowerTunnel(state, random, ref hallX, ref hallY, ref direction, hallHeight);
    }

    private static int FindChestGroundY(WorldGenState state, int x, int startY)
    {
        for (int y = startY; y < state.Options.Dimensions.Height - 10; y++)
        {
            if (IsSolidTile(state, x, y))
            {
                return y;
            }
        }

        return startY;
    }

    private static void SimulateLowerTunnel(
        WorldGenState state,
        UnifiedRandom random,
        ref int hallX,
        ref int hallY,
        ref int direction,
        int hallHeight)
    {
        int keepAliveCountdown = random.Next(100, 200);
        int hardStopCountdown = random.Next(500, 800);
        bool digging = true;
        int tunnelWidth = hallHeight;
        int turnCountdown = random.Next(10, 50);
        if (direction == 1)
        {
            hallX -= tunnelWidth;
        }

        int sidePadding = random.Next(5, 10);
        while (digging)
        {
            keepAliveCountdown--;
            hardStopCountdown--;
            turnCountdown--;
            int column = hallX - sidePadding - random.Next(0, 2);
            while (column <= hallX + tunnelWidth + sidePadding + random.Next(0, 2))
            {
                if (column >= hallX && column <= hallX + tunnelWidth)
                {
                    ClearTile(state, column, hallY);
                }
                else if (!IsDungeonWall(state, column, hallY))
                {
                    SetActiveType(state, column, hallY, TileIds.SandstoneBrick);
                }

                column++;
            }

            hallY++;
            hallX += direction;
            if (keepAliveCountdown <= 0)
            {
                digging = false;
                for (int columnToCheck = hallX + 1; columnToCheck <= hallX + tunnelWidth - 1; columnToCheck++)
                {
                    if (state.Tiles[columnToCheck, hallY].Active)
                    {
                        digging = true;
                    }
                }
            }

            if (turnCountdown < 0)
            {
                turnCountdown = random.Next(10, 50);
                direction *= -1;
            }

            if (hardStopCountdown <= 0)
            {
                digging = false;
            }
        }
    }

    private static bool IsSolidTile(WorldGenState state, int x, int y)
    {
        return state.Tiles[x, y].Active;
    }

    private static bool IsDungeonWall(WorldGenState state, int x, int y)
    {
        _ = state;
        _ = x;
        _ = y;
        return false;
    }

    private static void FillPyramidWalls(
        WorldGenState state,
        UnifiedRandom random,
        int x,
        int y,
        int bottom,
        int bodyHalfWidth)
    {
        for (int column = x - bodyHalfWidth - 5; column <= x + bodyHalfWidth + 5; column++)
        {
            for (int row = y - 1; row <= bottom + 1; row++)
            {
                bool surrounded = true;
                for (int checkX = column - 1; checkX <= column + 1; checkX++)
                {
                    for (int checkY = row - 1; checkY <= row + 1; checkY++)
                    {
                        if (!InWorld(state, checkX, checkY) ||
                            !state.Tiles[checkX, checkY].IsActiveType(TileIds.SandstoneBrick))
                        {
                            surrounded = false;
                        }
                    }
                }

                if (surrounded)
                {
                    state.Tiles[column, row].Wall = PyramidWall;
                    _ = random.Next(0, 3);
                }
            }
        }
    }

    private static void SetActiveType(WorldGenState state, int x, int y, int tileType)
    {
        if (!InWorld(state, x, y))
        {
            return;
        }

        state.Tiles[x, y].SetType(tileType);
    }

    private static void ClearTile(WorldGenState state, int x, int y)
    {
        if (!InWorld(state, x, y))
        {
            return;
        }

        state.Tiles[x, y].Active = false;
    }

    private static bool InWorld(WorldGenState state, int x, int y)
    {
        return (uint)x < (uint)state.Options.Dimensions.Width &&
            (uint)y < (uint)state.Options.Dimensions.Height;
    }

    private static int RollPyramidMainItem(UnifiedRandom random)
    {
        int roll = random.Next(3);
        if (roll == 0)
        {
            roll = random.Next(3);
        }

        return roll switch
        {
            0 => 848,
            1 => 857,
            _ => 934
        };
    }

    private static IReadOnlyList<PyramidChestItem> RollPyramidChestItems(
        WorldGenState state,
        UnifiedRandom random,
        int mainItem)
    {
        var items = new List<PyramidChestItem>();
        AddItem(items, mainItem);
        ConsumePrefixRoll(random, mainItem);
        if (mainItem == 848)
        {
            AddItem(items, 866);
        }

        if (random.Next(6) == 0)
        {
            AddItem(items, 282, random.Next(40, 76));
        }

        if (random.Next(6) == 0)
        {
            AddItem(items, 279, random.Next(150, 301));
        }

        if (random.Next(6) == 0)
        {
            int stack = 1;
            if (random.Next(5) == 0)
            {
                stack += random.Next(2);
            }

            if (random.Next(10) == 0)
            {
                stack += random.Next(3);
            }

            AddItem(items, 3093, stack);
        }

        if (random.Next(6) == 0)
        {
            int stack = 1;
            if (random.Next(5) == 0)
            {
                stack += random.Next(2);
            }

            if (random.Next(10) == 0)
            {
                stack += random.Next(3);
            }

            AddItem(items, 4345, stack);
        }

        if (random.Next(3) == 0)
        {
            AddItem(items, 168, random.Next(3, 6));
        }

        if (random.Next(2) == 0)
        {
            int item = random.Next(2) == 0 ? state.CopperBar : state.IronBar;
            AddItem(items, item, random.Next(8) + 3);
        }

        if (random.Next(2) == 0)
        {
            AddItem(items, 965, random.Next(50, 101));
        }

        if (random.Next(3) != 0)
        {
            int item = random.Next(2) == 0 ? 40 : 42;
            AddItem(items, item, random.Next(26) + 25);
        }

        if (random.Next(2) == 0)
        {
            AddItem(items, 28, random.Next(3) + 3);
        }

        if (random.Next(3) != 0)
        {
            AddItem(items, 2350, random.Next(3, 6));
        }

        if (random.Next(3) > 0)
        {
            int item = random.Next(6) switch
            {
                0 => 292,
                1 => 298,
                2 => 299,
                3 => 290,
                4 => 2322,
                _ => 2325
            };
            AddItem(items, item, random.Next(1, 3));
        }

        if (random.Next(2) == 0)
        {
            int item = random.Next(2) == 0 ? 8 : 31;
            AddItem(items, item, random.Next(11) + 10);
        }

        if (random.Next(2) == 0)
        {
            AddItem(items, 72, random.Next(10, 30));
        }

        if (random.Next(2) == 0)
        {
            AddItem(items, 9, random.Next(50, 100));
        }

        return items;
    }

    private static void ConsumePrefixRoll(UnifiedRandom random, int itemType)
    {
        if (!IsPyramidPrefixableAccessory(itemType))
        {
            return;
        }

        if (random.Next(4) == 0)
        {
            return;
        }

        _ = random.Next(19);
    }

    private static bool IsPyramidPrefixableAccessory(int itemType)
    {
        return itemType is 857 or 934;
    }

    private static void AddItem(List<PyramidChestItem> items, int type, int stack = 1)
    {
        items.Add(new PyramidChestItem(items.Count, type, stack, Prefix: 0));
    }
}

internal readonly record struct PyramidCandidateAnalysis(
    int Index,
    PyramidCandidate Candidate,
    bool BuildableBand,
    int ScanY,
    bool ScanTileActive,
    int ScanTileType,
    int MinPreviousDistance,
    string Fate);
