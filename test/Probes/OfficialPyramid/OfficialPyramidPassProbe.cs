// Standalone Terraria 1.4.5.8 pass-stop probe for pyramid pre-screen diagnostics.
// This file is intentionally outside test/*.cs so it is not compiled into the test project.
//
// Build with the .NET Framework csc.exe and reference the real Terraria.exe.
// See README.md in this directory.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.WorldBuilding;
using TMain = Terraria.Main;

public static class OfficialPyramidPassProbe
{
    private const string DefaultTerrariaDir = @"C:\Program Files (x86)\Steam\steamapps\common\Terraria";
    private const int SmallWorldWidth = 4200;
    private const int SmallWorldHeight = 1200;
    private const int TargetYMin = 180;
    private const int TargetYMaxExclusive = 420;

    private static string _terrariaDir = DefaultTerrariaDir;
    private static string _dependencyDir = string.Empty;

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            DumpException(ex);
            return 2;
        }
    }

    private static int Run(string[] args)
    {
        var options = ProbeOptions.Parse(args);
        if (!options.IsValid)
        {
            PrintUsage();
            return 1;
        }

        _terrariaDir = options.TerrariaDir;
        _dependencyDir = options.DependencyDir;
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

        SetupPaths();
        InitTerraria();

        var allRows = new List<ProbeRow>();
        foreach (int seed in options.Seeds)
        {
            Console.Error.WriteLine("[OfficialProbe] seed " + seed.ToString(CultureInfo.InvariantCulture));
            allRows.AddRange(RunSeed(seed));
        }

        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new StreamWriter(options.OutputPath, false))
            {
                WriteCsv(writer, allRows);
            }
        }
        else
        {
            WriteCsv(Console.Out, allRows);
        }

        return 0;
    }

    private static Assembly ResolveAssembly(object sender, ResolveEventArgs e)
    {
        string name = new AssemblyName(e.Name).Name;
        if (string.Equals(name, "Terraria", StringComparison.OrdinalIgnoreCase))
        {
            return Assembly.LoadFrom(Path.Combine(_terrariaDir, "Terraria.exe"));
        }

        foreach (string baseDir in CandidateDependencyDirs())
        {
            string dllPath = Path.Combine(baseDir, name + ".dll");
            if (File.Exists(dllPath))
            {
                return Assembly.LoadFrom(dllPath);
            }

            string exePath = Path.Combine(baseDir, name + ".exe");
            if (File.Exists(exePath))
            {
                return Assembly.LoadFrom(exePath);
            }

            if (!Directory.Exists(baseDir))
            {
                continue;
            }

            foreach (string candidatePath in Directory.EnumerateFiles(baseDir, "*.dll", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (string.Equals(
                            AssemblyName.GetAssemblyName(candidatePath).Name,
                            name,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return Assembly.LoadFrom(candidatePath);
                    }
                }
                catch (BadImageFormatException)
                {
                }
                catch (FileLoadException)
                {
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateDependencyDirs()
    {
        if (!string.IsNullOrWhiteSpace(_dependencyDir))
        {
            yield return _dependencyDir;
        }

        string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrWhiteSpace(exeDir))
        {
            yield return exeDir;
            string found = FindDependencyDir(exeDir);
            if (!string.IsNullOrWhiteSpace(found))
            {
                yield return found;
            }
        }

        yield return _terrariaDir;

        string cwdFound = FindDependencyDir(Environment.CurrentDirectory);
        if (!string.IsNullOrWhiteSpace(cwdFound))
        {
            yield return cwdFound;
        }
    }

    private static string FindDependencyDir(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "reference",
                "Terraria1458");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return string.Empty;
    }

    private static void DumpException(Exception ex)
    {
        for (Exception e = ex; e != null; e = e.InnerException)
        {
            Console.Error.WriteLine("[EX] " + e.GetType().FullName + ": " + e.Message);
            Console.Error.WriteLine(e.StackTrace);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SetupPaths()
    {
        Terraria.Program.LaunchParameters = new Dictionary<string, string>();
        Terraria.Program.SavePath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "SaveData");
        Directory.CreateDirectory(Terraria.Program.SavePath);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitTerraria()
    {
        LanguageManager.Instance.SetLanguage(GameCulture.DefaultCulture);
        TMain main = new TMain();
        Lang.InitializeLegacyLocalization();
        Terraria.Social.SocialAPI.Initialize(Terraria.Social.SocialMode.None);
        TMain.dedServ = true;
        TMain.showSplash = false;
        MethodInfo init = typeof(TMain).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic);
        init.Invoke(main, null);
        TMain.gameMenu = true;
    }

    private static IReadOnlyList<ProbeRow> RunSeed(int seed)
    {
        var rows = new List<ProbeRow>();
        var context = new SeedProbeContext(seed, rows);
        var sw = Stopwatch.StartNew();

        TMain.maxTilesX = SmallWorldWidth;
        TMain.maxTilesY = SmallWorldHeight;
        TMain.GameMode = 0;
        WorldGen.WorldGenParam_Evil = 1;
        WorldGenerationOptions.SelectOption(null);
        TMain.worldName = "official_probe";
        TMain.ActiveWorldFileData = WorldFile.CreateMetadata("official_probe", false, TMain.GameMode);
        TMain.ActiveWorldFileData.SetSeed(seed.ToString(CultureInfo.InvariantCulture));

        var controller = new WorldGenerator.Controller();
        controller.PauseOnHashMismatch = false;
        controller.OnPassesLoaded = delegate(WorldGenerator.Controller c)
        {
            for (int i = 0; i < c.Passes.Count; i++)
            {
                GenPass pass = c.Passes[i];
                string name = pass.Name;
                if (name == "Dunes")
                {
                    c.Passes[i] = new ProbePass(pass, null, delegate
                    {
                        CaptureCheckpoint(context, "after:Dunes", sw.ElapsedMilliseconds);
                        context.DunesCandidateCount = CurrentCandidateCount();
                    });
                }
                else if (name == "Ocean Sand")
                {
                    c.Passes[i] = new ProbePass(pass, null, delegate
                    {
                        CaptureCheckpoint(context, "after:Ocean Sand", sw.ElapsedMilliseconds);
                        context.OceanCandidateCount = CurrentCandidateCount();
                    });
                }
                else if (name == "Jungle")
                {
                    c.Passes[i] = new ProbePass(pass, null, delegate
                    {
                        CaptureCheckpoint(context, "after:Jungle", sw.ElapsedMilliseconds);
                    });
                }
                else if (name == "Mud Caves To Grass")
                {
                    c.Passes[i] = new ProbePass(pass, null, delegate
                    {
                        CaptureCheckpoint(context, "after:Mud Caves To Grass", sw.ElapsedMilliseconds);
                    });
                }
                else if (name == "Full Desert")
                {
                    c.Passes[i] = new ProbePass(pass, null, delegate
                    {
                        CaptureCheckpoint(context, "after:Full Desert", sw.ElapsedMilliseconds);
                    });
                }
                else if (name == "Corruption")
                {
                    c.Passes[i] = new ProbePass(pass, null, delegate
                    {
                        CaptureCheckpoint(context, "after:Corruption", sw.ElapsedMilliseconds);
                    });
                }
                else if (name == "Clean Up Dirt")
                {
                    c.Passes[i] = new ProbePass(pass, null, delegate
                    {
                        CaptureCheckpoint(context, "after:Clean Up Dirt", sw.ElapsedMilliseconds);
                    });
                }
                else if (name == "Pyramids")
                {
                    c.Passes[i] = new ProbePass(pass, delegate
                    {
                        CaptureCheckpoint(context, "before:Pyramids", sw.ElapsedMilliseconds);
                    }, delegate
                    {
                        CaptureCheckpoint(context, "after:Pyramids", sw.ElapsedMilliseconds);
                        c.QueuedAbort = true;
                    });
                }
            }
        };

        bool finished = WorldGen.GenerateWorld(null, controller);
        sw.Stop();
        rows.Add(ProbeRow.CreateSummary(seed, finished, sw.ElapsedMilliseconds));
        return rows;
    }

    private static int CurrentCandidateCount()
    {
        if (GenVars.PyrX == null || GenVars.PyrY == null)
        {
            return 0;
        }

        return GenVars.numPyr;
    }

    private static void CaptureCheckpoint(SeedProbeContext context, string checkpoint, long elapsedMilliseconds)
    {
        int targetTileCount;
        int targetFirstX;
        int targetFirstY;
        CountPyramidTiles(TargetXMin(), TargetXMaxExclusive(), TargetYMin, TargetYMaxExclusive, out targetTileCount, out targetFirstX, out targetFirstY);

        int allTileCount;
        int allFirstX;
        int allFirstY;
        CountPyramidTiles(0, TMain.maxTilesX, 100, 500, out allTileCount, out allFirstX, out allFirstY);

        string targetChestClass;
        string targetChestSummary;
        ScanTargetChests(out targetChestClass, out targetChestSummary);
        int targetCopperPiles;
        int targetSilverPiles;
        int targetGoldPiles;
        CountTargetCoinPiles(out targetCopperPiles, out targetSilverPiles, out targetGoldPiles);

        int count = CurrentCandidateCount();
        if (count == 0)
        {
            context.Rows.Add(ProbeRow.CreateCheckpoint(
                context.Seed,
                checkpoint,
                elapsedMilliseconds,
                targetTileCount,
                targetFirstX,
                targetFirstY,
                allTileCount,
                allFirstX,
                allFirstY,
                targetChestClass,
                targetChestSummary,
                targetCopperPiles,
                targetSilverPiles,
                targetGoldPiles));
            return;
        }

        for (int i = 0; i < count; i++)
        {
            int x = GenVars.PyrX[i];
            int y = GenVars.PyrY[i];
            CandidateAnalysis analysis = AnalyzeCandidate(i, x, y);
            context.Rows.Add(ProbeRow.CreateCandidate(
                context.Seed,
                checkpoint,
                elapsedMilliseconds,
                i,
                CandidateOrigin(context, checkpoint, i),
                x,
                y,
                analysis,
                targetTileCount,
                targetFirstX,
                targetFirstY,
                allTileCount,
                allFirstX,
                allFirstY,
                targetChestClass,
                targetChestSummary,
                targetCopperPiles,
                targetSilverPiles,
                targetGoldPiles));
        }
    }

    private static string CandidateOrigin(SeedProbeContext context, string checkpoint, int index)
    {
        if (checkpoint == "after:Dunes")
        {
            return "Dunes";
        }

        if (context.DunesCandidateCount >= 0)
        {
            if (index < context.DunesCandidateCount)
            {
                return "Dunes";
            }

            return "Ocean Sand";
        }

        return "unknown";
    }

    private static CandidateAnalysis AnalyzeCandidate(int index, int x, int y)
    {
        int scanY = y;
        while (InWorld(x, scanY) && !IsActive(x, scanY) && (double)scanY < TMain.worldSurface)
        {
            scanY++;
        }

        bool scanInWorld = InWorld(x, scanY);
        bool scanActive = scanInWorld && IsActive(x, scanY);
        int scanType = scanActive ? (int)TMain.tile[x, scanY].type : -1;
        int minPreviousDistance = TMain.maxTilesX;
        for (int i = 0; i < index; i++)
        {
            int distance = Math.Abs(x - GenVars.PyrX[i]);
            if (distance < minPreviousDistance)
            {
                minPreviousDistance = distance;
            }
        }

        bool boundaryOk = x > 300 && x < TMain.maxTilesX - 300;
        int dungeonSide = GenVars.CurrentDungeonGenVars.dungeonSide;
        int dungeonX = GenVars.CurrentDungeonGenVars.generatingDungeonPositionX;
        bool dungeonLeftOk = dungeonSide > DungeonSide.Left || !((double)x < (double)dungeonX + (double)TMain.maxTilesX * 0.15);
        bool dungeonRightOk = dungeonSide < DungeonSide.Right || !((double)x > (double)dungeonX - (double)TMain.maxTilesX * 0.15);
        bool surfaceOk = (double)scanY < TMain.worldSurface;
        bool sandOk = surfaceOk && scanActive && scanType == 53;
        bool spacingOk = minPreviousDistance >= 220;
        bool eligible = boundaryOk && dungeonLeftOk && dungeonRightOk && sandOk && spacingOk;

        string reject = RejectReason(boundaryOk, dungeonLeftOk, dungeonRightOk, surfaceOk, sandOk, spacingOk);
        int sandDepth = CountVerticalRun(x, scanY, 53);
        int activeDepth = CountActiveDepth(x, scanY);
        int sandSpan = CountHorizontalRun(x, scanY, 53);

        return new CandidateAnalysis(
            scanY,
            scanActive,
            scanType,
            TileName(scanType),
            minPreviousDistance,
            eligible,
            reject,
            sandDepth,
            sandSpan,
            activeDepth,
            dungeonSide,
            dungeonX,
            TMain.worldSurface);
    }

    private static string RejectReason(bool boundaryOk, bool dungeonLeftOk, bool dungeonRightOk, bool surfaceOk, bool sandOk, bool spacingOk)
    {
        var reasons = new List<string>();
        if (!boundaryOk)
        {
            reasons.Add("boundary");
        }

        if (!dungeonLeftOk)
        {
            reasons.Add("dungeon-left");
        }

        if (!dungeonRightOk)
        {
            reasons.Add("dungeon-right");
        }

        if (!surfaceOk)
        {
            reasons.Add("below-surface");
        }
        else if (!sandOk)
        {
            reasons.Add("scan-not-sand");
        }

        if (!spacingOk)
        {
            reasons.Add("spacing");
        }

        return reasons.Count == 0 ? "none" : string.Join("|", reasons.ToArray());
    }

    private static bool InWorld(int x, int y)
    {
        return x >= 0 && x < TMain.maxTilesX && y >= 0 && y < TMain.maxTilesY;
    }

    private static bool IsActive(int x, int y)
    {
        Tile tile = TMain.tile[x, y];
        return tile != null && tile.active();
    }

    private static int CountVerticalRun(int x, int startY, int tileType)
    {
        if (!InWorld(x, startY))
        {
            return 0;
        }

        int count = 0;
        for (int y = startY; y < TMain.maxTilesY; y++)
        {
            Tile tile = TMain.tile[x, y];
            if (tile == null || !tile.active() || tile.type != tileType)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static int CountActiveDepth(int x, int startY)
    {
        if (!InWorld(x, startY))
        {
            return 0;
        }

        int count = 0;
        for (int y = startY; y < TMain.maxTilesY; y++)
        {
            Tile tile = TMain.tile[x, y];
            if (tile == null || !tile.active())
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static int CountHorizontalRun(int x, int y, int tileType)
    {
        if (!InWorld(x, y))
        {
            return 0;
        }

        Tile center = TMain.tile[x, y];
        if (center == null || !center.active() || center.type != tileType)
        {
            return 0;
        }

        int left = x;
        while (left - 1 >= 0)
        {
            Tile tile = TMain.tile[left - 1, y];
            if (tile == null || !tile.active() || tile.type != tileType)
            {
                break;
            }

            left--;
        }

        int right = x;
        while (right + 1 < TMain.maxTilesX)
        {
            Tile tile = TMain.tile[right + 1, y];
            if (tile == null || !tile.active() || tile.type != tileType)
            {
                break;
            }

            right++;
        }

        return right - left + 1;
    }

    private static void CountPyramidTiles(
        int minX,
        int maxXExclusive,
        int minY,
        int maxYExclusive,
        out int count,
        out int firstX,
        out int firstY)
    {
        count = 0;
        firstX = -1;
        firstY = -1;
        int clampedMinX = Math.Max(0, minX);
        int clampedMaxX = Math.Min(TMain.maxTilesX, maxXExclusive);
        int clampedMinY = Math.Max(0, minY);
        int clampedMaxY = Math.Min(TMain.maxTilesY, maxYExclusive);

        for (int x = clampedMinX; x < clampedMaxX; x++)
        {
            for (int y = clampedMinY; y < clampedMaxY; y++)
            {
                Tile tile = TMain.tile[x, y];
                if (tile != null && tile.active() && tile.type == 151)
                {
                    count++;
                    if (firstX < 0)
                    {
                        firstX = x;
                        firstY = y;
                    }
                }
            }
        }
    }

    private static void ScanTargetChests(out string targetClass, out string summary)
    {
        targetClass = "none";
        var summaries = new List<string>();
        if (TMain.chest == null)
        {
            summary = string.Empty;
            return;
        }

        for (int i = 0; i < TMain.chest.Length; i++)
        {
            Chest chest = TMain.chest[i];
            if (chest == null)
            {
                continue;
            }

            if (chest.x < TargetXMin() || chest.x >= TargetXMaxExclusive() || chest.y < TargetYMin || chest.y >= TargetYMaxExclusive)
            {
                continue;
            }

            string chestClass = ClassifyChest(chest);
            if (chestClass == "none")
            {
                continue;
            }

            if (targetClass == "none" || chestClass == "flying" || chestClass == "sandstorm")
            {
                targetClass = chestClass;
            }

            summaries.Add(i.ToString(CultureInfo.InvariantCulture) + "@" +
                chest.x.ToString(CultureInfo.InvariantCulture) + ":" +
                chest.y.ToString(CultureInfo.InvariantCulture) + "=" +
                chestClass + ":" + LootSummary(chest));
        }

        summary = string.Join(";", summaries.ToArray());
    }

    private static void CountTargetCoinPiles(out int copper, out int silver, out int gold)
    {
        copper = 0;
        silver = 0;
        gold = 0;
        for (int x = TargetXMin() - 16; x < TargetXMaxExclusive() + 16; x++)
        {
            if (x < 0 || x >= TMain.maxTilesX)
            {
                continue;
            }

            for (int y = TargetYMin - 16; y < TargetYMaxExclusive + 16; y++)
            {
                if (y < 0 || y >= TMain.maxTilesY)
                {
                    continue;
                }

                Tile tile = TMain.tile[x, y];
                if (tile == null || !tile.active() || tile.type != 185 || tile.frameY != 18)
                {
                    continue;
                }

                if (tile.frameX == 576)
                {
                    copper++;
                }
                else if (tile.frameX == 612)
                {
                    silver++;
                }
                else if (tile.frameX == 648)
                {
                    gold++;
                }
            }
        }
    }

    private static string ClassifyChest(Chest chest)
    {
        bool hasFlying = false;
        bool hasSandstorm = false;
        bool hasPyramidLoot = false;

        if (chest.item == null)
        {
            return "none";
        }

        for (int i = 0; i < chest.item.Length; i++)
        {
            Item item = chest.item[i];
            if (item == null || item.type <= 0)
            {
                continue;
            }

            if (item.type == ItemID.FlyingCarpet)
            {
                hasFlying = true;
                hasPyramidLoot = true;
            }
            else if (item.type == ItemID.SandstorminaBottle)
            {
                hasSandstorm = true;
                hasPyramidLoot = true;
            }
            else if (item.type == ItemID.PharaohsMask || item.type == ItemID.PharaohsRobe)
            {
                hasPyramidLoot = true;
            }
        }

        if (hasFlying && hasSandstorm)
        {
            return "flying+sandstorm";
        }

        if (hasFlying)
        {
            return "flying";
        }

        if (hasSandstorm)
        {
            return "sandstorm";
        }

        return hasPyramidLoot ? "other" : "none";
    }

    private static string LootSummary(Chest chest)
    {
        var items = new List<string>();
        if (chest.item == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < chest.item.Length; i++)
        {
            Item item = chest.item[i];
            if (item != null && item.type > 0)
            {
                items.Add(item.type.ToString(CultureInfo.InvariantCulture) + "x" + item.stack.ToString(CultureInfo.InvariantCulture));
            }
        }

        return string.Join("|", items.ToArray());
    }

    private static string TileName(int tileType)
    {
        switch (tileType)
        {
            case -1:
                return "inactive";
            case 0:
                return "dirt";
            case 1:
                return "stone";
            case 53:
                return "sand";
            case 112:
                return "pearlsand";
            case 116:
                return "ebonsand";
            case 117:
                return "hardened-sand";
            case 164:
                return "sandstone";
            case 203:
                return "crimstone";
            case 234:
                return "crimsand";
            case 401:
                return "hardened-crimsand";
            case 404:
                return "crimsandstone";
            default:
                return tileType.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static string RectCsv(object rect)
    {
        if (rect == null)
        {
            return "0;0;0;0";
        }

        Type type = rect.GetType();
        int x = GetIntMember(type, rect, "X");
        int y = GetIntMember(type, rect, "Y");
        int width = GetIntMember(type, rect, "Width");
        int height = GetIntMember(type, rect, "Height");
        return string.Join(
            ";",
            x.ToString(CultureInfo.InvariantCulture),
            y.ToString(CultureInfo.InvariantCulture),
            width.ToString(CultureInfo.InvariantCulture),
            height.ToString(CultureInfo.InvariantCulture));
    }

    private static int GetIntMember(Type type, object instance, string name)
    {
        PropertyInfo property = type.GetProperty(name);
        if (property != null)
        {
            return (int)property.GetValue(instance, null);
        }

        FieldInfo field = type.GetField(name);
        if (field != null)
        {
            return (int)field.GetValue(instance);
        }

        return 0;
    }

    private static int TargetXMin()
    {
        return (int)(TMain.maxTilesX * 0.32);
    }

    private static int TargetXMaxExclusive()
    {
        return (int)(TMain.maxTilesX * 0.68);
    }

    private static void WriteCsv(TextWriter writer, IReadOnlyList<ProbeRow> rows)
    {
        writer.WriteLine(ProbeRow.Header);
        for (int i = 0; i < rows.Count; i++)
        {
            writer.WriteLine(rows[i].FormatCsv());
        }
    }

    private static string Csv(string value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: OfficialPyramidPassProbe [--terraria <dir>] [--deps <dir>] [--out <csv>] <seed> [<seed2> ...]");
        Console.Error.WriteLine("Assumptions: small classic crimson world, no special seeds.");
    }

    private sealed class ProbePass : GenPass
    {
        private readonly GenPass _inner;
        private readonly Action _before;
        private readonly Action _after;

        public ProbePass(GenPass inner, Action before, Action after)
            : base(inner.Name, inner.Weight)
        {
            _inner = inner;
            _before = before;
            _after = after;
            if (!inner.Enabled)
            {
                Disable();
            }
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            if (_before != null)
            {
                _before();
            }

            _inner.Apply(progress, configuration);

            if (_after != null)
            {
                _after();
            }
        }
    }

    private sealed class SeedProbeContext
    {
        public readonly int Seed;
        public readonly List<ProbeRow> Rows;
        public int DunesCandidateCount = -1;
        public int OceanCandidateCount = -1;

        public SeedProbeContext(int seed, List<ProbeRow> rows)
        {
            Seed = seed;
            Rows = rows;
        }
    }

    private sealed class CandidateAnalysis
    {
        public readonly int ScanY;
        public readonly bool ScanActive;
        public readonly int ScanTileType;
        public readonly string ScanTileName;
        public readonly int MinPreviousDistance;
        public readonly bool OfficialEligible;
        public readonly string RejectReason;
        public readonly int SandDepth;
        public readonly int SandSpan;
        public readonly int ActiveDepth;
        public readonly int DungeonSide;
        public readonly int DungeonPositionX;
        public readonly double WorldSurface;

        public CandidateAnalysis(
            int scanY,
            bool scanActive,
            int scanTileType,
            string scanTileName,
            int minPreviousDistance,
            bool officialEligible,
            string rejectReason,
            int sandDepth,
            int sandSpan,
            int activeDepth,
            int dungeonSide,
            int dungeonPositionX,
            double worldSurface)
        {
            ScanY = scanY;
            ScanActive = scanActive;
            ScanTileType = scanTileType;
            ScanTileName = scanTileName;
            MinPreviousDistance = minPreviousDistance;
            OfficialEligible = officialEligible;
            RejectReason = rejectReason;
            SandDepth = sandDepth;
            SandSpan = sandSpan;
            ActiveDepth = activeDepth;
            DungeonSide = dungeonSide;
            DungeonPositionX = dungeonPositionX;
            WorldSurface = worldSurface;
        }
    }

    private sealed class ProbeRow
    {
        public const string Header =
            "seed,checkpoint,rowKind,candidateIndex,candidateOrigin,candidateX,candidateY,scanY,scanActive,scanTileType,scanTileName,minPreviousDistance,officialEligible,rejectReason,sandDepth,sandSpan,activeDepth,dungeonSide,dungeonPositionX,worldSurface,undergroundDesert,undergroundDesertHive,targetPyramidTiles,targetPyramidFirstX,targetPyramidFirstY,allSurfacePyramidTiles,allSurfaceFirstX,allSurfaceFirstY,targetChestClass,targetChestSummary,targetCopperPiles,targetSilverPiles,targetGoldPiles,finished,durationMs";

        private readonly int _seed;
        private readonly string _checkpoint;
        private readonly string _rowKind;
        private readonly int _candidateIndex;
        private readonly string _candidateOrigin;
        private readonly int _candidateX;
        private readonly int _candidateY;
        private readonly int _scanY;
        private readonly bool _scanActive;
        private readonly int _scanTileType;
        private readonly string _scanTileName;
        private readonly int _minPreviousDistance;
        private readonly bool _officialEligible;
        private readonly string _rejectReason;
        private readonly int _sandDepth;
        private readonly int _sandSpan;
        private readonly int _activeDepth;
        private readonly int _dungeonSide;
        private readonly int _dungeonPositionX;
        private readonly double _worldSurface;
        private readonly string _undergroundDesert;
        private readonly string _undergroundDesertHive;
        private readonly int _targetPyramidTiles;
        private readonly int _targetPyramidFirstX;
        private readonly int _targetPyramidFirstY;
        private readonly int _allSurfacePyramidTiles;
        private readonly int _allSurfaceFirstX;
        private readonly int _allSurfaceFirstY;
        private readonly string _targetChestClass;
        private readonly string _targetChestSummary;
        private readonly int _targetCopperPiles;
        private readonly int _targetSilverPiles;
        private readonly int _targetGoldPiles;
        private readonly bool _finished;
        private readonly long _durationMilliseconds;

        private ProbeRow(
            int seed,
            string checkpoint,
            string rowKind,
            int candidateIndex,
            string candidateOrigin,
            int candidateX,
            int candidateY,
            int scanY,
            bool scanActive,
            int scanTileType,
            string scanTileName,
            int minPreviousDistance,
            bool officialEligible,
            string rejectReason,
            int sandDepth,
            int sandSpan,
            int activeDepth,
            int dungeonSide,
            int dungeonPositionX,
            double worldSurface,
            string undergroundDesert,
            string undergroundDesertHive,
            int targetPyramidTiles,
            int targetPyramidFirstX,
            int targetPyramidFirstY,
            int allSurfacePyramidTiles,
            int allSurfaceFirstX,
            int allSurfaceFirstY,
            string targetChestClass,
            string targetChestSummary,
            int targetCopperPiles,
            int targetSilverPiles,
            int targetGoldPiles,
            bool finished,
            long durationMilliseconds)
        {
            _seed = seed;
            _checkpoint = checkpoint;
            _rowKind = rowKind;
            _candidateIndex = candidateIndex;
            _candidateOrigin = candidateOrigin;
            _candidateX = candidateX;
            _candidateY = candidateY;
            _scanY = scanY;
            _scanActive = scanActive;
            _scanTileType = scanTileType;
            _scanTileName = scanTileName;
            _minPreviousDistance = minPreviousDistance;
            _officialEligible = officialEligible;
            _rejectReason = rejectReason;
            _sandDepth = sandDepth;
            _sandSpan = sandSpan;
            _activeDepth = activeDepth;
            _dungeonSide = dungeonSide;
            _dungeonPositionX = dungeonPositionX;
            _worldSurface = worldSurface;
            _undergroundDesert = undergroundDesert;
            _undergroundDesertHive = undergroundDesertHive;
            _targetPyramidTiles = targetPyramidTiles;
            _targetPyramidFirstX = targetPyramidFirstX;
            _targetPyramidFirstY = targetPyramidFirstY;
            _allSurfacePyramidTiles = allSurfacePyramidTiles;
            _allSurfaceFirstX = allSurfaceFirstX;
            _allSurfaceFirstY = allSurfaceFirstY;
            _targetChestClass = targetChestClass;
            _targetChestSummary = targetChestSummary;
            _targetCopperPiles = targetCopperPiles;
            _targetSilverPiles = targetSilverPiles;
            _targetGoldPiles = targetGoldPiles;
            _finished = finished;
            _durationMilliseconds = durationMilliseconds;
        }

        public static ProbeRow CreateCheckpoint(
            int seed,
            string checkpoint,
            long elapsedMilliseconds,
            int targetPyramidTiles,
            int targetPyramidFirstX,
            int targetPyramidFirstY,
            int allSurfacePyramidTiles,
            int allSurfaceFirstX,
            int allSurfaceFirstY,
            string targetChestClass,
            string targetChestSummary,
            int targetCopperPiles,
            int targetSilverPiles,
            int targetGoldPiles)
        {
            return new ProbeRow(
                seed,
                checkpoint,
                "checkpoint",
                -1,
                string.Empty,
                -1,
                -1,
                -1,
                false,
                -1,
                string.Empty,
                -1,
                false,
                string.Empty,
                0,
                0,
                0,
                SafeDungeonSide(),
                SafeDungeonPositionX(),
                TMain.worldSurface,
                RectCsv(GenVars.UndergroundDesertLocation),
                RectCsv(GenVars.UndergroundDesertHiveLocation),
                targetPyramidTiles,
                targetPyramidFirstX,
                targetPyramidFirstY,
                allSurfacePyramidTiles,
                allSurfaceFirstX,
                allSurfaceFirstY,
                targetChestClass,
                targetChestSummary,
                targetCopperPiles,
                targetSilverPiles,
                targetGoldPiles,
                false,
                elapsedMilliseconds);
        }

        public static ProbeRow CreateCandidate(
            int seed,
            string checkpoint,
            long elapsedMilliseconds,
            int index,
            string origin,
            int x,
            int y,
            CandidateAnalysis analysis,
            int targetPyramidTiles,
            int targetPyramidFirstX,
            int targetPyramidFirstY,
            int allSurfacePyramidTiles,
            int allSurfaceFirstX,
            int allSurfaceFirstY,
            string targetChestClass,
            string targetChestSummary,
            int targetCopperPiles,
            int targetSilverPiles,
            int targetGoldPiles)
        {
            return new ProbeRow(
                seed,
                checkpoint,
                "candidate",
                index,
                origin,
                x,
                y,
                analysis.ScanY,
                analysis.ScanActive,
                analysis.ScanTileType,
                analysis.ScanTileName,
                analysis.MinPreviousDistance,
                analysis.OfficialEligible,
                analysis.RejectReason,
                analysis.SandDepth,
                analysis.SandSpan,
                analysis.ActiveDepth,
                analysis.DungeonSide,
                analysis.DungeonPositionX,
                analysis.WorldSurface,
                RectCsv(GenVars.UndergroundDesertLocation),
                RectCsv(GenVars.UndergroundDesertHiveLocation),
                targetPyramidTiles,
                targetPyramidFirstX,
                targetPyramidFirstY,
                allSurfacePyramidTiles,
                allSurfaceFirstX,
                allSurfaceFirstY,
                targetChestClass,
                targetChestSummary,
                targetCopperPiles,
                targetSilverPiles,
                targetGoldPiles,
                false,
                elapsedMilliseconds);
        }

        public static ProbeRow CreateSummary(int seed, bool finished, long durationMilliseconds)
        {
            return new ProbeRow(
                seed,
                "summary",
                "summary",
                -1,
                string.Empty,
                -1,
                -1,
                -1,
                false,
                -1,
                string.Empty,
                -1,
                false,
                string.Empty,
                0,
                0,
                0,
                SafeDungeonSide(),
                SafeDungeonPositionX(),
                TMain.worldSurface,
                RectCsv(GenVars.UndergroundDesertLocation),
                RectCsv(GenVars.UndergroundDesertHiveLocation),
                -1,
                -1,
                -1,
                -1,
                -1,
                -1,
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                finished,
                durationMilliseconds);
        }

        private static int SafeDungeonSide()
        {
            try
            {
                return GenVars.CurrentDungeonGenVars.dungeonSide;
            }
            catch
            {
                return 0;
            }
        }

        private static int SafeDungeonPositionX()
        {
            try
            {
                return GenVars.CurrentDungeonGenVars.generatingDungeonPositionX;
            }
            catch
            {
                return 0;
            }
        }

        public string FormatCsv()
        {
            return string.Join(
                ",",
                _seed.ToString(CultureInfo.InvariantCulture),
                Csv(_checkpoint),
                Csv(_rowKind),
                _candidateIndex.ToString(CultureInfo.InvariantCulture),
                Csv(_candidateOrigin),
                _candidateX.ToString(CultureInfo.InvariantCulture),
                _candidateY.ToString(CultureInfo.InvariantCulture),
                _scanY.ToString(CultureInfo.InvariantCulture),
                _scanActive ? "true" : "false",
                _scanTileType.ToString(CultureInfo.InvariantCulture),
                Csv(_scanTileName),
                _minPreviousDistance.ToString(CultureInfo.InvariantCulture),
                _officialEligible ? "true" : "false",
                Csv(_rejectReason),
                _sandDepth.ToString(CultureInfo.InvariantCulture),
                _sandSpan.ToString(CultureInfo.InvariantCulture),
                _activeDepth.ToString(CultureInfo.InvariantCulture),
                _dungeonSide.ToString(CultureInfo.InvariantCulture),
                _dungeonPositionX.ToString(CultureInfo.InvariantCulture),
                _worldSurface.ToString("F3", CultureInfo.InvariantCulture),
                Csv(_undergroundDesert),
                Csv(_undergroundDesertHive),
                _targetPyramidTiles.ToString(CultureInfo.InvariantCulture),
                _targetPyramidFirstX.ToString(CultureInfo.InvariantCulture),
                _targetPyramidFirstY.ToString(CultureInfo.InvariantCulture),
                _allSurfacePyramidTiles.ToString(CultureInfo.InvariantCulture),
                _allSurfaceFirstX.ToString(CultureInfo.InvariantCulture),
                _allSurfaceFirstY.ToString(CultureInfo.InvariantCulture),
                Csv(_targetChestClass),
                Csv(_targetChestSummary),
                _targetCopperPiles.ToString(CultureInfo.InvariantCulture),
                _targetSilverPiles.ToString(CultureInfo.InvariantCulture),
                _targetGoldPiles.ToString(CultureInfo.InvariantCulture),
                _finished ? "true" : "false",
                _durationMilliseconds.ToString(CultureInfo.InvariantCulture));
        }
    }

    private sealed class ProbeOptions
    {
        public readonly string TerrariaDir;
        public readonly string DependencyDir;
        public readonly string OutputPath;
        public readonly List<int> Seeds;
        public readonly bool IsValid;

        private ProbeOptions(string terrariaDir, string dependencyDir, string outputPath, List<int> seeds, bool isValid)
        {
            TerrariaDir = terrariaDir;
            DependencyDir = dependencyDir;
            OutputPath = outputPath;
            Seeds = seeds;
            IsValid = isValid;
        }

        public static ProbeOptions Parse(string[] args)
        {
            string terrariaDir = DefaultTerrariaDir;
            string dependencyDir = string.Empty;
            string outputPath = string.Empty;
            var seeds = new List<int>();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--terraria" && i + 1 < args.Length)
                {
                    terrariaDir = args[++i];
                }
                else if (arg == "--deps" && i + 1 < args.Length)
                {
                    dependencyDir = args[++i];
                }
                else if (arg == "--out" && i + 1 < args.Length)
                {
                    outputPath = args[++i];
                }
                else
                {
                    int seed;
                    if (!int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
                    {
                        return new ProbeOptions(terrariaDir, dependencyDir, outputPath, seeds, false);
                    }

                    seeds.Add(seed);
                }
            }

            bool isValid = seeds.Count > 0 && Directory.Exists(terrariaDir);
            return new ProbeOptions(terrariaDir, dependencyDir, outputPath, seeds, isValid);
        }
    }
}
