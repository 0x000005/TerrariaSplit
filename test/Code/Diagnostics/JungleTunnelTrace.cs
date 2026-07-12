using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using TerrariaSplit.Terraria.Automation;
using TerrariaSplit.Terraria.WorldGeneration;
using TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal static class JungleTunnelTrace
{
    public static bool TryRun(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "jungle-trace", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: jungle-trace <world.wld> [--csv <path>] [--map <path> --overlay <path>]");
            Environment.ExitCode = 2;
            return true;
        }

        string worldPath = Path.GetFullPath(args[1]);
        string csvPath = string.Empty;
        string mapPath = string.Empty;
        string overlayPath = string.Empty;
        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--csv":
                    csvPath = RequireValue(args, ref i);
                    break;
                case "--map":
                    mapPath = RequireValue(args, ref i);
                    break;
                case "--overlay":
                    overlayPath = RequireValue(args, ref i);
                    break;
                default:
                    throw new ArgumentException("Unknown jungle-trace option: " + args[i]);
            }
        }

        var scanner = new TerrariaWorldFilePyramidScanner();
        if (!scanner.TryReadWorldSeedMetadata(worldPath, out TerrariaWorldSeedMetadata metadata, out string detail))
        {
            Console.Error.WriteLine("Could not read world metadata: " + detail);
            Environment.ExitCode = 1;
            return true;
        }

        StageOneReplicaResult result = new StageOneReplicaSimulator().Generate(new WorldSeedMetadata(
            metadata.SeedText,
            metadata.SizeCode,
            metadata.DifficultyCode,
            metadata.HasCrimson,
            metadata.SpecialSeedMask));
        if (!result.IsComplete || result.State.JungleTunnelSteps.Count == 0)
        {
            Console.Error.WriteLine("Could not replay jungle tunnel: " + result.Detail);
            Environment.ExitCode = 1;
            return true;
        }

        IReadOnlyList<JungleTunnelStep> steps = result.State.JungleTunnelSteps;
        int jungleRandNext = result.Run.Results
            .Single(pass => string.Equals(pass.Name, "Jungle", StringComparison.Ordinal))
            .RandNext;
        Point[] centerline = steps
            .Select(step => new Point((int)step.CenterX, (int)step.CenterY))
            .Distinct()
            .ToArray();
        if (!scanner.TryMeasureJungleTunnelAlignment(worldPath, centerline, out JungleTunnelAlignmentResult alignment, out detail))
        {
            Console.Error.WriteLine("Could not measure tunnel alignment: " + detail);
            Environment.ExitCode = 1;
            return true;
        }

        Console.WriteLine(
            $"world={Path.GetFileName(worldPath)} seed={metadata.SeedText} steps={steps.Count} " +
            $"start={Point(steps[0])} end={Point(steps[^1])} " +
            $"openCenters={alignment.OpenCenterCount}/{alignment.SampleCount} ({alignment.OpenRatio:P2}) " +
            $"jungleRandNext={jungleRandNext}");

        if (!string.IsNullOrWhiteSpace(csvPath))
        {
            WriteCsv(Path.GetFullPath(csvPath), steps);
        }

        if (!string.IsNullOrWhiteSpace(mapPath) || !string.IsNullOrWhiteSpace(overlayPath))
        {
            if (string.IsNullOrWhiteSpace(mapPath) || string.IsNullOrWhiteSpace(overlayPath))
            {
                throw new ArgumentException("--map and --overlay must be supplied together.");
            }

            RenderOverlay(
                Path.GetFullPath(mapPath),
                Path.GetFullPath(overlayPath),
                metadata.SizeCode,
                steps);
        }

        return true;
    }

    private static void WriteCsv(string path, IReadOnlyList<JungleTunnelStep> steps)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory());
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine("sequence,centerX,centerY,strength,left,top,rightExclusive,bottomExclusive");
        foreach (JungleTunnelStep step in steps)
        {
            writer.WriteLine(string.Join(
                ',',
                step.Sequence.ToString(CultureInfo.InvariantCulture),
                step.CenterX.ToString("R", CultureInfo.InvariantCulture),
                step.CenterY.ToString("R", CultureInfo.InvariantCulture),
                step.Strength.ToString("R", CultureInfo.InvariantCulture),
                step.Left.ToString(CultureInfo.InvariantCulture),
                step.Top.ToString(CultureInfo.InvariantCulture),
                step.RightExclusive.ToString(CultureInfo.InvariantCulture),
                step.BottomExclusive.ToString(CultureInfo.InvariantCulture)));
        }

        Console.WriteLine("csv=" + path);
    }

    private static void RenderOverlay(
        string mapPath,
        string outputPath,
        int sizeCode,
        IReadOnlyList<JungleTunnelStep> steps)
    {
        (int worldWidth, int worldHeight) = sizeCode switch
        {
            1 => (4200, 1200),
            3 => (8400, 2400),
            _ => (6400, 1800)
        };

        using Image source = Image.FromFile(mapPath);
        using var bitmap = new Bitmap(source);
        float scaleX = bitmap.Width / (float)worldWidth;
        float scaleY = bitmap.Height / (float)worldHeight;
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var routePen = new Pen(Color.FromArgb(245, 255, 70, 30), Math.Max(3f, 2.5f * scaleX));
        PointF[] points = steps
            .Select(step => new PointF((float)step.CenterX * scaleX, (float)step.CenterY * scaleY))
            .ToArray();
        if (points.Length > 1)
        {
            graphics.DrawLines(routePen, points);
        }

        float markerSize = Math.Max(12f, 7f * scaleX);
        DrawMarker(graphics, Brushes.LimeGreen, points[0], markerSize);
        DrawMarker(graphics, Brushes.Red, points[^1], markerSize);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory());
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine("overlay=" + outputPath);
    }

    private static void DrawMarker(Graphics graphics, Brush brush, PointF point, float size)
    {
        graphics.FillEllipse(brush, point.X - size / 2f, point.Y - size / 2f, size, size);
    }

    private static string Point(JungleTunnelStep step)
    {
        return $"({step.CenterX.ToString("0.##", CultureInfo.InvariantCulture)},{step.CenterY.ToString("0.##", CultureInfo.InvariantCulture)})";
    }

    private static string RequireValue(string[] args, ref int index)
    {
        if (++index >= args.Length)
        {
            throw new ArgumentException(args[index - 1] + " requires a value.");
        }

        return args[index];
    }
}
