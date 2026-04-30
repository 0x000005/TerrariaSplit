using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class MainForm : Form
{
    private static readonly Color TransparentKeyColor = Color.FromArgb(1, 2, 3);
    private static readonly TimeSpan MinimumVisibleDelta = TimeSpan.FromMinutes(-1);
    private const int ResizeBorder = 8;

    private readonly SplitTimer runTimer = new();
    private readonly BossSplitTracker splitTracker = new();
    private readonly List<BossSplitRecord> splitRecords = new();
    private readonly TerrariaWorldWatcher watcher = new();
    private readonly System.Windows.Forms.Timer uiTimer = new();
    private readonly Dictionary<string, IconPair> iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ContextMenuStrip contextMenu = new();

    private readonly Font headerFont = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font splitFont = new("Segoe UI", 12f, FontStyle.Regular);
    private readonly Font splitFontBold = new("Segoe UI", 12f, FontStyle.Bold);
    private readonly Font timerFont = new("Segoe UI", 42f, FontStyle.Bold);
    private readonly Font statusFont = new("Segoe UI", 9.5f, FontStyle.Regular);

    private AppSettings settings = AppSettingsStore.Load();
    private TerrariaWatchSnapshot snapshot =
        new(false, null, false, null, TerrariaBossStates.Unknown, false, "waiting for Terraria.exe");

    public MainForm()
    {
        Text = "TerrariaSplit";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(360, 500);
        Size = new Size(480, 640);
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = TransparentKeyColor;
        TransparencyKey = TransparentKeyColor;
        Padding = Padding.Empty;

        contextMenu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (_, _) => Close());
        ContextMenuStrip = contextMenu;

        uiTimer.Interval = 50;
        uiTimer.Tick += (_, _) => Tick();
        uiTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        uiTimer.Stop();
        watcher.Dispose();

        foreach (IconPair iconPair in iconCache.Values)
        {
            iconPair.Lit.Dispose();
            iconPair.Dim.Dispose();
        }

        headerFont.Dispose();
        splitFont.Dispose();
        splitFontBold.Dispose();
        timerFont.Dispose();
        statusFont.Dispose();

        base.OnFormClosed(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, 0xA1, 0x2, 0);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(TransparentKeyColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        DrawOverlay(graphics);
    }

    protected override void WndProc(ref Message m)
    {
        const int wmNcHitTest = 0x84;
        const int htClient = 1;
        const int htLeft = 10;
        const int htRight = 11;
        const int htTop = 12;
        const int htTopLeft = 13;
        const int htTopRight = 14;
        const int htBottom = 15;
        const int htBottomLeft = 16;
        const int htBottomRight = 17;

        base.WndProc(ref m);

        if (m.Msg != wmNcHitTest || m.Result != (IntPtr)htClient)
        {
            return;
        }

        long lParam = m.LParam.ToInt64();
        int x = unchecked((short)(lParam & 0xFFFF));
        int y = unchecked((short)((lParam >> 16) & 0xFFFF));
        Point point = PointToClient(new Point(x, y));

        bool left = point.X <= ResizeBorder;
        bool right = point.X >= ClientSize.Width - ResizeBorder;
        bool top = point.Y <= ResizeBorder;
        bool bottom = point.Y >= ClientSize.Height - ResizeBorder;

        if (left && top)
        {
            m.Result = (IntPtr)htTopLeft;
        }
        else if (right && top)
        {
            m.Result = (IntPtr)htTopRight;
        }
        else if (left && bottom)
        {
            m.Result = (IntPtr)htBottomLeft;
        }
        else if (right && bottom)
        {
            m.Result = (IntPtr)htBottomRight;
        }
        else if (left)
        {
            m.Result = (IntPtr)htLeft;
        }
        else if (right)
        {
            m.Result = (IntPtr)htRight;
        }
        else if (top)
        {
            m.Result = (IntPtr)htTop;
        }
        else if (bottom)
        {
            m.Result = (IntPtr)htBottom;
        }
    }

    private void Tick()
    {
        snapshot = watcher.Poll();

        if (Keyboard.PollPressed(settings.PauseResumeKeys))
        {
            runTimer.TogglePause();
        }

        if (Keyboard.PollPressed(settings.ResetKeys) && CanReset(snapshot))
        {
            runTimer.Reset();
            splitTracker.Reset();
            splitRecords.Clear();
        }

        if (snapshot.EnteredWorld && runTimer.Phase == SplitTimerPhase.NotStarted)
        {
            runTimer.Start();
            splitTracker.OnRunStarted(snapshot);
        }

        if (runTimer.Phase == SplitTimerPhase.Running)
        {
            BossSplitRecord? split = splitTracker.Update(snapshot, runTimer.Elapsed);
            if (split is BossSplitRecord record)
            {
                splitRecords.Add(record);
            }
        }

        Text = $"TerrariaSplit - {FormatTimerPhase()} - {FormatWorldState()}";
        Invalidate();
    }

    private void DrawOverlay(Graphics graphics)
    {
        UiPalette palette = UiPalette.From(settings.Colors);
        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;

        const int margin = 12;
        const int rowGap = 5;
        const int headerHeight = 28;
        const int statusHeight = 22;

        Rectangle bounds = ClientRectangle;
        if (bounds.Width < 160 || bounds.Height < 160)
        {
            return;
        }

        Rectangle content = Rectangle.Inflate(bounds, -margin, -margin);
        int timerHeight = Math.Clamp((int)(content.Height * 0.17), 82, 110);
        int listSpace = content.Height - headerHeight - 8 - timerHeight - statusHeight - 12;
        int rowHeight = Math.Clamp(
            (listSpace - Math.Max(0, statuses.Count - 1) * rowGap) / Math.Max(1, statuses.Count),
            32,
            48);

        if (rowHeight <= 0)
        {
            return;
        }

        int y = content.Y;
        DrawHeader(graphics, new Rectangle(content.X + 4, y, content.Width - 8, headerHeight), palette);
        y += headerHeight + 8;

        for (int i = 0; i < statuses.Count; i++)
        {
            BossSplitStatus status = statuses[i];
            bool isCurrent = i == splitTracker.CurrentIndex && runTimer.Phase != SplitTimerPhase.NotStarted;
            Rectangle rowRect = new(content.X + 2, y, content.Width - 4, rowHeight);
            DrawSplitRow(graphics, rowRect, status, isCurrent, palette);
            y += rowHeight + rowGap;
        }

        y += 2;
        Rectangle timerRect = new(content.X, y, content.Width, timerHeight);
        DrawTimer(graphics, timerRect, palette);

        Rectangle statusRect = new(content.X + 4, timerRect.Bottom - 6, content.Width - 8, statusHeight);
        DrawStatus(graphics, statusRect, palette);
    }

    private void DrawHeader(Graphics graphics, Rectangle rect, UiPalette palette)
    {
        int iconWidth = 48;
        int nameWidth = (int)(rect.Width * 0.43);
        int bestWidth = (int)(rect.Width * 0.24);
        var nameRect = new Rectangle(rect.X + iconWidth, rect.Y, nameWidth, rect.Height);
        var bestRect = new Rectangle(nameRect.Right, rect.Y, bestWidth, rect.Height);
        var currentRect = new Rectangle(bestRect.Right, rect.Y, rect.Right - bestRect.Right, rect.Height);

        using var brush = new SolidBrush(Color.FromArgb(170, palette.WorldRecordText));
        DrawText(graphics, "Split", headerFont, brush, nameRect, ContentAlignment.MiddleLeft);
        DrawText(graphics, "Best", headerFont, brush, bestRect, ContentAlignment.MiddleRight);
        DrawText(graphics, "Current", headerFont, brush, currentRect, ContentAlignment.MiddleRight);
    }

    private void DrawSplitRow(Graphics graphics, Rectangle rect, BossSplitStatus status, bool isCurrent, UiPalette palette)
    {
        Color nameColor = palette.BossNameText;

        if (status.IsCompleted)
        {
            nameColor = palette.CompletedText;
        }
        else if (status.IsSkipped)
        {
            nameColor = palette.SkippedText;
        }
        else if (isCurrent)
        {
            nameColor = palette.CurrentText;
        }

        int iconWidth = 48;
        int nameWidth = (int)(rect.Width * 0.43);
        int bestWidth = (int)(rect.Width * 0.24);
        int iconSize = Math.Min(34, Math.Max(24, rect.Height - 8));
        var iconRect = new Rectangle(rect.X + 4, rect.Y + (rect.Height - iconSize) / 2, iconSize, iconSize);
        var nameRect = new Rectangle(rect.X + iconWidth, rect.Y, nameWidth, rect.Height);
        var bestRect = new Rectangle(nameRect.Right, rect.Y, bestWidth, rect.Height);
        var currentRect = new Rectangle(bestRect.Right, rect.Y, rect.Right - bestRect.Right - 4, rect.Height);

        DrawIcons(graphics, iconRect, status.Definition, status.IsCompleted || status.IsSkipped);

        using var nameBrush = new SolidBrush(nameColor);
        DrawText(graphics, status.Definition.DisplayName, isCurrent ? splitFontBold : splitFont, nameBrush, nameRect, ContentAlignment.MiddleLeft);

        using var bestBrush = new SolidBrush(palette.WorldRecordText);
        DrawText(graphics, FormatWorldRecordTime(status.Definition.Name), splitFont, bestBrush, bestRect, ContentAlignment.MiddleRight);

        RunComparison comparison = GetRunComparison(status, isCurrent);
        using var currentBrush = new SolidBrush(GetRunComparisonColor(comparison, palette));
        DrawText(graphics, FormatRunComparison(comparison), splitFontBold, currentBrush, currentRect, ContentAlignment.MiddleRight);
    }

    private void DrawIcons(Graphics graphics, Rectangle rect, BossSplitDefinition definition, bool lit)
    {
        int count = definition.IconFileNames.Count;
        if (count == 0)
        {
            return;
        }

        if (count == 1)
        {
            IconPair icon = LoadIconPair(definition.IconFileNames[0]);
            graphics.DrawImage(lit ? icon.Lit : icon.Dim, rect);
            return;
        }

        int size = Math.Min(18, rect.Height);
        int totalWidth = count * size + (count - 1) * 2;
        int startX = rect.X + Math.Max(0, (rect.Width - totalWidth) / 2);
        int y = rect.Y + Math.Max(0, (rect.Height - size) / 2);
        for (int i = 0; i < count; i++)
        {
            IconPair icon = LoadIconPair(definition.IconFileNames[i]);
            graphics.DrawImage(lit ? icon.Lit : icon.Dim, new Rectangle(startX + i * (size + 2), y, size, size));
        }
    }

    private void DrawTimer(Graphics graphics, Rectangle rect, UiPalette palette)
    {
        var timeRect = new Rectangle(rect.X + 4, rect.Y - 4, rect.Width - 8, rect.Height - 16);
        using var timerTextBrush = new SolidBrush(palette.TimerText);
        DrawText(graphics, SplitTimerFormatter.Format(runTimer.Elapsed), timerFont, timerTextBrush, timeRect, ContentAlignment.MiddleRight);
    }

    private void DrawStatus(Graphics graphics, Rectangle rect, UiPalette palette)
    {
        string status = $"{FormatTimerPhase()}  {FormatWorldState()}";
        using var brush = new SolidBrush(Color.FromArgb(160, palette.WorldRecordText));
        DrawText(graphics, status, statusFont, brush, rect, ContentAlignment.MiddleRight);
    }

    private string FormatWorldRecordTime(BossSplitName name)
    {
        return settings.TryGetWorldRecordSplit(name, out TimeSpan split)
            ? TimeText.FormatSplit(split)
            : "--";
    }

    private RunComparison GetRunComparison(BossSplitStatus status, bool isCurrent)
    {
        bool hasWorldRecord = settings.TryGetWorldRecordSplit(status.Definition.Name, out TimeSpan worldRecordSplit);

        if (status.Time is TimeSpan completedTime)
        {
            return new RunComparison(completedTime, hasWorldRecord ? completedTime - worldRecordSplit : null);
        }

        if (!isCurrent || runTimer.Phase == SplitTimerPhase.NotStarted)
        {
            return RunComparison.Empty;
        }

        TimeSpan? delta = null;
        if (hasWorldRecord)
        {
            TimeSpan currentDelta = runTimer.Elapsed - worldRecordSplit;
            if (currentDelta >= MinimumVisibleDelta)
            {
                delta = currentDelta;
            }
        }

        return new RunComparison(runTimer.Elapsed, delta);
    }

    private static string FormatRunComparison(RunComparison comparison)
    {
        if (comparison.CurrentTime is not TimeSpan currentTime)
        {
            return string.Empty;
        }

        string current = TimeText.FormatSplit(currentTime);
        return comparison.Delta is TimeSpan delta
            ? $"{current} {TimeText.FormatDelta(delta)}"
            : current;
    }

    private static void DrawText(
        Graphics graphics,
        string text,
        Font font,
        Brush brush,
        Rectangle bounds,
        ContentAlignment alignment)
    {
        using var format = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
            LineAlignment = StringAlignment.Center
        };

        format.Alignment = alignment switch
        {
            ContentAlignment.MiddleRight => StringAlignment.Far,
            ContentAlignment.MiddleCenter => StringAlignment.Center,
            _ => StringAlignment.Near
        };

        graphics.DrawString(text, font, brush, bounds, format);
    }

    private Color GetRunComparisonColor(RunComparison comparison, UiPalette palette)
    {
        TimeSpan? delta = comparison.Delta;
        if (delta is null)
        {
            return palette.CurrentText;
        }

        if (delta < TimeSpan.Zero)
        {
            return palette.DeltaAheadText;
        }

        if (delta > TimeSpan.Zero)
        {
            return palette.DeltaBehindText;
        }

        return palette.DeltaEvenText;
    }

    private static bool CanReset(TerrariaWatchSnapshot snapshot)
    {
        return snapshot.IsGameMenu != false;
    }

    private string FormatTimerPhase()
    {
        return runTimer.Phase switch
        {
            SplitTimerPhase.NotStarted => "READY",
            SplitTimerPhase.Running => "RUNNING",
            SplitTimerPhase.Paused => "PAUSED",
            _ => "UNKNOWN"
        };
    }

    private string FormatWorldState()
    {
        return snapshot.IsGameMenu switch
        {
            true => "menu",
            false => FormatBossSummary(),
            null => "unknown"
        };
    }

    private string FormatBossSummary()
    {
        return $"Skl:{FormatFlag(snapshot.BossStates.Skeletron)} " +
            $"WoF:{FormatFlag(snapshot.BossStates.WallOfFlesh)} " +
            $"ML:{FormatFlag(snapshot.BossStates.MoonLord)}";
    }

    private static string FormatFlag(bool? value)
    {
        return value switch
        {
            true => "down",
            false => "up",
            null => "?"
        };
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(settings);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        settings = form.Result;
        AppSettingsStore.Save(settings);
        Invalidate();
    }

    private IconPair LoadIconPair(string fileName)
    {
        if (iconCache.TryGetValue(fileName, out IconPair? iconPair))
        {
            return iconPair;
        }

        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "BossIcons", fileName);
        Bitmap lit = File.Exists(path) ? new Bitmap(path) : CreatePlaceholderIcon();
        Bitmap dim = CreateDimmedIcon(lit);
        iconPair = new IconPair(lit, dim);
        iconCache[fileName] = iconPair;
        return iconPair;
    }

    private static Bitmap CreateDimmedIcon(Image source)
    {
        var bitmap = new Bitmap(source.Width, source.Height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        var matrix = new ColorMatrix
        {
            Matrix00 = 0.32f,
            Matrix11 = 0.32f,
            Matrix22 = 0.32f,
            Matrix33 = 0.55f
        };
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(matrix);
        graphics.DrawImage(
            source,
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            0,
            0,
            source.Width,
            source.Height,
            GraphicsUnit.Pixel,
            attributes);
        return bitmap;
    }

    private static Bitmap CreatePlaceholderIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.FromArgb(100, 100, 100));
        graphics.FillEllipse(brush, 2, 2, 28, 28);
        return bitmap;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private sealed record IconPair(Image Lit, Image Dim);

    private readonly record struct RunComparison(TimeSpan? CurrentTime, TimeSpan? Delta)
    {
        public static RunComparison Empty => new(null, null);
    }

    private readonly record struct UiPalette(
        Color BossNameText,
        Color WorldRecordText,
        Color CurrentText,
        Color CompletedText,
        Color SkippedText,
        Color DeltaAheadText,
        Color DeltaBehindText,
        Color DeltaEvenText,
        Color TimerText)
    {
        public static UiPalette From(UiColorSettings settings)
        {
            return new UiPalette(
                ColorText.Parse(settings.BossNameText, Color.Gainsboro),
                ColorText.Parse(settings.WorldRecordText, Color.FromArgb(200, 200, 200)),
                ColorText.Parse(settings.CurrentText, Color.White),
                ColorText.Parse(settings.CompletedText, Color.White),
                ColorText.Parse(settings.SkippedText, Color.Gray),
                ColorText.Parse(settings.DeltaAheadText, Color.LightGreen),
                ColorText.Parse(settings.DeltaBehindText, Color.LightCoral),
                ColorText.Parse(settings.DeltaEvenText, Color.Gainsboro),
                ColorText.Parse(settings.TimerText, Color.FromArgb(242, 242, 242)));
        }
    }
}
