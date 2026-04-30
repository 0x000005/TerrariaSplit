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
    private const int ResizeBorder = 8;
    private const int RowGap = 9;

    private readonly SplitTimer runTimer = new();
    private readonly BossSplitTracker splitTracker = new();
    private readonly TerrariaWorldWatcher watcher = new();
    private readonly System.Windows.Forms.Timer uiTimer = new();
    private readonly Dictionary<string, IconPair> iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<FontKey, Font> fontCache = new();
    private readonly ContextMenuStrip contextMenu = new();
    private bool mouseClickThrough;
    private bool dragging;
    private Point dragStartCursor;
    private Point dragStartLocation;

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
        MinimumSize = new Size(300, 420);
        Size = new Size(GetDefaultWindowWidth(settings), 720);
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = TransparentKeyColor;
        TransparencyKey = TransparentKeyColor;
        Padding = Padding.Empty;

        contextMenu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (_, _) => Close());
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

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        uiTimer.Stop();
        watcher.Dispose();

        foreach (IconPair iconPair in iconCache.Values)
        {
            iconPair.Lit.Dispose();
            iconPair.Undefeated.Dispose();
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
            ResetRun();
            return;
        }

        if (Keyboard.PollPressed(settings.MouseClickThroughKeys))
        {
            SetMouseClickThrough(!mouseClickThrough);
        }

        if (snapshot.EnteredWorld && runTimer.Phase == SplitTimerPhase.NotStarted)
        {
            runTimer.Start();
            splitTracker.OnRunStarted(snapshot);
        }

        if (runTimer.Phase == SplitTimerPhase.Running)
        {
            BossSplitRecord? split = splitTracker.Update(snapshot, runTimer.Elapsed);
            if (split is not null)
            {
                if (splitTracker.CurrentIndex >= splitTracker.Statuses.Count)
                {
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

        for (int i = 0; i < statuses.Count; i++)
        {
            BossSplitStatus status = statuses[i];
            bool isCurrent = i == splitTracker.CurrentIndex && runTimer.Phase != SplitTimerPhase.NotStarted;
            DrawSplitRow(graphics, layout.GetRowRect(i), status, isCurrent, palette);
        }

        DrawTimer(graphics, layout.TimerRect, palette);
    }

    private void DrawSplitRow(Graphics graphics, Rectangle rect, BossSplitStatus status, bool isCurrent, UiPalette palette)
    {
        ColumnRects columns = GetColumnRects(rect);

        if (columns.Icon is Rectangle iconColumnRect)
        {
            Rectangle iconRect = Rectangle.Inflate(iconColumnRect, -2, 0);
            DrawIcons(graphics, iconRect, status);
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
                : FormatReferenceTime(status.Definition.Name);

            using var timeBrush = new SolidBrush(timeColor);
            DrawText(
                graphics,
                timeText,
                GetColumnFont(settings.Columns.Time),
                timeBrush,
                timeRect,
                ContentAlignment.MiddleRight);
        }

        if (columns.Delta is Rectangle deltaRect)
        {
            using var compareBrush = new SolidBrush(GetDeltaComparisonColor(comparison, palette));
            DrawText(
                graphics,
                FormatSplitDelta(comparison),
                GetColumnFont(settings.Columns.Delta),
                compareBrush,
                deltaRect,
                ContentAlignment.MiddleRight);
        }
    }

    private ColumnRects GetColumnRects(Rectangle rect)
    {
        List<ColumnWidth> visibleColumns = new();
        AddColumn(visibleColumns, SplitColumn.Icon, settings.Columns.Icon);
        AddColumn(visibleColumns, SplitColumn.Time, settings.Columns.Time);
        AddColumn(visibleColumns, SplitColumn.Delta, settings.Columns.Delta);

        int requestedWidth = visibleColumns.Sum(column => column.Width);
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
                : Math.Max(1, (int)Math.Round(column.Width * scale));
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
            string currentText = TimeText.FormatSplit(runTimer.Elapsed);
            if (!PromptForTime("Edit total time", currentText, allowEmpty: false, out string? editedText) ||
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
        string currentText = status.Time is TimeSpan time ? TimeText.FormatSplit(time) : string.Empty;
        if (!PromptForTime("Edit split time", currentText, allowEmpty: true, out string? editedText))
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
        Invalidate();
    }

    private bool PromptForTime(string title, string value, bool allowEmpty, out string editedText)
    {
        return TimeEditDialog.TryShow(this, title, value, allowEmpty, out editedText);
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

        const int margin = 12;
        Rectangle bounds = ClientRectangle;
        if (bounds.Width < 160 || bounds.Height < 160)
        {
            return false;
        }

        Rectangle content = Rectangle.Inflate(bounds, -margin, -margin);
        int timerHeight = Math.Clamp((int)(content.Height * 0.17), 82, 110);
        int listSpace = content.Height - timerHeight - 10;
        int rowHeight = Math.Clamp(
            (listSpace - Math.Max(0, statuses.Count - 1) * RowGap) / Math.Max(1, statuses.Count),
            42,
            58);
        if (rowHeight <= 0)
        {
            return false;
        }

        int timerY = content.Y + statuses.Count * rowHeight + Math.Max(0, statuses.Count - 1) * RowGap + 2;
        layout = new SplitLayout(
            new Rectangle(content.X + 2, content.Y, content.Width - 4, rowHeight),
            new Rectangle(content.X, timerY, content.Width, timerHeight),
            RowGap);
        return true;
    }

    private static void AddColumn(List<ColumnWidth> columns, SplitColumn column, UiColumnSettings settings)
    {
        if (settings.Show)
        {
            columns.Add(new ColumnWidth(column, Math.Max(1, settings.Width)));
        }
    }

    private Font GetColumnFont(UiColumnSettings columnSettings, bool forceBold = false)
    {
        float size = Math.Clamp(columnSettings.FontSize, 6f, 48f);
        bool bold = forceBold || columnSettings.Bold;
        var key = new FontKey(size, bold);
        if (fontCache.TryGetValue(key, out Font? font))
        {
            return font;
        }

        font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
        fontCache[key] = font;
        return font;
    }

    private static int GetDefaultWindowWidth(AppSettings settings)
    {
        int columnsWidth = 0;
        columnsWidth += settings.Columns.Icon.Show ? settings.Columns.Icon.Width : 0;
        columnsWidth += settings.Columns.Time.Show ? settings.Columns.Time.Width : 0;
        columnsWidth += settings.Columns.Delta.Show ? settings.Columns.Delta.Width : 0;
        return Math.Clamp(columnsWidth + 28, 300, 1200);
    }

    private void DrawIcons(Graphics graphics, Rectangle rect, BossSplitStatus status)
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
                Math.Min(Math.Max(12, (int)Math.Round(settings.Columns.Icon.FontSize)), rect.Height),
                rect.Width);
            var iconRect = new Rectangle(
                rect.Right - singleIconSize,
                rect.Y + Math.Max(0, (rect.Height - singleIconSize) / 2),
                singleIconSize,
                singleIconSize);
            graphics.DrawImage(lit ? icon.Lit : icon.Undefeated, iconRect);
            return;
        }

        int iconGap = 6;
        int size = Math.Min(
            Math.Min(Math.Max(12, (int)Math.Round(settings.Columns.Icon.FontSize)), rect.Height),
            Math.Max(12, (rect.Width - Math.Max(0, count - 1) * iconGap) / count));
        int totalWidth = count * size + (count - 1) * iconGap;
        int startX = rect.Right - totalWidth;
        int y = rect.Y + Math.Max(0, (rect.Height - size) / 2);
        for (int i = 0; i < count; i++)
        {
            IconPair icon = LoadIconPair(definition, definition.IconFileNames[i]);
            bool lit = IsIconLit(status, i);
            graphics.DrawImage(lit ? icon.Lit : icon.Undefeated, new Rectangle(startX + i * (size + iconGap), y, size, size));
        }
    }

    private bool IsIconLit(BossSplitStatus status, int iconIndex)
    {
        if (status.IsCompleted || status.IsSkipped)
        {
            return true;
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
        var timeRect = new Rectangle(rect.X + 4, rect.Y - 4, rect.Width - 8, rect.Height - 16);
        using var timerTextBrush = new SolidBrush(GetTimerTextColor(palette));
        DrawTimerText(graphics, runTimer.Elapsed, timerTextBrush, timeRect, GetTimerMainRightEdge());
    }

    private string FormatReferenceTime(string name)
    {
        return settings.TryGetReferenceSplit(name, out TimeSpan split)
            ? TimeText.FormatSplit(split)
            : "--";
    }

    private SplitComparison GetSplitComparison(BossSplitStatus status, bool isCurrent)
    {
        if (!settings.TryGetReferenceSplit(status.Definition.Name, out TimeSpan referenceTime))
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

        float gap = settings.Columns.Timer.Show && settings.Columns.TimerMilliseconds.Show ? 2f : 0f;
        FontMetrics mainMetrics = GetFontMetrics(graphics, mainFont);
        FontMetrics millisecondsMetrics = GetFontMetrics(graphics, millisecondsFont);
        float groupAscent = Math.Max(mainMetrics.Ascent, millisecondsMetrics.Ascent);
        float groupDescent = Math.Max(mainMetrics.Descent, millisecondsMetrics.Descent);
        float groupHeight = groupAscent + groupDescent;
        float groupY = bounds.Y + Math.Max(0, (bounds.Height - groupHeight) / 2f);
        float baselineY = groupY + groupAscent;

        float mainRight = Math.Clamp(mainRightEdge, bounds.Left, bounds.Right);
        float mainX = mainRight - mainSize.Width;
        float mainY = baselineY - mainMetrics.Ascent;
        float millisecondsX = mainRight + gap;
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

    private Color GetTimerTextColor(UiPalette palette)
    {
        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (statuses.Count > 0 && statuses[^1].Time is TimeSpan finalTime)
        {
            return settings.TryGetReferenceSplit(statuses[^1].Definition.Name, out TimeSpan finalReference) &&
                finalTime < finalReference
                ? palette.TimerRecordText
                : palette.TimerBehindText;
        }

        if (splitTracker.CurrentIndex < statuses.Count &&
            settings.TryGetReferenceSplit(statuses[splitTracker.CurrentIndex].Definition.Name, out TimeSpan currentReference))
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
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        settings = form.Result;
        AppSettingsStore.Save(settings);
        splitTracker.SetDefinitions(BossSplitDefinitions.Build(settings));
        ResetRun();
        TopMost = settings.AlwaysOnTop;
        Width = Math.Max(MinimumSize.Width, GetDefaultWindowWidth(settings));
        ClearIconCache();
        Invalidate();
    }

    private void ResetRun()
    {
        runTimer.Reset();
        splitTracker.Reset();
        Invalidate();
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
        iconPair = new IconPair(lit, undefeated);
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

    private sealed record IconPair(Image Lit, Image Undefeated);

    private readonly record struct FontKey(float Size, bool Bold);

    private readonly record struct FontMetrics(float Ascent, float Descent);

    private readonly record struct ColumnWidth(SplitColumn Column, int Width);

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
