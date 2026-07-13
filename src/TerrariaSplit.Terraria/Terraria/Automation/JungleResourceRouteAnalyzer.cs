using System.Drawing;
using System.Globalization;

namespace TerrariaSplit.Terraria.Automation;

internal sealed record JungleResourceRouteOptions(int SolidGap = 5);

internal static class JungleRouteAnalyzer
{
    private const int CellSize = 4;
    private const int CorridorBandStep = 3;
    private const int CorridorBandHalfHeight = 2;
    private const int MinimumCorridorSpan = 5;
    private static readonly (int X, int Y)[] Directions =
    {
        (-1, 0),
        (1, 0),
        (0, -1),
        (0, 1),
        (-1, -1),
        (1, -1),
        (-1, 1),
        (1, 1)
    };

    public static RouteResult Analyze(WorldData world, JungleResourceRouteOptions options)
    {
        bool traceRoute = string.Equals(
            Environment.GetEnvironmentVariable("TERRARIA_SPLIT_ROUTE_TRACE"),
            "1",
            StringComparison.Ordinal);
        TileGrid tiles = world.Tiles;
        JungleSide side = DetermineJungleSide(world);
        JungleCoreBounds jungleCore = FindJungleCoreBounds(world, side);

        int left = Math.Clamp(jungleCore.Left, 0, tiles.Width - 1);
        int right = Math.Clamp(jungleCore.Right, left, tiles.Width - 1);
        int top = Math.Clamp((int)Math.Floor(world.Header.WorldSurface) - 240, 0, tiles.Height - 1);
        int bottom = Math.Clamp(tiles.Height - 180, top, tiles.Height - 1);

        int gridWidth = (tiles.Width + CellSize - 1) / CellSize;
        int gridHeight = (tiles.Height + CellSize - 1) / CellSize;
        int[] surfaceEnvelope = BuildSurfaceEnvelope(world, left, right);
        RouteGrid routeGrid = BuildRouteGrid(world, left, right, top, bottom, gridWidth, gridHeight, surfaceEnvelope, ignoreGeneratedBlockers: true);
        if (traceRoute)
        {
            Console.WriteLine($"TRACE core={left}..{right} center={jungleCore.CenterX} top={top} bottom={bottom} open={routeGrid.Open.Count(value => value)}");
        }
        int[] visited = new int[routeGrid.Open.Length];
        Array.Fill(visited, -1);

        const int jumpCells = 1;
        List<int> bestCells = new();
        List<int> bestRoute = new();
        double bestScore = double.NegativeInfinity;
        int componentId = 0;
        List<List<int>> tracedStrictEntranceComponents = new();
        List<int>? tracedLargestStrictDisconnectedDeepComponent = null;

        for (int i = 0; i < routeGrid.Open.Length; i++)
        {
            if (!routeGrid.Open[i] || visited[i] >= 0)
            {
                continue;
            }

            List<int> cells = FloodComponent(
                routeGrid,
                visited,
                componentId++,
                i,
                gridWidth,
                gridHeight,
                jumpCells,
                requireBoundaryConnection: true);
            ComponentStats stats = ComponentStats.From(cells, gridWidth, world.Header.WorldSurface);
            List<int> surfaceEntrances = FindSurfaceEntranceCells(world, cells, gridWidth, surfaceEnvelope);
            List<int> entrances = CollapseSurfaceEntrances(
                surfaceEntrances,
                gridWidth,
                jungleCore.CenterX);
            if (traceRoute)
            {
                if (entrances.Count > 0)
                {
                    tracedStrictEntranceComponents.Add(cells);
                }
                else if (stats.Depth >= 24 &&
                         (tracedLargestStrictDisconnectedDeepComponent is null ||
                          cells.Count > tracedLargestStrictDisconnectedDeepComponent.Count))
                {
                    tracedLargestStrictDisconnectedDeepComponent = cells;
                }
            }
            if (traceRoute && (entrances.Count > 0 || stats.Depth >= 24))
            {
                Console.WriteLine($"TRACE strict component cells={cells.Count} depth={stats.Depth} x={stats.MinX}..{stats.MaxX} y={stats.MinY}..{stats.MaxY} entrances={entrances.Count}");
            }
            if (entrances.Count == 0 || stats.Depth < 24)
            {
                continue;
            }

            entrances = entrances
                .Where(cell => IsEligibleEntrance(world, cell, gridWidth, jungleCore))
                .ToList();
            int strictEntranceLimit = cells.Count >= 5000
                ? ShouldUseSingleLargeComponentEntrance(entrances, gridWidth, jungleCore) ? 1 : 2
                : 3;
            entrances = entrances.Take(strictEntranceLimit).ToList();
            if (traceRoute && entrances.Count > 0)
            {
                Console.WriteLine("TRACE strict downward entrances=" + string.Join(',', entrances.Select(cell => $"{cell % gridWidth}:{cell / gridWidth}")));
            }
            if (entrances.Count == 0)
            {
                continue;
            }

            bool[] candidateMask = new bool[routeGrid.Open.Length];
            foreach (int cell in cells)
            {
                candidateMask[cell] = true;
            }

            foreach (int entrance in entrances)
            {
                List<int> candidateRoute = ExtractMainPath(
                    world,
                    candidateMask,
                    cells,
                    new List<int> { entrance },
                    gridWidth,
                    gridHeight,
                    jumpCells,
                    jungleCore.CenterX,
                    routeGrid);
                double score = ScoreRouteCandidate(world, candidateRoute, candidateMask, gridWidth, jungleCore, routeGrid, surfaceEnvelope);
                if (traceRoute)
                {
                    Console.WriteLine(DescribeCandidate("strict", world, candidateRoute, gridWidth, jungleCore, routeGrid, surfaceEnvelope, score));
                }
                if (!IsBetterRouteCandidate(score, candidateRoute, bestScore, bestRoute, gridWidth))
                {
                    continue;
                }

                bestScore = score;
                bestCells = cells;
                bestRoute = candidateRoute;
            }
        }

        if (traceRoute &&
            bestRoute.Count > 0 &&
            tracedLargestStrictDisconnectedDeepComponent is not null)
        {
            TraceClosestDisconnectedGap(
                world,
                routeGrid,
                surfaceEnvelope,
                tracedStrictEntranceComponents,
                tracedLargestStrictDisconnectedDeepComponent,
                gridWidth);
        }

        if (bestRoute.Count == 0)
        {
            List<List<int>> tracedLooseEntranceComponents = new();
            List<int>? tracedLargestLooseDeepComponent = null;
            Array.Fill(visited, -1);
            componentId = 0;
            for (int i = 0; i < routeGrid.Open.Length; i++)
            {
                if (!routeGrid.Open[i] || visited[i] >= 0)
                {
                    continue;
                }

                List<int> cells = FloodComponent(
                    routeGrid,
                    visited,
                    componentId++,
                    i,
                    gridWidth,
                    gridHeight,
                    jumpCells,
                    requireBoundaryConnection: true);
                ComponentStats stats = ComponentStats.From(cells, gridWidth, world.Header.WorldSurface);
                List<int> surfaceEntrances = FindSurfaceEntranceCells(world, cells, gridWidth, surfaceEnvelope);
                List<int> entrances = CollapseSurfaceEntrances(
                    surfaceEntrances,
                    gridWidth,
                    jungleCore.CenterX);
                if (traceRoute && (entrances.Count > 0 || stats.Depth >= 24))
                {
                    Console.WriteLine($"TRACE loose component cells={cells.Count} depth={stats.Depth} x={stats.MinX}..{stats.MaxX} y={stats.MinY}..{stats.MaxY} entrances={entrances.Count}");
                    if (entrances.Count > 0)
                    {
                        tracedLooseEntranceComponents.Add(cells);
                    }
                    if (stats.Depth >= 24 && (tracedLargestLooseDeepComponent is null || cells.Count > tracedLargestLooseDeepComponent.Count))
                    {
                        tracedLargestLooseDeepComponent = cells;
                    }
                }
                if (entrances.Count == 0 || stats.Depth < 24)
                {
                    continue;
                }

                entrances = entrances
                    .Where(cell => IsEligibleEntrance(world, cell, gridWidth, jungleCore))
                    .ToList();
                int looseEntranceLimit = cells.Count >= 5000
                    ? ShouldUseSingleLargeComponentEntrance(entrances, gridWidth, jungleCore) ? 1 : 2
                    : 3;
                entrances = entrances.Take(looseEntranceLimit).ToList();
                if (entrances.Count == 0)
                {
                    continue;
                }

                bool[] candidateMask = new bool[routeGrid.Open.Length];
                foreach (int cell in cells)
                {
                    candidateMask[cell] = true;
                }

                foreach (int entrance in entrances)
                {
                    List<int> candidateRoute = FindBestNaturalPath(
                        world,
                        routeGrid,
                        candidateMask,
                        entrance,
                        gridWidth,
                        gridHeight,
                        jumpCells,
                        jungleCore.CenterX,
                        requireBoundaryConnection: true);
                    double score = ScoreRouteCandidate(world, candidateRoute, candidateMask, gridWidth, jungleCore, routeGrid, surfaceEnvelope);
                    if (traceRoute)
                    {
                        Console.WriteLine(DescribeCandidate("loose-natural", world, candidateRoute, gridWidth, jungleCore, routeGrid, surfaceEnvelope, score));
                    }
                    if (double.IsNegativeInfinity(score))
                    {
                        int target = ChooseAlignedDeepTarget(cells, gridWidth, entrance, jungleCore.CenterX);
                        candidateRoute = FindWeightedPath(
                            world,
                            routeGrid,
                            candidateMask,
                            entrance,
                            target,
                            gridWidth,
                            gridHeight,
                            jumpCells,
                            routeDistance: null,
                            requireBoundaryConnection: true);
                        score = ScoreRouteCandidate(world, candidateRoute, candidateMask, gridWidth, jungleCore, routeGrid, surfaceEnvelope);
                        if (traceRoute)
                        {
                            Console.WriteLine(DescribeCandidate("loose-deep", world, candidateRoute, gridWidth, jungleCore, routeGrid, surfaceEnvelope, score));
                        }
                    }
                    if (!IsBetterRouteCandidate(score, candidateRoute, bestScore, bestRoute, gridWidth))
                    {
                        continue;
                    }

                    bestScore = score;
                    bestCells = cells;
                    bestRoute = candidateRoute;
                }
            }

            if (traceRoute && bestRoute.Count == 0 && tracedLargestLooseDeepComponent is not null)
            {
                TraceClosestDisconnectedGap(
                    world,
                    routeGrid,
                    surfaceEnvelope,
                    tracedLooseEntranceComponents,
                    tracedLargestLooseDeepComponent,
                    gridWidth);
            }
        }
        bool[] componentMask = new bool[routeGrid.Open.Length];
        if (bestCells.Count == 0 || bestRoute.Count == 0)
        {
            if (traceRoute)
            {
                Console.WriteLine("TRACE result=empty");
            }
            return new RouteResult(side.ToString(), componentMask, gridWidth, gridHeight, CellSize, 0, 0, 0, Array.Empty<RouteBridge>(), routeGrid.PassableMasks);
        }

        foreach (int cell in bestCells)
        {
            componentMask[cell] = true;
        }

        const int detourMarginTiles = 72 * CellSize;
        int routeLeft = Math.Max(left, bestRoute.Min(cell => cell % gridWidth) * CellSize - detourMarginTiles);
        int routeRight = Math.Min(right, (bestRoute.Max(cell => cell % gridWidth) + 1) * CellSize + detourMarginTiles);
        int routeTop = Math.Max(top, bestRoute.Min(cell => cell / gridWidth) * CellSize - detourMarginTiles);
        int routeBottom = Math.Min(bottom, (bestRoute.Max(cell => cell / gridWidth) + 1) * CellSize + detourMarginTiles);
        RouteGrid actualGrid = BuildRouteGrid(
            world,
            routeLeft,
            routeRight,
            routeTop,
            routeBottom,
            gridWidth,
            gridHeight,
            surfaceEnvelope,
            ignoreGeneratedBlockers: false);
        List<int> routeCells = ApplyGeneratedObstacleDetours(
            world,
            bestRoute,
            actualGrid,
            gridWidth,
            gridHeight,
            jumpCells,
            jungleCore.CenterX);
        bool[] routeMask = new bool[routeGrid.Open.Length];
        foreach (int cell in routeCells)
        {
            routeMask[cell] = true;
        }

        List<RouteBridge> bridges = new();
        Point deepest = FindDeepestPoint(routeCells, gridWidth, tiles.Width, tiles.Height);
        return new RouteResult(
            side.ToString(),
            routeMask,
            gridWidth,
            gridHeight,
            CellSize,
            CountTrue(routeMask),
            deepest.X,
            deepest.Y,
            bridges,
            actualGrid.PassableMasks);
    }

    private static int FindFirstBlockerIndex(WorldData world, IReadOnlyList<int> routeCells, int gridWidth)
    {
        for (int i = 0; i < routeCells.Count; i++)
        {
            int cell = routeCells[i];
            int cellX = cell % gridWidth;
            int cellY = cell / gridWidth;
            int x0 = cellX * CellSize;
            int y0 = cellY * CellSize;
            for (int x = x0; x < Math.Min(world.Tiles.Width, x0 + CellSize); x++)
            {
                for (int y = y0; y < Math.Min(world.Tiles.Height, y0 + CellSize); y++)
                {
                    RouteBlockerKind kind = GetRouteBlockingTileKind(world.Tiles, x, y);
                    if (kind != RouteBlockerKind.None)
                    {
                        return i;
                    }
                }
            }
        }

        return -1;
    }

    private static List<int> ApplyGeneratedObstacleDetours(
        WorldData world,
        List<int> originalRoute,
        RouteGrid actualGrid,
        int gridWidth,
        int gridHeight,
        int jumpCells,
        int jungleCenterX)
    {
        bool traceRoute = string.Equals(
            Environment.GetEnvironmentVariable("TERRARIA_SPLIT_ROUTE_TRACE"),
            "1",
            StringComparison.Ordinal);
        int firstBlocked = FindFirstBlockerIndex(world, originalRoute, gridWidth);
        if (firstBlocked < 0)
        {
            return originalRoute;
        }
        RouteBlockerKind blockerKind = GetRouteCellBlockerKind(world, originalRoute[firstBlocked], gridWidth);

        const int maxDetourDistance = 72;
        int[] routeDistance = BuildRouteDistance(originalRoute, gridWidth, gridHeight, maxDetourDistance);
        bool[] allowed = new bool[actualGrid.Open.Length];
        for (int cell = 0; cell < allowed.Length; cell++)
        {
            allowed[cell] = actualGrid.Open[cell] && routeDistance[cell] <= maxDetourDistance;
        }

        int start = originalRoute[0];
        int originalTarget = originalRoute[^1];
        int target = actualGrid.Open[originalTarget]
            ? originalTarget
            : ChooseOpenDetourTarget(
                allowed,
                routeDistance,
                gridWidth,
                originalTarget,
                originalRoute[firstBlocked] / gridWidth);
        if (traceRoute)
        {
            Console.WriteLine(
                $"TRACE detour originalCells={originalRoute.Count} firstBlocked={firstBlocked} " +
                $"blocked={originalRoute[firstBlocked] % gridWidth}:{originalRoute[firstBlocked] / gridWidth} " +
                $"originalTarget={originalTarget % gridWidth}:{originalTarget / gridWidth} target=" +
                (target < 0 ? "none" : $"{target % gridWidth}:{target / gridWidth}"));
        }
        allowed[start] = true;
        if (target >= 0)
        {
            List<int> detour = FindWeightedPath(
                world,
                actualGrid,
                allowed,
                start,
                target,
                gridWidth,
                gridHeight,
                jumpCells,
                routeDistance,
                requireBoundaryConnection: true);
            int detourHorizontalMoves = CountHorizontalMoves(detour, gridWidth);
            int originalHorizontalMoves = CountHorizontalMoves(originalRoute, gridWidth);
            if (traceRoute)
            {
                Console.WriteLine(
                    $"TRACE detour-result cells={detour.Count} horizontal={detourHorizontalMoves} " +
                    $"cellLimit={originalRoute.Count * 1.7d + 40d:F1} horizontalLimit={originalHorizontalMoves + 28}");
            }
            if (detour.Count > 0 &&
                detour.Count <= originalRoute.Count * 1.7d + 40d &&
                detourHorizontalMoves <= originalHorizontalMoves + 28)
            {
                return detour;
            }
        }

        int branchIndex = Math.Max(0, firstBlocked - 30);
        while (branchIndex > 0 && !actualGrid.Open[originalRoute[branchIndex]])
        {
            branchIndex--;
        }
        int branchStart = originalRoute[branchIndex];
        allowed[branchStart] = true;
        List<int> alternateBranch = FindBestNaturalPath(
            world,
            actualGrid,
            allowed,
            branchStart,
            gridWidth,
            gridHeight,
            jumpCells,
            jungleCenterX,
            requireBoundaryConnection: true,
            preferDeepestReachable: true);
        int blockedRow = originalRoute[firstBlocked] / gridWidth;
        if (alternateBranch.Count > 0 && alternateBranch[^1] / gridWidth >= blockedRow + 8)
        {
            List<int> combined = originalRoute.Take(branchIndex).ToList();
            combined.AddRange(alternateBranch);
            int combinedHorizontalMoves = CountHorizontalMoves(combined, gridWidth);
            int originalHorizontalMoves = CountHorizontalMoves(originalRoute, gridWidth);
            if (combined.Count <= originalRoute.Count * 2d + 80d &&
                combinedHorizontalMoves <= originalHorizontalMoves + 60)
            {
                if (traceRoute)
                {
                    Console.WriteLine(
                        $"TRACE detour-rebranch accepted branchIndex={branchIndex} cells={combined.Count} " +
                        $"end={combined[^1] % gridWidth}:{combined[^1] / gridWidth}");
                }
                return combined;
            }
        }
        if (traceRoute)
        {
            Console.WriteLine(
                $"TRACE detour-rebranch rejected branchIndex={branchIndex} cells={alternateBranch.Count} " +
                "end=" + (alternateBranch.Count == 0 ? "none" : $"{alternateBranch[^1] % gridWidth}:{alternateBranch[^1] / gridWidth}"));
        }

        if (traceRoute)
        {
            Console.WriteLine("TRACE detour-result rejected; truncating at first blocker");
        }

        return originalRoute.Take(firstBlocked + 1).ToList();
    }

    private static RouteBlockerKind GetRouteCellBlockerKind(WorldData world, int cell, int gridWidth)
    {
        int cellX = cell % gridWidth;
        int cellY = cell / gridWidth;
        int x0 = cellX * CellSize;
        int y0 = cellY * CellSize;
        for (int x = x0; x < Math.Min(world.Tiles.Width, x0 + CellSize); x++)
        {
            for (int y = y0; y < Math.Min(world.Tiles.Height, y0 + CellSize); y++)
            {
                RouteBlockerKind kind = GetRouteBlockingTileKind(world.Tiles, x, y);
                if (kind != RouteBlockerKind.None)
                {
                    return kind;
                }
            }
        }

        return RouteBlockerKind.None;
    }

    private static int ChooseOpenDetourTarget(
        bool[] allowed,
        int[] routeDistance,
        int gridWidth,
        int originalTarget,
        int blockedRow)
    {
        int originalTargetX = originalTarget % gridWidth;
        int originalTargetY = originalTarget / gridWidth;
        int best = -1;
        double bestScore = double.NegativeInfinity;
        for (int cell = 0; cell < allowed.Length; cell++)
        {
            if (!allowed[cell])
            {
                continue;
            }

            int y = cell / gridWidth;
            if (y <= blockedRow || y > originalTargetY + 4)
            {
                continue;
            }

            int x = cell % gridWidth;
            double score =
                y * 100d -
                Math.Abs(x - originalTargetX) * 2d -
                routeDistance[cell] * 3d;
            if (score > bestScore)
            {
                bestScore = score;
                best = cell;
            }
        }

        return best;
    }

    private static int[] BuildRouteDistance(IReadOnlyList<int> route, int gridWidth, int gridHeight, int maxDistance)
    {
        int[] distance = new int[gridWidth * gridHeight];
        Array.Fill(distance, int.MaxValue);
        Queue<int> queue = new();
        foreach (int cell in route)
        {
            if (distance[cell] == 0)
            {
                continue;
            }

            distance[cell] = 0;
            queue.Enqueue(cell);
        }

        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            int nextDistance = distance[cell] + 1;
            if (nextDistance > maxDistance)
            {
                continue;
            }

            int cx = cell % gridWidth;
            int cy = cell / gridWidth;
            foreach ((int dx, int dy) in Directions)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= gridWidth || ny >= gridHeight)
                {
                    continue;
                }

                int next = ny * gridWidth + nx;
                if (nextDistance >= distance[next])
                {
                    continue;
                }

                distance[next] = nextDistance;
                queue.Enqueue(next);
            }
        }

        return distance;
    }

    private static int CountHorizontalMoves(IReadOnlyList<int> route, int gridWidth)
    {
        int count = 0;
        for (int i = 1; i < route.Count; i++)
        {
            if (route[i] / gridWidth == route[i - 1] / gridWidth)
            {
                count++;
            }
        }

        return count;
    }

    private static RouteBlockerKind GetRouteBlockingTileKind(TileGrid tiles, int x, int y)
    {
        int index = tiles.Index(x, y);
        if (!tiles.Active[index])
        {
            return RouteBlockerKind.None;
        }

        return tiles.Type[index] switch
        {
            TileIds.LihzahrdBrick => RouteBlockerKind.Temple,
            TileIds.Hive => RouteBlockerKind.Hive,
            _ => RouteBlockerKind.None
        };
    }

    private static bool TryAnalyzeMainJungleCorridor(
        WorldData world,
        JungleResourceRouteOptions options,
        JungleSide side,
        JungleCoreBounds jungleCore,
        int top,
        int bottom,
        int gridWidth,
        int gridHeight,
        out RouteResult? route)
    {
        route = null;
        (int searchLeft, int searchRight) = BuildCorridorSearchBounds(world, jungleCore);
        List<List<CorridorNode>> rows = BuildCorridorRows(world, options, jungleCore, searchLeft, searchRight, top, bottom);
        if (rows.Sum(row => row.Count) < 12)
        {
            return false;
        }

        LinkCorridorRows(rows, world, world.Header.WorldSurface, options.SolidGap, jungleCore.CenterX);
        CorridorNode? best = ChooseTempleApproach(rows, world) ??
            ChooseBestCorridor(rows, world.Header.WorldSurface, jungleCore.CenterX);
        if (best is null)
        {
            return false;
        }

        List<CorridorNode> path = BuildCorridorPath(best);
        int pathSpan = path[^1].Y - path[0].Y;
        if (path.Count < 8 || pathSpan < 90 || !path[0].SurfaceEntrance)
        {
            return false;
        }

        List<Point> pathCenters = path.Select(node => new Point(node.CenterX, node.Y)).ToList();
        RouteBlocker? blocker = FindFirstRouteBlocker(world, pathCenters);
        int requiredDepth = (int)Math.Round(world.Header.WorldSurface) + 240;
        if (blocker is null && path[^1].Y < requiredDepth)
        {
            return false;
        }

        if (blocker is not null)
        {
            path = path.Where(node => node.Y < blocker.Value.TopY - 1).ToList();
            if (path.Count < 3)
            {
                return false;
            }
        }

        bool[] routeMask = new bool[gridWidth * gridHeight];
        List<RouteBridge> bridges = new();
        CorridorNode start = path[0];
        MarkRouteLine(routeMask, gridWidth, gridHeight, start.SurfaceX, start.SurfaceY, start.CenterX, start.Y);
        MarkSurfaceEntrance(routeMask, gridWidth, gridHeight, start.SurfaceX, start.SurfaceY);
        for (int i = 0; i < path.Count; i++)
        {
            MarkCorridorNode(routeMask, gridWidth, gridHeight, path[i]);
            if (i == 0)
            {
                continue;
            }

            CorridorNode previous = path[i - 1];
            CorridorNode current = path[i];
            MarkRouteLine(routeMask, gridWidth, gridHeight, previous.CenterX, previous.Y, current.CenterX, current.Y);
        }

        int deepestX = path[^1].CenterX;
        int deepestY = path[^1].Y;
        if (blocker is not null)
        {
            deepestX = blocker.Value.X;
            deepestY = Math.Max(path[^1].Y, blocker.Value.TopY - 1);
            MarkRouteLine(routeMask, gridWidth, gridHeight, path[^1].CenterX, path[^1].Y, deepestX, deepestY);
        }

        int routeCellCount = CountTrue(routeMask);
        if (routeCellCount == 0)
        {
            return false;
        }

        route = new RouteResult(
            side.ToString(),
            routeMask,
            gridWidth,
            gridHeight,
            CellSize,
            routeCellCount,
            deepestX,
            deepestY,
            bridges);
        return true;
    }

    private static RouteBlocker? FindFirstRouteBlocker(WorldData world, IReadOnlyList<Point> path)
    {
        RouteBlocker? templeBlocker = FindTempleBlocker(world, path);
        if (templeBlocker is not null)
        {
            return templeBlocker;
        }

        int minimumY = (int)Math.Round(world.Header.WorldSurface) + 20;
        for (int i = 0; i < path.Count; i++)
        {
            Point node = path[i];
            if (node.Y < minimumY)
            {
                continue;
            }

            int historyIndex = Math.Max(0, i - 6);
            Point history = path[historyIndex];
            double slope = node.Y == history.Y
                ? 0d
                : Math.Clamp((node.X - history.X) / (double)(node.Y - history.Y), -0.65d, 0.65d);
            int templeRows = 0;
            int hiveRows = 0;
            int firstTempleY = -1;
            int firstHiveY = -1;
            for (int probeY = node.Y + 3; probeY <= Math.Min(world.Tiles.Height - 1, node.Y + 54); probeY++)
            {
                int projectedX = Math.Clamp(
                    node.X + (int)Math.Round((probeY - node.Y) * slope),
                    0,
                    world.Tiles.Width - 1);
                int templeCount = 0;
                int hiveCount = 0;
                for (int x = Math.Max(0, projectedX - 8); x <= Math.Min(world.Tiles.Width - 1, projectedX + 8); x++)
                {
                    RouteBlockerKind kind = GetRouteBlockerKind(world.Tiles, x, probeY);
                    templeCount += kind == RouteBlockerKind.Temple ? 1 : 0;
                    hiveCount += kind == RouteBlockerKind.Hive ? 1 : 0;
                }

                if (templeCount >= 6)
                {
                    firstTempleY = firstTempleY < 0 ? probeY : firstTempleY;
                    templeRows++;
                    if (templeRows >= 2)
                    {
                        return new RouteBlocker(projectedX, firstTempleY, RouteBlockerKind.Temple);
                    }
                }
                else
                {
                    templeRows = 0;
                    firstTempleY = -1;
                }

                if (hiveCount >= 6)
                {
                    firstHiveY = firstHiveY < 0 ? probeY : firstHiveY;
                    hiveRows++;
                    if (hiveRows >= 3)
                    {
                        return new RouteBlocker(projectedX, firstHiveY, RouteBlockerKind.Hive);
                    }
                }
                else
                {
                    hiveRows = 0;
                    firstHiveY = -1;
                }
            }
        }

        return null;
    }

    private static RouteBlocker? FindTempleBlocker(WorldData world, IReadOnlyList<Point> path)
    {
        RouteBlockerBounds? bounds = FindTempleBounds(world);
        if (bounds is null)
        {
            return null;
        }

        int left = bounds.Value.Left;
        int right = bounds.Value.Right;
        int top = bounds.Value.Top;
        if (top <= path[0].Y || top >= path[^1].Y)
        {
            return null;
        }

        int anchorLimit = top - 60;
        int anchorIndex = -1;
        for (int i = 0; i < path.Count; i++)
        {
            if (path[i].Y <= anchorLimit)
            {
                anchorIndex = i;
            }
        }

        if (anchorIndex < 0)
        {
            return null;
        }

        Point anchor = path[anchorIndex];
        Point history = path[Math.Max(0, anchorIndex - 10)];
        double slope = anchor.Y == history.Y
            ? 0d
            : Math.Clamp((anchor.X - history.X) / (double)(anchor.Y - history.Y), -0.45d, 0.45d);
        int projectedX = anchor.X + (int)Math.Round((top - anchor.Y) * slope);
        int margin = Math.Clamp((right - left + 1) / 2, 100, 220);
        if (projectedX < left - margin || projectedX > right + margin)
        {
            int aboveIndex = -1;
            int belowIndex = -1;
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i].Y <= top - 8)
                {
                    aboveIndex = i;
                }
                else if (belowIndex < 0 && path[i].Y >= Math.Min(bounds.Value.Bottom, top + 80))
                {
                    belowIndex = i;
                }
            }

            if (aboveIndex < 0 || belowIndex <= aboveIndex)
            {
                return null;
            }

            Point above = path[aboveIndex];
            Point below = path[belowIndex];
            double t = (top - above.Y) / (double)Math.Max(1, below.Y - above.Y);
            projectedX = above.X + (int)Math.Round((below.X - above.X) * t);
            if (projectedX < left - margin || projectedX > right + margin)
            {
                return null;
            }
        }

        return new RouteBlocker(Math.Clamp(projectedX, left, right), top, RouteBlockerKind.Temple);
    }

    private static RouteBlockerBounds? FindTempleBounds(WorldData world)
    {
        int left = world.Tiles.Width;
        int right = -1;
        int top = world.Tiles.Height;
        int bottom = -1;
        for (int x = 0; x < world.Tiles.Width; x++)
        {
            for (int y = (int)Math.Round(world.Header.WorldSurface); y < world.Tiles.Height; y++)
            {
                if (GetRouteBlockerKind(world.Tiles, x, y) != RouteBlockerKind.Temple)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return right >= left && bottom >= top && right - left >= 30 && bottom - top >= 20
            ? new RouteBlockerBounds(left, right, top, bottom)
            : null;
    }

    private static RouteBlockerKind GetRouteBlockerKind(TileGrid tiles, int x, int y)
    {
        int index = tiles.Index(x, y);
        if (tiles.Wall[index] == WallIds.LihzahrdBrick ||
            (tiles.Active[index] && tiles.Type[index] == TileIds.LihzahrdBrick))
        {
            return RouteBlockerKind.Temple;
        }

        if (tiles.Wall[index] is WallIds.Hive or WallIds.HiveSafe ||
            (tiles.Active[index] && tiles.Type[index] is
                TileIds.Hive or
                TileIds.HoneyBlock or
                TileIds.CrispyHoneyBlock or
                TileIds.BeeHive))
        {
            return RouteBlockerKind.Hive;
        }

        return RouteBlockerKind.None;
    }

    private static (int Left, int Right) BuildCorridorSearchBounds(WorldData world, JungleCoreBounds jungleCore)
    {
        int coreWidth = jungleCore.Right - jungleCore.Left + 1;
        int edgeInset = Math.Clamp(coreWidth / 5, 90, 260);
        int halfWidth = Math.Clamp((int)Math.Round(coreWidth * 0.36d), 260, 640);
        int left = Math.Max(jungleCore.Left + edgeInset, jungleCore.CenterX - halfWidth);
        int right = Math.Min(jungleCore.Right - edgeInset, jungleCore.CenterX + halfWidth);
        if (right - left + 1 < 260)
        {
            left = Math.Max(jungleCore.Left, jungleCore.CenterX - 180);
            right = Math.Min(jungleCore.Right, jungleCore.CenterX + 180);
        }

        return (
            Math.Clamp(left, 0, world.Tiles.Width - 1),
            Math.Clamp(right, 0, world.Tiles.Width - 1));
    }

    private static List<List<CorridorNode>> BuildCorridorRows(
        WorldData world,
        JungleResourceRouteOptions options,
        JungleCoreBounds jungleCore,
        int searchLeft,
        int searchRight,
        int top,
        int bottom)
    {
        List<List<CorridorNode>> rows = new();
        int rowIndex = 0;
        for (int y = top; y <= bottom; y += CorridorBandStep)
        {
            bool[] open = BuildBandOpenMask(world, searchLeft, searchRight, y);
            FillSmallSolidGaps(open, options.SolidGap);
            List<CorridorNode> row = new();
            foreach ((int localLeft, int localRight) in ExtractOpenIntervals(open))
            {
                int left = searchLeft + localLeft;
                int right = searchLeft + localRight;
                int width = right - left + 1;
                int minimumWidth = MinimumCorridorSpan;
                if (width < minimumWidth)
                {
                    continue;
                }

                int centerX = (left + right) / 2;
                double jungleContext = EstimateJungleContext(world, centerX, y, width);
                if (jungleContext < 7.5d)
                {
                    continue;
                }

                double centerDistance = Math.Abs(centerX - jungleCore.CenterX);
                SurfaceEntrance surfaceEntrance = EvaluateSurfaceEntrance(world, left, right, y);
                double widthQuality = 90d - Math.Abs(width - 9) * 4.5d - Math.Max(0, width - 28) * 6d;
                double quality =
                    widthQuality +
                    Math.Min(jungleContext, 85d) * 1.7d -
                    centerDistance * 0.045d +
                    (surfaceEntrance.IsEntrance ? 120d : 0d);
                row.Add(new CorridorNode(rowIndex, y, left, right, jungleContext, quality, surfaceEntrance));
            }

            if (row.Count > 0)
            {
                rows.Add(row);
            }

            rowIndex++;
        }

        return rows;
    }

    private static bool[] BuildBandOpenMask(WorldData world, int left, int right, int y)
    {
        int width = Math.Max(0, right - left + 1);
        bool[] open = new bool[width];
        int y0 = Math.Max(0, y - CorridorBandHalfHeight);
        int y1 = Math.Min(world.Tiles.Height - 1, y + CorridorBandHalfHeight);
        int sampleCount = y1 - y0 + 1;
        int requiredPassable = Math.Max(1, (sampleCount + 1) / 2);
        for (int localX = 0; localX < width; localX++)
        {
            int x = left + localX;
            int passable = 0;
            for (int sampleY = y0; sampleY <= y1; sampleY++)
            {
                if (IsPassable(world.Tiles, x, sampleY))
                {
                    passable++;
                }
            }

            open[localX] = passable >= requiredPassable;
        }

        return open;
    }

    private static void FillSmallSolidGaps(bool[] open, int maxGap)
    {
        if (maxGap <= 0)
        {
            return;
        }

        int i = 0;
        while (i < open.Length)
        {
            if (open[i])
            {
                i++;
                continue;
            }

            int start = i;
            while (i < open.Length && !open[i])
            {
                i++;
            }

            int length = i - start;
            if (length <= maxGap && start > 0 && i < open.Length)
            {
                for (int x = start; x < i; x++)
                {
                    open[x] = true;
                }
            }
        }
    }

    private static IEnumerable<(int Left, int Right)> ExtractOpenIntervals(bool[] open)
    {
        int x = 0;
        while (x < open.Length)
        {
            while (x < open.Length && !open[x])
            {
                x++;
            }

            if (x >= open.Length)
            {
                yield break;
            }

            int left = x;
            while (x < open.Length && open[x])
            {
                x++;
            }

            yield return (left, x - 1);
        }
    }

    private static double EstimateJungleContext(WorldData world, int centerX, int y, int corridorWidth)
    {
        int radiusX = Math.Clamp(corridorWidth / 2 + 58, 64, 110);
        int radiusY = 46;
        double score = 0;
        for (int sampleY = Math.Max(0, y - radiusY); sampleY <= Math.Min(world.Tiles.Height - 1, y + radiusY); sampleY += 6)
        {
            for (int sampleX = Math.Max(0, centerX - radiusX); sampleX <= Math.Min(world.Tiles.Width - 1, centerX + radiusX); sampleX += 6)
            {
                int index = world.Tiles.Index(sampleX, sampleY);
                if (!world.Tiles.Active[index])
                {
                    continue;
                }

                ushort type = world.Tiles.Type[index];
                if (IsJungleMaterial(type))
                {
                    score += type is TileIds.JungleGrass or TileIds.Hive or TileIds.BeeHive or TileIds.LivingMahogany ? 1.9d : 1d;
                }
                else if (type == TileIds.LihzahrdBrick)
                {
                    score += 0.35d;
                }
            }
        }

        return score;
    }

    private static SurfaceEntrance EvaluateSurfaceEntrance(WorldData world, int left, int right, int y)
    {
        if (y < world.Header.WorldSurface - 55d || y > world.Header.WorldSurface + 115d || right - left + 1 > 54)
        {
            return new SurfaceEntrance(false, (left + right) / 2, (int)Math.Round(world.Header.WorldSurface), 0);
        }

        int bestX = (left + right) / 2;
        int bestSurfaceY = -1;
        int bestClearDepth = int.MinValue;
        int entranceVotes = 0;
        int step = Math.Max(1, Math.Min(3, (right - left + 1) / 10));
        for (int x = left; x <= right; x += step)
        {
            int clearBottom = FindOpenSkyBottom(world, x, Math.Min(world.Tiles.Height - 1, y + 100));
            if (clearBottom < y + CorridorBandHalfHeight)
            {
                continue;
            }

            entranceVotes++;
            if (clearBottom > bestClearDepth ||
                (clearBottom == bestClearDepth && Math.Abs(x - (left + right) / 2) < Math.Abs(bestX - (left + right) / 2)))
            {
                bestX = x;
                bestSurfaceY = Math.Min(clearBottom, y);
                bestClearDepth = clearBottom;
            }
        }

        int requiredVotes = Math.Max(2, (right - left + 1) / Math.Max(1, step) / 5);
        bool isEntrance = entranceVotes >= requiredVotes;
        return new SurfaceEntrance(isEntrance, bestX, bestSurfaceY, entranceVotes);
    }

    private static int FindOpenSkyBottom(WorldData world, int x, int maxY)
    {
        int startY = 20;
        int endY = Math.Clamp(maxY, startY, Math.Min(world.Tiles.Height - 1, (int)world.Header.WorldSurface + 420));
        for (int y = startY; y <= endY; y++)
        {
            int index = world.Tiles.Index(x, y);
            if (!IsPassable(world.Tiles, x, y))
            {
                return y;
            }
        }

        return endY;
    }

    private static void LinkCorridorRows(List<List<CorridorNode>> rows, WorldData world, double worldSurface, int solidGap, int jungleCenterX)
    {
        int maxVerticalSkip = Math.Max(CorridorBandStep + Math.Max(0, solidGap), 24);
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            foreach (CorridorNode node in rows[rowIndex])
            {
                if (node.SurfaceEntrance)
                {
                    double surfaceDistance = Math.Abs(node.Y - worldSurface);
                    node.StartY = node.Y;
                    node.NodeCount = 1;
                    node.WidthSum = node.Width;
                    node.JungleSum = node.JungleContext;
                    node.CenterDistanceSum = Math.Abs(node.CenterX - jungleCenterX);
                    node.BestScore = node.Quality + Math.Max(0d, 180d - surfaceDistance) * 0.35d;
                }

                for (int previousRowIndex = rowIndex - 1; previousRowIndex >= 0; previousRowIndex--)
                {
                    List<CorridorNode> previousRow = rows[previousRowIndex];
                    if (node.Y - previousRow[0].Y > maxVerticalSkip)
                    {
                        break;
                    }

                    foreach (CorridorNode previous in previousRow)
                    {
                        if (!previous.HasScore || !AreCorridorNodesCompatible(world, previous, node, solidGap))
                        {
                            continue;
                        }

                        int overlap = Math.Max(0, Math.Min(previous.Right, node.Right) - Math.Max(previous.Left, node.Left) + 1);
                        int shift = Math.Abs(previous.CenterX - node.CenterX);
                        double score =
                            previous.BestScore +
                            node.Quality +
                            (node.Y - previous.Y) * 8d +
                            Math.Min(overlap, 80) * 0.45d -
                            shift * 0.85d;
                        if (score > node.BestScore)
                        {
                            node.Previous = previous;
                            node.StartY = previous.StartY;
                            node.NodeCount = previous.NodeCount + 1;
                            node.WidthSum = previous.WidthSum + node.Width;
                            node.JungleSum = previous.JungleSum + node.JungleContext;
                            node.CenterDistanceSum = previous.CenterDistanceSum + Math.Abs(node.CenterX - jungleCenterX);
                            node.BestScore = score;
                        }
                    }
                }
            }
        }
    }

    private static bool AreCorridorNodesCompatible(WorldData world, CorridorNode previous, CorridorNode current, int solidGap)
    {
        int overlap = Math.Min(previous.Right, current.Right) - Math.Max(previous.Left, current.Left) + 1;
        if (overlap >= -solidGap)
        {
            return true;
        }

        int centerShift = Math.Abs(previous.CenterX - current.CenterX);
        int maxShift = Math.Clamp(Math.Max(previous.Width, current.Width) + 6, 14, 36);
        return centerShift <= maxShift && PassageFitsSolidGap(world, previous.CenterX, previous.Y, current.CenterX, current.Y, solidGap);
    }

    private static bool PassageFitsSolidGap(WorldData world, int x1, int y1, int x2, int y2, int solidGap)
    {
        int dx = x2 - x1;
        int dy = y2 - y1;
        int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (steps == 0)
        {
            return true;
        }

        int solidRun = 0;
        for (int step = 0; step <= steps; step++)
        {
            int x = Math.Clamp(x1 + (int)Math.Round(dx * (step / (double)steps)), 0, world.Tiles.Width - 1);
            int y = Math.Clamp(y1 + (int)Math.Round(dy * (step / (double)steps)), 0, world.Tiles.Height - 1);
            if (IsPassable(world.Tiles, x, y))
            {
                solidRun = 0;
                continue;
            }

            solidRun++;
            if (solidRun > solidGap)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CorridorIntervalsTouch(CorridorNode previous, CorridorNode current, int solidGap)
    {
        int overlap = Math.Min(previous.Right, current.Right) - Math.Max(previous.Left, current.Left) + 1;
        return overlap >= -solidGap;
    }

    private static CorridorNode? ChooseBestCorridor(List<List<CorridorNode>> rows, double worldSurface, int jungleCenterX)
    {
        CorridorNode? best = null;
        double bestScore = double.NegativeInfinity;
        foreach (List<CorridorNode> row in rows)
        {
            foreach (CorridorNode node in row)
            {
                if (!node.HasScore || node.NodeCount < 8)
                {
                    continue;
                }

                int span = node.Y - node.StartY;
                if (span < 90)
                {
                    continue;
                }

                double averageWidth = node.WidthSum / node.NodeCount;
                double averageJungle = node.JungleSum / node.NodeCount;
                double averageCenterDistance = node.CenterDistanceSum / node.NodeCount;
                double surfaceStartPenalty = Math.Abs(node.StartY - worldSurface) * 0.32d;
                double widthScore = 120d - Math.Abs(averageWidth - 10d) * 10d - Math.Max(0d, averageWidth - 26d) * 8d;
                double score =
                    span * 9.2d +
                    widthScore +
                    averageJungle * 4.8d +
                    node.NodeCount * 3.5d -
                    averageCenterDistance * 0.72d -
                    surfaceStartPenalty +
                    node.Y * 0.04d;
                if (Math.Abs(node.CenterX - jungleCenterX) > 520)
                {
                    score -= 240d;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = node;
                }
            }
        }

        return best;
    }

    private static CorridorNode? ChooseTempleApproach(List<List<CorridorNode>> rows, WorldData world)
    {
        RouteBlockerBounds? bounds = FindTempleBounds(world);
        if (bounds is null)
        {
            return null;
        }

        int templeCenterX = (bounds.Value.Left + bounds.Value.Right) / 2;
        int margin = Math.Clamp((bounds.Value.Right - bounds.Value.Left + 1) / 10, 18, 42);
        CorridorNode? best = null;
        double bestScore = double.NegativeInfinity;
        foreach (CorridorNode node in rows.SelectMany(row => row))
        {
            if (!node.HasScore || node.NodeCount < 8)
            {
                continue;
            }

            int span = node.Y - node.StartY;
            int roofDistance = bounds.Value.Top - node.Y;
            if (span < 90 || roofDistance < 2 || roofDistance > 150 ||
                node.CenterX < bounds.Value.Left - margin || node.CenterX > bounds.Value.Right + margin)
            {
                continue;
            }

            double averageWidth = node.WidthSum / node.NodeCount;
            double score =
                span * 12d -
                roofDistance * 5d -
                Math.Abs(node.CenterX - templeCenterX) * 0.65d -
                Math.Abs(averageWidth - 10d) * 10d +
                node.JungleSum / node.NodeCount * 2d;
            if (score > bestScore)
            {
                bestScore = score;
                best = node;
            }
        }

        return best;
    }

    private static List<CorridorNode> BuildCorridorPath(CorridorNode end)
    {
        List<CorridorNode> path = new();
        for (CorridorNode? node = end; node is not null; node = node.Previous)
        {
            path.Add(node);
        }

        path.Reverse();
        return path;
    }

    private static List<CorridorBranch> SelectCorridorBranches(List<List<CorridorNode>> rows, List<CorridorNode> mainPath, int solidGap)
    {
        HashSet<CorridorNode> mainNodes = new(mainPath);
        List<CorridorBranch> candidates = new();
        foreach (CorridorNode node in rows.SelectMany(row => row))
        {
            if (mainNodes.Contains(node) || !node.HasScore)
            {
                continue;
            }

            List<CorridorNode> chain = BuildCorridorPath(node);
            int attachIndex = -1;
            for (int i = chain.Count - 2; i >= 0; i--)
            {
                if (mainNodes.Contains(chain[i]))
                {
                    attachIndex = i;
                    break;
                }
            }

            if (attachIndex < 0 || attachIndex >= chain.Count - 1)
            {
                continue;
            }

            CorridorNode attach = chain[attachIndex];
            List<CorridorNode> branchNodes = new();
            for (int i = attachIndex + 1; i < chain.Count && branchNodes.Count < 24; i++)
            {
                if (mainNodes.Contains(chain[i]))
                {
                    break;
                }

                branchNodes.Add(chain[i]);
            }

            if (branchNodes.Count < 4)
            {
                continue;
            }

            int minY = branchNodes.Min(item => item.Y);
            int maxY = branchNodes.Max(item => item.Y);
            int span = maxY - minY;
            int maxLateral = branchNodes.Max(item => Math.Abs(item.CenterX - attach.CenterX));
            if (span < 18 && maxLateral < 32)
            {
                continue;
            }

            int nearestDistance = branchNodes.Min(item => DistanceToPath(item, mainPath));
            int farthestDistance = branchNodes.Max(item => DistanceToPath(item, mainPath));
            if (nearestDistance > 90 || farthestDistance > 300)
            {
                continue;
            }

            double averageWidth = branchNodes.Average(item => item.Width);
            double averageJungle = branchNodes.Average(item => item.JungleContext);
            double score =
                span * 4.2d +
                Math.Min(maxLateral, 180) * 1.8d +
                averageWidth * 9d +
                averageJungle * 2.2d -
                branchNodes.Count * 1.5d -
                Math.Max(0, farthestDistance - 180) * 0.8d;
            candidates.Add(new CorridorBranch(attach, branchNodes, score));
        }

        List<CorridorBranch> selected = new();
        HashSet<CorridorNode> used = new(mainNodes);
        int nodeBudget = Math.Clamp(mainPath.Count / 2, 18, 70);
        foreach (CorridorBranch candidate in candidates.OrderByDescending(item => item.Score))
        {
            if (selected.Count >= 4 || nodeBudget <= 0)
            {
                break;
            }

            if (candidate.Nodes.Any(used.Contains))
            {
                continue;
            }

            List<CorridorNode> nodes = candidate.Nodes.Take(nodeBudget).ToList();
            if (nodes.Count < 4)
            {
                continue;
            }

            foreach (CorridorNode node in nodes)
            {
                used.Add(node);
            }

            selected.Add(candidate with { Nodes = nodes });
            nodeBudget -= nodes.Count;
        }

        return selected;
    }

    private static int DistanceToPath(CorridorNode node, IReadOnlyList<CorridorNode> path)
    {
        int best = int.MaxValue;
        foreach (CorridorNode pathNode in path)
        {
            int dx = node.CenterX - pathNode.CenterX;
            int dy = node.Y - pathNode.Y;
            int distance = (int)Math.Round(Math.Sqrt(dx * dx + dy * dy));
            if (distance < best)
            {
                best = distance;
            }
        }

        return best;
    }

    private static void MarkCorridorNode(bool[] routeMask, int gridWidth, int gridHeight, CorridorNode node)
    {
        int centerCellX = Math.Clamp(node.CenterX / CellSize, 0, gridWidth - 1);
        int centerCellY = Math.Clamp(node.Y / CellSize, 0, gridHeight - 1);
        const int halfCells = 1;
        for (int y = Math.Max(0, centerCellY - 1); y <= Math.Min(gridHeight - 1, centerCellY + 1); y++)
        {
            for (int x = Math.Max(0, centerCellX - halfCells); x <= Math.Min(gridWidth - 1, centerCellX + halfCells); x++)
            {
                routeMask[y * gridWidth + x] = true;
            }
        }
    }

    private static void MarkSurfaceEntrance(bool[] routeMask, int gridWidth, int gridHeight, int x, int y)
    {
        int centerCellX = Math.Clamp(x / CellSize, 0, gridWidth - 1);
        int centerCellY = Math.Clamp(y / CellSize, 0, gridHeight - 1);
        for (int cy = Math.Max(0, centerCellY - 1); cy <= Math.Min(gridHeight - 1, centerCellY + 1); cy++)
        {
            for (int cx = Math.Max(0, centerCellX - 1); cx <= Math.Min(gridWidth - 1, centerCellX + 1); cx++)
            {
                routeMask[cy * gridWidth + cx] = true;
            }
        }
    }

    private static void MarkRouteLine(bool[] routeMask, int gridWidth, int gridHeight, int x1, int y1, int x2, int y2)
    {
        int dx = x2 - x1;
        int dy = y2 - y1;
        int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (steps == 0)
        {
            return;
        }

        for (int step = 0; step <= steps; step += Math.Max(1, CellSize / 2))
        {
            int tileX = x1 + (int)Math.Round(dx * (step / (double)steps));
            int tileY = y1 + (int)Math.Round(dy * (step / (double)steps));
            int cx = Math.Clamp(tileX / CellSize, 0, gridWidth - 1);
            int cy = Math.Clamp(tileY / CellSize, 0, gridHeight - 1);
            routeMask[cy * gridWidth + cx] = true;
        }
    }

    private static int CountTrue(bool[] values)
    {
        int count = 0;
        foreach (bool value in values)
        {
            if (value)
            {
                count++;
            }
        }

        return count;
    }

    private static JungleSide DetermineJungleSide(WorldData world)
    {
        long leftScore = 0;
        long rightScore = 0;
        int startY = Math.Clamp((int)world.Header.WorldSurface, 0, world.Tiles.Height - 1);
        int endY = Math.Max(startY, world.Tiles.Height - 220);
        for (int x = 0; x < world.Tiles.Width; x++)
        {
            long columnScore = 0;
            for (int y = startY; y < endY; y += 2)
            {
                int index = world.Tiles.Index(x, y);
                if (world.Tiles.Active[index] && IsJungleMaterial(world.Tiles.Type[index]))
                {
                    columnScore++;
                }
            }

            if (x < world.Tiles.Width / 2)
            {
                leftScore += columnScore;
            }
            else
            {
                rightScore += columnScore;
            }
        }

        if (leftScore > rightScore * 1.15d)
        {
            return JungleSide.Left;
        }

        if (rightScore > leftScore * 1.15d)
        {
            return JungleSide.Right;
        }

        if (world.Header.DungeonX > 0)
        {
            return world.Header.DungeonX < world.Tiles.Width / 2 ? JungleSide.Right : JungleSide.Left;
        }

        return world.Header.SpawnX < world.Tiles.Width / 2 ? JungleSide.Right : JungleSide.Left;
    }

    private static JungleCoreBounds FindJungleCoreBounds(WorldData world, JungleSide side)
    {
        int width = world.Tiles.Width;
        int half = width / 2;
        int minCandidateX = side == JungleSide.Right ? half : 0;
        int maxCandidateX = side == JungleSide.Right ? width - 1 : half;
        int startY = Math.Clamp((int)world.Header.WorldSurface, 0, world.Tiles.Height - 1);
        int endY = Math.Max(startY, world.Tiles.Height - 220);
        double[] scores = new double[width];

        for (int x = minCandidateX; x <= maxCandidateX; x++)
        {
            double score = 0;
            for (int y = startY; y < endY; y += 2)
            {
                int index = world.Tiles.Index(x, y);
                if (!world.Tiles.Active[index])
                {
                    continue;
                }

                ushort type = world.Tiles.Type[index];
                if (IsJungleMaterial(type))
                {
                    score += type is TileIds.JungleGrass or TileIds.Hive or TileIds.BeeHive or TileIds.LivingMahogany ? 3d : 1d;
                }
            }

            scores[x] = score;
        }

        SmoothInPlace(scores, minCandidateX, maxCandidateX, radius: 48);
        double total = 0;
        double weightedX = 0;
        for (int x = minCandidateX; x <= maxCandidateX; x++)
        {
            double score = scores[x];
            total += score;
            weightedX += score * x;
        }

        if (total <= 0)
        {
            int fallbackCenter = (minCandidateX + maxCandidateX) / 2;
            int fallbackHalfWidth = Math.Clamp(width / 10, 360, 700);
            return new JungleCoreBounds(
                Math.Clamp(fallbackCenter - fallbackHalfWidth, minCandidateX, maxCandidateX),
                Math.Clamp(fallbackCenter + fallbackHalfWidth, minCandidateX, maxCandidateX),
                fallbackCenter);
        }

        int center = (int)Math.Round(weightedX / total);
        int q10 = WeightedQuantile(scores, minCandidateX, maxCandidateX, total, 0.10d);
        int q90 = WeightedQuantile(scores, minCandidateX, maxCandidateX, total, 0.90d);
        int left = q10 - 140;
        int right = q90 + 140;
        int minWidth = Math.Clamp(width / 5, 760, 1300);
        if (right - left + 1 < minWidth)
        {
            left = center - minWidth / 2;
            right = center + minWidth / 2;
        }

        left = Math.Clamp(left, minCandidateX, maxCandidateX);
        right = Math.Clamp(right, minCandidateX, maxCandidateX);
        if (right <= left)
        {
            left = Math.Clamp(center - minWidth / 2, minCandidateX, maxCandidateX);
            right = Math.Clamp(center + minWidth / 2, minCandidateX, maxCandidateX);
        }

        return new JungleCoreBounds(left, right, center);
    }

    private static void SmoothInPlace(double[] values, int left, int right, int radius)
    {
        double[] copy = (double[])values.Clone();
        double running = 0;
        int start = left;
        int end = left - 1;
        for (int x = left; x <= right; x++)
        {
            while (end < right && end < x + radius)
            {
                end++;
                running += copy[end];
            }

            while (start < x - radius)
            {
                running -= copy[start];
                start++;
            }

            values[x] = running / Math.Max(1, end - start + 1);
        }
    }

    private static int WeightedQuantile(double[] scores, int left, int right, double total, double quantile)
    {
        double target = total * quantile;
        double running = 0;
        for (int x = left; x <= right; x++)
        {
            running += scores[x];
            if (running >= target)
            {
                return x;
            }
        }

        return right;
    }

    private static int[] BuildSurfaceEnvelope(WorldData world, int analysisLeft, int analysisRight)
    {
        int[] rawSurface = new int[world.Tiles.Width];
        int fallbackSurface = Math.Clamp((int)Math.Round(world.Header.WorldSurface), 0, world.Tiles.Height - 1);
        Array.Fill(rawSurface, fallbackSurface);
        int scanLeft = Math.Max(0, analysisLeft - 64);
        int scanRight = Math.Min(world.Tiles.Width - 1, analysisRight + 64);
        int maxY = Math.Min(world.Tiles.Height - 1, (int)Math.Round(world.Header.WorldSurface) + 420);
        for (int x = scanLeft; x <= scanRight; x++)
        {
            rawSurface[x] = FindOpenSkyBottom(world, x, maxY);
        }

        const int radius = 30;
        int[] envelope = new int[rawSurface.Length];
        Array.Fill(envelope, fallbackSurface);
        List<int> window = new(radius * 2 + 1);
        for (int x = scanLeft; x <= scanRight; x++)
        {
            window.Clear();
            for (int sampleX = Math.Max(scanLeft, x - radius); sampleX <= Math.Min(scanRight, x + radius); sampleX++)
            {
                window.Add(rawSurface[sampleX]);
            }

            window.Sort();
            envelope[x] = window[window.Count / 2];
        }

        return envelope;
    }

    private static void TraceClosestDisconnectedGap(
        WorldData world,
        RouteGrid grid,
        int[] surfaceEnvelope,
        IReadOnlyList<List<int>> entranceComponents,
        IReadOnlyList<int> deepComponent,
        int gridWidth)
    {
        int bestEntranceCell = -1;
        int bestDeepCell = -1;
        int bestDistance = int.MaxValue;
        foreach (List<int> entranceComponent in entranceComponents)
        {
            if (ReferenceEquals(entranceComponent, deepComponent))
            {
                continue;
            }

            foreach (int entranceCell in entranceComponent)
            {
                int entranceX = entranceCell % gridWidth;
                int entranceY = entranceCell / gridWidth;
                foreach (int deepCell in deepComponent)
                {
                    int distance = Math.Max(
                        Math.Abs(deepCell % gridWidth - entranceX),
                        Math.Abs(deepCell / gridWidth - entranceY));
                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    bestEntranceCell = entranceCell;
                    bestDeepCell = deepCell;
                }
            }
        }

        if (bestEntranceCell < 0 || bestDeepCell < 0)
        {
            Console.WriteLine("TRACE gap unavailable");
            return;
        }

        int startX = bestEntranceCell % gridWidth;
        int startY = bestEntranceCell / gridWidth;
        int endX = bestDeepCell % gridWidth;
        int endY = bestDeepCell / gridWidth;
        int passableTileGap = MeasurePassableTileGap(world, surfaceEnvelope, startX, startY, endX, endY);
        Console.WriteLine($"TRACE gap nearest entrance={startX}:{startY} deep={endX}:{endY} chebyshev={bestDistance} closedCells={Math.Max(0, bestDistance - 1)} passableTileGap={passableTileGap}");
        bool[] deepMask = new bool[grid.Open.Length];
        foreach (int cell in deepComponent)
        {
            deepMask[cell] = true;
        }

        HashSet<int> rawSurfaceContactCells = new();
        int maxSurfaceY = Math.Min(world.Tiles.Height - 1, (int)Math.Round(world.Header.WorldSurface) + 420);
        for (int tileX = 0; tileX < world.Tiles.Width; tileX++)
        {
            int rawSurface = FindOpenSkyBottom(world, tileX, maxSurfaceY);
            for (int tileY = Math.Max(0, rawSurface - 3); tileY <= Math.Min(world.Tiles.Height - 1, rawSurface + 10); tileY++)
            {
                int cellX = tileX / CellSize;
                int cellY = tileY / CellSize;
                int cell = cellY * gridWidth + cellX;
                if (!deepMask[cell] || !IsPassable(world.Tiles, tileX, tileY))
                {
                    continue;
                }

                if (rawSurfaceContactCells.Add(cell) && rawSurfaceContactCells.Count <= 12)
                {
                    Console.WriteLine($"TRACE raw-surface-contact cell={cellX}:{cellY} tile={tileX}:{tileY} raw={rawSurface} smooth={surfaceEnvelope[tileX]} delta={rawSurface - surfaceEnvelope[tileX]}");
                }
            }
        }
        Console.WriteLine($"TRACE raw-surface-contact-count={rawSurfaceContactCells.Count}");

        int steps = Math.Max(Math.Abs(endX - startX), Math.Abs(endY - startY));
        for (int step = 1; step < steps; step++)
        {
            int cellX = startX + (int)Math.Round((endX - startX) * (step / (double)steps));
            int cellY = startY + (int)Math.Round((endY - startY) * (step / (double)steps));
            int cell = cellY * gridWidth + cellX;
            int rawPassable = 0;
            int envelopePassable = 0;
            int solid = 0;
            for (int x = cellX * CellSize; x < Math.Min(world.Tiles.Width, (cellX + 1) * CellSize); x++)
            {
                for (int y = cellY * CellSize; y < Math.Min(world.Tiles.Height, (cellY + 1) * CellSize); y++)
                {
                    if (IsPassable(world.Tiles, x, y))
                    {
                        rawPassable++;
                        if (y >= surfaceEnvelope[x] - 2)
                        {
                            envelopePassable++;
                        }
                    }
                    else
                    {
                        solid++;
                    }
                }
            }

            Console.WriteLine($"TRACE gap-cell={cellX}:{cellY} tile={cellX * CellSize}:{cellY * CellSize} open={grid.Open[cell]} rawPassable={rawPassable} envelopePassable={envelopePassable} solid={solid}");
        }
    }

    private static int MeasurePassableTileGap(
        WorldData world,
        int[] surfaceEnvelope,
        int firstCellX,
        int firstCellY,
        int secondCellX,
        int secondCellY)
    {
        int best = int.MaxValue;
        for (int firstX = firstCellX * CellSize; firstX < Math.Min(world.Tiles.Width, (firstCellX + 1) * CellSize); firstX++)
        {
            for (int firstY = firstCellY * CellSize; firstY < Math.Min(world.Tiles.Height, (firstCellY + 1) * CellSize); firstY++)
            {
                if (firstY < surfaceEnvelope[firstX] - 2 || !IsPassable(world.Tiles, firstX, firstY))
                {
                    continue;
                }

                for (int secondX = secondCellX * CellSize; secondX < Math.Min(world.Tiles.Width, (secondCellX + 1) * CellSize); secondX++)
                {
                    for (int secondY = secondCellY * CellSize; secondY < Math.Min(world.Tiles.Height, (secondCellY + 1) * CellSize); secondY++)
                    {
                        if (secondY < surfaceEnvelope[secondX] - 2 || !IsPassable(world.Tiles, secondX, secondY))
                        {
                            continue;
                        }

                        best = Math.Min(best, Math.Max(Math.Abs(secondX - firstX), Math.Abs(secondY - firstY)));
                    }
                }
            }
        }

        return best == int.MaxValue ? -1 : best;
    }

    private static RouteGrid BuildRouteGrid(
        WorldData world,
        int left,
        int right,
        int top,
        int bottom,
        int gridWidth,
        int gridHeight,
        int[] surfaceEnvelope,
        bool ignoreGeneratedBlockers)
    {
        bool[] open = new bool[gridWidth * gridHeight];
        ushort[] passableMasks = new ushort[open.Length];
        ushort[] unclippedPassableMasks = new ushort[open.Length];
        byte[] passableCounts = new byte[open.Length];
        byte[] qualityCounts = new byte[open.Length];
        byte[] generatedBlockerCounts = new byte[open.Length];
        byte[] jungleMaterialCounts = new byte[open.Length];
        byte[] activeTileCounts = new byte[open.Length];
        bool[] containsTrack = new bool[open.Length];
        int cellLeft = left / CellSize;
        int cellRight = right / CellSize;
        int cellTop = top / CellSize;
        int cellBottom = bottom / CellSize;
        for (int cy = cellTop; cy <= cellBottom && cy < gridHeight; cy++)
        {
            int y0 = cy * CellSize;
            int y1 = Math.Min(world.Tiles.Height, y0 + CellSize);
            for (int cx = cellLeft; cx <= cellRight && cx < gridWidth; cx++)
            {
                int x0 = cx * CellSize;
                int x1 = Math.Min(world.Tiles.Width, x0 + CellSize);
                ushort rawMask = 0;
                int qualityPassable = 0;
                int generatedBlockerCount = 0;
                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        int worldIndex = world.Tiles.Index(x, y);
                        int currentCell = cy * gridWidth + cx;
                        RouteBlockerKind routeBlockerKind = GetRouteBlockingTileKind(world.Tiles, x, y);
                        if (ignoreGeneratedBlockers && world.Tiles.Active[worldIndex] && routeBlockerKind == RouteBlockerKind.None)
                        {
                            activeTileCounts[currentCell]++;
                            if (IsJungleRouteContextMaterial(world.Tiles.Type[worldIndex]))
                            {
                                jungleMaterialCounts[currentCell]++;
                            }
                        }
                        if (world.Tiles.Active[worldIndex] && world.Tiles.Type[worldIndex] is TileIds.MinecartTrack or TileIds.PressureTrack)
                        {
                            containsTrack[cy * gridWidth + cx] = true;
                        }
                        if (routeBlockerKind != RouteBlockerKind.None)
                        {
                            generatedBlockerCount++;
                        }
                        if (y >= surfaceEnvelope[x] - 2 && IsPassable(world.Tiles, x, y, ignoreGeneratedBlockers))
                        {
                            int localX = x - x0;
                            int localY = y - y0;
                            rawMask |= (ushort)(1 << (localY * CellSize + localX));
                        }
                        if (ignoreGeneratedBlockers && IsPassable(world.Tiles, x, y, ignoreGeneratedBlockers))
                        {
                            int localX = x - x0;
                            int localY = y - y0;
                            unclippedPassableMasks[currentCell] |= (ushort)(1 << (localY * CellSize + localX));
                        }
                        if (y >= surfaceEnvelope[x] - 2 && IsPassable(world.Tiles, x, y, ignoreGeneratedBlockers: false))
                        {
                            qualityPassable++;
                        }
                    }
                }

                ushort connectedMask = rawMask;
                int passable = CountBits(connectedMask);
                int minimumPassable = ignoreGeneratedBlockers ? 1 : 2;
                if ((ignoreGeneratedBlockers || generatedBlockerCount < 4) && passable >= minimumPassable)
                {
                    int cell = cy * gridWidth + cx;
                    open[cell] = true;
                    passableMasks[cell] = connectedMask;
                    passableCounts[cell] = (byte)passable;
                    qualityCounts[cell] = (byte)Math.Min(passable, qualityPassable);
                    generatedBlockerCounts[cell] = (byte)Math.Min(byte.MaxValue, generatedBlockerCount);
                }
            }
        }

        float[] jungleContext;
        float[] jungleBilateralSupport;
        if (ignoreGeneratedBlockers)
        {
            jungleContext = BuildLocalJungleContext(
                jungleMaterialCounts,
                activeTileCounts,
                gridWidth,
                gridHeight,
                out jungleBilateralSupport);
        }
        else
        {
            jungleContext = new float[open.Length];
            jungleBilateralSupport = new float[open.Length];
        }
        if (ignoreGeneratedBlockers)
        {
            AddSurfaceEnvelopeBridges(
                open,
                passableMasks,
                passableCounts,
                qualityCounts,
                unclippedPassableMasks,
                jungleContext,
                gridWidth,
                gridHeight);
        }
        float[] localOpenness = BuildLocalOpenness(open, qualityCounts, gridWidth, gridHeight);
        return new RouteGrid(
            open,
            passableMasks,
            passableCounts,
            qualityCounts,
            generatedBlockerCounts,
            localOpenness,
            jungleContext,
            jungleBilateralSupport,
            containsTrack,
            gridWidth,
            gridHeight);
    }

    private static void AddSurfaceEnvelopeBridges(
        bool[] open,
        ushort[] passableMasks,
        byte[] passableCounts,
        byte[] qualityCounts,
        ushort[] unclippedPassableMasks,
        float[] jungleContext,
        int gridWidth,
        int gridHeight)
    {
        List<int> bridges = new();
        for (int y = 1; y < gridHeight - 1; y++)
        {
            for (int x = 1; x < gridWidth - 1; x++)
            {
                int cell = y * gridWidth + x;
                if (open[cell] || jungleContext[cell] < 0.55f)
                {
                    continue;
                }

                int rawPassable = CountBits(unclippedPassableMasks[cell]);
                if (rawPassable < 6 || rawPassable >= CellSize * CellSize)
                {
                    continue;
                }

                bool hasLeft = false;
                bool hasRight = false;
                for (int dy = -1; dy <= 1; dy++)
                {
                    hasLeft |= open[(y + dy) * gridWidth + x - 1];
                    hasRight |= open[(y + dy) * gridWidth + x + 1];
                }
                if (hasLeft && hasRight)
                {
                    bridges.Add(cell);
                }
            }
        }

        foreach (int cell in bridges)
        {
            int rawPassable = CountBits(unclippedPassableMasks[cell]);
            open[cell] = true;
            passableMasks[cell] = unclippedPassableMasks[cell];
            passableCounts[cell] = (byte)rawPassable;
            qualityCounts[cell] = (byte)rawPassable;
        }
    }

    private static ushort SelectLargestPassableRegion(ushort rawMask)
    {
        ushort remaining = rawMask;
        ushort best = 0;
        int bestCount = 0;
        while (remaining != 0)
        {
            int start = 0;
            while ((remaining & (1 << start)) == 0)
            {
                start++;
            }

            ushort region = 0;
            Queue<int> queue = new();
            queue.Enqueue(start);
            remaining &= (ushort)~(1 << start);
            while (queue.Count > 0)
            {
                int bit = queue.Dequeue();
                region |= (ushort)(1 << bit);
                int x = bit % CellSize;
                int y = bit / CellSize;
                foreach ((int dx, int dy) in Directions)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= CellSize || ny >= CellSize)
                    {
                        continue;
                    }

                    int next = ny * CellSize + nx;
                    ushort nextBit = (ushort)(1 << next);
                    if ((remaining & nextBit) == 0)
                    {
                        continue;
                    }

                    remaining &= (ushort)~nextBit;
                    queue.Enqueue(next);
                }
            }

            int count = CountBits(region);
            if (count > bestCount)
            {
                best = region;
                bestCount = count;
            }
        }

        return best;
    }

    private static int CountBits(ushort mask)
    {
        int count = 0;
        while (mask != 0)
        {
            mask &= (ushort)(mask - 1);
            count++;
        }

        return count;
    }

    private static float[] BuildLocalOpenness(bool[] open, byte[] passableCounts, int gridWidth, int gridHeight)
    {
        int stride = gridWidth + 1;
        int[] integral = new int[stride * (gridHeight + 1)];
        for (int y = 0; y < gridHeight; y++)
        {
            int running = 0;
            for (int x = 0; x < gridWidth; x++)
            {
                int cell = y * gridWidth + x;
                running += open[cell] ? passableCounts[cell] : 0;
                integral[(y + 1) * stride + x + 1] = integral[y * stride + x + 1] + running;
            }
        }

        const int radius = 3;
        float[] result = new float[open.Length];
        for (int cell = 0; cell < open.Length; cell++)
        {
            if (!open[cell])
            {
                continue;
            }

            int x = cell % gridWidth;
            int y = cell / gridWidth;
            int left = Math.Max(0, x - radius);
            int right = Math.Min(gridWidth - 1, x + radius);
            int top = Math.Max(0, y - radius);
            int bottom = Math.Min(gridHeight - 1, y + radius);
            int total = integral[(bottom + 1) * stride + right + 1] -
                integral[top * stride + right + 1] -
                integral[(bottom + 1) * stride + left] +
                integral[top * stride + left];
            int sampleCount = (right - left + 1) * (bottom - top + 1) * CellSize * CellSize;
            result[cell] = total / (float)Math.Max(1, sampleCount);
        }

        return result;
    }

    private static float[] BuildLocalJungleContext(
        byte[] jungleCounts,
        byte[] activeCounts,
        int gridWidth,
        int gridHeight,
        out float[] bilateralSupport)
    {
        int stride = gridWidth + 1;
        int[] jungleIntegral = new int[stride * (gridHeight + 1)];
        int[] activeIntegral = new int[stride * (gridHeight + 1)];
        for (int y = 0; y < gridHeight; y++)
        {
            int jungleRunning = 0;
            int activeRunning = 0;
            for (int x = 0; x < gridWidth; x++)
            {
                int cell = y * gridWidth + x;
                jungleRunning += jungleCounts[cell];
                activeRunning += activeCounts[cell];
                jungleIntegral[(y + 1) * stride + x + 1] = jungleIntegral[y * stride + x + 1] + jungleRunning;
                activeIntegral[(y + 1) * stride + x + 1] = activeIntegral[y * stride + x + 1] + activeRunning;
            }
        }

        const int radius = 6;
        float[] result = new float[jungleCounts.Length];
        bilateralSupport = new float[jungleCounts.Length];
        for (int cell = 0; cell < result.Length; cell++)
        {
            int x = cell % gridWidth;
            int y = cell / gridWidth;
            int left = Math.Max(0, x - radius);
            int right = Math.Min(gridWidth - 1, x + radius);
            int top = Math.Max(0, y - radius);
            int bottom = Math.Min(gridHeight - 1, y + radius);
            int jungle = SumIntegral(jungleIntegral, stride, left, top, right, bottom);
            int active = SumIntegral(activeIntegral, stride, left, top, right, bottom);
            result[cell] = jungle / (float)Math.Max(1, active);

            int leftBandLeft = Math.Max(0, x - 12);
            int leftBandRight = Math.Max(0, x - 2);
            int rightBandLeft = Math.Min(gridWidth - 1, x + 2);
            int rightBandRight = Math.Min(gridWidth - 1, x + 12);
            int leftJungle = SumIntegral(jungleIntegral, stride, leftBandLeft, top, leftBandRight, bottom);
            int leftActive = SumIntegral(activeIntegral, stride, leftBandLeft, top, leftBandRight, bottom);
            int rightJungle = SumIntegral(jungleIntegral, stride, rightBandLeft, top, rightBandRight, bottom);
            int rightActive = SumIntegral(activeIntegral, stride, rightBandLeft, top, rightBandRight, bottom);
            bilateralSupport[cell] = Math.Min(
                leftJungle / (float)Math.Max(1, leftActive),
                rightJungle / (float)Math.Max(1, rightActive));
        }

        return result;
    }

    private static int SumIntegral(int[] integral, int stride, int left, int top, int right, int bottom) =>
        integral[(bottom + 1) * stride + right + 1] -
        integral[top * stride + right + 1] -
        integral[(bottom + 1) * stride + left] +
        integral[top * stride + left];

    private static List<int> FindSurfaceEntranceCells(WorldData world, IReadOnlyList<int> cells, int gridWidth, int[] surfaceEnvelope)
    {
        List<int> entrances = new();
        foreach (int cell in cells)
        {
            int cx = cell % gridWidth;
            int cy = cell / gridWidth;
            int x0 = Math.Clamp(cx * CellSize, 0, world.Tiles.Width - 1);
            int x1 = Math.Clamp(x0 + CellSize - 1, x0, world.Tiles.Width - 1);
            int y0 = Math.Clamp(cy * CellSize, 0, world.Tiles.Height - 1);
            int y1 = Math.Clamp(y0 + CellSize - 1, y0, world.Tiles.Height - 1);
            bool touchesSurface = false;
            for (int x = x0; x <= x1 && !touchesSurface; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (!IsPassable(world.Tiles, x, y) || y < surfaceEnvelope[x] - 3 || y > surfaceEnvelope[x] + 10)
                    {
                        continue;
                    }

                    touchesSurface = true;
                    break;
                }
            }

            if (touchesSurface)
            {
                entrances.Add(cell);
            }
        }

        return entrances;
    }

    private static List<int> CollapseSurfaceEntrances(IReadOnlyList<int> entrances, int gridWidth, int jungleCenterX)
    {
        List<int> uniqueByColumn = entrances
            .GroupBy(cell => cell % gridWidth)
            .Select(group => group.OrderBy(cell => cell / gridWidth).First())
            .OrderBy(cell => cell % gridWidth)
            .ToList();
        if (uniqueByColumn.Count == 0)
        {
            return new List<int>();
        }

        List<List<int>> clusters = new();
        foreach (int entrance in uniqueByColumn)
        {
            int x = entrance % gridWidth;
            if (clusters.Count == 0 || x - clusters[^1][^1] % gridWidth > 2)
            {
                clusters.Add(new List<int>());
            }

            clusters[^1].Add(entrance);
        }

        List<int> collapsed = new();
        foreach (List<int> cluster in clusters)
        {
            collapsed.Add(cluster[0]);
            collapsed.Add(cluster[^1]);
            int sampleCount = Math.Min(3, Math.Max(1, (cluster.Count + 5) / 6));
            for (int sample = 0; sample < sampleCount; sample++)
            {
                int index = (int)Math.Round((sample + 1d) * (cluster.Count - 1) / (sampleCount + 1d));
                collapsed.Add(cluster[Math.Clamp(index, 0, cluster.Count - 1)]);
            }
        }

        if (string.Equals(Environment.GetEnvironmentVariable("TERRARIA_SPLIT_ROUTE_TRACE"), "1", StringComparison.Ordinal))
        {
            Console.WriteLine(
                "TRACE entrance-clusters " +
                string.Join(',', clusters.Select(cluster =>
                    $"{cluster[0] % gridWidth}..{cluster[^1] % gridWidth}({cluster.Count})")) +
                " selected=" +
                string.Join(',', collapsed.Select(cell => $"{cell % gridWidth}:{cell / gridWidth}")));
        }

        int centerCellX = jungleCenterX / CellSize;
        return collapsed
            .Distinct()
            .OrderBy(cell => Math.Abs(cell % gridWidth - centerCellX))
            .Take(18)
            .ToList();
    }

    private static List<int> SelectDownwardEntrances(
        IReadOnlyList<int> componentCells,
        IReadOnlyList<int> entrances,
        int gridWidth,
        out double bestQuality)
    {
        HashSet<int> component = new(componentCells);
        List<(int Cell, double Quality)> candidates = new();
        foreach (int entrance in entrances)
        {
            int startX = entrance % gridWidth;
            int startY = entrance / gridWidth;
            int supportedRows = 0;
            int horizontalCost = 0;
            Dictionary<int, int> frontier = ExpandHorizontalFrontier(
                component,
                startY,
                gridWidth,
                new Dictionary<int, int>
                {
                    [startX] = 0
                },
                maxSteps: 4);
            for (int dy = 1; dy <= 24; dy++)
            {
                int y = startY + dy;
                Dictionary<int, int> nextSeeds = new();
                foreach ((int previousX, int previousCost) in frontier)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = previousX + dx;
                        if (x < 0 || x >= gridWidth || !component.Contains(y * gridWidth + x))
                        {
                            continue;
                        }

                        int cost = previousCost + Math.Abs(dx);
                        if (!nextSeeds.TryGetValue(x, out int existingCost) || cost < existingCost)
                        {
                            nextSeeds[x] = cost;
                        }
                    }
                }

                if (nextSeeds.Count == 0)
                {
                    break;
                }

                Dictionary<int, int> nextFrontier = ExpandHorizontalFrontier(
                    component,
                    y,
                    gridWidth,
                    nextSeeds,
                    maxSteps: 4);
                supportedRows++;
                horizontalCost = nextFrontier.Values.Min();
                frontier = nextFrontier;
            }

            if (supportedRows < 12)
            {
                continue;
            }

            double quality = supportedRows - horizontalCost * 0.35d;
            candidates.Add((entrance, quality));
        }

        candidates.Sort((left, right) => right.Quality.CompareTo(left.Quality));
        bestQuality = candidates.Count == 0 ? 0d : candidates[0].Quality;
        return candidates.Select(candidate => candidate.Cell).ToList();
    }

    private static Dictionary<int, int> ExpandHorizontalFrontier(
        HashSet<int> component,
        int row,
        int gridWidth,
        Dictionary<int, int> seeds,
        int maxSteps)
    {
        Dictionary<int, int> expanded = new(seeds);
        HashSet<int> current = new(seeds.Keys);
        for (int step = 0; step < maxSteps && current.Count > 0; step++)
        {
            HashSet<int> next = new();
            foreach (int x in current)
            {
                foreach (int adjacentX in new[] { x - 1, x + 1 })
                {
                    if (adjacentX < 0 || adjacentX >= gridWidth ||
                        !component.Contains(row * gridWidth + adjacentX))
                    {
                        continue;
                    }

                    int cost = expanded[x] + 1;
                    if (!expanded.TryGetValue(adjacentX, out int existingCost) || cost < existingCost)
                    {
                        expanded[adjacentX] = cost;
                        next.Add(adjacentX);
                    }
                }
            }

            current = next;
        }

        return expanded;
    }

    private static ComponentCavityMetrics MeasureComponentCavities(IReadOnlyList<int> cells, int gridWidth, ComponentStats stats)
    {
        Dictionary<int, int> rowCounts = new();
        foreach (int cell in cells)
        {
            int row = cell / gridWidth;
            rowCounts[row] = rowCounts.GetValueOrDefault(row) + 1;
        }

        int[] counts = rowCounts.Values.OrderBy(value => value).ToArray();
        int quartileIndex = counts.Length == 0 ? 0 : Math.Clamp((int)Math.Floor((counts.Length - 1) * 0.75d), 0, counts.Length - 1);
        int upperQuartile = counts.Length == 0 ? 0 : counts[quartileIndex];
        double verticalCoverage = rowCounts.Count / (double)Math.Max(1, stats.Depth);
        return new ComponentCavityMetrics(upperQuartile, verticalCoverage);
    }

    private static List<int> FloodComponent(
        RouteGrid grid,
        int[] visited,
        int componentId,
        int start,
        int gridWidth,
        int gridHeight,
        int jumpCells,
        bool requireBoundaryConnection = true)
    {
        Queue<int> queue = new();
        List<int> cells = new();
        visited[start] = componentId;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            cells.Add(cell);
            int cx = cell % gridWidth;
            int cy = cell / gridWidth;
            foreach ((int dx, int dy) in Directions)
            {
                for (int step = 1; step <= jumpCells; step++)
                {
                    int nx = cx + dx * step;
                    int ny = cy + dy * step;
                    if (nx < 0 || ny < 0 || nx >= gridWidth || ny >= gridHeight)
                    {
                        break;
                    }

                    int next = ny * gridWidth + nx;
                    if (!grid.Open[next] || visited[next] >= 0 || (requireBoundaryConnection && !grid.CanMove(cell, next)))
                    {
                        continue;
                    }

                    visited[next] = componentId;
                    queue.Enqueue(next);
                }
            }
        }

        return cells;
    }

    private static List<int> ExtractMainPath(
        WorldData world,
        bool[] componentMask,
        List<int> componentCells,
        List<int> entranceCells,
        int gridWidth,
        int gridHeight,
        int jumpCells,
        int jungleCenterX,
        RouteGrid grid)
    {
        if (componentCells.Count == 0)
        {
            return new List<int>();
        }

        int start = entranceCells[0];
        return FindBestNaturalPath(
            world,
            grid,
            componentMask,
            start,
            gridWidth,
            gridHeight,
            jumpCells,
            jungleCenterX,
            requireBoundaryConnection: true);
    }

    private static double ScoreRouteCandidate(
        WorldData world,
        IReadOnlyList<int> route,
        bool[] componentMask,
        int gridWidth,
        JungleCoreBounds jungleCore,
        RouteGrid grid,
        int[] surfaceEnvelope)
    {
        if (route.Count < 2)
        {
            return double.NegativeInfinity;
        }

        int startX = route[0] % gridWidth;
        int startY = route[0] / gridWidth;
        int endY = route[^1] / gridWidth;
        int depth = endY - startY;
        RouteBlockerKind endpointBlocker = MeasureEndpointBlocker(world, route[^1], gridWidth);
        if (depth < 24 || (depth < 32 && endpointBlocker == RouteBlockerKind.None))
        {
            return double.NegativeInfinity;
        }

        int horizontalMoves = 0;
        int upwardMoves = 0;
        int trackFollowingMoves = 0;
        int reconstructedMoves = 0;
        int horizontalRun = 0;
        int maxHorizontalRun = 0;
        for (int i = 1; i < route.Count; i++)
        {
            int previousX = route[i - 1] % gridWidth;
            int previousY = route[i - 1] / gridWidth;
            int currentX = route[i] % gridWidth;
            int currentY = route[i] / gridWidth;
            if (currentY == previousY)
            {
                horizontalMoves++;
                horizontalRun++;
                maxHorizontalRun = Math.Max(maxHorizontalRun, horizontalRun);
            }
            else
            {
                horizontalRun = 0;
            }

            if (currentY < previousY)
            {
                upwardMoves++;
            }

            if (currentX != previousX &&
                CellContainsTrack(world, previousX, previousY) &&
                CellContainsTrack(world, currentX, currentY))
            {
                trackFollowingMoves++;
            }

            if (grid.IsReconstructedConnection(route[i - 1], route[i]))
            {
                reconstructedMoves++;
            }
        }

        double opennessTotal = 0d;
        double passableTotal = 0d;
        int narrowCells = 0;
        int opennessSamples = 0;
        for (int i = 0; i < route.Count; i += 3)
        {
            int cell = route[i];
            opennessTotal += grid.LocalOpenness[cell];
            passableTotal += grid.QualityCounts[cell] / (double)(CellSize * CellSize);
            if (grid.QualityCounts[cell] <= 2)
            {
                narrowCells++;
            }
            opennessSamples++;
        }

        double averageOpenness = opennessTotal / Math.Max(1, opennessSamples);
        double averagePassable = passableTotal / Math.Max(1, opennessSamples);
        double downwardRatio = depth / (double)Math.Max(1, route.Count - 1);
        int startTileX = startX * CellSize + CellSize / 2;
        int startTileY = startY * CellSize + CellSize / 2;
        double halfCoreWidth = Math.Max(1d, (jungleCore.Right - jungleCore.Left) / 2d);
        double normalizedCenterDistance = Math.Abs(startTileX - jungleCore.CenterX) / halfCoreWidth;
        double outerDistance = Math.Max(0d, normalizedCenterDistance - 0.50d);
        double centerPenalty =
            Math.Abs(startTileX - jungleCore.CenterX) * 0.35d +
            Math.Pow(Math.Max(0d, normalizedCenterDistance - 0.30d), 2d) * 80000d +
            outerDistance * outerDistance * 100000d +
            (normalizedCenterDistance > 0.68d ? 25000d : 0d);
        double entranceJungleDensity = MeasureEntranceJungleDensity(world, startTileX, startTileY);
        if (entranceJungleDensity < 0.12d || normalizedCenterDistance > 0.75d)
        {
            return double.NegativeInfinity;
        }

        RouteJungleMetrics jungleMetrics = MeasureRouteJungleMetrics(route, grid);
        RouteSurfaceMetrics surfaceMetrics = MeasureRouteSurfaceMetrics(route, gridWidth, surfaceEnvelope);
        RouteCenterMetrics centerMetrics = MeasureRouteCenterMetrics(route, gridWidth, jungleCore);
        bool centeredSupportedRoute =
            normalizedCenterDistance <= 0.35d &&
            jungleMetrics.Average >= 0.62d &&
            jungleMetrics.BilateralAverage >= 0.62d;
        if (jungleMetrics.Average < 0.62d ||
            (!centeredSupportedRoute && jungleMetrics.LowFraction > 0.16d && jungleMetrics.MaxLowRun >= 8) ||
            (jungleMetrics.BilateralAverage < 0.60d &&
             jungleMetrics.BilateralLowFraction > 0.20d &&
             jungleMetrics.MaxBilateralLowRun >= 12))
        {
            return double.NegativeInfinity;
        }

        double jungleContextPenalty =
            Math.Max(0d, 0.70d - jungleMetrics.Average) * 12000d +
            Math.Max(0d, jungleMetrics.LowFraction - 0.08d) * 8000d +
            Math.Max(0, jungleMetrics.MaxLowRun - 5) * 160d;
        double jungleEdgePenalty =
            Math.Max(0d, 0.60d - jungleMetrics.BilateralAverage) * 14000d +
            Math.Max(0d, jungleMetrics.BilateralLowFraction - 0.18d) * 9000d +
            (jungleMetrics.BilateralLowFraction > 0.18d
                ? Math.Max(0, jungleMetrics.MaxBilateralLowRun - 10) * 180d
                : 0d);
        double surfaceApproachPenalty =
            Math.Max(0, surfaceMetrics.ShallowHorizontalDrift - 14) * 180d +
            Math.Max(0, surfaceMetrics.MaxNearSurfaceRun - 16) * 90d;
        double routeCenterPenalty =
            Math.Pow(Math.Max(0d, centerMetrics.Average - 0.30d), 2d) * 100000d +
            centerMetrics.OutsideFraction * 4000d +
            Math.Pow(Math.Max(0d, centerMetrics.Maximum - 0.60d), 2d) * 50000d;
        double score =
            depth * 26d +
            downwardRatio * 1200d +
            averageOpenness * 4200d +
            averagePassable * 1800d -
            narrowCells * 80d -
            horizontalMoves * 28d -
            upwardMoves * 120d -
            maxHorizontalRun * 110d -
            trackFollowingMoves * 90d -
            reconstructedMoves * 900d -
            centerPenalty +
            entranceJungleDensity * 6500d -
            jungleContextPenalty -
            jungleEdgePenalty -
            surfaceApproachPenalty -
            routeCenterPenalty;
        return score;
    }

    private static RouteCenterMetrics MeasureRouteCenterMetrics(
        IReadOnlyList<int> route,
        int gridWidth,
        JungleCoreBounds jungleCore)
    {
        double halfCoreWidth = Math.Max(1d, (jungleCore.Right - jungleCore.Left) / 2d);
        double total = 0d;
        double maximum = 0d;
        int outside = 0;
        int samples = 0;
        for (int i = 0; i < route.Count; i += 2)
        {
            int tileX = route[i] % gridWidth * CellSize + CellSize / 2;
            double distance = Math.Abs(tileX - jungleCore.CenterX) / halfCoreWidth;
            total += distance;
            maximum = Math.Max(maximum, distance);
            outside += distance > 0.45d ? 1 : 0;
            samples++;
        }

        return new RouteCenterMetrics(
            total / Math.Max(1, samples),
            maximum,
            outside / (double)Math.Max(1, samples));
    }

    private static bool IsBetterRouteCandidate(
        double candidateScore,
        IReadOnlyList<int> candidateRoute,
        double currentScore,
        IReadOnlyList<int> currentRoute,
        int gridWidth)
    {
        if (double.IsNegativeInfinity(candidateScore) || candidateRoute.Count == 0)
        {
            return false;
        }
        if (currentRoute.Count == 0 || double.IsNegativeInfinity(currentScore))
        {
            return true;
        }

        const double nearTieTolerance = 50d;
        if (Math.Abs(candidateScore - currentScore) <= nearTieTolerance)
        {
            int candidateDepth = candidateRoute[^1] / gridWidth - candidateRoute[0] / gridWidth;
            int currentDepth = currentRoute[^1] / gridWidth - currentRoute[0] / gridWidth;
            if (candidateDepth != currentDepth)
            {
                return candidateDepth > currentDepth;
            }
        }

        return candidateScore > currentScore;
    }

    private static string DescribeCandidate(
        string kind,
        WorldData world,
        IReadOnlyList<int> route,
        int gridWidth,
        JungleCoreBounds jungleCore,
        RouteGrid grid,
        int[] surfaceEnvelope,
        double score)
    {
        if (route.Count == 0)
        {
            return $"TRACE {kind} route=empty score=-inf";
        }

        int startX = route[0] % gridWidth;
        int startY = route[0] / gridWidth;
        int endY = route[^1] / gridWidth;
        int startTileX = startX * CellSize + CellSize / 2;
        int startTileY = startY * CellSize + CellSize / 2;
        double halfCoreWidth = Math.Max(1d, (jungleCore.Right - jungleCore.Left) / 2d);
        double normalizedCenterDistance = Math.Abs(startTileX - jungleCore.CenterX) / halfCoreWidth;
        double jungleDensity = MeasureEntranceJungleDensity(world, startTileX, startTileY);
        double sideSupport = MeasureEntranceJungleSideSupport(world, startTileX, startTileY);
        RouteJungleMetrics jungleMetrics = MeasureRouteJungleMetrics(route, grid);
        RouteSurfaceMetrics surfaceMetrics = MeasureRouteSurfaceMetrics(route, gridWidth, surfaceEnvelope);
        RouteCenterMetrics centerMetrics = MeasureRouteCenterMetrics(route, gridWidth, jungleCore);
        RouteBlockerKind endpointBlocker = MeasureEndpointBlocker(world, route[^1], gridWidth);
        string scoreText = double.IsNegativeInfinity(score) ? "-inf" : score.ToString("F1", CultureInfo.InvariantCulture);
        return $"TRACE {kind} cells={route.Count} start={startX}:{startY} depth={endY - startY} density={jungleDensity:F3} sideSupport={sideSupport:F3} coreDistance={normalizedCenterDistance:F3} routeCenterAvg={centerMetrics.Average:F3} routeCenterOut={centerMetrics.OutsideFraction:F3} routeJungleAvg={jungleMetrics.Average:F3} routeJungleLow={jungleMetrics.LowFraction:F3} routeJungleRun={jungleMetrics.MaxLowRun} bilateralAvg={jungleMetrics.BilateralAverage:F3} bilateralLow={jungleMetrics.BilateralLowFraction:F3} bilateralRun={jungleMetrics.MaxBilateralLowRun} surfaceRun={surfaceMetrics.MaxNearSurfaceRun} shallowDrift={surfaceMetrics.ShallowHorizontalDrift} blocker={endpointBlocker} score={scoreText}";
    }

    private static RouteSurfaceMetrics MeasureRouteSurfaceMetrics(
        IReadOnlyList<int> route,
        int gridWidth,
        int[] surfaceEnvelope)
    {
        int startX = route[0] % gridWidth;
        int startY = route[0] / gridWidth;
        int nearSurfaceRun = 0;
        int maxNearSurfaceRun = 0;
        int shallowHorizontalDrift = 0;
        for (int i = 0; i < route.Count && i < 80; i++)
        {
            int cellX = route[i] % gridWidth;
            int cellY = route[i] / gridWidth;
            int tileX = Math.Clamp(cellX * CellSize + CellSize / 2, 0, surfaceEnvelope.Length - 1);
            int tileY = cellY * CellSize + CellSize / 2;
            if (tileY <= surfaceEnvelope[tileX] + 16)
            {
                nearSurfaceRun++;
                maxNearSurfaceRun = Math.Max(maxNearSurfaceRun, nearSurfaceRun);
            }
            else
            {
                nearSurfaceRun = 0;
            }

            if (cellY - startY < 16)
            {
                shallowHorizontalDrift = Math.Max(shallowHorizontalDrift, Math.Abs(cellX - startX));
            }
        }

        return new RouteSurfaceMetrics(maxNearSurfaceRun, shallowHorizontalDrift);
    }

    private static RouteBlockerKind MeasureEndpointBlocker(WorldData world, int endpoint, int gridWidth)
    {
        int centerX = endpoint % gridWidth * CellSize + CellSize / 2;
        int bottomY = (endpoint / gridWidth + 1) * CellSize;
        int temple = 0;
        int hive = 0;
        for (int y = bottomY; y <= Math.Min(world.Tiles.Height - 1, bottomY + 12); y++)
        {
            for (int x = Math.Max(0, centerX - 8); x <= Math.Min(world.Tiles.Width - 1, centerX + 8); x++)
            {
                RouteBlockerKind kind = GetRouteBlockingTileKind(world.Tiles, x, y);
                temple += kind == RouteBlockerKind.Temple ? 1 : 0;
                hive += kind == RouteBlockerKind.Hive ? 1 : 0;
            }
        }

        if (temple >= 8)
        {
            return RouteBlockerKind.Temple;
        }
        return hive >= 8 ? RouteBlockerKind.Hive : RouteBlockerKind.None;
    }

    private static RouteJungleMetrics MeasureRouteJungleMetrics(IReadOnlyList<int> route, RouteGrid grid)
    {
        const float lowContextThreshold = 0.34f;
        const float lowBilateralThreshold = 0.35f;
        double total = 0d;
        double bilateralTotal = 0d;
        int samples = 0;
        int lowSamples = 0;
        int bilateralLowSamples = 0;
        int lowRun = 0;
        int bilateralLowRun = 0;
        int maxLowRun = 0;
        int maxBilateralLowRun = 0;
        for (int i = 0; i < route.Count; i += 2)
        {
            float context = grid.JungleContext[route[i]];
            float bilateral = grid.JungleBilateralSupport[route[i]];
            total += context;
            bilateralTotal += bilateral;
            samples++;
            if (context < lowContextThreshold)
            {
                lowSamples++;
                lowRun++;
                maxLowRun = Math.Max(maxLowRun, lowRun);
            }
            else
            {
                lowRun = 0;
            }

            if (bilateral < lowBilateralThreshold)
            {
                bilateralLowSamples++;
                bilateralLowRun++;
                maxBilateralLowRun = Math.Max(maxBilateralLowRun, bilateralLowRun);
            }
            else
            {
                bilateralLowRun = 0;
            }
        }

        return new RouteJungleMetrics(
            total / Math.Max(1, samples),
            lowSamples / (double)Math.Max(1, samples),
            maxLowRun,
            bilateralTotal / Math.Max(1, samples),
            bilateralLowSamples / (double)Math.Max(1, samples),
            maxBilateralLowRun);
    }

    private static bool IsEligibleEntrance(WorldData world, int cell, int gridWidth, JungleCoreBounds jungleCore)
    {
        int startX = cell % gridWidth;
        int startY = cell / gridWidth;
        int startTileX = startX * CellSize + CellSize / 2;
        int startTileY = startY * CellSize + CellSize / 2;
        double halfCoreWidth = Math.Max(1d, (jungleCore.Right - jungleCore.Left) / 2d);
        double normalizedCenterDistance = Math.Abs(startTileX - jungleCore.CenterX) / halfCoreWidth;
        return normalizedCenterDistance <= 0.75d && MeasureEntranceJungleDensity(world, startTileX, startTileY) >= 0.12d;
    }

    private static double MeasureNormalizedCoreDistance(int cell, int gridWidth, JungleCoreBounds jungleCore)
    {
        int tileX = cell % gridWidth * CellSize + CellSize / 2;
        double halfCoreWidth = Math.Max(1d, (jungleCore.Right - jungleCore.Left) / 2d);
        return Math.Abs(tileX - jungleCore.CenterX) / halfCoreWidth;
    }

    private static bool ShouldUseSingleLargeComponentEntrance(
        IReadOnlyList<int> entrances,
        int gridWidth,
        JungleCoreBounds jungleCore)
    {
        if (entrances.Count == 0)
        {
            return false;
        }
        if (MeasureNormalizedCoreDistance(entrances[0], gridWidth, jungleCore) <= 0.02d || entrances.Count == 1)
        {
            return true;
        }

        int firstX = entrances[0] % gridWidth;
        int firstY = entrances[0] / gridWidth;
        int secondX = entrances[1] % gridWidth;
        int secondY = entrances[1] / gridWidth;
        return Math.Abs(secondX - firstX) <= 4 && Math.Abs(secondY - firstY) <= 1;
    }

    private static double MeasureEntranceJungleDensity(WorldData world, int centerX, int startY)
    {
        int jungleTiles = 0;
        int activeTiles = 0;
        for (int y = Math.Max(0, startY); y <= Math.Min(world.Tiles.Height - 1, startY + 96); y += 4)
        {
            for (int x = Math.Max(0, centerX - 40); x <= Math.Min(world.Tiles.Width - 1, centerX + 40); x += 4)
            {
                int index = world.Tiles.Index(x, y);
                if (!world.Tiles.Active[index])
                {
                    continue;
                }

                activeTiles++;
                if (IsJungleMaterial(world.Tiles.Type[index]))
                {
                    jungleTiles++;
                }
            }
        }

        return jungleTiles / (double)Math.Max(1, activeTiles);
    }

    private static double MeasureEntranceJungleSideSupport(WorldData world, int centerX, int startY)
    {
        double left = MeasureJungleBandDensity(world, centerX - 128, centerX - 20, startY, startY + 160);
        double right = MeasureJungleBandDensity(world, centerX + 20, centerX + 128, startY, startY + 160);
        return Math.Min(left, right);
    }

    private static double MeasureJungleBandDensity(WorldData world, int left, int right, int top, int bottom)
    {
        int jungleTiles = 0;
        int activeTiles = 0;
        for (int y = Math.Max(0, top); y <= Math.Min(world.Tiles.Height - 1, bottom); y += 4)
        {
            for (int x = Math.Max(0, left); x <= Math.Min(world.Tiles.Width - 1, right); x += 4)
            {
                int index = world.Tiles.Index(x, y);
                if (!world.Tiles.Active[index])
                {
                    continue;
                }

                activeTiles++;
                if (IsJungleMaterial(world.Tiles.Type[index]))
                {
                    jungleTiles++;
                }
            }
        }

        return jungleTiles / (double)Math.Max(1, activeTiles);
    }

    private static List<int> FindWeightedPath(
        WorldData world,
        RouteGrid grid,
        bool[] allowedMask,
        int start,
        int target,
        int gridWidth,
        int gridHeight,
        int jumpCells,
        int[]? routeDistance,
        bool requireBoundaryConnection = true)
    {
        if (start < 0 || target < 0 || start >= allowedMask.Length || target >= allowedMask.Length ||
            !allowedMask[start] || !allowedMask[target])
        {
            return new List<int>();
        }

        int[] previous = new int[allowedMask.Length];
        Array.Fill(previous, -2);
        double[] distances = new double[allowedMask.Length];
        Array.Fill(distances, double.PositiveInfinity);
        PriorityQueue<int, double> queue = new();
        previous[start] = -1;
        distances[start] = 0d;
        queue.Enqueue(start, 0d);

        while (queue.Count > 0)
        {
            queue.TryDequeue(out int cell, out double distance);
            if (distance > distances[cell])
            {
                continue;
            }

            if (cell == target)
            {
                break;
            }

            int cx = cell % gridWidth;
            int cy = cell / gridWidth;
            foreach ((int dx, int dy) in Directions)
            {
                for (int step = 1; step <= jumpCells; step++)
                {
                    int nx = cx + dx * step;
                    int ny = cy + dy * step;
                    if (nx < 0 || ny < 0 || nx >= gridWidth || ny >= gridHeight)
                    {
                        break;
                    }

                    int next = ny * gridWidth + nx;
                    if (!allowedMask[next] || (requireBoundaryConnection && !grid.CanMove(cell, next)))
                    {
                        continue;
                    }

                    double moveCost = MeasureMoveCost(grid, cell, next, dx, dy);
                    if (dy < 0)
                    {
                        moveCost += 12d;
                    }
                    if (dx != 0 && CellContainsTrack(world, cx, cy) && CellContainsTrack(world, nx, ny))
                    {
                        moveCost += 8d;
                    }
                    if (routeDistance is not null)
                    {
                        moveCost += routeDistance[next] * 0.4d;
                    }

                    double candidateDistance = distance + moveCost;
                    if (candidateDistance >= distances[next])
                    {
                        continue;
                    }

                    distances[next] = candidateDistance;
                    previous[next] = cell;
                    queue.Enqueue(next, candidateDistance);
                }
            }
        }

        if (previous[target] == -2)
        {
            return new List<int>();
        }

        List<int> path = new();
        for (int cell = target; cell >= 0; cell = previous[cell])
        {
            path.Add(cell);
        }

        path.Reverse();
        return path;
    }

    private static List<int> FindBestNaturalPath(
        WorldData world,
        RouteGrid grid,
        bool[] allowedMask,
        int start,
        int gridWidth,
        int gridHeight,
        int jumpCells,
        int jungleCenterX,
        bool requireBoundaryConnection = true,
        bool preferDeepestReachable = false)
    {
        if (start < 0 || start >= allowedMask.Length || !allowedMask[start])
        {
            return new List<int>();
        }

        int[] previous = new int[allowedMask.Length];
        Array.Fill(previous, -2);
        double[] distances = new double[allowedMask.Length];
        Array.Fill(distances, double.PositiveInfinity);
        PriorityQueue<int, double> queue = new();
        previous[start] = -1;
        distances[start] = 0d;
        queue.Enqueue(start, 0d);

        int startX = start % gridWidth;
        int startY = start / gridWidth;
        int centerCellX = jungleCenterX / CellSize;
        int best = -1;
        double bestScore = double.NegativeInfinity;
        int deepestReachable = start;
        while (queue.Count > 0)
        {
            queue.TryDequeue(out int cell, out double distance);
            if (distance > distances[cell])
            {
                continue;
            }

            int cx = cell % gridWidth;
            int cy = cell / gridWidth;
            int depth = cy - startY;
            if (depth > deepestReachable / gridWidth - startY)
            {
                deepestReachable = cell;
            }
            if (depth >= 12)
            {
                double targetScore = MeasureDepthUtility(depth) -
                    distance -
                    Math.Abs(cx - startX) * 0.35d -
                    Math.Abs(cx - centerCellX) * 0.04d +
                    grid.LocalOpenness[cell] * 22d +
                    grid.QualityCounts[cell] * 0.35d;
                if (targetScore > bestScore)
                {
                    bestScore = targetScore;
                    best = cell;
                }
            }

            foreach ((int dx, int dy) in Directions)
            {
                for (int step = 1; step <= jumpCells; step++)
                {
                    int nx = cx + dx * step;
                    int ny = cy + dy * step;
                    if (nx < 0 || ny < 0 || nx >= gridWidth || ny >= gridHeight)
                    {
                        break;
                    }
                    if (ny < startY - 4 || Math.Abs(nx - startX) > 90)
                    {
                        continue;
                    }

                    int next = ny * gridWidth + nx;
                    if (!allowedMask[next] || (requireBoundaryConnection && !grid.CanMove(cell, next)))
                    {
                        continue;
                    }

                    double moveCost = MeasureMoveCost(grid, cell, next, dx, dy);
                    if (dy < 0)
                    {
                        moveCost += 12d;
                    }
                    if (dx != 0 && grid.ContainsTrack[cell] && grid.ContainsTrack[next])
                    {
                        moveCost += 8d;
                    }

                    double candidateDistance = distance + moveCost;
                    if (candidateDistance >= distances[next])
                    {
                        continue;
                    }

                    distances[next] = candidateDistance;
                    previous[next] = cell;
                    queue.Enqueue(next, candidateDistance);
                }
            }
        }

        if (best < 0 || previous[best] == -2)
        {
            return new List<int>();
        }

        int deepestDepth = deepestReachable / gridWidth - startY;
        double deepestTargetScore = MeasureDepthUtility(deepestDepth) -
            distances[deepestReachable] -
            Math.Abs(deepestReachable % gridWidth - startX) * 0.35d -
            Math.Abs(deepestReachable % gridWidth - centerCellX) * 0.04d +
            grid.LocalOpenness[deepestReachable] * 22d +
            grid.QualityCounts[deepestReachable] * 0.35d;
        if (!preferDeepestReachable &&
            grid.LocalOpenness[best] >= 0.80f &&
            deepestDepth >= best / gridWidth - startY + 30 &&
            deepestTargetScore >= bestScore - 35d &&
            grid.QualityCounts[deepestReachable] >= 6 &&
            grid.LocalOpenness[deepestReachable] >= 0.20f)
        {
            best = deepestReachable;
        }

        if (preferDeepestReachable &&
            deepestReachable / gridWidth - startY >= 12 &&
            grid.QualityCounts[deepestReachable] >= 4 &&
            grid.LocalOpenness[deepestReachable] >= 0.10f)
        {
            best = deepestReachable;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("TERRARIA_SPLIT_ROUTE_TRACE"), "1", StringComparison.Ordinal))
        {
            Console.WriteLine(
                $"TRACE natural-target start={startX}:{startY} best={best % gridWidth}:{best / gridWidth} " +
                $"bestDepth={best / gridWidth - startY} bestDistance={distances[best]:F1} bestScore={bestScore:F1} " +
                $"bestOpen={grid.LocalOpenness[best]:F3} bestQuality={grid.QualityCounts[best]} " +
                $"deepest={deepestReachable % gridWidth}:{deepestReachable / gridWidth} " +
                $"deepestDepth={deepestReachable / gridWidth - startY} deepestDistance={distances[deepestReachable]:F1} " +
                $"deepestOpen={grid.LocalOpenness[deepestReachable]:F3} deepestQuality={grid.QualityCounts[deepestReachable]}");
        }

        List<int> path = new();
        for (int cell = best; cell >= 0; cell = previous[cell])
        {
            path.Add(cell);
        }

        path.Reverse();
        return path;
    }

    private static double MeasureDepthUtility(int depth)
    {
        int first = Math.Min(depth, 80);
        int middle = Math.Min(Math.Max(0, depth - 80), 60);
        int deep = Math.Max(0, depth - 140);
        return first * 3.2d + middle * 2.00d + deep * 1.80d;
    }

    private static double MeasureMoveCost(RouteGrid grid, int current, int next, int dx, int dy)
    {
        double cost = dy == 0 ? 4.5d : dx == 0 ? 1d : 1.15d;
        int passable = Math.Min(grid.QualityCounts[current], grid.QualityCounts[next]);
        cost += passable switch
        {
            <= 1 => 8d,
            2 => 5d,
            <= 4 => 2.6d,
            <= 7 => 1.1d,
            <= 11 => 0.35d,
            _ => 0d
        };
        double localOpenness = (grid.LocalOpenness[current] + grid.LocalOpenness[next]) * 0.5d;
        cost += Math.Max(0d, 0.24d - localOpenness) * 2.0d;
        int blockerTiles = Math.Max(grid.GeneratedBlockerCounts[current], grid.GeneratedBlockerCounts[next]);
        cost += blockerTiles * 0.45d;
        int connections = grid.CountConnections(current, next);
        if (grid.IsReconstructedConnection(current, next))
        {
            cost += 18d;
        }
        if (connections == 0)
        {
            cost += 6d;
        }
        else if (connections == 1)
        {
            cost += 1.4d;
        }

        return cost;
    }

    private static bool CellContainsTrack(WorldData world, int cellX, int cellY)
    {
        int x0 = cellX * CellSize;
        int y0 = cellY * CellSize;
        for (int x = x0; x < Math.Min(world.Tiles.Width, x0 + CellSize); x++)
        {
            for (int y = y0; y < Math.Min(world.Tiles.Height, y0 + CellSize); y++)
            {
                int index = world.Tiles.Index(x, y);
                if (world.Tiles.Active[index] && world.Tiles.Type[index] is TileIds.MinecartTrack or TileIds.PressureTrack)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int ChooseAlignedDeepTarget(List<int> cells, int gridWidth, int start, int jungleCenterX)
    {
        int startX = start % gridWidth;
        int startY = start / gridWidth;
        int jungleCenterCellX = jungleCenterX / CellSize;
        int best = start;
        double bestScore = double.NegativeInfinity;
        foreach (int cell in cells)
        {
            int x = cell % gridWidth;
            int y = cell / gridWidth;
            int depth = y - startY;
            if (depth <= 0)
            {
                continue;
            }

            int horizontalDrift = Math.Abs(x - startX);
            double score =
                depth * 12d -
                horizontalDrift * 3.5d -
                Math.Abs(x - jungleCenterCellX) * 0.15d;
            if (score > bestScore)
            {
                bestScore = score;
                best = cell;
            }
        }

        return best;
    }

    private static int ChooseDeepestCell(List<int> cells, int gridWidth, int jungleCenterX)
    {
        int centerCellX = Math.Max(0, jungleCenterX / CellSize);
        for (int radius = 80; radius <= 260; radius += 45)
        {
            int radiusBest = -1;
            foreach (int cell in cells)
            {
                int x = cell % gridWidth;
                if (Math.Abs(x - centerCellX) > radius)
                {
                    continue;
                }

                if (radiusBest < 0 || IsBetterDeepTarget(cell, radiusBest, gridWidth, centerCellX))
                {
                    radiusBest = cell;
                }
            }

            if (radiusBest >= 0)
            {
                return radiusBest;
            }
        }

        int best = cells[0];
        foreach (int cell in cells)
        {
            if (IsBetterDeepTarget(cell, best, gridWidth, centerCellX))
            {
                best = cell;
            }
        }

        return best;
    }

    private static bool IsBetterDeepTarget(int candidate, int current, int gridWidth, int centerCellX)
    {
        int candidateY = candidate / gridWidth;
        int currentY = current / gridWidth;
        if (candidateY != currentY)
        {
            return candidateY > currentY;
        }

        return Math.Abs(candidate % gridWidth - centerCellX) < Math.Abs(current % gridWidth - centerCellX);
    }

    private static int ChooseSurfaceStartCell(List<int> cells, int gridWidth, int targetCell)
    {
        int targetX = targetCell % gridWidth;
        int best = cells[0];
        double bestScore = double.PositiveInfinity;
        foreach (int cell in cells)
        {
            int x = cell % gridWidth;
            int y = cell / gridWidth;
            double score = Math.Abs(x - targetX) * 5d + y * 0.25d;
            if (score < bestScore)
            {
                bestScore = score;
                best = cell;
            }
        }

        return best;
    }

    private static List<RouteBridge> FindBridgeHints(bool[] routeMask, int gridWidth, int gridHeight, int jumpCells)
    {
        List<RouteBridge> bridges = new();
        for (int cell = 0; cell < routeMask.Length && bridges.Count < 4000; cell++)
        {
            if (!routeMask[cell])
            {
                continue;
            }

            int cx = cell % gridWidth;
            int cy = cell / gridWidth;
            foreach ((int dx, int dy) in Directions)
            {
                int adjacentX = cx + dx;
                int adjacentY = cy + dy;
                if (adjacentX >= 0 && adjacentY >= 0 && adjacentX < gridWidth && adjacentY < gridHeight && routeMask[adjacentY * gridWidth + adjacentX])
                {
                    continue;
                }

                for (int step = 2; step <= jumpCells; step++)
                {
                    int nx = cx + dx * step;
                    int ny = cy + dy * step;
                    if (nx < 0 || ny < 0 || nx >= gridWidth || ny >= gridHeight)
                    {
                        break;
                    }

                    if (routeMask[ny * gridWidth + nx])
                    {
                        bridges.Add(new RouteBridge(
                            cx * CellSize + CellSize / 2,
                            cy * CellSize + CellSize / 2,
                            nx * CellSize + CellSize / 2,
                            ny * CellSize + CellSize / 2));
                        break;
                    }
                }
            }
        }

        return bridges;
    }

    private static Point FindDeepestPoint(List<int> cells, int gridWidth, int worldWidth, int worldHeight)
    {
        if (cells.Count == 0)
        {
            return Point.Empty;
        }

        int best = cells[0];
        foreach (int cell in cells)
        {
            if (cell / gridWidth > best / gridWidth)
            {
                best = cell;
            }
        }

        int cx = best % gridWidth;
        int cy = best / gridWidth;
        return new Point(
            Math.Clamp(cx * CellSize + CellSize / 2, 0, worldWidth - 1),
            Math.Clamp(cy * CellSize + CellSize / 2, 0, worldHeight - 1));
    }

    internal static bool IsPassable(TileGrid tiles, int x, int y, bool ignoreGeneratedBlockers = true)
    {
        int index = tiles.Index(x, y);
        if (ignoreGeneratedBlockers && GetRouteBlockerKind(tiles, x, y) != RouteBlockerKind.None)
        {
            return true;
        }
        if (tiles.Active[index] && tiles.Type[index] is
            TileIds.LivingWood or
            TileIds.LeafBlock or
            TileIds.LivingMahoganyLeaves or
            TileIds.CorruptThorns or
            TileIds.JungleThorns or
            TileIds.CrimsonThorns or
            TileIds.PlanteraThorns or
            TileIds.BeeHive)
        {
            return true;
        }
        return !tiles.Active[index] || !IsSolidTile(tiles.Type[index]);
    }

    private static bool IsSolidTile(ushort type)
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

    private static bool IsJungleMaterial(ushort type)
    {
        return type is
            TileIds.Mud or
            TileIds.JungleGrass or
            TileIds.JunglePlants or
            TileIds.JungleVines or
            TileIds.JunglePlants2 or
            TileIds.RichMahogany or
            TileIds.Hive or
            TileIds.Larva or
            TileIds.LivingMahogany or
            TileIds.LivingMahoganyLeaves or
            TileIds.BeeHive;
    }

    private static bool IsJungleRouteContextMaterial(ushort type) =>
        IsJungleMaterial(type) && type is not TileIds.Hive and not TileIds.BeeHive and not TileIds.Larva;

    private sealed record CorridorBranch(CorridorNode Attach, List<CorridorNode> Nodes, double Score);

    private readonly record struct RouteJungleMetrics(
        double Average,
        double LowFraction,
        int MaxLowRun,
        double BilateralAverage,
        double BilateralLowFraction,
        int MaxBilateralLowRun);

    private readonly record struct RouteSurfaceMetrics(int MaxNearSurfaceRun, int ShallowHorizontalDrift);

    private readonly record struct RouteCenterMetrics(double Average, double Maximum, double OutsideFraction);

    private sealed class RouteGrid
    {
        public RouteGrid(
            bool[] open,
            ushort[] passableMasks,
            byte[] passableCounts,
            byte[] qualityCounts,
            byte[] generatedBlockerCounts,
            float[] localOpenness,
            float[] jungleContext,
            float[] jungleBilateralSupport,
            bool[] containsTrack,
            int width,
            int height)
        {
            Open = open;
            PassableMasks = passableMasks;
            PassableCounts = passableCounts;
            QualityCounts = qualityCounts;
            GeneratedBlockerCounts = generatedBlockerCounts;
            LocalOpenness = localOpenness;
            JungleContext = jungleContext;
            JungleBilateralSupport = jungleBilateralSupport;
            ContainsTrack = containsTrack;
            Width = width;
            Height = height;
            reconstructedConnections = new bool[Open.Length * Directions.Length];
            connectionCounts = BuildConnectionCounts();
        }

        public bool[] Open { get; }

        public ushort[] PassableMasks { get; }

        public byte[] PassableCounts { get; }

        public byte[] QualityCounts { get; }

        public byte[] GeneratedBlockerCounts { get; }

        public float[] LocalOpenness { get; }

        public float[] JungleContext { get; }

        public float[] JungleBilateralSupport { get; }

        public bool[] ContainsTrack { get; }

        public int Width { get; }

        public int Height { get; }

        private readonly byte[] connectionCounts;
        private readonly bool[] reconstructedConnections;

        public bool CanMove(int from, int to) => CountConnections(from, to) > 0;

        public bool IsReconstructedConnection(int from, int to)
        {
            if (from < 0 || to < 0 || from >= Open.Length || to >= Open.Length)
            {
                return false;
            }

            int dx = to % Width - from % Width;
            int dy = to / Width - from / Width;
            int directionIndex = DirectionIndex(dx, dy);
            return directionIndex >= 0 && reconstructedConnections[from * Directions.Length + directionIndex];
        }

        public int CountConnections(int from, int to)
        {
            if (from < 0 || to < 0 || from >= Open.Length || to >= Open.Length || !Open[from] || !Open[to])
            {
                return 0;
            }

            int fromX = from % Width;
            int fromY = from / Width;
            int toX = to % Width;
            int toY = to / Width;
            int dx = toX - fromX;
            int dy = toY - fromY;
            if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || (dx == 0 && dy == 0))
            {
                return 0;
            }

            int directionIndex = DirectionIndex(dx, dy);
            return directionIndex < 0 ? 0 : connectionCounts[from * Directions.Length + directionIndex];
        }

        private byte[] BuildConnectionCounts()
        {
            byte[] counts = new byte[Open.Length * Directions.Length];
            for (int from = 0; from < Open.Length; from++)
            {
                if (!Open[from])
                {
                    continue;
                }

                int fromX = from % Width;
                int fromY = from / Width;
                for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
                {
                    (int dx, int dy) = Directions[directionIndex];
                    int toX = fromX + dx;
                    int toY = fromY + dy;
                    if (toX < 0 || toY < 0 || toX >= Width || toY >= Height)
                    {
                        continue;
                    }

                    int to = toY * Width + toX;
                    if (to < from)
                    {
                        int reverseDirectionIndex = DirectionIndex(-dx, -dy);
                        counts[from * Directions.Length + directionIndex] = counts[to * Directions.Length + reverseDirectionIndex];
                        reconstructedConnections[from * Directions.Length + directionIndex] = reconstructedConnections[to * Directions.Length + reverseDirectionIndex];
                        continue;
                    }

                    int connectionCount = ComputeConnectionCount(from, to, dx, dy);
                    if (connectionCount == 0 && CanReconstructDiagonalConnection(from, to, dx, dy))
                    {
                        connectionCount = 1;
                        reconstructedConnections[from * Directions.Length + directionIndex] = true;
                    }
                    counts[from * Directions.Length + directionIndex] = (byte)Math.Min(byte.MaxValue, connectionCount);
                }
            }

            return counts;
        }

        private bool CanReconstructDiagonalConnection(int from, int to, int dx, int dy)
        {
            if (dx == 0 || dy == 0 || !Open[from] || !Open[to] ||
                JungleContext[from] < 0.65f || JungleContext[to] < 0.65f ||
                JungleBilateralSupport[from] < 0.60f || JungleBilateralSupport[to] < 0.60f)
            {
                return false;
            }

            return HasMaskGapAtMost(PassableMasks[from], PassableMasks[to], dx, dy, 4);
        }

        private static bool HasMaskGapAtMost(ushort fromMask, ushort toMask, int dx, int dy, int maximumGap)
        {
            for (int fromY = 0; fromY < CellSize; fromY++)
            {
                for (int fromX = 0; fromX < CellSize; fromX++)
                {
                    if (!HasPassable(fromMask, fromX, fromY))
                    {
                        continue;
                    }

                    for (int toY = 0; toY < CellSize; toY++)
                    {
                        for (int toX = 0; toX < CellSize; toX++)
                        {
                            if (!HasPassable(toMask, toX, toY))
                            {
                                continue;
                            }

                            int worldDeltaX = toX + dx * CellSize - fromX;
                            int worldDeltaY = toY + dy * CellSize - fromY;
                            if (Math.Max(Math.Abs(worldDeltaX), Math.Abs(worldDeltaY)) <= maximumGap)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private int ComputeConnectionCount(int from, int to, int dx, int dy)
        {
            if (!Open[from] || !Open[to])
            {
                return 0;
            }

            ushort fromMask = PassableMasks[from];
            ushort toMask = PassableMasks[to];
            int connections = 0;
            if (dx != 0 && dy != 0)
            {
                int fromLocalX = dx > 0 ? CellSize - 1 : 0;
                int fromLocalY = dy > 0 ? CellSize - 1 : 0;
                int toLocalX = dx > 0 ? 0 : CellSize - 1;
                int toLocalY = dy > 0 ? 0 : CellSize - 1;
                return HasPassable(fromMask, fromLocalX, fromLocalY) && HasPassable(toMask, toLocalX, toLocalY) ? 1 : 0;
            }

            if (dx != 0)
            {
                int fromLocalX = dx > 0 ? CellSize - 1 : 0;
                int toLocalX = dx > 0 ? 0 : CellSize - 1;
                for (int fromLocalY = 0; fromLocalY < CellSize; fromLocalY++)
                {
                    if (!HasPassable(fromMask, fromLocalX, fromLocalY))
                    {
                        continue;
                    }

                    for (int toLocalY = Math.Max(0, fromLocalY - 1); toLocalY <= Math.Min(CellSize - 1, fromLocalY + 1); toLocalY++)
                    {
                        if (HasPassable(toMask, toLocalX, toLocalY))
                        {
                            connections++;
                        }
                    }
                }

                return connections;
            }

            int fromLocalBottom = dy > 0 ? CellSize - 1 : 0;
            int toLocalTop = dy > 0 ? 0 : CellSize - 1;
            for (int fromLocalX = 0; fromLocalX < CellSize; fromLocalX++)
            {
                if (!HasPassable(fromMask, fromLocalX, fromLocalBottom))
                {
                    continue;
                }

                for (int toLocalX = Math.Max(0, fromLocalX - 1); toLocalX <= Math.Min(CellSize - 1, fromLocalX + 1); toLocalX++)
                {
                    if (HasPassable(toMask, toLocalX, toLocalTop))
                    {
                        connections++;
                    }
                }
            }

            return connections;
        }

        private static int DirectionIndex(int dx, int dy)
        {
            for (int i = 0; i < Directions.Length; i++)
            {
                if (Directions[i].X == dx && Directions[i].Y == dy)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool HasPassable(ushort mask, int x, int y) =>
            (mask & (1 << (y * CellSize + x))) != 0;
    }

    private sealed class CorridorNode
    {
        public CorridorNode(int rowIndex, int y, int left, int right, double jungleContext, double quality, SurfaceEntrance surfaceEntrance)
        {
            RowIndex = rowIndex;
            Y = y;
            Left = left;
            Right = right;
            JungleContext = jungleContext;
            Quality = quality;
            SurfaceEntrance = surfaceEntrance.IsEntrance;
            SurfaceX = surfaceEntrance.X;
            SurfaceY = surfaceEntrance.Y;
            BestScore = double.NegativeInfinity;
            StartY = y;
        }

        public int RowIndex { get; }

        public int Y { get; }

        public int Left { get; }

        public int Right { get; }

        public int CenterX => (Left + Right) / 2;

        public int Width => Right - Left + 1;

        public double JungleContext { get; }

        public double Quality { get; }

        public bool SurfaceEntrance { get; }

        public int SurfaceX { get; }

        public int SurfaceY { get; }

        public CorridorNode? Previous { get; set; }

        public double BestScore { get; set; }

        public bool HasScore => !double.IsNegativeInfinity(BestScore);

        public int StartY { get; set; }

        public int NodeCount { get; set; }

        public double WidthSum { get; set; }

        public double JungleSum { get; set; }

        public double CenterDistanceSum { get; set; }
    }

    private readonly record struct SurfaceEntrance(bool IsEntrance, int X, int Y, int Votes);

    private readonly record struct RouteBlocker(int X, int TopY, RouteBlockerKind Kind);

    private readonly record struct RouteBlockerBounds(int Left, int Right, int Top, int Bottom);

    private readonly record struct ComponentCavityMetrics(int UpperQuartileRowCells, double VerticalCoverage);

    private enum RouteBlockerKind
    {
        None,
        Temple,
        Hive
    }

    private enum JungleSide
    {
        Left,
        Right
    }

    private readonly record struct JungleCoreBounds(int Left, int Right, int CenterX);

    private readonly record struct ComponentStats(
        int Count,
        int MinX,
        int MaxX,
        int MinY,
        int MaxY,
        bool TouchesSurface)
    {
        public int Width => MaxX - MinX + 1;
        public int Depth => MaxY - MinY + 1;
        public double CenterX => (MinX + MaxX) / 2d;

        public static ComponentStats From(List<int> cells, int gridWidth, double worldSurface)
        {
            int minX = int.MaxValue;
            int maxX = 0;
            int minY = int.MaxValue;
            int maxY = 0;
            foreach (int cell in cells)
            {
                int x = cell % gridWidth;
                int y = cell / gridWidth;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }

            bool touchesSurface = minY * CellSize <= worldSurface + 140 && maxY * CellSize >= worldSurface - 100;
            return new ComponentStats(cells.Count, minX, maxX, minY, maxY, touchesSurface);
        }
    }
}
