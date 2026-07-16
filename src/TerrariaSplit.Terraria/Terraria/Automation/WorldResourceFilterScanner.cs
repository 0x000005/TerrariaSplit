using System.Diagnostics;
using System.Globalization;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class WorldResourceFilterScanner
{
    private const int JungleCostLimit = 50;
    private const int HellavatorCostLimit = 5;
    private const int SolidPenalty = 40;
    private const int InternalUnitsPerDisplayedCost = 10;

    public bool TryScan(
        string worldPath,
        AutoCreateWorldSettings settings,
        out WorldResourceFilterResult result,
        out string detail)
    {
        result = WorldResourceFilterResult.Empty;
        detail = string.Empty;
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            WorldData world = new TerrariaResourceWorldReader().Read(worldPath);
            RouteResult jungleRoute = JungleRouteAnalyzer.Analyze(world, new JungleResourceRouteOptions());
            RouteResult hellavatorRoute = HellavatorResourceRouteFactory.Create(world);
            var jungleField = new WeightedRouteCostField(world, jungleRoute, JungleCostLimit, SolidPenalty);
            var hellavatorField = new WeightedRouteCostField(world, hellavatorRoute, HellavatorCostLimit, SolidPenalty);

            ResourceAccumulator resources = ScanResources(world, jungleField, hellavatorField);
            stopwatch.Stop();
            WorldResourceFilterResult measured = resources.ToResult(
                keep: false,
                stopwatch.Elapsed,
                jungleRoute.DeepestY);
            result = measured with { Keep = WorldResourceFilterMatcher.Matches(settings, measured) };
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException or ArgumentException or InvalidDataException or OverflowException)
        {
            detail = ex.Message;
            StaticAppLogger.Instance.Error(ex, $"World resource filter failed to scan Terraria world file: {worldPath}");
            return false;
        }
    }

    private static ResourceAccumulator ScanResources(
        WorldData world,
        WeightedRouteCostField jungleField,
        WeightedRouteCostField hellavatorField)
    {
        var resources = new ResourceAccumulator();
        TileGrid tiles = world.Tiles;
        for (int x = 0; x < tiles.Width; x++)
        {
            for (int y = 0; y < tiles.Height; y++)
            {
                int index = tiles.Index(x, y);
                if (!tiles.Active[index])
                {
                    continue;
                }

                ushort type = tiles.Type[index];
                if (type == TileIds.Heart && IsTopLeft(tiles.FrameX[index], tiles.FrameY[index], 2, 2))
                {
                    if (Qualifies(jungleField, x, y, 2, 2, JungleCostLimit) ||
                        Qualifies(hellavatorField, x, y, 2, 2, HellavatorCostLimit))
                    {
                        resources.LifeCrystals++;
                    }

                    continue;
                }

                if (TryGetGem(type, tiles.FrameX[index], out string gem) &&
                    Qualifies(hellavatorField, x, y, 1, 1, HellavatorCostLimit))
                {
                    resources.Gems[gem]++;
                }
            }
        }

        foreach (ChestData chest in world.Chests)
        {
            bool jungleNearby = false;
            bool hellavatorNearby = false;
            bool jungleMeasured = false;
            bool hellavatorMeasured = false;
            foreach (ChestItem item in chest.Items)
            {
                ResourceChestKind kind = ResourceChestKindFromItem(item.ItemId);
                if (kind == ResourceChestKind.None)
                {
                    continue;
                }

                if (kind is ResourceChestKind.Boomstick or ResourceChestKind.FeralClaws or ResourceChestKind.AnkletOfTheWind or
                    ResourceChestKind.HermesBoots or ResourceChestKind.CloudBottle or
                    ResourceChestKind.SpelunkerPotion or ResourceChestKind.FeatherfallPotion)
                {
                    if (!jungleMeasured)
                    {
                        jungleNearby = Qualifies(jungleField, chest.X, chest.Y, 2, 2, JungleCostLimit);
                        jungleMeasured = true;
                    }
                }

                if (kind is ResourceChestKind.HermesBoots or ResourceChestKind.CloudBottle or
                    ResourceChestKind.SpelunkerPotion or ResourceChestKind.FeatherfallPotion)
                {
                    if (!hellavatorMeasured)
                    {
                        hellavatorNearby = Qualifies(hellavatorField, chest.X, chest.Y, 2, 2, HellavatorCostLimit);
                        hellavatorMeasured = true;
                    }
                }

                bool nearby = kind switch
                {
                    ResourceChestKind.Boomstick or ResourceChestKind.FeralClaws or ResourceChestKind.AnkletOfTheWind => jungleNearby,
                    _ => jungleNearby || hellavatorNearby
                };
                if (!nearby)
                {
                    continue;
                }

                int stack = Math.Max(1, item.Stack);
                switch (kind)
                {
                    case ResourceChestKind.Boomstick:
                        resources.Boomsticks++;
                        break;
                    case ResourceChestKind.FeralClaws:
                        resources.FeralClaws++;
                        break;
                    case ResourceChestKind.CloudBottle:
                        resources.CloudBottles++;
                        break;
                    case ResourceChestKind.AnkletOfTheWind:
                        resources.AnkletsOfTheWind++;
                        break;
                    case ResourceChestKind.HermesBoots:
                        resources.HermesBoots++;
                        break;
                    case ResourceChestKind.SpelunkerPotion:
                        resources.SpelunkerPotions += stack;
                        break;
                    case ResourceChestKind.FeatherfallPotion:
                        resources.FeatherfallPotions += stack;
                        break;
                }
            }
        }

        return resources;
    }

    private static bool Qualifies(
        WeightedRouteCostField field,
        int x,
        int y,
        int width,
        int height,
        int costLimit)
    {
        WeightedAccessResult access = field.Measure(x, y, width, height, capturePath: false);
        return access.Reachable && access.InternalCost < costLimit * InternalUnitsPerDisplayedCost;
    }

    private static ResourceChestKind ResourceChestKindFromItem(int itemId) => itemId switch
    {
        ItemIds.Boomstick => ResourceChestKind.Boomstick,
        ItemIds.FeralClaws => ResourceChestKind.FeralClaws,
        ItemIds.AnkletOfTheWind => ResourceChestKind.AnkletOfTheWind,
        ItemIds.HermesBoots => ResourceChestKind.HermesBoots,
        ItemIds.CloudinaBottle => ResourceChestKind.CloudBottle,
        ItemIds.SpelunkerPotion => ResourceChestKind.SpelunkerPotion,
        ItemIds.FeatherfallPotion => ResourceChestKind.FeatherfallPotion,
        _ => ResourceChestKind.None
    };

    private static bool TryGetGem(ushort type, short frameX, out string gem)
    {
        gem = type switch
        {
            TileIds.Amethyst => AutoCreateResourceHook.Amethyst,
            TileIds.Topaz => AutoCreateResourceHook.Topaz,
            TileIds.Sapphire => AutoCreateResourceHook.Sapphire,
            TileIds.Emerald => AutoCreateResourceHook.Emerald,
            TileIds.Ruby => AutoCreateResourceHook.Ruby,
            TileIds.Diamond => AutoCreateResourceHook.Diamond,
            TileIds.ExposedGems => ExposedGem(frameX),
            _ => string.Empty
        };
        return gem.Length > 0;
    }

    private static string ExposedGem(short frameX) => (frameX >= 0 ? frameX / 18 : -1) switch
    {
        0 => AutoCreateResourceHook.Amethyst,
        1 => AutoCreateResourceHook.Topaz,
        2 => AutoCreateResourceHook.Sapphire,
        3 => AutoCreateResourceHook.Emerald,
        4 => AutoCreateResourceHook.Ruby,
        5 => AutoCreateResourceHook.Diamond,
        _ => string.Empty
    };

    private static bool IsTopLeft(short frameX, short frameY, int width, int height) =>
        frameX < 0 || frameY < 0 || ((frameX / 18) % width == 0 && (frameY / 18) % height == 0);

    private enum ResourceChestKind
    {
        None,
        Boomstick,
        FeralClaws,
        CloudBottle,
        AnkletOfTheWind,
        HermesBoots,
        SpelunkerPotion,
        FeatherfallPotion
    }

    private sealed class ResourceAccumulator
    {
        public int Boomsticks { get; set; }
        public int FeralClaws { get; set; }
        public int CloudBottles { get; set; }
        public int AnkletsOfTheWind { get; set; }
        public int HermesBoots { get; set; }
        public int LifeCrystals { get; set; }
        public int SpelunkerPotions { get; set; }
        public int FeatherfallPotions { get; set; }
        public Dictionary<string, int> Gems { get; } = AutoCreateResourceHook.All
            .Where(hook => hook != AutoCreateResourceHook.None)
            .ToDictionary(hook => hook, _ => 0, StringComparer.Ordinal);

        public WorldResourceFilterResult ToResult(bool keep, TimeSpan duration, int jungleRouteDeepestY) => new(
            keep,
            Boomsticks,
            FeralClaws,
            CloudBottles,
            AnkletsOfTheWind,
            HermesBoots,
            LifeCrystals,
            SpelunkerPotions,
            FeatherfallPotions,
            new Dictionary<string, int>(Gems, StringComparer.Ordinal),
            duration,
            jungleRouteDeepestY);
    }
}

internal static class WorldResourceFilterMatcher
{
    public static bool Matches(AutoCreateWorldSettings settings, WorldResourceFilterResult resources)
    {
        if (resources.JungleRouteDeepestY < AutoCreateJungleRouteDepth.MinimumY(settings.JungleRouteDepth))
        {
            return false;
        }

        int mask = AutoCreateResourceFilterItem.NormalizeMask(settings.ResourceFilterItemMask);
        if ((mask & AutoCreateResourceFilterItem.BoomstickMask) != 0 && resources.Boomsticks == 0)
        {
            return false;
        }

        if ((mask & AutoCreateResourceFilterItem.FeralClawsMask) != 0 && resources.FeralClaws == 0)
        {
            return false;
        }

        if ((mask & AutoCreateResourceFilterItem.CloudInABottleMask) != 0 && resources.CloudBottles == 0)
        {
            return false;
        }

        if ((mask & AutoCreateResourceFilterItem.AnkletOfTheWindMask) != 0 && resources.AnkletsOfTheWind == 0)
        {
            return false;
        }

        if ((mask & AutoCreateResourceFilterItem.HermesBootsMask) != 0 && resources.HermesBoots == 0)
        {
            return false;
        }

        if (resources.LifeCrystals < AutoCreateResourceMinimum.NormalizeLifeCrystals(settings.ResourceFilterLifeCrystalMinimum) ||
            resources.SpelunkerPotions < AutoCreateResourceMinimum.NormalizePotions(settings.ResourceFilterSpelunkerPotionMinimum) ||
            resources.FeatherfallPotions < AutoCreateResourceMinimum.NormalizePotions(settings.ResourceFilterFeatherfallPotionMinimum))
        {
            return false;
        }

        string minimumHook = AutoCreateResourceHook.Normalize(settings.ResourceFilterHookMinimum);
        if (minimumHook == AutoCreateResourceHook.None)
        {
            return true;
        }

        return AutoCreateResourceHook.All
            .Where(hook => hook != AutoCreateResourceHook.None && AutoCreateResourceHook.Includes(minimumHook, hook))
            .Any(hook => resources.Gems.GetValueOrDefault(hook) >= AutoCreateResourceHook.RequiredGemCount);
    }
}

internal static class HellavatorResourceRouteFactory
{
    private const int HalfWidth = 8;
    private const int UnderworldHeight = 200;
    private const int CellSize = 4;

    public static RouteResult Create(WorldData world)
    {
        int gridWidth = (world.Tiles.Width + CellSize - 1) / CellSize;
        int gridHeight = (world.Tiles.Height + CellSize - 1) / CellSize;
        bool[] routeMask = new bool[gridWidth * gridHeight];
        ushort[] passableMasks = new ushort[routeMask.Length];
        int left = Math.Max(0, world.Header.SpawnX - HalfWidth);
        int right = Math.Min(world.Tiles.Width - 1, world.Header.SpawnX + HalfWidth);
        int top = Math.Clamp(world.Header.SpawnY, 0, world.Tiles.Height - 1);
        int bottom = Math.Clamp(world.Tiles.Height - UnderworldHeight, top, world.Tiles.Height - 1);

        for (int y = top; y <= bottom; y++)
        {
            int cellY = y / CellSize;
            int localY = y % CellSize;
            for (int x = left; x <= right; x++)
            {
                int cellX = x / CellSize;
                int localX = x % CellSize;
                int cell = cellY * gridWidth + cellX;
                routeMask[cell] = true;
                passableMasks[cell] |= (ushort)(1 << (localY * CellSize + localX));
            }
        }

        return new RouteResult(
            "Hellavator",
            routeMask,
            gridWidth,
            gridHeight,
            CellSize,
            routeMask.Count(value => value),
            world.Header.SpawnX,
            bottom,
            Array.Empty<RouteBridge>(),
            passableMasks);
    }
}

internal sealed record WorldResourceFilterResult(
    bool Keep,
    int Boomsticks,
    int FeralClaws,
    int CloudBottles,
    int AnkletsOfTheWind,
    int HermesBoots,
    int LifeCrystals,
    int SpelunkerPotions,
    int FeatherfallPotions,
    IReadOnlyDictionary<string, int> Gems,
    TimeSpan ScanDuration,
    int JungleRouteDeepestY = 0)
{
    public static WorldResourceFilterResult Empty { get; } = new(
        false,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        new Dictionary<string, int>(StringComparer.Ordinal),
        TimeSpan.Zero);

    public string FormatSummary()
    {
        string gems = string.Join(
            ",",
            AutoCreateResourceHook.All
                .Where(hook => hook != AutoCreateResourceHook.None)
                .Select(hook => hook + "=" + Gems.GetValueOrDefault(hook).ToString(CultureInfo.InvariantCulture)));
        return $"boomstick={Boomsticks}, claws={FeralClaws}, cloud={CloudBottles}, " +
            $"anklet={AnkletsOfTheWind}, hermes={HermesBoots}, life={LifeCrystals}, " +
            $"spelunker={SpelunkerPotions}, featherfall={FeatherfallPotions}, " +
            $"jungleDepth={JungleRouteDeepestY}, gems=[{gems}]";
    }
}
