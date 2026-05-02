using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class MainForm : Form
{
    private static readonly Color TransparentKeyColor = Color.FromArgb(1, 2, 3);
    private static readonly TimeSpan MaximumVisibleDeltaDistance = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SplitCompletionFadeDuration = TimeSpan.FromSeconds(0.45);
    private const int ResizeBorder = 8;
    private const int RowGap = 9;

    private readonly SplitTimer runTimer = new();
    private readonly BossSplitTracker splitTracker = new();
    private readonly TerrariaWorldWatcher watcher = new();
    private readonly System.Windows.Forms.Timer uiTimer = new();
    private readonly Dictionary<string, IconPair> iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<FontKey, Font> fontCache = new();
    private readonly Dictionary<int, SegmentBestDeltaHighlight> segmentBestDeltaHighlights = new();
    private readonly ContextMenuStrip contextMenu = new();
    private bool mouseClickThrough;
    private bool dragging;
    private Point dragStartCursor;
    private Point dragStartLocation;
    private SplitCompletionAnimation? splitCompletionAnimation;
    private bool runStatsRecorded;

    private AppSettings settings = AppSettingsStore.Load();
    private TerrariaWatchSnapshot snapshot =
        new(false, null, false, null, TerrariaBossStates.Unknown, false, "waiting for Terraria.exe");

    public MainForm()
    {
        splitTracker.SetDefinitions(BossSplitDefinitions.Build(settings));
        Text = "TerrariaSplit";
        TopMost = settings.AlwaysOnTop;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = GetMinimumWindowSize(settings);
        Size = new Size(GetDefaultWindowWidth(settings), GetDefaultWindowHeight(settings));
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = TransparentKeyColor;
        TransparencyKey = TransparentKeyColor;
        Padding = Padding.Empty;

        UpdateContextMenu();
        contextMenu.Opening += (_, e) =>
        {
            if (settings.PracticeMode && IsEditablePracticePoint(PointToClient(Cursor.Position)))
            {
                e.Cancel = true;
            }
        };
        ContextMenuStrip = contextMenu;

        uiTimer.Interval = 50;
        uiTimer.Tick += (_, _) => Tick();
        uiTimer.Start();
    }

    private void UpdateContextMenu()
    {
        contextMenu.Items.Clear();
        contextMenu.Items.Add(Localizer.Get("Statistics...", settings), null, (_, _) => OpenStatistics());
        contextMenu.Items.Add(Localizer.Get("Settings...", settings), null, (_, _) => OpenSettings());
        contextMenu.Items.Add(CreateSettingsFileMenu());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(Localizer.Get("Exit", settings), null, (_, _) => Close());
    }

    private ToolStripMenuItem CreateSettingsFileMenu()
    {
        var menu = new ToolStripMenuItem(Localizer.Get("Switch config", settings));
        IReadOnlyList<string> files = AppSettingsStore.GetSettingsFiles();
        if (files.Count == 0)
        {
            ToolStripMenuItem empty = new(Localizer.Get("No config files", settings))
            {
                Enabled = false
            };
            menu.DropDownItems.Add(empty);
            return menu;
        }

        string activePath = Path.GetFullPath(AppSettingsStore.SettingsPath);
        foreach (string file in files)
        {
            string filePath = Path.GetFullPath(file);
            string fileName = Path.GetFileName(file);
            var item = new ToolStripMenuItem(fileName)
            {
                Checked = string.Equals(filePath, activePath, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) => SwitchSettingsFile(filePath);
            menu.DropDownItems.Add(item);
        }

        return menu;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        uiTimer.Stop();
        watcher.Dispose();

        foreach (IconPair iconPair in iconCache.Values)
        {
            iconPair.Lit.Dispose();
            iconPair.Undefeated.Dispose();
            iconPair.Current.Dispose();
        }

        foreach (Font font in fontCache.Values)
        {
            font.Dispose();
        }

        base.OnFormClosed(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            dragging = true;
            dragStartCursor = Cursor.Position;
            dragStartLocation = Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging)
        {
            return;
        }

        Point delta = new(Cursor.Position.X - dragStartCursor.X, Cursor.Position.Y - dragStartCursor.Y);
        Location = new Point(dragStartLocation.X + delta.X, dragStartLocation.Y + delta.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            dragging = false;
        }

        if (e.Button == MouseButtons.Right && settings.PracticeMode)
        {
            TryOpenPracticeEdit(e.Location);
        }
    }

    private bool IsEditablePracticePoint(Point point)
    {
        if (TryGetTimerRect(out Rectangle timerRect) && timerRect.Contains(point))
        {
            return true;
        }

        if (!TryGetSplitRowAt(point, out int rowIndex, out Rectangle rowRect))
        {
            return false;
        }

        ColumnRects columns = GetColumnRects(rowRect);
        if (columns.Time is Rectangle timeRect && timeRect.Contains(point))
        {
            BossSplitStatus status = splitTracker.Statuses[rowIndex];
            return status.IsCompleted;
        }

        return false;
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
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

        DrawOverlay(graphics);
    }

    protected override void WndProc(ref Message m)
    {
        const int wmNcHitTest = 0x84;
        const int htTransparent = -1;
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

        if (mouseClickThrough && m.Msg == wmNcHitTest)
        {
            m.Result = (IntPtr)htTransparent;
            return;
        }

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
            ResetRun(recordStats: true);
            return;
        }

        if (Keyboard.PollPressed(settings.MouseClickThroughKeys))
        {
            SetMouseClickThrough(!mouseClickThrough);
        }

        if (snapshot.EnteredWorld && runTimer.Phase == SplitTimerPhase.NotStarted)
        {
            runStatsRecorded = false;
            runTimer.Start();
            splitTracker.OnRunStarted(snapshot);
        }

        if (runTimer.Phase == SplitTimerPhase.Running)
        {
            BossSplitRecord? split = splitTracker.Update(snapshot, runTimer.Elapsed);
            if (split is not null)
            {
                int completedIndex = splitTracker.CurrentIndex - 1;
                TrackSegmentBestDeltaHighlight(completedIndex);

                if (settings.ShowSplitCompletionAnimation)
                {
                    StartSplitCompletionAnimation(completedIndex);
                }
                else
                {
                    splitCompletionAnimation = null;
                }

                TryAutoUpdatePersonalBestSegment(completedIndex);

                if (splitTracker.CurrentIndex >= splitTracker.Statuses.Count)
                {
                    TryAutoUpdatePersonalBestTimes();
                    RecordRunStatsOnce();
                    runTimer.Stop();
                }
            }
        }

        Text = $"TerrariaSplit - {FormatTimerPhase()} - {FormatWorldState()}";
        Invalidate();
    }

    private void DrawOverlay(Graphics graphics)
    {
        UiPalette palette = UiPalette.From(settings.Colors);
        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (!TryGetLayout(out SplitLayout layout))
        {
            return;
        }

        bool hasAnimation = TryGetActiveSplitCompletionAnimation(
            out SplitCompletionAnimation? animation,
            out TimeSpan animationElapsed,
            out float animationOpacity);
        float listOpacity = hasAnimation ? 1f - animationOpacity : 1f;

        int focusIndex = GetCurrentSplitHighlightIndex();
        IEnumerable<int> rowOrder = Enumerable.Range(0, statuses.Count);
        if (focusIndex >= 0)
        {
            rowOrder = rowOrder
                .OrderByDescending(index => Math.Abs(index - focusIndex))
                .ThenBy(index => index);
        }

        foreach (int i in rowOrder)
        {
            BossSplitStatus status = statuses[i];
            bool isCurrent = i == splitTracker.CurrentIndex && runTimer.Phase != SplitTimerPhase.NotStarted;
            float depthScale = GetCurrentSplitDepthScale(i, focusIndex);
            DrawSplitRow(
                graphics,
                layout.GetRowRect(i),
                status,
                i,
                isCurrent,
                palette,
                GetCurrentSplitDepthOpacity(i, focusIndex, listOpacity),
                depthScale);
        }

        if (hasAnimation && animation is not null)
        {
            DrawSplitCompletionAnimation(graphics, layout, statuses.Count, animation, animationElapsed, animationOpacity);
        }

        DrawTimer(graphics, layout.TimerRect, palette);
    }

    private void DrawSplitRow(
        Graphics graphics,
        Rectangle rect,
        BossSplitStatus status,
        int rowIndex,
        bool isCurrent,
        UiPalette palette,
        float opacity,
        float wheelScale = 1f)
    {
        if (opacity <= 0.01f)
        {
            return;
        }

        ColumnRects columns = GetColumnRects(rect);

        if (columns.Icon is Rectangle iconColumnRect)
        {
            Rectangle iconRect = Rectangle.Inflate(iconColumnRect, -2, 0);
            DrawIcons(
                graphics,
                iconRect,
                status,
                opacity,
                wheelScale,
                settings.EnableDefeatedBossIconLighting && rowIndex == GetCurrentSplitHighlightIndex());
        }

        SplitComparison comparison = GetSplitComparison(status, isCurrent);

        if (columns.Time is Rectangle timeRect)
        {
            bool showSplitTime = status.IsCompleted && status.Time is not null;
            Color timeColor = showSplitTime
                ? palette.SplitText
                : isCurrent ? palette.ActiveReferenceText : palette.ReferenceText;
            string timeText = showSplitTime
                ? TimeText.FormatSplit(status.Time!.Value)
                : FormatReferenceTime(status.Definition);

            using var timeBrush = new SolidBrush(WithOpacity(timeColor, opacity));
            DrawText(
                graphics,
                timeText,
                GetColumnFont(settings.Columns.Time, sizeScale: wheelScale),
                timeBrush,
                timeRect,
                ContentAlignment.MiddleRight);
        }

        if (columns.Delta is Rectangle deltaRect)
        {
            Color deltaColor = GetDeltaComparisonColor(comparison, palette);
            if (TryGetSegmentBestDeltaHighlight(rowIndex, out SegmentBestDeltaHighlight highlight))
            {
                double seconds = (DateTime.UtcNow - highlight.StartedAtUtc).TotalSeconds;
                deltaColor = SegmentBestDeltaHighlightStyles.Apply(deltaColor, highlight.Style, seconds);
            }

            using var compareBrush = new SolidBrush(WithOpacity(deltaColor, opacity));
            DrawText(
                graphics,
                FormatSplitDelta(comparison),
                GetColumnFont(settings.Columns.Delta, sizeScale: wheelScale),
                compareBrush,
                deltaRect,
                ContentAlignment.MiddleLeft);
        }
    }

    private int GetCurrentSplitHighlightIndex()
    {
        return settings.ShowCurrentSplitHighlight &&
            runTimer.Phase != SplitTimerPhase.NotStarted &&
            splitTracker.CurrentIndex >= 0 &&
            splitTracker.CurrentIndex < splitTracker.Statuses.Count
            ? splitTracker.CurrentIndex
            : -1;
    }

    private float GetCurrentSplitDepthScale(int rowIndex, int focusIndex)
    {
        if (focusIndex < 0)
        {
            return 1f;
        }

        float maximumScale = Math.Clamp(settings.CurrentSplitHighlightScalePercent, 100, 140) / 100f;
        float lift = maximumScale - 1f;
        if (lift <= 0.001f)
        {
            return 1f;
        }

        int distance = Math.Abs(rowIndex - focusIndex);
        float falloff = distance switch
        {
            0 => 1f,
            1 => 0.58f,
            2 => 0.28f,
            3 => 0.10f,
            _ => 0f
        };
        return 1f + lift * falloff;
    }

    private float GetCurrentSplitDepthOpacity(int rowIndex, int focusIndex, float baseOpacity)
    {
        if (focusIndex < 0)
        {
            return baseOpacity;
        }

        float strength = Math.Clamp(settings.CurrentSplitDepthStrengthPercent * 2f, 0f, 100f) / 100f;
        int distance = Math.Abs(rowIndex - focusIndex);
        float depthLoss = distance switch
        {
            0 => 0f,
            1 => 0.24f,
            2 => 0.46f,
            3 => 0.62f,
            _ => 0.72f
        };
        float depthOpacity = 1f - depthLoss * strength;
        return baseOpacity * depthOpacity;
    }

    private ColumnRects GetColumnRects(Rectangle rect)
    {
        List<ColumnWidth> visibleColumns = new();
        AddColumn(visibleColumns, SplitColumn.Icon, settings.Columns.Icon);
        AddColumn(visibleColumns, SplitColumn.Time, settings.Columns.Time);
        AddColumn(visibleColumns, SplitColumn.Delta, settings.Columns.Delta);

        int requestedWidth = visibleColumns.Sum(column => ScaleInt(column.Width));
        float scale = requestedWidth > rect.Width && requestedWidth > 0
            ? rect.Width / (float)requestedWidth
            : 1f;

        Rectangle? icon = null;
        Rectangle? time = null;
        Rectangle? delta = null;

        int x = rect.X;
        for (int i = 0; i < visibleColumns.Count; i++)
        {
            ColumnWidth column = visibleColumns[i];
            int width = i == visibleColumns.Count - 1
                ? rect.Right - x
                : Math.Max(1, (int)Math.Round(ScaleInt(column.Width) * scale));
            var columnRect = new Rectangle(x, rect.Y, width, rect.Height);
            x += width;

            switch (column.Column)
            {
                case SplitColumn.Icon:
                    icon = columnRect;
                    break;
                case SplitColumn.Time:
                    time = Rectangle.Inflate(columnRect, -4, 0);
                    break;
                case SplitColumn.Delta:
                    delta = Rectangle.Inflate(columnRect, -4, 0);
                    break;
            }
        }

        return new ColumnRects(icon, time, delta);
    }

    private bool TryGetSplitRowAt(Point point, out int rowIndex, out Rectangle rowRect)
    {
        rowIndex = -1;
        rowRect = Rectangle.Empty;
        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (!TryGetLayout(out SplitLayout layout))
        {
            return false;
        }

        for (int i = 0; i < statuses.Count; i++)
        {
            Rectangle currentRowRect = layout.GetRowRect(i);
            if (currentRowRect.Contains(point))
            {
                rowIndex = i;
                rowRect = currentRowRect;
                return true;
            }
        }

        return false;
    }

    private void TryOpenPracticeEdit(Point point)
    {
        if (TryGetTimerRect(out Rectangle timerRect) && timerRect.Contains(point))
        {
            string currentText = TimeText.FormatRecord(runTimer.Elapsed);
            if (!PromptForTime(Localizer.Get("Edit total time", settings), currentText, allowEmpty: false, out string? editedText) ||
                !TimeText.TryParse(editedText, out TimeSpan editedTime))
            {
                return;
            }

            runTimer.SetPracticeElapsed(editedTime);
            splitTracker.ClampCompletedTimes(editedTime);
            Invalidate();
            return;
        }

        if (!TryGetSplitRowAt(point, out int rowIndex, out Rectangle rowRect))
        {
            return;
        }

        BossSplitStatus status = splitTracker.Statuses[rowIndex];
        ColumnRects columns = GetColumnRects(rowRect);

        if (columns.Time is Rectangle timeRect && timeRect.Contains(point))
        {
            if (status.IsCompleted)
            {
                EditPracticeSplitTime(rowIndex, status);
            }
        }

    }

    private void EditPracticeSplitTime(int rowIndex, BossSplitStatus status)
    {
        string currentText = status.Time is TimeSpan time ? TimeText.FormatRecord(time) : string.Empty;
        if (!PromptForTime(Localizer.Get("Edit split time", settings), currentText, allowEmpty: true, out string? editedText))
        {
            return;
        }

        TimeSpan? parsedTime = null;
        if (!string.IsNullOrWhiteSpace(editedText))
        {
            if (!TimeText.TryParse(editedText, out TimeSpan value))
            {
                return;
            }

            parsedTime = value;
        }

        splitTracker.SetPracticeTime(rowIndex, parsedTime);
        TrackSegmentBestDeltaHighlight(rowIndex);
        Invalidate();
    }

    private bool PromptForTime(string title, string value, bool allowEmpty, out string editedText)
    {
        return TimeEditDialog.TryShow(this, settings, title, value, allowEmpty, out editedText);
    }

    private bool TryGetTimerRect(out Rectangle timerRect)
    {
        timerRect = Rectangle.Empty;
        if (!TryGetLayout(out SplitLayout layout))
        {
            return false;
        }

        timerRect = layout.TimerRect;
        return true;
    }

    private bool TryGetLayout(out SplitLayout layout)
    {
        layout = default;
        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;

        int margin = ScaleInt(12);
        Rectangle bounds = ClientRectangle;
        if (bounds.Width < ScaleInt(160) || bounds.Height < ScaleInt(160))
        {
            return false;
        }

        Rectangle content = Rectangle.Inflate(bounds, -margin, -margin);
        int timerHeight = Math.Clamp((int)(content.Height * 0.17), ScaleInt(82), ScaleInt(110));
        int rowGap = ScaleInt(RowGap);
        int listSpace = content.Height - timerHeight - ScaleInt(10);
        int rowHeight = Math.Clamp(
            (listSpace - Math.Max(0, statuses.Count - 1) * rowGap) / Math.Max(1, statuses.Count),
            ScaleInt(42),
            ScaleInt(58));
        if (rowHeight <= 0)
        {
            return false;
        }

        int timerY = content.Y + statuses.Count * rowHeight + Math.Max(0, statuses.Count - 1) * rowGap + ScaleInt(2);
        layout = new SplitLayout(
            new Rectangle(content.X + ScaleInt(2), content.Y, content.Width - ScaleInt(4), rowHeight),
            new Rectangle(content.X, timerY, content.Width, timerHeight),
            rowGap);
        return true;
    }

    private static void AddColumn(List<ColumnWidth> columns, SplitColumn column, UiColumnSettings settings)
    {
        if (settings.Show)
        {
            columns.Add(new ColumnWidth(column, Math.Max(1, settings.Width)));
        }
    }

    private Font GetColumnFont(UiColumnSettings columnSettings, bool forceBold = false, float sizeScale = 1f)
    {
        float size = Math.Clamp(columnSettings.FontSize * GetScaleFactor() * Math.Max(0.1f, sizeScale), 6f, 144f);
        bool bold = forceBold || columnSettings.Bold;
        var key = new FontKey(size, bold);
        if (fontCache.TryGetValue(key, out Font? font))
        {
            return font;
        }

        font = new Font(UiTheme.FontFamilyName, size, bold ? FontStyle.Bold : FontStyle.Regular);
        fontCache[key] = font;
        return font;
    }

    private static int GetDefaultWindowWidth(AppSettings settings)
    {
        float scale = Math.Clamp(settings.Columns.ScalePercent, 25, 300) / 100f;
        int columnsWidth = 0;
        columnsWidth += settings.Columns.Icon.Show ? (int)Math.Round(settings.Columns.Icon.Width * scale) : 0;
        columnsWidth += settings.Columns.Time.Show ? (int)Math.Round(settings.Columns.Time.Width * scale) : 0;
        columnsWidth += settings.Columns.Delta.Show ? (int)Math.Round(settings.Columns.Delta.Width * scale) : 0;
        return Math.Clamp(columnsWidth + (int)Math.Round(28 * scale), 300, 2400);
    }

    private static int GetDefaultWindowHeight(AppSettings settings)
    {
        float scale = Math.Clamp(settings.Columns.ScalePercent, 25, 300) / 100f;
        return Math.Clamp((int)Math.Round(720 * scale), 420, 2160);
    }

    private static Size GetMinimumWindowSize(AppSettings settings)
    {
        float scale = Math.Clamp(settings.Columns.ScalePercent, 25, 300) / 100f;
        return new Size(
            Math.Clamp((int)Math.Round(300 * scale), 220, 1800),
            Math.Clamp((int)Math.Round(420 * scale), 260, 1600));
    }

    private void DrawIcons(
        Graphics graphics,
        Rectangle rect,
        BossSplitStatus status,
        float opacity = 1f,
        float sizeScale = 1f,
        bool brighten = false)
    {
        BossSplitDefinition definition = status.Definition;
        int count = definition.IconFileNames.Count;
        if (count == 0)
        {
            return;
        }

        if (count == 1)
        {
            IconPair icon = LoadIconPair(definition, definition.IconFileNames[0]);
            bool lit = IsIconLit(status, 0);
            int singleIconSize = Math.Min(
                Math.Min(Math.Max(12, ScaleInt((int)Math.Round(settings.Columns.Icon.FontSize * sizeScale))), rect.Height),
                rect.Width);
            var iconRect = new Rectangle(
                rect.Right - singleIconSize,
                rect.Y + Math.Max(0, (rect.Height - singleIconSize) / 2),
                singleIconSize,
                singleIconSize);
            Image image = lit ? icon.Lit : brighten ? icon.Current : icon.Undefeated;
            DrawImage(graphics, image, iconRect, opacity);
            return;
        }

        int iconGap = ScaleInt(6);
        int size = Math.Min(
            Math.Min(Math.Max(12, ScaleInt((int)Math.Round(settings.Columns.Icon.FontSize * sizeScale))), rect.Height),
            Math.Max(12, (rect.Width - Math.Max(0, count - 1) * iconGap) / count));
        int totalWidth = count * size + (count - 1) * iconGap;
        int startX = rect.Right - totalWidth;
        int y = rect.Y + Math.Max(0, (rect.Height - size) / 2);
        for (int i = 0; i < count; i++)
        {
            IconPair icon = LoadIconPair(definition, definition.IconFileNames[i]);
            bool lit = IsIconLit(status, i);
            Image image = lit ? icon.Lit : brighten ? icon.Current : icon.Undefeated;
            DrawImage(
                graphics,
                image,
                new Rectangle(startX + i * (size + iconGap), y, size, size),
                opacity);
        }
    }

    private bool IsIconLit(BossSplitStatus status, int iconIndex)
    {
        if (!settings.EnableDefeatedBossIconLighting)
        {
            return true;
        }

        if (status.IsCompleted || status.IsSkipped)
        {
            return true;
        }

        if (runTimer.Phase == SplitTimerPhase.NotStarted)
        {
            return false;
        }

        if (iconIndex < 0 || iconIndex >= status.Definition.IconKeys.Count)
        {
            return false;
        }

        return BossSplitDefinitions.TryGetUnit(status.Definition.IconKeys[iconIndex], out BossUnitDefinition unit) &&
            unit.RequiredFlags.All(flag => snapshot.BossStates.Get(flag) == true);
    }

    private void DrawTimer(Graphics graphics, Rectangle rect, UiPalette palette)
    {
        int offsetX = ScaleInt(settings.Columns.TimerOffsetX);
        int offsetY = ScaleInt(settings.Columns.TimerOffsetY);
        var timeRect = new Rectangle(rect.X + ScaleInt(4) + offsetX, rect.Y - ScaleInt(4) + offsetY, rect.Width - ScaleInt(8), rect.Height - ScaleInt(16));
        using var timerTextBrush = new SolidBrush(GetTimerTextColor(palette));
        DrawTimerText(graphics, runTimer.Elapsed, timerTextBrush, timeRect, GetTimerMainRightEdge() + offsetX);
    }

    private void StartSplitCompletionAnimation(int completedIndex)
    {
        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (completedIndex < 0 || completedIndex >= statuses.Count || statuses[completedIndex].Time is not TimeSpan splitTime)
        {
            return;
        }

        TimeSpan previousSplitTime = TimeSpan.Zero;
        for (int i = completedIndex - 1; i >= 0; i--)
        {
            if (statuses[i].Time is TimeSpan previousTime)
            {
                previousSplitTime = previousTime;
                break;
            }
        }

        TimeSpan segmentTime = splitTime - previousSplitTime;
        if (segmentTime < TimeSpan.Zero)
        {
            segmentTime = TimeSpan.Zero;
        }

        BossSplitDefinition definition = statuses[completedIndex].Definition;
        string groupKey = GetSplitCompletionGroupKey(definition);
        SplitComparison referenceSplitComparison = GetReferenceSplitComparison(definition, splitTime);
        SplitComparison personalBestSegmentComparison = GetPersonalBestSegmentComparison(definition, segmentTime);
        string segmentBestDeltaHighlightStyle = GetSegmentBestDeltaHighlightStyle(groupKey);

        splitCompletionAnimation = new SplitCompletionAnimation(
            definition,
            segmentTime,
            splitTime,
            referenceSplitComparison,
            personalBestSegmentComparison,
            IsSplitCompletionSplitComparisonEnabled(groupKey),
            GetSplitCompletionOutlineStyle(settings.SplitCompletionOutlineSplitStyles, groupKey),
            IsSplitCompletionSegmentComparisonEnabled(groupKey),
            GetSplitCompletionOutlineStyle(settings.SplitCompletionOutlineSegmentStyles, groupKey),
            segmentBestDeltaHighlightStyle,
            DateTime.UtcNow);
    }

    private bool TryGetActiveSplitCompletionAnimation(
        out SplitCompletionAnimation? animation,
        out TimeSpan elapsed,
        out float opacity)
    {
        animation = splitCompletionAnimation;
        elapsed = TimeSpan.Zero;
        opacity = 0f;

        if (animation is null)
        {
            return false;
        }

        elapsed = DateTime.UtcNow - animation.StartedAtUtc;
        TimeSpan duration = GetSplitCompletionAnimationDuration();
        if (elapsed >= duration)
        {
            splitCompletionAnimation = null;
            animation = null;
            return false;
        }

        opacity = GetSplitCompletionAnimationOpacity(elapsed, duration);
        return opacity > 0.01f;
    }

    private static float GetSplitCompletionAnimationOpacity(TimeSpan elapsed, TimeSpan duration)
    {
        if (elapsed < TimeSpan.Zero || elapsed >= duration)
        {
            return 0f;
        }

        TimeSpan fadeDuration = GetSplitCompletionFadeDuration(duration);
        if (elapsed < fadeDuration)
        {
            return EaseInOut((float)(elapsed.TotalMilliseconds / fadeDuration.TotalMilliseconds));
        }

        TimeSpan fadeOutStart = duration - fadeDuration;
        if (elapsed > fadeOutStart)
        {
            return EaseInOut((float)((duration - elapsed).TotalMilliseconds / fadeDuration.TotalMilliseconds));
        }

        return 1f;
    }

    private TimeSpan GetSplitCompletionAnimationDuration()
    {
        return TimeSpan.FromSeconds(Math.Clamp(settings.SplitCompletionAnimationDurationSeconds, 1f, 20f));
    }

    private static TimeSpan GetSplitCompletionFadeDuration(TimeSpan duration)
    {
        double seconds = Math.Min(SplitCompletionFadeDuration.TotalSeconds, duration.TotalSeconds * 0.45);
        return TimeSpan.FromSeconds(Math.Max(0.05, seconds));
    }

    private static float EaseInOut(float value)
    {
        float t = Math.Clamp(value, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private void DrawSplitCompletionAnimation(
        Graphics graphics,
        SplitLayout layout,
        int statusCount,
        SplitCompletionAnimation animation,
        TimeSpan elapsed,
        float opacity)
    {
        if (statusCount <= 0)
        {
            return;
        }

        Rectangle firstRow = layout.GetRowRect(0);
        Rectangle lastRow = layout.GetRowRect(statusCount - 1);
        var listBounds = new Rectangle(firstRow.X, firstRow.Y, firstRow.Width, lastRow.Bottom - firstRow.Top);
        if (listBounds.Width <= 0 || listBounds.Height <= 0)
        {
            return;
        }

        DrawSplitCompletionIcon(graphics, listBounds, animation, elapsed, opacity);
        DrawSplitCompletionTimes(graphics, listBounds, animation, elapsed, opacity);
    }

    private void DrawSplitCompletionIcon(
        Graphics graphics,
        Rectangle listBounds,
        SplitCompletionAnimation animation,
        TimeSpan elapsed,
        float opacity)
    {
        IReadOnlyList<string> iconFileNames = animation.Definition.IconFileNames;
        if (iconFileNames.Count == 0)
        {
            return;
        }

        int maxIconSize = Math.Max(1, Math.Min((int)(listBounds.Width * 0.38f), (int)(listBounds.Height * 0.34f)));
        int minIconSize = Math.Min(ScaleInt(72), maxIconSize);
        int iconSize = Math.Clamp(ScaleInt(150), minIconSize, maxIconSize);
        var iconRect = new Rectangle(
            listBounds.Left + (listBounds.Width - iconSize) / 2,
            listBounds.Top + Math.Max(0, (int)(listBounds.Height * 0.12f)),
            iconSize,
            iconSize);

        if (iconFileNames.Count == 1)
        {
            DrawSplitCompletionIconFrame(graphics, animation, iconFileNames[0], iconRect, opacity);
            return;
        }

        TimeSpan duration = GetSplitCompletionAnimationDuration();
        float progress = Math.Clamp((float)(elapsed.TotalMilliseconds / duration.TotalMilliseconds), 0f, 0.999f);
        float position = progress * iconFileNames.Count;
        int iconIndex = Math.Min(iconFileNames.Count - 1, (int)position);
        float localProgress = position - iconIndex;
        bool hasNextIcon = iconIndex < iconFileNames.Count - 1;
        float fadeProgress = hasNextIcon
            ? EaseInOut((localProgress - 0.68f) / 0.32f)
            : 0f;

        DrawSplitCompletionIconFrame(
            graphics,
            animation,
            iconFileNames[iconIndex],
            iconRect,
            opacity * (1f - fadeProgress));

        if (hasNextIcon && fadeProgress > 0.01f)
        {
            DrawSplitCompletionIconFrame(
                graphics,
                animation,
                iconFileNames[iconIndex + 1],
                iconRect,
                opacity * fadeProgress);
        }
    }

    private void DrawSplitCompletionIconFrame(
        Graphics graphics,
        SplitCompletionAnimation animation,
        string iconFileName,
        Rectangle iconRect,
        float opacity)
    {
        if (opacity <= 0.01f)
        {
            return;
        }

        IconPair icon = LoadIconPair(animation.Definition, iconFileName);
        DrawImage(graphics, icon.Lit, iconRect, opacity);
    }

    private void DrawSplitCompletionTimes(
        Graphics graphics,
        Rectangle listBounds,
        SplitCompletionAnimation animation,
        TimeSpan elapsed,
        float opacity)
    {
        float scale = GetScaleFactor();
        using var labelFont = new Font(UiTheme.FontFamilyName, Math.Clamp(9f * scale, 7f, 16f), FontStyle.Regular);
        using var valueFont = new Font(UiTheme.FontFamilyName, Math.Clamp(18f * scale, 12f, 32f), FontStyle.Bold);
        using var deltaFont = new Font(UiTheme.FontFamilyName, Math.Clamp(13f * scale, 9f, 24f), FontStyle.Bold);
        UiPalette palette = UiPalette.From(settings.Colors);

        int labelHeight = Math.Max(ScaleInt(12), (int)Math.Ceiling(labelFont.GetHeight(graphics)));
        int valueHeight = Math.Max(ScaleInt(26), (int)Math.Ceiling(valueFont.GetHeight(graphics)) + ScaleInt(2));
        int rowHeight = labelHeight + valueHeight + ScaleInt(2);
        int gap = ScaleInt(7);
        int totalHeight = rowHeight * 2 + gap;
        int startY = listBounds.Top + (int)(listBounds.Height * 0.54f);
        if (startY + totalHeight > listBounds.Bottom)
        {
            startY = Math.Max(listBounds.Top + ScaleInt(4), listBounds.Bottom - totalHeight - ScaleInt(2));
        }

        var segmentRect = new Rectangle(listBounds.Left + ScaleInt(8), startY, listBounds.Width - ScaleInt(16), rowHeight);
        var splitRect = new Rectangle(listBounds.Left + ScaleInt(8), startY + rowHeight + gap, listBounds.Width - ScaleInt(16), rowHeight);

        DrawSplitCompletionTimeRow(
            graphics,
            segmentRect,
            Localizer.Get("Segment time", settings),
            SplitTimerFormatter.Format(animation.SegmentTime),
            animation.PersonalBestSegmentComparison,
            animation.ShowSegmentComparison,
            animation.SegmentTimeOutlineStyle,
            labelFont,
            valueFont,
            deltaFont,
            palette,
            elapsed,
            opacity,
            animation.SegmentBestDeltaHighlightStyle);
        DrawSplitCompletionTimeRow(
            graphics,
            splitRect,
            Localizer.Get("Split time", settings),
            SplitTimerFormatter.Format(animation.SplitTime),
            animation.ReferenceSplitComparison,
            animation.ShowSplitComparison,
            animation.SplitTimeOutlineStyle,
            labelFont,
            valueFont,
            deltaFont,
            palette,
            elapsed,
            opacity,
            SegmentBestDeltaHighlightStyles.None);
    }

    private void DrawSplitCompletionTimeRow(
        Graphics graphics,
        Rectangle bounds,
        string label,
        string value,
        SplitComparison comparison,
        bool showComparison,
        string outlineStyle,
        Font labelFont,
        Font valueFont,
        Font deltaFont,
        UiPalette palette,
        TimeSpan elapsed,
        float opacity,
        string deltaHighlightStyle)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        string deltaText = showComparison && comparison.ShowDelta && comparison.Delta is TimeSpan delta
            ? TimeText.FormatDelta(delta)
            : string.Empty;
        bool isAhead = SplitCompletionOutlineStyles.Normalize(outlineStyle) != SplitCompletionOutlineStyles.None &&
            comparison.Delta is TimeSpan aheadDelta &&
            aheadDelta < TimeSpan.Zero;

        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };

        int labelHeight = Math.Max(ScaleInt(12), (int)Math.Ceiling(labelFont.GetHeight(graphics)));
        var labelRect = new Rectangle(bounds.Left, bounds.Top, bounds.Width, labelHeight);
        using var labelBrush = new SolidBrush(WithOpacity(Color.FromArgb(222, 222, 226), opacity * 0.86f));
        DrawText(
            graphics,
            label,
            labelFont,
            labelBrush,
            labelRect,
            ContentAlignment.MiddleCenter);

        SizeF valueSize = graphics.MeasureString(value, valueFont, bounds.Size, format);
        SizeF deltaSize = string.IsNullOrEmpty(deltaText)
            ? SizeF.Empty
            : graphics.MeasureString(deltaText, deltaFont, bounds.Size, format);
        float gap = string.IsNullOrEmpty(deltaText) ? 0f : ScaleInt(14);
        float startX = bounds.Left + Math.Max(0f, (bounds.Width - valueSize.Width) / 2f);
        FontMetrics valueMetrics = GetFontMetrics(graphics, valueFont);
        FontMetrics deltaMetrics = GetFontMetrics(graphics, deltaFont);
        float valueTextHeight = valueMetrics.Ascent + valueMetrics.Descent;
        float valueBaselineY = bounds.Top + labelHeight + Math.Max(0f, (bounds.Height - labelHeight - valueTextHeight) / 2f) + valueMetrics.Ascent;
        float valueY = valueBaselineY - valueMetrics.Ascent;

        if (isAhead)
        {
            DrawOutlinedString(
                graphics,
                value,
                valueFont,
                Color.White,
                startX,
                valueY,
                format,
                elapsed,
                settings.SplitCompletionOutlineThicknessPercent,
                outlineStyle,
                opacity);
        }
        else
        {
            DrawString(graphics, value, valueFont, Color.White, startX, valueY, format, opacity);
        }

        if (!string.IsNullOrEmpty(deltaText))
        {
            Color deltaColor = GetDeltaComparisonColor(comparison, palette);
            if (settings.ShowSegmentBestDeltaHighlight &&
                comparison.Delta is TimeSpan deltaValue &&
                deltaValue < TimeSpan.Zero)
            {
                deltaColor = SegmentBestDeltaHighlightStyles.Apply(deltaColor, deltaHighlightStyle, elapsed.TotalSeconds);
            }

            float deltaX = startX + valueSize.Width + gap;
            float deltaY = AlignTextPathBottom(graphics, value, valueFont, startX, valueY, deltaText, deltaFont, deltaX, valueY, format);
            DrawString(
                graphics,
                deltaText,
                deltaFont,
                deltaColor,
                deltaX,
                deltaY,
                format,
                opacity);
        }
    }

    private SplitComparison GetReferenceSplitComparison(BossSplitDefinition definition, TimeSpan splitTime)
    {
        if (!settings.TryGetReferenceSplit(definition, out TimeSpan referenceSplit))
        {
            return SplitComparison.Empty;
        }

        return new SplitComparison(splitTime - referenceSplit, ShowDelta: true);
    }

    private SplitComparison GetPersonalBestSegmentComparison(BossSplitDefinition definition, TimeSpan segmentTime)
    {
        if (!TryGetPersonalBestSegment(definition, out TimeSpan personalBestSegment))
        {
            return SplitComparison.Empty;
        }

        return new SplitComparison(segmentTime - personalBestSegment, ShowDelta: true);
    }

    private bool TryGetPersonalBestSegment(BossSplitDefinition definition, out TimeSpan segment)
    {
        segment = TimeSpan.Zero;
        string groupKey = string.Join("+", definition.BossIds);
        if (settings.PersonalBestSegmentTimes.TryGetValue(groupKey, out string? value) &&
            TimeText.TryParse(value, out TimeSpan parsed))
        {
            segment = parsed;
            return true;
        }

        if (settings.PersonalBestSegmentTimes.TryGetValue(definition.Name, out value) &&
            TimeText.TryParse(value, out parsed))
        {
            segment = parsed;
            return true;
        }

        return false;
    }

    private void TrackSegmentBestDeltaHighlight(int completedIndex)
    {
        segmentBestDeltaHighlights.Remove(completedIndex);

        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (completedIndex < 0 ||
            completedIndex >= statuses.Count ||
            !settings.ShowSegmentBestDeltaHighlight ||
            !TryGetCompletedSegmentTime(completedIndex, out TimeSpan segmentTime))
        {
            return;
        }

        BossSplitDefinition definition = statuses[completedIndex].Definition;
        if (!TryGetPersonalBestSegment(definition, out TimeSpan personalBestSegment) ||
            segmentTime >= personalBestSegment)
        {
            return;
        }

        string style = GetSegmentBestDeltaHighlightStyle(GetSplitCompletionGroupKey(definition));
        if (SegmentBestDeltaHighlightStyles.Normalize(style) == SegmentBestDeltaHighlightStyles.None)
        {
            return;
        }

        segmentBestDeltaHighlights[completedIndex] = new SegmentBestDeltaHighlight(style, DateTime.UtcNow);
    }

    private bool TryGetSegmentBestDeltaHighlight(int rowIndex, out SegmentBestDeltaHighlight highlight)
    {
        if (settings.ShowSegmentBestDeltaHighlight &&
            segmentBestDeltaHighlights.TryGetValue(rowIndex, out highlight) &&
            rowIndex >= 0 &&
            rowIndex < splitTracker.Statuses.Count &&
            splitTracker.Statuses[rowIndex].IsCompleted &&
            SegmentBestDeltaHighlightStyles.Normalize(highlight.Style) != SegmentBestDeltaHighlightStyles.None)
        {
            return true;
        }

        highlight = default;
        return false;
    }

    private bool TryGetCompletedSegmentTime(int completedIndex, out TimeSpan segmentTime)
    {
        segmentTime = TimeSpan.Zero;
        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (completedIndex < 0 ||
            completedIndex >= statuses.Count ||
            statuses[completedIndex].Time is not TimeSpan splitTime)
        {
            return false;
        }

        TimeSpan previousSplitTime = TimeSpan.Zero;
        for (int i = completedIndex - 1; i >= 0; i--)
        {
            if (statuses[i].Time is TimeSpan previousTime)
            {
                previousSplitTime = previousTime;
                break;
            }
        }

        segmentTime = splitTime - previousSplitTime;
        if (segmentTime < TimeSpan.Zero)
        {
            segmentTime = TimeSpan.Zero;
        }

        return true;
    }

    private void TryAutoUpdatePersonalBestSegment(int completedIndex)
    {
        if (!settings.AutoUpdatePersonalBestData)
        {
            return;
        }

        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (completedIndex < 0 ||
            completedIndex >= statuses.Count ||
            statuses[completedIndex].Time is not TimeSpan splitTime)
        {
            return;
        }

        if (completedIndex > 0 && (statuses.Count == 0 || statuses[0].Time is null))
        {
            return;
        }

        TimeSpan previousSplitTime = TimeSpan.Zero;
        for (int i = completedIndex - 1; i >= 0; i--)
        {
            if (statuses[i].Time is TimeSpan previousTime)
            {
                previousSplitTime = previousTime;
                break;
            }
        }

        TimeSpan segmentTime = splitTime - previousSplitTime;
        if (segmentTime < TimeSpan.Zero)
        {
            return;
        }

        string groupKey = GetSplitCompletionGroupKey(statuses[completedIndex].Definition);
        if (settings.PersonalBestSegmentTimes.TryGetValue(groupKey, out string? existingText) &&
            TimeText.TryParse(existingText, out TimeSpan existingSegment) &&
            existingSegment <= segmentTime)
        {
            return;
        }

        settings.SetPersonalBestSegmentText(groupKey, TimeText.FormatRecord(segmentTime));
        AppSettingsStore.Save(settings);
    }

    private void TryAutoUpdatePersonalBestTimes()
    {
        if (!settings.AutoUpdatePersonalBestData)
        {
            return;
        }

        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (statuses.Count == 0 || statuses.Any(status => status.Time is null || status.IsSkipped))
        {
            return;
        }

        BossSplitStatus? moonLordStatus = statuses.FirstOrDefault(status =>
            status.Definition.BossIds.Any(bossId => string.Equals(
                bossId,
                BossSplitDefinitions.MoonLord,
                StringComparison.OrdinalIgnoreCase)));
        if (moonLordStatus?.Time is not TimeSpan moonLordTime)
        {
            return;
        }

        if (settings.PersonalBestTimes.TryGetValue(BossSplitDefinitions.MoonLord, out string? existingMoonLordText) &&
            TimeText.TryParse(existingMoonLordText, out TimeSpan existingMoonLordTime) &&
            existingMoonLordTime <= moonLordTime)
        {
            return;
        }

        foreach (BossSplitStatus status in statuses)
        {
            TimeSpan splitTime = status.Time!.Value;
            string formatted = TimeText.FormatRecord(splitTime);
            foreach (string bossId in status.Definition.BossIds)
            {
                settings.SetPersonalBestTimeText(bossId, formatted);
            }
        }

        AppSettingsStore.Save(settings);
    }

    private static string GetSplitCompletionGroupKey(BossSplitDefinition definition)
    {
        return string.Join("+", definition.BossIds);
    }

    private static string GetSplitCompletionOutlineStyle(Dictionary<string, string> values, string groupKey)
    {
        return values.TryGetValue(groupKey, out string? style)
            ? SplitCompletionOutlineStyles.Normalize(style)
            : SplitCompletionOutlineStyles.Rainbow;
    }

    private bool IsSplitCompletionSplitComparisonEnabled(string groupKey)
    {
        return !settings.SplitCompletionSplitComparisons.TryGetValue(groupKey, out bool enabled) || enabled;
    }

    private bool IsSplitCompletionSegmentComparisonEnabled(string groupKey)
    {
        return !settings.SplitCompletionSegmentComparisons.TryGetValue(groupKey, out bool enabled) || enabled;
    }

    private string GetSegmentBestDeltaHighlightStyle(string groupKey)
    {
        return settings.SegmentBestDeltaHighlightStyles.TryGetValue(groupKey, out string? style)
            ? SegmentBestDeltaHighlightStyles.Normalize(style)
            : SegmentBestDeltaHighlightStyles.Aurora;
    }

    private string FormatReferenceTime(BossSplitDefinition definition)
    {
        return settings.TryGetReferenceSplit(definition, out TimeSpan split)
            ? TimeText.FormatSplit(split)
            : "--";
    }

    private SplitComparison GetSplitComparison(BossSplitStatus status, bool isCurrent)
    {
        if (!settings.TryGetReferenceSplit(status.Definition, out TimeSpan referenceTime))
        {
            return SplitComparison.Empty;
        }

        if (status.Time is TimeSpan splitTime)
        {
            return new SplitComparison(splitTime - referenceTime, ShowDelta: true);
        }

        if (!isCurrent || runTimer.Phase == SplitTimerPhase.NotStarted)
        {
            return SplitComparison.Empty;
        }

        TimeSpan runningDelta = runTimer.Elapsed - referenceTime;
        return new SplitComparison(runningDelta, runningDelta >= -MaximumVisibleDeltaDistance);
    }

    private static string FormatSplitDelta(SplitComparison comparison)
    {
        return comparison.ShowDelta && comparison.Delta is TimeSpan delta
            ? TimeText.FormatDelta(delta)
            : string.Empty;
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

    private static void DrawString(
        Graphics graphics,
        string text,
        Font font,
        Color color,
        float x,
        float y,
        StringFormat format,
        float opacity)
    {
        using var textBrush = new SolidBrush(WithOpacity(color, opacity));
        graphics.DrawString(text, font, textBrush, x, y, format);
    }

    private static void DrawOutlinedString(
        Graphics graphics,
        string text,
        Font font,
        Color fillColor,
        float x,
        float y,
        StringFormat format,
        TimeSpan elapsed,
        int thicknessPercent,
        string outlineStyle,
        float opacity)
    {
        using GraphicsPath path = CreateTextPath(graphics, text, font, x, y, format);
        if (path.PointCount == 0)
        {
            return;
        }

        string style = SplitCompletionOutlineStyles.Normalize(outlineStyle);
        if (style == SplitCompletionOutlineStyles.None)
        {
            DrawString(graphics, text, font, fillColor, x, y, format, opacity);
            return;
        }

        RectangleF bounds = path.GetBounds();
        RectangleF gradientBounds = InflateBounds(bounds, Math.Max(4f, font.Size * 0.35f));
        using var outlineBrush = new LinearGradientBrush(gradientBounds, Color.White, Color.White, LinearGradientMode.Horizontal);
        Color[] colors = SplitCompletionOutlineStyles.GetColors(style, elapsed.TotalSeconds)
            .Select(color => WithOpacity(color, opacity))
            .ToArray();
        var blend = new ColorBlend
        {
            Positions = CreateColorPositions(colors.Length),
            Colors = colors
        };
        outlineBrush.InterpolationColors = blend;

        float thickness = font.Size * Math.Clamp(thicknessPercent, 0, 100) / 100f;
        if (style is SplitCompletionOutlineStyles.Rainbow)
        {
            using var backingPen = new Pen(WithOpacity(Color.FromArgb(42, 255, 255, 255), opacity), Math.Max(1f, thickness * 1.35f))
            {
                LineJoin = LineJoin.Round
            };
            graphics.DrawPath(backingPen, path);
        }

        using var outlinePen = new Pen(outlineBrush, Math.Max(1f, thickness))
        {
            LineJoin = LineJoin.Round
        };
        graphics.DrawPath(outlinePen, path);

        using var fillBrush = new SolidBrush(WithOpacity(fillColor, opacity));
        graphics.FillPath(fillBrush, path);
    }

    private static float[] CreateColorPositions(int count)
    {
        if (count <= 1)
        {
            return new[] { 0f };
        }

        var positions = new float[count];
        for (int i = 0; i < count; i++)
        {
            positions[i] = i / (float)(count - 1);
        }

        return positions;
    }

    private static RectangleF InflateBounds(RectangleF bounds, float amount)
    {
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return new RectangleF(bounds.X - amount, bounds.Y - amount, amount * 2f + 1f, amount * 2f + 1f);
        }

        bounds.Inflate(amount, amount);
        return bounds;
    }

    private static GraphicsPath CreateTextPath(Graphics graphics, string text, Font font, float x, float y, StringFormat format)
    {
        var path = new GraphicsPath();
        using StringFormat pathFormat = (StringFormat)format.Clone();
        path.AddString(
            text,
            font.FontFamily,
            (int)font.Style,
            emSize: font.SizeInPoints * graphics.DpiY / 72f,
            origin: new PointF(x, y),
            format: pathFormat);
        return path;
    }

    private static float AlignTextPathBottom(
        Graphics graphics,
        string referenceText,
        Font referenceFont,
        float referenceX,
        float referenceY,
        string text,
        Font font,
        float x,
        float y,
        StringFormat format)
    {
        using GraphicsPath referencePath = CreateTextPath(graphics, referenceText, referenceFont, referenceX, referenceY, format);
        using GraphicsPath path = CreateTextPath(graphics, text, font, x, y, format);
        if (referencePath.PointCount == 0 || path.PointCount == 0)
        {
            return y;
        }

        return y + referencePath.GetBounds().Bottom - path.GetBounds().Bottom;
    }

    private static Color FromHsv(float hue, float saturation, float value)
    {
        float h = ((hue % 360f) + 360f) % 360f;
        float c = value * saturation;
        float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
        float m = value - c;

        (float r, float g, float b) = h switch
        {
            < 60f => (c, x, 0f),
            < 120f => (x, c, 0f),
            < 180f => (0f, c, x),
            < 240f => (0f, x, c),
            < 300f => (x, 0f, c),
            _ => (c, 0f, x)
        };

        return Color.FromArgb(
            (int)Math.Round((r + m) * 255f),
            (int)Math.Round((g + m) * 255f),
            (int)Math.Round((b + m) * 255f));
    }

    private static void DrawImage(Graphics graphics, Image image, Rectangle bounds, float opacity, float brighten = 0f)
    {
        if (opacity >= 0.99f && brighten <= 0.001f)
        {
            graphics.DrawImage(image, bounds);
            return;
        }

        using var attributes = new ImageAttributes();
        float brightness = Math.Clamp(brighten, 0f, 0.5f);
        var matrix = new ColorMatrix
        {
            Matrix00 = 1f + brightness,
            Matrix11 = 1f + brightness,
            Matrix22 = 1f + brightness,
            Matrix33 = Math.Clamp(opacity, 0f, 1f),
            Matrix40 = brightness * 0.08f,
            Matrix41 = brightness * 0.08f,
            Matrix42 = brightness * 0.08f
        };
        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        graphics.DrawImage(
            image,
            bounds,
            0,
            0,
            image.Width,
            image.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    private static Color WithOpacity(Color color, float opacity)
    {
        int alpha = (int)Math.Round(color.A * Math.Clamp(opacity, 0f, 1f));
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private int GetTimerMainRightEdge()
    {
        if (!TryGetLayout(out SplitLayout layout))
        {
            return ClientRectangle.Right - 12;
        }

        Rectangle firstRowRect = layout.GetRowRect(0);
        ColumnRects columns = GetColumnRects(firstRowRect);
        return columns.Time?.Right ?? firstRowRect.Right;
    }

    private void DrawTimerText(Graphics graphics, TimeSpan elapsed, Brush brush, Rectangle bounds, int mainRightEdge)
    {
        if (!settings.Columns.Timer.Show && !settings.Columns.TimerMilliseconds.Show)
        {
            return;
        }

        string mainText = SplitTimerFormatter.FormatWithoutMilliseconds(elapsed);
        string millisecondsText = SplitTimerFormatter.FormatMilliseconds(elapsed);
        Font mainFont = GetColumnFont(settings.Columns.Timer);
        Font millisecondsFont = GetColumnFont(settings.Columns.TimerMilliseconds);

        using var format = new StringFormat(StringFormat.GenericTypographic);
        SizeF millisecondsSize = settings.Columns.TimerMilliseconds.Show
            ? graphics.MeasureString(millisecondsText, millisecondsFont, bounds.Size, format)
            : SizeF.Empty;
        SizeF mainSize = settings.Columns.Timer.Show
            ? graphics.MeasureString(mainText, mainFont, bounds.Size, format)
            : SizeF.Empty;

        float gap = settings.Columns.Timer.Show && settings.Columns.TimerMilliseconds.Show ? ScaleInt(2) : 0f;
        FontMetrics mainMetrics = GetFontMetrics(graphics, mainFont);
        FontMetrics millisecondsMetrics = GetFontMetrics(graphics, millisecondsFont);
        float groupAscent = Math.Max(mainMetrics.Ascent, millisecondsMetrics.Ascent);
        float groupDescent = Math.Max(mainMetrics.Descent, millisecondsMetrics.Descent);
        float groupHeight = groupAscent + groupDescent;
        float groupY = bounds.Y + Math.Max(0, (bounds.Height - groupHeight) / 2f);
        float baselineY = groupY + groupAscent;

        float mainX = bounds.Left;
        float mainY = baselineY - mainMetrics.Ascent;
        float millisecondsX = mainX + (settings.Columns.Timer.Show ? mainSize.Width : 0f) + gap;
        float millisecondsY = baselineY - millisecondsMetrics.Ascent;

        if (settings.Columns.Timer.Show)
        {
            graphics.DrawString(mainText, mainFont, brush, mainX, mainY, format);
        }

        if (settings.Columns.TimerMilliseconds.Show)
        {
            graphics.DrawString(millisecondsText, millisecondsFont, brush, millisecondsX, millisecondsY, format);
        }
    }

    private static FontMetrics GetFontMetrics(Graphics graphics, Font font)
    {
        FontFamily family = font.FontFamily;
        FontStyle style = font.Style;
        float emHeight = family.GetEmHeight(style);
        float pixelsPerEm = font.SizeInPoints * graphics.DpiY / 72f;
        float ascent = family.GetCellAscent(style) * pixelsPerEm / emHeight;
        float descent = family.GetCellDescent(style) * pixelsPerEm / emHeight;
        return new FontMetrics(ascent, descent);
    }

    private float GetScaleFactor()
    {
        return Math.Clamp(settings.Columns.ScalePercent, 25, 300) / 100f;
    }

    private int ScaleInt(int value)
    {
        if (value == 0)
        {
            return 0;
        }

        int scaled = (int)Math.Round(value * GetScaleFactor(), MidpointRounding.AwayFromZero);
        if (scaled == 0)
        {
            return value < 0 ? -1 : 1;
        }

        return scaled;
    }

    private Color GetTimerTextColor(UiPalette palette)
    {
        if (runTimer.Phase == SplitTimerPhase.NotStarted)
        {
            return palette.TimerText;
        }

        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (statuses.Count > 0 && statuses[^1].Time is TimeSpan finalTime)
        {
            return settings.TryGetReferenceSplit(statuses[^1].Definition, out TimeSpan finalReference) &&
                finalTime < finalReference
                ? palette.TimerRecordText
                : palette.TimerBehindText;
        }

        if (splitTracker.CurrentIndex < statuses.Count &&
            settings.TryGetReferenceSplit(statuses[splitTracker.CurrentIndex].Definition, out TimeSpan currentReference))
        {
            return runTimer.Elapsed <= currentReference ? palette.TimerAheadText : palette.TimerBehindText;
        }

        return palette.TimerText;
    }

    private static Color GetDeltaComparisonColor(SplitComparison comparison, UiPalette palette)
    {
        TimeSpan? delta = comparison.Delta;
        if (delta is null)
        {
            return palette.DeltaEvenText;
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
        form.TopMost = TopMost;
        form.Applied += (_, _) => ApplySettings(form.Result);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ApplySettings(form.Result);
    }

    private void ApplySettings(AppSettings appliedSettings)
    {
        settings = AppSettingsStore.Clone(appliedSettings);
        AppSettingsStore.Save(settings);
        ApplyLoadedSettings();
    }

    private void SwitchSettingsFile(string path)
    {
        if (string.Equals(
                Path.GetFullPath(path),
                Path.GetFullPath(AppSettingsStore.SettingsPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        settings = AppSettingsStore.Load(path);
        ApplyLoadedSettings();
    }

    private void ApplyLoadedSettings()
    {
        splitTracker.SetDefinitions(BossSplitDefinitions.Build(settings));
        ResetRun();
        TopMost = settings.AlwaysOnTop;
        MinimumSize = GetMinimumWindowSize(settings);
        Width = Math.Max(MinimumSize.Width, GetDefaultWindowWidth(settings));
        Height = Math.Max(MinimumSize.Height, GetDefaultWindowHeight(settings));
        UpdateContextMenu();
        ClearIconCache();
        Invalidate();
    }

    private void OpenStatistics()
    {
        using var form = new StatisticsForm(settings);
        form.TopMost = TopMost;
        form.ShowDialog(this);
    }

    private void ResetRun(bool recordStats = false)
    {
        if (recordStats)
        {
            RecordRunStatsOnce();
        }

        runTimer.Reset();
        splitTracker.Reset();
        splitCompletionAnimation = null;
        segmentBestDeltaHighlights.Clear();
        runStatsRecorded = false;
        Invalidate();
    }

    private void RecordRunStatsOnce()
    {
        if (runStatsRecorded)
        {
            return;
        }

        RunStatsStore.RecordRun(splitTracker.Statuses);
        runStatsRecorded = true;
    }

    private void SetMouseClickThrough(bool enabled)
    {
        mouseClickThrough = enabled;
        UpdateMouseClickThroughStyle();
        Text = $"TerrariaSplit - {FormatTimerPhase()} - {FormatWorldState()}";
    }

    private void UpdateMouseClickThroughStyle()
    {
        const int gwlExStyle = -20;
        const int wsExTransparent = 0x20;
        const int wsExLayered = 0x80000;

        IntPtr handle = Handle;
        int style = GetWindowLong(handle, gwlExStyle);
        if (mouseClickThrough)
        {
            style |= wsExTransparent | wsExLayered;
        }
        else
        {
            style &= ~wsExTransparent;
        }

        SetWindowLong(handle, gwlExStyle, style);
    }

    private void ClearIconCache()
    {
        foreach (IconPair iconPair in iconCache.Values)
        {
            iconPair.Lit.Dispose();
            iconPair.Undefeated.Dispose();
            iconPair.Current.Dispose();
        }

        iconCache.Clear();
    }

    private IconPair LoadIconPair(BossSplitDefinition definition, string fileName)
    {
        string iconKey = GetIconKey(definition, fileName);
        string customPath = settings.GetBossIconPath(iconKey);
        string cacheKey = string.IsNullOrWhiteSpace(customPath)
            ? $"asset:{fileName}"
            : $"file:{customPath}";

        if (iconCache.TryGetValue(cacheKey, out IconPair? iconPair))
        {
            return iconPair;
        }

        string path = !string.IsNullOrWhiteSpace(customPath)
            ? customPath
            : Path.Combine(AppContext.BaseDirectory, "Assets", "BossIcons", fileName);
        Bitmap lit = File.Exists(path) ? new Bitmap(path) : CreatePlaceholderIcon();
        Bitmap undefeated = CreateBossChecklistUndefeatedIcon(
            lit,
            settings.UndefeatedIconGrayscalePercent,
            settings.UndefeatedIconBrightnessPercent);
        Bitmap current = CreateBossChecklistUndefeatedIcon(
            lit,
            Math.Max(0, settings.UndefeatedIconGrayscalePercent - settings.CurrentBossIconGrayscaleWeakenPercent),
            Math.Min(100, settings.UndefeatedIconBrightnessPercent + settings.CurrentBossIconBrightnessBoostPercent));
        iconPair = new IconPair(lit, undefeated, current);
        iconCache[cacheKey] = iconPair;
        return iconPair;
    }

    private static string GetIconKey(BossSplitDefinition definition, string fileName)
    {
        int index = definition.IconFileNames
            .Select((value, itemIndex) => new { value, itemIndex })
            .FirstOrDefault(item => string.Equals(item.value, fileName, StringComparison.OrdinalIgnoreCase))
            ?.itemIndex ?? -1;
        return index >= 0 && index < definition.IconKeys.Count
            ? definition.IconKeys[index]
            : definition.Name;
    }

    private static Bitmap CreateBossChecklistUndefeatedIcon(
        Bitmap source,
        int grayscalePercent,
        int brightnessPercent)
    {
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        float grayscale = Math.Clamp(grayscalePercent, 0, 100) / 100f;
        float brightness = Math.Clamp(brightnessPercent, 0, 100) / 100f;

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Color pixel = source.GetPixel(x, y);
                if (pixel.A == 0)
                {
                    continue;
                }

                int gray = (int)Math.Round(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                int red = Darken(Lerp(pixel.R, gray, grayscale), brightness);
                int green = Darken(Lerp(pixel.G, gray, grayscale), brightness);
                int blue = Darken(Lerp(pixel.B, gray, grayscale), brightness);
                bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, red, green, blue));
            }
        }

        return bitmap;
    }

    private static int Lerp(int from, int to, float amount)
    {
        return Math.Clamp((int)Math.Round(from + (to - from) * amount), 0, 255);
    }

    private static int Darken(int value, float amount)
    {
        return Math.Clamp((int)Math.Round(value * amount), 0, 255);
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
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private sealed record IconPair(Image Lit, Image Undefeated, Image Current);

    private readonly record struct FontKey(float Size, bool Bold);

    private readonly record struct FontMetrics(float Ascent, float Descent);

    private readonly record struct ColumnWidth(SplitColumn Column, int Width);

    private readonly record struct SegmentBestDeltaHighlight(string Style, DateTime StartedAtUtc);

    private sealed record SplitCompletionAnimation(
        BossSplitDefinition Definition,
        TimeSpan SegmentTime,
        TimeSpan SplitTime,
        SplitComparison ReferenceSplitComparison,
        SplitComparison PersonalBestSegmentComparison,
        bool ShowSplitComparison,
        string SplitTimeOutlineStyle,
        bool ShowSegmentComparison,
        string SegmentTimeOutlineStyle,
        string SegmentBestDeltaHighlightStyle,
        DateTime StartedAtUtc);

    private readonly record struct ColumnRects(
        Rectangle? Icon,
        Rectangle? Time,
        Rectangle? Delta);

    private readonly record struct SplitLayout(Rectangle FirstRowRect, Rectangle TimerRect, int RowGap)
    {
        public Rectangle GetRowRect(int index)
        {
            return new Rectangle(
                FirstRowRect.X,
                FirstRowRect.Y + index * (FirstRowRect.Height + RowGap),
                FirstRowRect.Width,
                FirstRowRect.Height);
        }
    }

    private enum SplitColumn
    {
        Icon,
        Time,
        Delta
    }

    private readonly record struct SplitComparison(TimeSpan? Delta, bool ShowDelta)
    {
        public static SplitComparison Empty => new(null, false);
    }

    private readonly record struct UiPalette(
        Color ReferenceText,
        Color ActiveReferenceText,
        Color SplitText,
        Color DeltaAheadText,
        Color DeltaBehindText,
        Color DeltaEvenText,
        Color TimerText,
        Color TimerAheadText,
        Color TimerBehindText,
        Color TimerRecordText)
    {
        public static UiPalette From(UiColorSettings settings)
        {
            return new UiPalette(
                ColorText.Parse(settings.ReferenceText, Color.FromArgb(200, 200, 200)),
                ColorText.Parse(settings.ActiveReferenceText, Color.FromArgb(255, 211, 90)),
                ColorText.Parse(settings.SplitText, Color.FromArgb(240, 160, 64)),
                ColorText.Parse(settings.DeltaAheadText, Color.LightGreen),
                ColorText.Parse(settings.DeltaBehindText, Color.LightCoral),
                ColorText.Parse(settings.DeltaEvenText, Color.Gainsboro),
                ColorText.Parse(settings.TimerText, Color.FromArgb(242, 242, 242)),
                ColorText.Parse(settings.TimerAheadText, Color.LightGreen),
                ColorText.Parse(settings.TimerBehindText, Color.LightCoral),
                ColorText.Parse(settings.TimerRecordText, Color.FromArgb(105, 167, 255)));
        }
    }
}
