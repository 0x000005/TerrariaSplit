using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Security.Cryptography;
using System.Windows.Forms;
using TerrariaSplit.Configuration;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.UI.Rendering;

namespace TerrariaSplit.UI;

internal sealed class RaceLeaderboardForm : Form
{
    private const string LeaderboardWindowTitle = "TerrariaSplit - Race Leaderboard";
    private const int ResizeBorder = 8;
    private const int RowPaddingX = 8;
    private const int LayoutVerticalPadding = 6;
    private const float MinimumFittingTextSize = 1f;
    private static readonly Size InitialBaseClientSize = new(720, 360);
    private static readonly Size MinimumBaseClientSize = new(220, 80);

    private readonly Func<AppSettings> getSettings;
    private readonly Func<string, string> localize;
    private readonly Func<string?> getLocalNickname;
    private readonly Action<Point> savePosition;
    private readonly OverlayWindowController overlayWindowController;
    private readonly BossIconCache iconCache = new();
    private readonly Dictionary<string, RouteIconCacheEntry> routeIconDataCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly OverlayFontCache fontCache = new();
    private RaceRoomState? state;
    private bool mouseClickThrough;
    private bool dragging;
    private Point dragStartCursor;
    private Point dragStartLocation;

    public RaceLeaderboardForm(
        Func<AppSettings> getSettings,
        Func<string, string> localize,
        Func<string?> getLocalNickname,
        Action<Point> savePosition)
    {
        this.getSettings = getSettings;
        this.localize = localize;
        this.getLocalNickname = getLocalNickname;
        this.savePosition = savePosition;
        overlayWindowController = new OverlayWindowController(this, DrawOverlay);

        Text = LeaderboardWindowTitle;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        BackColor = Color.Black;
        TransparencyKey = Color.Empty;
        Padding = Padding.Empty;
        UiTheme.ConfigureForm(this, new Size(560, 280));
        UiDpiScale.ApplyBase200ClientLayout(this, InitialBaseClientSize, MinimumBaseClientSize);
        MinimumSize = GetMinimumClientSize();
        ResizeRedraw = true;
        ApplySettings();
        RestoreWindowPosition();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.Style = OverlayWindowController.ComposeBorderlessStyle(parameters.Style);
            parameters.ExStyle = OverlayWindowController.ComposeExtendedStyle(parameters.ExStyle, mouseClickThrough);
            return parameters;
        }
    }

    internal bool MouseClickThrough => mouseClickThrough;

    public void UpdateState(RaceRoomState? nextState)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateState(nextState)));
            return;
        }

        if (state?.PackageRevision != nextState?.PackageRevision)
        {
            routeIconDataCache.Clear();
            iconCache.Clear();
        }

        state = nextState;
        ApplyPreferredClientSize();
        QueueRender();
    }

    public void ApplySettings()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(ApplySettings));
            return;
        }

        ApplyPreferredClientSize();
        QueueRender();
    }

    public void ApplyMouseClickThrough(bool enabled)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ApplyMouseClickThrough(enabled)));
            return;
        }

        mouseClickThrough = enabled;
        if (enabled)
        {
            dragging = false;
        }

        overlayWindowController.ApplyWindowStyle(mouseClickThrough);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            iconCache.Dispose();
            fontCache.Dispose();
            overlayWindowController.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        overlayWindowController.ApplyWindowStyle(mouseClickThrough);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        overlayWindowController.RenderImmediately();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        QueueRender();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        QueueRender();
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        QueueRender();
    }

    private bool DrawOverlay(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        IReadOnlyList<RaceLeaderboardEntry> rows = state?.Leaderboard ?? [];
        if (rows.Count == 0)
        {
            return true;
        }

        AppSettings settings = getSettings();
        RaceLeaderboardSettings leaderboard = settings.Race?.Leaderboard ?? new RaceLeaderboardSettings();
        RaceLeaderboardLayout layout = RaceLeaderboardLayout.From(settings, leaderboard);
        RaceLeaderboardRenderColors colors = RaceLeaderboardRenderColors.From(leaderboard.Colors ?? new RaceLeaderboardColorSettings());
        string? localNickname = getLocalNickname();
        int rowHeight = GetRowHeight(layout);
        RaceLeaderboardColumnWidths columnWidths = GetColumnWidths(layout, ClientSize.Width);
        int y = ScaleInt(LayoutVerticalPadding);
        int rowCount = rows.Count;
        foreach (RaceLeaderboardEntry entry in rows)
        {
            Rectangle rowRect = new(0, y, ClientSize.Width, rowHeight);
            DrawRow(graphics, settings, layout, columnWidths, colors, entry, rowCount, localNickname, rowRect);
            y += rowHeight;
            if (y > ClientSize.Height)
            {
                break;
            }
        }

        return true;
    }

    private void QueueRender()
    {
        if (!IsHandleCreated || IsDisposed || Disposing)
        {
            return;
        }

        overlayWindowController.QueueRender();
    }

    private void ApplyPreferredClientSize()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        Size minimumClientSize = GetMinimumClientSize();
        MinimumSize = minimumClientSize;
        Size preferredClientSize = ConstrainClientSizeToWorkingArea(CalculatePreferredClientSize(), minimumClientSize);
        if (ClientSize != preferredClientSize)
        {
            ClientSize = preferredClientSize;
        }

        KeepWindowWithinWorkingArea();
    }

    private Size CalculatePreferredClientSize()
    {
        AppSettings settings = getSettings();
        RaceLeaderboardSettings leaderboard = settings.Race?.Leaderboard ?? new RaceLeaderboardSettings();
        RaceLeaderboardLayout layout = RaceLeaderboardLayout.From(settings, leaderboard);
        int width = GetLayoutWidth(layout);
        int rowCount = Math.Max(1, state?.Leaderboard.Count ?? 0);
        int height = ScaleInt(LayoutVerticalPadding * 2) + rowCount * GetRowHeight(layout);
        Size minimumClientSize = GetMinimumClientSize();
        return new Size(
            Math.Max(minimumClientSize.Width, width),
            Math.Max(minimumClientSize.Height, height));
    }

    private Size ConstrainClientSizeToWorkingArea(Size preferredClientSize, Size minimumClientSize)
    {
        Rectangle workingArea = GetCurrentWorkingArea();
        int maximumWidth = Math.Max(minimumClientSize.Width, workingArea.Width);
        int maximumHeight = Math.Max(minimumClientSize.Height, workingArea.Height);
        return new Size(
            Math.Clamp(preferredClientSize.Width, minimumClientSize.Width, maximumWidth),
            Math.Clamp(preferredClientSize.Height, minimumClientSize.Height, maximumHeight));
    }

    private void KeepWindowWithinWorkingArea()
    {
        if (!Visible || WindowState != FormWindowState.Normal)
        {
            return;
        }

        Rectangle workingArea = GetCurrentWorkingArea();
        int left = Left;
        int top = Top;
        if (Width >= workingArea.Width)
        {
            left = workingArea.Left;
        }
        else
        {
            left = Math.Clamp(left, workingArea.Left, workingArea.Right - Width);
        }

        if (Height >= workingArea.Height)
        {
            top = workingArea.Top;
        }
        else
        {
            top = Math.Clamp(top, workingArea.Top, workingArea.Bottom - Height);
        }

        if (left != Left || top != Top)
        {
            Location = new Point(left, top);
        }
    }

    private Rectangle GetCurrentWorkingArea()
    {
        return IsHandleCreated
            ? Screen.FromControl(this).WorkingArea
            : Screen.FromPoint(Cursor.Position).WorkingArea;
    }

    private Size GetMinimumClientSize()
    {
        return UiDpiScale.ScaleSize(MinimumBaseClientSize, UiDpiScale.GetAppliedScale(this));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (mouseClickThrough || e.Button != MouseButtons.Left || WindowState == FormWindowState.Maximized)
        {
            return;
        }

        dragging = true;
        dragStartCursor = Cursor.Position;
        dragStartLocation = Location;
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
        if (e.Button == MouseButtons.Left && dragging)
        {
            dragging = false;
            savePosition(Location);
        }

        base.OnMouseUp(e);
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        savePosition(Location);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        savePosition(Location);
        base.OnFormClosing(e);
    }

    private void RestoreWindowPosition()
    {
        RaceLeaderboardSettings leaderboard =
            getSettings().Race?.Leaderboard ?? new RaceLeaderboardSettings();
        Rectangle fallbackWorkingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = OverlayWindowPlacement.Resolve(
            Size,
            leaderboard.WindowPositionX,
            leaderboard.WindowPositionY,
            fallbackWorkingArea,
            Screen.AllScreens.Select(screen => screen.WorkingArea));
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

        if (m.Msg != wmNcHitTest ||
            m.Result != (IntPtr)htClient ||
            WindowState == FormWindowState.Maximized)
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

    private void DrawRow(
        Graphics graphics,
        AppSettings settings,
        RaceLeaderboardLayout layout,
        RaceLeaderboardColumnWidths columnWidths,
        RaceLeaderboardRenderColors colors,
        RaceLeaderboardEntry entry,
        int rowCount,
        string? localNickname,
        Rectangle rowRect)
    {
        int x = ScaleInt(RowPaddingX);
        Color rankFill = RaceLeaderboardColorMath.GetRankFillColor(
            entry.Rank,
            rowCount,
            colors.RankGradient.Start,
            colors.RankGradient.Middle,
            colors.RankGradient.End);
        DrawTextColumn(
            graphics,
            "#" + entry.Rank.ToString(CultureInfo.InvariantCulture),
            layout.Rank,
            layout.RankEffect,
            columnWidths.Rank,
            columnWidths.RankGap,
            layout.RankAlignment,
            ref x,
            rowRect,
            rankFill,
            colors.Rank.Outline,
            colors.Rank.Shadow);
        RaceLeaderboardColumnRenderColors playerColors = IsLocalPlayer(entry, localNickname)
            ? colors.PlayerSelf
            : colors.PlayerOther;
        DrawTextColumn(
            graphics,
            entry.Nickname,
            layout.Player,
            layout.PlayerEffect,
            columnWidths.Player,
            columnWidths.PlayerGap,
            layout.PlayerAlignment,
            ref x,
            rowRect,
            playerColors.Text,
            playerColors.Outline,
            playerColors.Shadow);
        DrawIconColumn(
            graphics,
            settings,
            layout.Icon,
            layout.IconEffect,
            colors.Icon,
            columnWidths.Icon,
            columnWidths.IconGap,
            layout.IconAlignment,
            ref x,
            rowRect,
            entry);
        DrawTextColumn(
            graphics,
            FormatMilliseconds(entry.LastSplitElapsedMilliseconds),
            layout.Time,
            layout.TimeEffect,
            columnWidths.Time,
            columnWidths.TimeGap,
            layout.TimeAlignment,
            ref x,
            rowRect,
            colors.Time.Text,
            colors.Time.Outline,
            colors.Time.Shadow);
    }

    private void DrawTextColumn(
        Graphics graphics,
        string text,
        UiColumnSettings column,
        RaceLeaderboardColumnEffectSettings effect,
        int width,
        int gapBefore,
        string alignment,
        ref int x,
        Rectangle rowRect,
        Color fill,
        Color outline,
        Color shadow)
    {
        if (!column.Show)
        {
            return;
        }

        x += gapBefore;
        Rectangle bounds = GetColumnContentBounds(x, width, rowRect);
        Font font = CreateFittingFont(graphics, text, column, effect, bounds);
        DrawFittedText(
            graphics,
            text,
            font,
            new TextRenderStyle(
                fill,
                outline,
                shadow,
                effect.ShadowPercent,
                effect.OutlineThicknessPercent,
                LinearEffects: true),
            bounds,
            GetContentAlignment(alignment),
            Math.Clamp(effect.OpacityPercent, 0, 100) / 100f);
        x += width;
    }

    private static void DrawFittedText(
        Graphics graphics,
        string text,
        Font font,
        TextRenderStyle style,
        Rectangle bounds,
        ContentAlignment alignment,
        float opacity)
    {
        if (string.IsNullOrEmpty(text) || opacity <= 0.01f || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.None
        };
        using GraphicsPath measurePath = TextEffectRenderer.CreateTextPath(graphics, text, font, 0f, 0f, format);
        if (measurePath.PointCount == 0)
        {
            return;
        }

        RectangleF textBounds = TextEffectGeometry.GetTextEffectLayerBounds(graphics, measurePath, font, style);
        float x = alignment switch
        {
            ContentAlignment.MiddleRight => bounds.Right - textBounds.Right,
            ContentAlignment.MiddleCenter => bounds.Left + (bounds.Width - textBounds.Width) / 2f - textBounds.Left,
            _ => bounds.Left - textBounds.Left
        };
        float y = bounds.Top + (bounds.Height - textBounds.Height) / 2f - textBounds.Top;
        TextEffectRenderer.DrawStyledString(
            graphics,
            text,
            font,
            style,
            x,
            y,
            format,
            opacity,
            supersampleEffects: false);
    }

    private void DrawIconColumn(
        Graphics graphics,
        AppSettings settings,
        UiColumnSettings column,
        RaceLeaderboardColumnEffectSettings effect,
        RaceLeaderboardColumnRenderColors colors,
        int width,
        int gapBefore,
        string alignment,
        ref int x,
        Rectangle rowRect,
        RaceLeaderboardEntry entry)
    {
        if (!column.Show)
        {
            return;
        }

        x += gapBefore;
        Rectangle bounds = GetColumnContentBounds(x, width, rowRect);
        Image? image = GetIconImage(entry, settings);
        if (image is not null)
        {
            int size = Math.Min(
                Math.Min(bounds.Height, bounds.Width),
                Math.Max(12, ScaleInt((int)Math.Round(column.FontSize))));
            Rectangle iconRect = new(
                GetAlignedLeft(bounds, size, alignment),
                bounds.Y + Math.Max(0, (bounds.Height - size) / 2),
                size,
                size);
            TextEffectRenderer.DrawImage(
                graphics,
                image,
                iconRect,
                Math.Clamp(effect.OpacityPercent, 0, 100) / 100f,
                new ImageRenderStyle(colors.Outline, colors.Shadow, effect.ShadowPercent, effect.OutlineThicknessPercent));
        }

        x += width;
    }

    private static ContentAlignment GetContentAlignment(string? alignment)
    {
        return UiColumnAlignment.Normalize(alignment, UiColumnAlignment.Right) switch
        {
            UiColumnAlignment.Left => ContentAlignment.MiddleLeft,
            UiColumnAlignment.Center => ContentAlignment.MiddleCenter,
            _ => ContentAlignment.MiddleRight
        };
    }

    private static int GetAlignedLeft(Rectangle bounds, int contentWidth, string? alignment)
    {
        return UiColumnAlignment.Normalize(alignment, UiColumnAlignment.Right) switch
        {
            UiColumnAlignment.Left => bounds.Left,
            UiColumnAlignment.Center => bounds.Left + Math.Max(0, (bounds.Width - contentWidth) / 2),
            _ => bounds.Right - contentWidth
        };
    }

    private Image? GetIconImage(RaceLeaderboardEntry entry, AppSettings settings)
    {
        string iconKey = ResolveIconKey(entry);
        string iconFileName = ResolveIconFileName(entry, iconKey);
        if (string.IsNullOrWhiteSpace(iconFileName))
        {
            return null;
        }

        var definition = new SplitDefinition(
            entry.LastSplitId ?? iconKey,
            entry.LastIconDisplayName ?? entry.LastSplitId ?? iconKey,
            SplitCondition.All([]),
            [iconFileName],
            [iconKey],
                [iconKey]);
        IconPair icon = TryLoadRouteIcon(entry, iconKey, iconFileName, settings) ??
            iconCache.Load(definition, 0, settings);
        iconCache.TrackRendered(icon);
        return icon.GetLitImage(DateTime.UtcNow);
    }

    private string ResolveIconKey(RaceLeaderboardEntry entry)
    {
        if (TryGetRouteSplit(entry, out RaceSplitDefinition? routeSplit))
        {
            if (!string.IsNullOrWhiteSpace(entry.LastTargetId) &&
                routeSplit.IconKeys.Any(key => string.Equals(key, entry.LastTargetId, StringComparison.OrdinalIgnoreCase)))
            {
                return entry.LastTargetId.Trim();
            }

            if (routeSplit.IconKeys.Count == 1)
            {
                return routeSplit.IconKeys[0];
            }
        }

        if (!string.IsNullOrWhiteSpace(entry.LastTargetId))
        {
            return entry.LastTargetId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(entry.LastFactKey) &&
            SplitCatalog.TryGetTargetByFactKey(entry.LastFactKey, out SplitTargetDefinition target))
        {
            return target.Id;
        }

        return entry.LastSplitId?.Trim() ?? string.Empty;
    }

    private string ResolveIconFileName(RaceLeaderboardEntry entry, string iconKey)
    {
        if (!string.IsNullOrWhiteSpace(entry.LastIconFileName))
        {
            return entry.LastIconFileName.Trim();
        }

        if (TryResolveRouteIconFileName(entry, iconKey, out string routeIconFileName))
        {
            return routeIconFileName;
        }

        if (!string.IsNullOrWhiteSpace(entry.LastTargetId) &&
            SplitCatalog.TryGetTarget(entry.LastTargetId, out SplitTargetDefinition target))
        {
            return target.IconFileName;
        }

        if (!string.IsNullOrWhiteSpace(entry.LastFactKey) &&
            SplitCatalog.TryGetTargetByFactKey(entry.LastFactKey, out SplitTargetDefinition factTarget))
        {
            return factTarget.IconFileName;
        }

        return !string.IsNullOrWhiteSpace(iconKey) &&
            SplitCatalog.TryGetReferenceIconFileName(iconKey, out string referenceFileName)
                ? referenceFileName
                : string.Empty;
    }

    private IconPair? TryLoadRouteIcon(
        RaceLeaderboardEntry entry,
        string iconKey,
        string iconFileName,
        AppSettings settings)
    {
        RaceRouteIconPayload? payload = FindRouteIconPayload(entry, iconKey, iconFileName);
        if (payload?.DataBase64 is not string dataBase64 || string.IsNullOrWhiteSpace(dataBase64))
        {
            return null;
        }

        string payloadKey = payload.Key + "|" + payload.FileName;
        if (!routeIconDataCache.TryGetValue(payloadKey, out RouteIconCacheEntry? cached) ||
            !string.Equals(cached.DataBase64, dataBase64, StringComparison.Ordinal))
        {
            byte[] data;
            try
            {
                data = Convert.FromBase64String(dataBase64);
            }
            catch (FormatException)
            {
                return null;
            }

            string contentHash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
            cached = new RouteIconCacheEntry(
                dataBase64,
                data,
                payloadKey + "|" + contentHash);
            routeIconDataCache[payloadKey] = cached;
        }

        try
        {
            return iconCache.LoadEmbedded(
                cached.CacheKey,
                cached.Data,
                string.IsNullOrWhiteSpace(payload.Key) ? iconKey : payload.Key,
                settings);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private sealed record RouteIconCacheEntry(
        string DataBase64,
        byte[] Data,
        string CacheKey);

    private RaceRouteIconPayload? FindRouteIconPayload(
        RaceLeaderboardEntry entry,
        string iconKey,
        string iconFileName)
    {
        IReadOnlyList<RaceRouteIconPayload> icons = state?.Route?.Icons ?? [];
        if (icons.Count == 0)
        {
            return null;
        }

        RaceRouteIconPayload? exact = icons.FirstOrDefault(icon =>
            string.Equals(icon.Key, iconKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(icon.FileName, iconFileName, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        if (!string.IsNullOrWhiteSpace(iconFileName))
        {
            RaceRouteIconPayload? fileMatch = icons.FirstOrDefault(icon =>
                string.Equals(icon.FileName, iconFileName, StringComparison.OrdinalIgnoreCase));
            if (fileMatch is not null)
            {
                return fileMatch;
            }
        }

        return !string.IsNullOrWhiteSpace(iconKey)
            ? icons.FirstOrDefault(icon => string.Equals(icon.Key, iconKey, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private bool TryResolveRouteIconFileName(
        RaceLeaderboardEntry entry,
        string iconKey,
        out string iconFileName)
    {
        iconFileName = string.Empty;
        if (!TryGetRouteSplit(entry, out RaceSplitDefinition? routeSplit))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(iconKey))
        {
            for (int index = 0; index < routeSplit.IconKeys.Count && index < routeSplit.IconFileNames.Count; index++)
            {
                if (string.Equals(routeSplit.IconKeys[index], iconKey, StringComparison.OrdinalIgnoreCase))
                {
                    iconFileName = routeSplit.IconFileNames[index];
                    return !string.IsNullOrWhiteSpace(iconFileName);
                }
            }
        }

        if (entry.LastConditionIndex >= 0 &&
            entry.LastConditionIndex < routeSplit.Conditions.Count &&
            !string.IsNullOrWhiteSpace(routeSplit.Conditions[entry.LastConditionIndex].IconFileName))
        {
            iconFileName = routeSplit.Conditions[entry.LastConditionIndex].IconFileName!;
            return true;
        }

        if (routeSplit.IconFileNames.Count == 1)
        {
            iconFileName = routeSplit.IconFileNames[0];
            return !string.IsNullOrWhiteSpace(iconFileName);
        }

        return false;
    }

    private bool TryGetRouteSplit(RaceLeaderboardEntry entry, out RaceSplitDefinition routeSplit)
    {
        routeSplit = null!;
        IReadOnlyList<RaceSplitDefinition> routeSplits = state?.Route?.Splits ?? [];
        routeSplit = routeSplits.FirstOrDefault(split =>
            split.Index == entry.LastSplitIndex ||
            string.Equals(split.Id, entry.LastSplitId, StringComparison.OrdinalIgnoreCase))!;
        return routeSplit is not null;
    }

    private Font CreateFont(UiColumnSettings column)
    {
        return fontCache.GetColumnFont(column, GetContentScale());
    }

    private Rectangle GetColumnContentBounds(int x, int width, Rectangle rowRect)
    {
        int padding = Math.Min(ScaleInt(4), Math.Max(0, (width - 1) / 2));
        return Rectangle.Inflate(new Rectangle(x, rowRect.Y, width, rowRect.Height), -padding, 0);
    }

    private Font CreateFittingFont(
        Graphics graphics,
        string text,
        UiColumnSettings column,
        RaceLeaderboardColumnEffectSettings effect,
        Rectangle bounds)
    {
        Font baseFont = CreateFont(column);
        float sizeScale = GetFittingTextSizeScale(graphics, text, column, effect, bounds);
        return sizeScale >= 0.995f
            ? baseFont
            : fontCache.GetColumnFont(
                column,
                GetContentScale(),
                sizeScale: sizeScale,
                minimumSize: MinimumFittingTextSize);
    }

    private float GetFittingTextSizeScale(
        Graphics graphics,
        string text,
        UiColumnSettings column,
        RaceLeaderboardColumnEffectSettings effect,
        Rectangle bounds)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return 1f;
        }

        if (DoesTextFit(graphics, text, column, effect, bounds, 1f))
        {
            return 1f;
        }

        float low = 0.01f;
        float high = 1f;
        for (int i = 0; i < 12; i++)
        {
            float middle = (low + high) / 2f;
            if (DoesTextFit(graphics, text, column, effect, bounds, middle))
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return Math.Clamp((float)Math.Round(low, 3), 0.01f, 1f);
    }

    private bool DoesTextFit(
        Graphics graphics,
        string text,
        UiColumnSettings column,
        RaceLeaderboardColumnEffectSettings effect,
        Rectangle bounds,
        float sizeScale)
    {
        Font font = fontCache.GetColumnFont(
            column,
            GetContentScale(),
            sizeScale: sizeScale,
            minimumSize: MinimumFittingTextSize);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.None
        };
        using GraphicsPath path = TextEffectRenderer.CreateTextPath(graphics, text, font, 0f, 0f, format);
        if (path.PointCount == 0)
        {
            return true;
        }

        var style = new TextRenderStyle(
            Color.White,
            Color.Black,
            Color.Black,
            effect.ShadowPercent,
            effect.OutlineThicknessPercent,
            LinearEffects: true);
        RectangleF effectBounds = TextEffectGeometry.GetTextEffectLayerBounds(graphics, path, font, style);
        float safetyPadding = Math.Max(2f, font.SizeInPoints * 0.08f);
        return effectBounds.Width + safetyPadding <= bounds.Width &&
            effectBounds.Height + safetyPadding <= bounds.Height;
    }

    private int GetRowHeight(RaceLeaderboardLayout layout)
    {
        float fontSize = new[]
        {
            layout.Rank.FontSize,
            layout.Player.FontSize,
            layout.Icon.FontSize,
            layout.Time.FontSize
        }.Max();
        return Math.Max(ScaleInt(36), ScaleInt((int)Math.Ceiling(fontSize + 14)));
    }

    private int GetLayoutWidth(RaceLeaderboardLayout layout)
    {
        int width = ScaleInt(RowPaddingX * 2);
        width += GetColumnWidth(layout.Rank);
        width += GetColumnWidth(layout.Player);
        width += GetColumnWidth(layout.Icon);
        width += GetColumnWidth(layout.Time);
        width += GetGapWidth(GetPlayerGap(layout));
        width += GetGapWidth(GetIconGap(layout));
        width += GetGapWidth(GetTimeGap(layout));
        return width;
    }

    private RaceLeaderboardColumnWidths GetColumnWidths(RaceLeaderboardLayout layout, int clientWidth)
    {
        int rankWidth = GetColumnWidth(layout.Rank);
        int playerWidth = GetColumnWidth(layout.Player);
        int iconWidth = GetColumnWidth(layout.Icon);
        int timeWidth = GetColumnWidth(layout.Time);
        int playerGap = GetGapWidth(GetPlayerGap(layout));
        int iconGap = GetGapWidth(GetIconGap(layout));
        int timeGap = GetGapWidth(GetTimeGap(layout));
        int requestedWidth = rankWidth + playerWidth + iconWidth + timeWidth + playerGap + iconGap + timeGap;
        int availableWidth = Math.Max(1, clientWidth - ScaleInt(RowPaddingX * 2));
        float widthScale = requestedWidth > availableWidth && requestedWidth > 0
            ? availableWidth / (float)requestedWidth
            : 1f;

        rankWidth = ScaleLayoutValue(rankWidth, layout.Rank.Show, widthScale);
        playerWidth = ScaleLayoutValue(playerWidth, layout.Player.Show, widthScale);
        iconWidth = ScaleLayoutValue(iconWidth, layout.Icon.Show, widthScale);
        timeWidth = ScaleLayoutValue(timeWidth, layout.Time.Show, widthScale);
        playerGap = ScaleLayoutValue(playerGap, playerGap > 0, widthScale);
        iconGap = ScaleLayoutValue(iconGap, iconGap > 0, widthScale);
        timeGap = ScaleLayoutValue(timeGap, timeGap > 0, widthScale);
        int contentWidth = Math.Max(1, availableWidth - playerGap - iconGap - timeGap);
        RaceLeaderboardColumnWidths fitted = FitColumnWidthsToAvailableWidth(
            contentWidth,
            rankWidth,
            playerWidth,
            iconWidth,
            timeWidth);
        return fitted with
        {
            PlayerGap = playerGap,
            IconGap = iconGap,
            TimeGap = timeGap
        };
    }

    private int GetColumnWidth(UiColumnSettings column)
    {
        return column.Show ? ScaleInt(Math.Max(1, column.Width)) : 0;
    }

    private int GetGapWidth(int gap)
    {
        return ScaleInt(Math.Max(0, gap));
    }

    private static int ScaleLayoutValue(int value, bool visible, float widthScale)
    {
        return visible ? Math.Max(1, (int)Math.Round(value * widthScale)) : 0;
    }

    private static int GetPlayerGap(RaceLeaderboardLayout layout)
    {
        return layout.Rank.Show && layout.Player.Show ? layout.RankPlayerGap : 0;
    }

    private static int GetIconGap(RaceLeaderboardLayout layout)
    {
        return layout.Icon.Show && (layout.Rank.Show || layout.Player.Show) ? layout.PlayerIconGap : 0;
    }

    private static int GetTimeGap(RaceLeaderboardLayout layout)
    {
        return layout.Time.Show && (layout.Rank.Show || layout.Player.Show || layout.Icon.Show)
            ? layout.IconTimeGap
            : 0;
    }

    private static RaceLeaderboardColumnWidths FitColumnWidthsToAvailableWidth(
        int availableWidth,
        int rank,
        int player,
        int icon,
        int time)
    {
        int overflow = rank + player + icon + time - availableWidth;
        TrimColumnOverflow(ref time, ref overflow);
        TrimColumnOverflow(ref player, ref overflow);
        TrimColumnOverflow(ref icon, ref overflow);
        TrimColumnOverflow(ref rank, ref overflow);
        return new RaceLeaderboardColumnWidths(rank, player, icon, time, 0, 0, 0, 0);
    }

    private static void TrimColumnOverflow(ref int width, ref int overflow)
    {
        if (overflow <= 0 || width <= 0)
        {
            return;
        }

        int reduction = Math.Min(width - 1, overflow);
        if (reduction <= 0)
        {
            return;
        }

        width -= reduction;
        overflow -= reduction;
    }

    private static string FormatMilliseconds(long? milliseconds)
    {
        return milliseconds.HasValue
            ? TimeText.FormatSplit(TimeSpan.FromMilliseconds(milliseconds.Value))
            : "--";
    }

    private static bool IsLocalPlayer(RaceLeaderboardEntry entry, string? localNickname)
    {
        return !string.IsNullOrWhiteSpace(localNickname) &&
            string.Equals(entry.Nickname, localNickname.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private int ScaleInt(int value)
    {
        return OverlayRenderContext.ScaleInt(getSettings(), value);
    }

    private float GetContentScale()
    {
        return OverlayRenderContext.GetScaleFactor(getSettings());
    }

    private readonly record struct RaceLeaderboardColumnWidths(
        int Rank,
        int Player,
        int Icon,
        int Time,
        int RankGap,
        int PlayerGap,
        int IconGap,
        int TimeGap);

    private readonly record struct RaceLeaderboardColumnRenderColors(
        Color Text,
        Color Outline,
        Color Shadow);

    private readonly record struct RaceLeaderboardRankGradientRenderColors(
        Color Start,
        Color Middle,
        Color End);

    private sealed record RaceLeaderboardRenderColors(
        RaceLeaderboardColumnRenderColors Rank,
        RaceLeaderboardRankGradientRenderColors RankGradient,
        RaceLeaderboardColumnRenderColors PlayerSelf,
        RaceLeaderboardColumnRenderColors PlayerOther,
        RaceLeaderboardColumnRenderColors Icon,
        RaceLeaderboardColumnRenderColors Time)
    {
        public static RaceLeaderboardRenderColors From(RaceLeaderboardColorSettings colors)
        {
            RaceLeaderboardColorSettings defaults = new();
            return new RaceLeaderboardRenderColors(
                FromColumn(colors.Rank, defaults.Rank),
                FromRankGradient(colors.RankGradient, defaults.RankGradient),
                FromColumn(colors.PlayerSelf ?? colors.Player, defaults.PlayerSelf ?? defaults.Player),
                FromColumn(colors.PlayerOther ?? colors.Player, defaults.PlayerOther ?? defaults.Player),
                FromColumn(colors.Icon, defaults.Icon),
                FromColumn(colors.Time, defaults.Time));
        }

        private static RaceLeaderboardRankGradientRenderColors FromRankGradient(
            RaceLeaderboardRankGradientColorSettings? gradient,
            RaceLeaderboardRankGradientColorSettings defaults)
        {
            gradient ??= defaults;
            return new RaceLeaderboardRankGradientRenderColors(
                ColorText.Parse(gradient.Start, ColorText.Parse(defaults.Start, Color.Gold)),
                ColorText.Parse(gradient.Middle, ColorText.Parse(defaults.Middle, Color.White)),
                ColorText.Parse(gradient.End, ColorText.Parse(defaults.End, Color.Red)));
        }

        private static RaceLeaderboardColumnRenderColors FromColumn(
            RaceLeaderboardColumnColorSettings? colors,
            RaceLeaderboardColumnColorSettings defaults)
        {
            colors ??= defaults;
            return new RaceLeaderboardColumnRenderColors(
                ColorText.Parse(colors.Text, ColorText.Parse(defaults.Text, Color.White)),
                ColorText.Parse(colors.Outline, ColorText.Parse(defaults.Outline, Color.FromArgb(16, 16, 16))),
                ColorText.Parse(colors.Shadow, ColorText.Parse(defaults.Shadow, Color.Black)));
        }
    }

    private sealed record RaceLeaderboardLayout(
        UiColumnSettings Rank,
        RaceLeaderboardColumnEffectSettings RankEffect,
        UiColumnSettings Player,
        RaceLeaderboardColumnEffectSettings PlayerEffect,
        UiColumnSettings Icon,
        RaceLeaderboardColumnEffectSettings IconEffect,
        UiColumnSettings Time,
        RaceLeaderboardColumnEffectSettings TimeEffect,
        int RankPlayerGap,
        int PlayerIconGap,
        int IconTimeGap,
        string RankAlignment,
        string PlayerAlignment,
        string IconAlignment,
        string TimeAlignment)
    {
        public static RaceLeaderboardLayout From(AppSettings settings, RaceLeaderboardSettings leaderboard)
        {
            RaceLeaderboardTextEffectSettings raceEffects = leaderboard.TextEffects ?? new RaceLeaderboardTextEffectSettings();
            RaceLeaderboardSettings defaults = new();
            return new RaceLeaderboardLayout(
                leaderboard.Rank ?? defaults.Rank,
                raceEffects.Rank ?? new RaceLeaderboardColumnEffectSettings(),
                leaderboard.Player ?? defaults.Player,
                raceEffects.Player ?? new RaceLeaderboardColumnEffectSettings(),
                leaderboard.Icon ?? defaults.Icon,
                raceEffects.Icon ?? new RaceLeaderboardColumnEffectSettings(),
                leaderboard.Time ?? defaults.Time,
                raceEffects.Time ?? new RaceLeaderboardColumnEffectSettings(),
                Math.Clamp(leaderboard.RankPlayerGap, 0, 1000),
                Math.Clamp(leaderboard.PlayerIconGap, 0, 1000),
                Math.Clamp(leaderboard.IconTimeGap, 0, 1000),
                UiColumnAlignment.Normalize(leaderboard.RankAlignment, UiColumnAlignment.Right),
                UiColumnAlignment.Normalize(leaderboard.PlayerAlignment, UiColumnAlignment.Right),
                UiColumnAlignment.Normalize(leaderboard.IconAlignment, UiColumnAlignment.Right),
                UiColumnAlignment.Normalize(leaderboard.TimeAlignment, UiColumnAlignment.Right));
        }
    }
}
