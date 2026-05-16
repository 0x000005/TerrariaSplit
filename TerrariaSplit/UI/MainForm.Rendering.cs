using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class MainForm : Form
{
    private void DrawOverlay(Graphics graphics)
    {
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
            string timeText = showSplitTime
                ? TimeText.FormatSplit(status.Time!.Value)
                : FormatReferenceTime(status.Definition);
            TextRenderStyle timeStyle = showSplitTime
                ? GetSplitTextStyle(palette)
                : GetReferenceTextStyle(palette, isCurrent);

            DrawStyledText(
                graphics,
                timeText,
                GetColumnFont(settings.Columns.Time, sizeScale: wheelScale),
                timeStyle,
                timeRect,
                ContentAlignment.MiddleRight,
                opacity);
        }

        if (columns.Delta is Rectangle deltaRect)
        {
            bool enableDeltaGradient = status.Time is TimeSpan
                ? settings.EnableDeltaGradientColor
                : settings.EnableCurrentDeltaGradientColor;
            Color deltaColor = GetDeltaComparisonColor(comparison, palette, enableDeltaGradient);
            TextRenderStyle deltaStyle = GetDeltaTextStyle(comparison, palette);
            if (TryGetSegmentBestDeltaHighlight(rowIndex, out SegmentBestDeltaHighlight highlight))
            {
                double seconds = (DateTime.UtcNow - highlight.StartedAtUtc).TotalSeconds;
                deltaColor = SegmentBestDeltaHighlightStyles.Apply(deltaColor, highlight.Style, seconds);
            }

            DrawStyledText(
                graphics,
                FormatSplitDelta(comparison),
                GetColumnFont(settings.Columns.Delta, sizeScale: wheelScale),
                deltaStyle with { Fill = deltaColor },
                deltaRect,
                ContentAlignment.MiddleLeft,
                opacity);
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
        Rectangle bounds = ClientRectangle;
        int statusCount = splitTracker.Statuses.Count;
        int scalePercent = settings.Columns.ScalePercent;
        if (hasCachedLayout &&
            cachedLayoutBounds == bounds &&
            cachedLayoutStatusCount == statusCount &&
            cachedLayoutScalePercent == scalePercent)
        {
            layout = cachedLayout;
            return true;
        }

        if (!SplitLayoutCalculator.TryCreate(
                bounds,
                statusCount,
                RowGap,
                ScaleInt,
                out layout))
        {
            hasCachedLayout = false;
            return false;
        }

        cachedLayout = layout;
        cachedLayoutBounds = bounds;
        cachedLayoutStatusCount = statusCount;
        cachedLayoutScalePercent = scalePercent;
        hasCachedLayout = true;
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
        if (!settings.Columns.Timer.Show && !settings.Columns.TimerMilliseconds.Show)
        {
            return;
        }

        Rectangle timeRect = GetTimerTextBounds(rect);
        TimerTextLayout timerTextLayout = DrawTimerText(
            graphics,
            runTimer.Elapsed,
            GetTimerTextStyle(palette, milliseconds: false),
            GetTimerTextStyle(palette, milliseconds: true),
            timeRect);
        if (settings.ShowMouseClickThroughIndicator && !mouseClickThrough)
        {
            DrawMouseClickThroughIndicator(graphics, timeRect, timerTextLayout);
        }
    }


    private Rectangle GetTimerTextBounds(Rectangle rect)
    {
        int offsetX = ScaleInt(settings.Columns.TimerOffsetX);
        int offsetY = ScaleInt(settings.Columns.TimerOffsetY);
        return new Rectangle(
            rect.X + ScaleInt(4) + offsetX,
            rect.Y - ScaleInt(4) + offsetY,
            rect.Width - ScaleInt(8),
            rect.Height - ScaleInt(16));
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
        return TimeSpan.FromSeconds(Math.Clamp(settings.SplitCompletionAnimationDurationSeconds, 2f, 20f));
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


    private static float GetSplitCompletionDeltaSlideDistance(float deltaFontSize)
    {
        return Math.Clamp(
            deltaFontSize * SplitCompletionDeltaSlideDistanceRatio,
            SplitCompletionDeltaMinSlideDistance,
            SplitCompletionDeltaMaxSlideDistance);
    }


    private static SplitCompletionDeltaMotion GetSplitCompletionDeltaMotion(
        TimeSpan elapsed,
        TimeSpan duration,
        float slideDistance)
    {
        if (slideDistance <= 0f || duration <= TimeSpan.Zero)
        {
            return new SplitCompletionDeltaMotion(0f, 1f);
        }

        TimeSpan fadeDuration = GetSplitCompletionFadeDuration(duration);
        if (elapsed < TimeSpan.Zero || elapsed >= duration)
        {
            return new SplitCompletionDeltaMotion(slideDistance, 0f);
        }

        TimeSpan fadeOutStart = duration - fadeDuration;
        TimeSpan deltaFadeOutStart = fadeOutStart - TimeSpan.FromMilliseconds(
            fadeDuration.TotalMilliseconds * SplitCompletionDeltaOutroLeadRatio);
        TimeSpan deltaIntroStart = fadeDuration + SplitCompletionDeltaIntroGap;
        TimeSpan deltaIntroDuration = TimeSpan.FromMilliseconds(Math.Max(
            0.24 * 1000d,
            Math.Min(
                0.40 * 1000d,
                fadeDuration.TotalMilliseconds * SplitCompletionDeltaIntroDurationRatio)));
        TimeSpan deltaIntroEnd = deltaIntroStart + deltaIntroDuration;

        if (elapsed < deltaIntroStart)
        {
            return new SplitCompletionDeltaMotion(slideDistance, 0f);
        }

        if (elapsed < deltaIntroEnd)
        {
            float progress = (float)((elapsed - deltaIntroStart).TotalMilliseconds / deltaIntroDuration.TotalMilliseconds);
            float reveal = EaseInOut(progress);
            return new SplitCompletionDeltaMotion(slideDistance * (1f - reveal), reveal);
        }

        if (elapsed > deltaFadeOutStart)
        {
            float progress = (float)((elapsed - deltaFadeOutStart).TotalMilliseconds / fadeDuration.TotalMilliseconds);
            float hide = EaseInOut(progress);
            return new SplitCompletionDeltaMotion(slideDistance * hide, 1f - hide);
        }

        return new SplitCompletionDeltaMotion(0f, 1f);
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

        float centerX = GetSplitCompletionCenterX(graphics, layout.TimerRect, listBounds);
        DrawSplitCompletionIcon(graphics, listBounds, centerX, animation, elapsed, opacity);
        DrawSplitCompletionTimes(graphics, listBounds, centerX, animation, elapsed, opacity);
    }


    private void DrawSplitCompletionIcon(
        Graphics graphics,
        Rectangle listBounds,
        float centerX,
        SplitCompletionAnimation animation,
        TimeSpan elapsed,
        float opacity)
    {
        IReadOnlyList<string> iconFileNames = animation.Definition.IconFileNames;
        if (iconFileNames.Count == 0)
        {
            return;
        }

        Rectangle iconRect = GetSplitCompletionIconRect(listBounds, centerX);

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


    private Rectangle GetSplitCompletionIconRect(Rectangle listBounds, float centerX)
    {
        int maxIconSize = Math.Max(1, Math.Min((int)(listBounds.Width * 0.475f), (int)(listBounds.Height * 0.425f)));
        int minIconSize = Math.Min(ScaleInt(90), maxIconSize);
        int iconSize = Math.Clamp(ScaleInt(188), minIconSize, maxIconSize);
        int iconX = (int)Math.Round(centerX - iconSize / 2f, MidpointRounding.AwayFromZero);
        iconX = Math.Clamp(iconX, listBounds.Left, listBounds.Right - iconSize);
        return new Rectangle(
            iconX,
            listBounds.Top + Math.Max(0, (int)(listBounds.Height * 0.12f)),
            iconSize,
            iconSize);
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
        float centerX,
        SplitCompletionAnimation animation,
        TimeSpan elapsed,
        float opacity)
    {
        Rectangle iconRect = GetSplitCompletionIconRect(listBounds, centerX);
        int sidePadding = Math.Max(ScaleInt(8), (int)Math.Round(listBounds.Width * 0.03f));
        int top = iconRect.Bottom + Math.Max(ScaleInt(6), (int)Math.Round(listBounds.Height * 0.02f));
        int bottom = listBounds.Bottom - ScaleInt(2);
        float leftLimit = listBounds.Left + sidePadding;
        float rightLimit = listBounds.Right - sidePadding;
        float textCenterX = Math.Clamp(centerX, leftLimit, rightLimit);
        float halfWidth = Math.Max(0f, Math.Min(textCenterX - leftLimit, rightLimit - textCenterX));
        var textBounds = Rectangle.FromLTRB(
            (int)Math.Floor(textCenterX - halfWidth),
            Math.Min(top, bottom),
            (int)Math.Ceiling(textCenterX + halfWidth),
            bottom);
        if (textBounds.Width <= 0 || textBounds.Height <= 0)
        {
            return;
        }

        string segmentValue = SplitTimerFormatter.Format(animation.SegmentTime);
        string segmentDelta = GetSplitCompletionDeltaText(animation.PersonalBestSegmentComparison, animation.ShowSegmentComparison);
        string splitValue = SplitTimerFormatter.Format(animation.SplitTime);
        string splitDelta = GetSplitCompletionDeltaText(animation.ReferenceSplitComparison, animation.ShowSplitComparison);
        float valueSize = GetSplitCompletionValueFontSize(
            graphics,
            textBounds.Width,
            textBounds.Height,
            segmentValue,
            segmentDelta,
            splitValue,
            splitDelta,
            GetScaleFactor());
        float labelSize = valueSize * SplitCompletionLabelFontRatio;
        float deltaSize = valueSize * SplitCompletionDeltaFontRatio;
        TimeSpan animationDuration = GetSplitCompletionAnimationDuration();

        using var labelFont = CreatePixelFont(labelSize, FontStyle.Bold);
        using var valueFont = CreatePixelFont(valueSize, FontStyle.Bold);
        using var deltaFont = CreatePixelFont(deltaSize, FontStyle.Bold);

        int labelHeight = Math.Max(1, (int)Math.Ceiling(labelFont.GetHeight(graphics)));
        int valueHeight = Math.Max(1, (int)Math.Ceiling(valueFont.GetHeight(graphics)) + ScaleInt(2));
        int rowHeight = labelHeight + valueHeight + ScaleInt(2);
        float reservedGap = string.IsNullOrEmpty(segmentDelta) && string.IsNullOrEmpty(splitDelta)
            ? 0f
            : Math.Max(6f, valueFont.Size * 0.55f);
        int gap = Math.Max(3, (int)Math.Round(valueFont.Size * 0.32f));
        int totalHeight = rowHeight * 2 + gap;
        int startY = textBounds.Top + Math.Max(0, (textBounds.Height - totalHeight) / 2);

        var segmentRect = new Rectangle(textBounds.Left, startY, textBounds.Width, rowHeight);
        var splitRect = new Rectangle(textBounds.Left, startY + rowHeight + gap, textBounds.Width, rowHeight);

        DrawSplitCompletionTimeRow(
            graphics,
            segmentRect,
            Localizer.Get("Segment time", settings),
            segmentValue,
            animation.PersonalBestSegmentComparison,
            animation.ShowSegmentComparison,
            animation.SegmentTimeOutlineStyle,
            labelFont,
            valueFont,
            deltaFont,
            reservedGap,
            palette,
            animationDuration,
            elapsed,
            opacity,
            animation.SegmentBestDeltaHighlightStyle);
        DrawSplitCompletionTimeRow(
            graphics,
            splitRect,
            Localizer.Get("Split time", settings),
            splitValue,
            animation.ReferenceSplitComparison,
            animation.ShowSplitComparison,
            animation.SplitTimeOutlineStyle,
            labelFont,
            valueFont,
            deltaFont,
            reservedGap,
            palette,
            animationDuration,
            elapsed,
            opacity,
            SegmentBestDeltaHighlightStyles.None);
    }


    private float GetSplitCompletionCenterX(Graphics graphics, Rectangle timerRect, Rectangle listBounds)
    {
        if (!settings.Columns.Timer.Show && !settings.Columns.TimerMilliseconds.Show)
        {
            return listBounds.Left + listBounds.Width / 2f;
        }

        Rectangle timerTextBounds = GetTimerTextBounds(timerRect);
        float groupWidth = MeasureTimerTextGroupWidth(graphics, runTimer.Elapsed, timerTextBounds);
        float centerX = timerTextBounds.Left + groupWidth / 2f;
        return Math.Clamp(centerX, listBounds.Left, listBounds.Right);
    }


    private float MeasureTimerTextGroupWidth(Graphics graphics, TimeSpan elapsed, Rectangle bounds)
    {
        if (!settings.Columns.Timer.Show && !settings.Columns.TimerMilliseconds.Show)
        {
            return bounds.Width;
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
        return (settings.Columns.Timer.Show ? mainSize.Width : 0f) + gap +
            (settings.Columns.TimerMilliseconds.Show ? millisecondsSize.Width : 0f);
    }


    private static Font CreatePixelFont(float size, FontStyle style)
    {
        return new Font(UiTheme.FontFamilyName, Math.Max(1f, size), style, GraphicsUnit.Pixel);
    }


    private string GetSplitCompletionDeltaText(SplitComparison comparison, bool showComparison)
    {
        return showComparison && comparison.ShowDelta && comparison.Delta is TimeSpan delta
            ? TimeText.FormatDelta(delta, settings.EnableDynamicDeltaTimeUnits)
            : string.Empty;
    }


    private static float GetSplitCompletionValueFontSize(
        Graphics graphics,
        int availableWidth,
        int availableHeight,
        string firstValue,
        string firstDelta,
        string secondValue,
        string secondDelta,
        float scale)
    {
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return 24f;
        }

        float low = 8f;
        float high = Math.Clamp(56f * scale, 24f, 96f);
        for (int i = 0; i < 12; i++)
        {
            float mid = (low + high) / 2f;
            if (DoesSplitCompletionTextFit(
                graphics,
                availableWidth,
                availableHeight,
                mid,
                firstValue,
                firstDelta,
                secondValue,
                secondDelta))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }


    private static bool DoesSplitCompletionTextFit(
        Graphics graphics,
        int availableWidth,
        int availableHeight,
        float valueSize,
        string firstValue,
        string firstDelta,
        string secondValue,
        string secondDelta)
    {
        using var labelFont = CreatePixelFont(valueSize * SplitCompletionLabelFontRatio, FontStyle.Bold);
        using var valueFont = CreatePixelFont(valueSize, FontStyle.Bold);
        using var deltaFont = CreatePixelFont(valueSize * SplitCompletionDeltaFontRatio, FontStyle.Bold);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.NoWrap
        };

        float firstValueWidth = graphics.MeasureString(firstValue, valueFont, Size.Empty, format).Width;
        float secondValueWidth = graphics.MeasureString(secondValue, valueFont, Size.Empty, format).Width;
        float firstDeltaWidth = MeasureDeltaTextWidth(graphics, deltaFont, firstDelta, format);
        float secondDeltaWidth = MeasureDeltaTextWidth(graphics, deltaFont, secondDelta, format);
        float slidePadding = GetSplitCompletionDeltaSlideDistance(deltaFont.Size);
        float deltaGap = firstDeltaWidth > 0f || secondDeltaWidth > 0f
            ? Math.Max(6f, valueFont.Size * 0.55f)
            : 0f;
        float requiredHalfWidth = Math.Max(
            firstValueWidth / 2f + (firstDeltaWidth > 0f ? deltaGap + firstDeltaWidth + slidePadding : 0f),
            secondValueWidth / 2f + (secondDeltaWidth > 0f ? deltaGap + secondDeltaWidth + slidePadding : 0f));
        float labelHeight = labelFont.GetHeight(graphics);
        float valueHeight = valueFont.GetHeight(graphics) + 2f;
        float rowHeight = labelHeight + valueHeight + 2f;
        float totalHeight = rowHeight * 2f + Math.Max(3f, valueFont.Size * 0.32f);
        return requiredHalfWidth <= availableWidth / 2f && totalHeight <= availableHeight;
    }


    private static float MeasureDeltaTextWidth(
        Graphics graphics,
        Font deltaFont,
        string deltaText,
        StringFormat format)
    {
        return string.IsNullOrEmpty(deltaText)
            ? 0f
            : graphics.MeasureString(deltaText, deltaFont, Size.Empty, format).Width;
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
        float reservedGap,
        UiPalette palette,
        TimeSpan animationDuration,
        TimeSpan elapsed,
        float opacity,
        string deltaHighlightStyle)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        string deltaText = GetSplitCompletionDeltaText(comparison, showComparison);
        bool isAhead = SplitCompletionOutlineStyles.Normalize(outlineStyle) != SplitCompletionOutlineStyles.None &&
            comparison.Delta is TimeSpan aheadDelta &&
            aheadDelta < TimeSpan.Zero;

        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };

        int labelHeight = Math.Max(1, (int)Math.Ceiling(labelFont.GetHeight(graphics)));
        var labelRect = new Rectangle(bounds.Left, bounds.Top, bounds.Width, labelHeight);
        using var labelBrush = new SolidBrush(WithOpacity(palette.SplitCompletionLabelText, opacity * 0.86f));
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
        float gap = string.IsNullOrEmpty(deltaText) ? 0f : reservedGap;
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
                palette.SplitCompletionTimeText,
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
            DrawString(graphics, value, valueFont, palette.SplitCompletionTimeText, startX, valueY, format, opacity);
        }

        if (!string.IsNullOrEmpty(deltaText))
        {
            Color deltaColor = GetDeltaComparisonColor(
                comparison,
                palette,
                settings.EnableCurrentDeltaGradientColor);
            if (settings.ShowSegmentBestDeltaHighlight &&
                comparison.Delta is TimeSpan deltaValue &&
                deltaValue < TimeSpan.Zero)
            {
                deltaColor = SegmentBestDeltaHighlightStyles.Apply(deltaColor, deltaHighlightStyle, elapsed.TotalSeconds);
            }

            SplitCompletionDeltaMotion deltaMotion = GetSplitCompletionDeltaMotion(
                elapsed,
                animationDuration,
                GetSplitCompletionDeltaSlideDistance(deltaFont.Size));
            float deltaX = startX + valueSize.Width + gap + deltaMotion.OffsetX;
            float deltaY = AlignTextPathBottom(graphics, value, valueFont, startX, valueY, deltaText, deltaFont, deltaX, valueY, format);
            DrawString(
                graphics,
                deltaText,
                deltaFont,
                deltaColor,
                deltaX,
                deltaY,
                format,
                opacity * deltaMotion.Opacity);
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


    private void PlaySplitSound(int completedIndex)
    {
        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (completedIndex < 0 ||
            completedIndex >= statuses.Count ||
            statuses[completedIndex].Time is not TimeSpan splitTime)
        {
            return;
        }

        BossSplitDefinition definition = statuses[completedIndex].Definition;
        bool totalBehindReference = settings.TryGetReferenceSplit(definition, out TimeSpan referenceSplit) &&
            splitTime > referenceSplit;
        bool segmentBehindPersonalBest = TryGetCompletedSegmentTime(completedIndex, out TimeSpan segmentTime) &&
            TryGetPersonalBestSegment(definition, out TimeSpan personalBestSegment) &&
            segmentTime > personalBestSegment;

        string path = (totalBehindReference, segmentBehindPersonalBest) switch
        {
            (true, true) => settings.Sounds.SplitBehindReferenceBehindSegment,
            (true, false) => settings.Sounds.SplitBehindReferenceAheadSegment,
            (false, true) => settings.Sounds.SplitAheadReferenceBehindSegment,
            _ => settings.Sounds.SplitAheadReferenceAheadSegment
        };
        soundPlayer.Play(path);
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
        TimeSpan visibleDeltaDistance = TimeSpan.FromSeconds(settings.EarlyDeltaTimeSeconds);
        bool showRunningDelta = settings.ShowEarlyDeltaTime && runningDelta >= -visibleDeltaDistance;
        return new SplitComparison(runningDelta, showRunningDelta);
    }


    private string FormatSplitDelta(SplitComparison comparison)
    {
        return comparison.ShowDelta && comparison.Delta is TimeSpan delta
            ? TimeText.FormatDelta(delta, settings.EnableDynamicDeltaTimeUnits)
            : string.Empty;
    }


    private TextRenderStyle GetReferenceTextStyle(UiPalette palette, bool active)
    {
        return active
            ? CreateReferenceTextStyle(
                palette.ActiveReferenceText,
                palette.ActiveReferenceTextOutline,
                palette.ActiveReferenceTextShadow)
            : CreateReferenceTextStyle(
                palette.ReferenceText,
                palette.ReferenceTextOutline,
                palette.ReferenceTextShadow);
    }


    private TextRenderStyle GetSplitTextStyle(UiPalette palette)
    {
        return new TextRenderStyle(
            palette.SplitText,
            palette.SplitTextOutline,
            palette.SplitTextShadow,
            settings.TextEffects.TimeShadowPercent,
            settings.TextEffects.TimeOutlineThicknessPercent);
    }


    private TextRenderStyle GetDeltaTextStyle(SplitComparison comparison, UiPalette palette)
    {
        bool ahead = comparison.Delta is TimeSpan delta && delta < TimeSpan.Zero;
        return ahead
            ? CreateDeltaTextStyle(
                palette.DeltaAheadText,
                palette.DeltaAheadTextOutline,
                palette.DeltaAheadTextShadow)
            : CreateDeltaTextStyle(
                palette.DeltaBehindText,
                palette.DeltaBehindTextOutline,
                palette.DeltaBehindTextShadow);
    }


    private TextRenderStyle CreateReferenceTextStyle(Color fill, Color outline, Color shadow)
    {
        return new TextRenderStyle(
            fill,
            outline,
            shadow,
            settings.TextEffects.TimeShadowPercent,
            settings.TextEffects.TimeOutlineThicknessPercent);
    }


    private TextRenderStyle CreateDeltaTextStyle(Color fill, Color outline, Color shadow)
    {
        return new TextRenderStyle(
            fill,
            outline,
            shadow,
            settings.TextEffects.DeltaShadowPercent,
            settings.TextEffects.DeltaOutlineThicknessPercent);
    }


    private TextRenderStyle CreateTimerTextStyle(Color fill, Color outline, Color shadow, bool milliseconds)
    {
        return new TextRenderStyle(
            fill,
            outline,
            shadow,
            milliseconds
                ? settings.TextEffects.TimerMillisecondsShadowPercent
                : settings.TextEffects.TimerShadowPercent,
            milliseconds
                ? settings.TextEffects.TimerMillisecondsOutlineThicknessPercent
                : settings.TextEffects.TimerOutlineThicknessPercent);
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


    private static void DrawStyledText(
        Graphics graphics,
        string text,
        Font font,
        TextRenderStyle style,
        Rectangle bounds,
        ContentAlignment alignment,
        float opacity)
    {
        if (string.IsNullOrEmpty(text) || opacity <= 0.01f)
        {
            return;
        }

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

        if (HasTextEffects(style))
        {
            using GraphicsPath path = CreateTextPath(
                graphics,
                text,
                font,
                new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                format);
            DrawSupersampledTextLayer(
                graphics,
                path,
                font,
                style,
                opacity,
                targetGraphics =>
                {
                    using var fillBrush = new SolidBrush(WithOpacity(style.Fill, opacity));
                    targetGraphics.DrawString(text, font, fillBrush, bounds, format);
                });
            return;
        }

        using var textBrush = new SolidBrush(WithOpacity(style.Fill, opacity));
        graphics.DrawString(text, font, textBrush, bounds, format);
    }


    private static void DrawStyledString(
        Graphics graphics,
        string text,
        Font font,
        TextRenderStyle style,
        float x,
        float y,
        StringFormat format,
        float opacity)
    {
        if (string.IsNullOrEmpty(text) || opacity <= 0.01f)
        {
            return;
        }

        if (HasTextEffects(style))
        {
            using GraphicsPath path = CreateTextPath(graphics, text, font, x, y, format);
            DrawSupersampledTextLayer(
                graphics,
                path,
                font,
                style,
                opacity,
                targetGraphics =>
                {
                    using var fillBrush = new SolidBrush(WithOpacity(style.Fill, opacity));
                    targetGraphics.DrawString(text, font, fillBrush, x, y, format);
                });
            return;
        }

        using var textBrush = new SolidBrush(WithOpacity(style.Fill, opacity));
        graphics.DrawString(text, font, textBrush, x, y, format);
    }


    private static bool HasTextEffects(TextRenderStyle style)
    {
        return style.ShadowPercent > 0 || style.OutlineThicknessPercent > 0;
    }


    private static void DrawSupersampledTextLayer(
        Graphics graphics,
        GraphicsPath path,
        Font font,
        TextRenderStyle style,
        float opacity,
        Action<Graphics> drawFill)
    {
        if (path.PointCount == 0)
        {
            return;
        }

        RectangleF layerBounds = GetTextEffectLayerBounds(graphics, path, font, style);
        if (layerBounds.Width <= 0f || layerBounds.Height <= 0f)
        {
            return;
        }

        int scale = TextEffectSupersampleScale;
        int layerWidth = (int)Math.Ceiling(layerBounds.Width * scale);
        int layerHeight = (int)Math.Ceiling(layerBounds.Height * scale);
        if (layerWidth <= 0 || layerHeight <= 0 || layerWidth > 4096 || layerHeight > 4096)
        {
            DrawTextEffects(graphics, path, font, style, opacity);
            drawFill(graphics);
            return;
        }

        using var layer = new Bitmap(layerWidth, layerHeight, PixelFormat.Format32bppPArgb);
        using (Graphics layerGraphics = Graphics.FromImage(layer))
        {
            layerGraphics.Clear(Color.Transparent);
            layerGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            layerGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            layerGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            layerGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            layerGraphics.CompositingMode = CompositingMode.SourceOver;
            layerGraphics.CompositingQuality = CompositingQuality.HighQuality;
            using var transform = new Matrix(
                scale,
                0f,
                0f,
                scale,
                -layerBounds.X * scale,
                -layerBounds.Y * scale);
            layerGraphics.Transform = transform;

            DrawTextEffects(layerGraphics, path, font, style, opacity);
            drawFill(layerGraphics);
        }

        GraphicsState state = graphics.Save();
        try
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.DrawImage(
                layer,
                new RectangleF(
                    layerBounds.X,
                    layerBounds.Y,
                    layerWidth / (float)scale,
                    layerHeight / (float)scale));
        }
        finally
        {
            graphics.Restore(state);
        }
    }


    private static RectangleF GetTextEffectLayerBounds(
        Graphics graphics,
        GraphicsPath path,
        Font font,
        TextRenderStyle style)
    {
        RectangleF bounds = path.GetBounds();
        float shadowOffset = GetTextShadowOpacity(style.ShadowPercent) > 0f
            ? GetTextShadowOffset(graphics, font)
            : 0f;
        if (shadowOffset > 0f)
        {
            bounds = RectangleF.Union(
                bounds,
                new RectangleF(
                    bounds.X + shadowOffset,
                    bounds.Y + shadowOffset,
                    bounds.Width,
                    bounds.Height));
        }

        float outlineRadius = style.OutlineThicknessPercent > 0
            ? GetTextOutlineRadius(graphics, font, style.OutlineThicknessPercent)
            : 0f;
        float padding = MathF.Ceiling(Math.Max(outlineRadius, shadowOffset) + 3f);
        return RectangleF.FromLTRB(
            MathF.Floor(bounds.Left - padding),
            MathF.Floor(bounds.Top - padding),
            MathF.Ceiling(bounds.Right + padding),
            MathF.Ceiling(bounds.Bottom + padding));
    }


    private static void DrawTextEffects(
        Graphics graphics,
        GraphicsPath path,
        Font font,
        TextRenderStyle style,
        float opacity)
    {
        if (path.PointCount == 0)
        {
            return;
        }

        float shadowOpacity = GetTextShadowOpacity(style.ShadowPercent);
        if (shadowOpacity > 0f)
        {
            using GraphicsPath shadowPath = (GraphicsPath)path.Clone();
            using var matrix = new Matrix();
            float offset = GetTextShadowOffset(graphics, font);
            matrix.Translate(offset, offset);
            shadowPath.Transform(matrix);

            using var shadowBrush = new SolidBrush(WithOpacity(style.Shadow, opacity * shadowOpacity));
            graphics.FillPath(shadowBrush, shadowPath);
        }

        if (style.OutlineThicknessPercent > 0)
        {
            using GraphicsPath outlinePath = CreateWidenedOutlinePath(
                path,
                GetTextOutlineRadius(graphics, font, style.OutlineThicknessPercent));
            using var outlineBrush = new SolidBrush(WithOpacity(style.Outline, opacity));
            graphics.FillPath(outlineBrush, outlinePath);
        }
    }


    private static GraphicsPath CreateWidenedOutlinePath(GraphicsPath path, float radius)
    {
        GraphicsPath outlinePath = (GraphicsPath)path.Clone();
        using var outlinePen = new Pen(Color.Black, Math.Max(0.2f, radius * 2f))
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        outlinePath.Widen(outlinePen);

        return outlinePath;
    }


    private static float GetTextShadowOpacity(int shadowPercent)
    {
        float amount = Math.Clamp(shadowPercent, 0, 100) / 100f;
        if (amount <= 0f)
        {
            return 0f;
        }

        return Math.Clamp(0.08f + 0.58f * MathF.Pow(amount, 0.85f), 0f, 0.66f);
    }


    private static float GetTextShadowOffset(Graphics graphics, Font font)
    {
        return Math.Clamp(GetFontPixelsPerEm(graphics, font) * 0.08f, 1f, 4f);
    }


    private static float GetTextOutlineRadius(Graphics graphics, Font font, int thicknessPercent)
    {
        float amount = Math.Clamp(thicknessPercent, 0, 100) / 100f;
        float radius = GetFontPixelsPerEm(graphics, font) * 0.075f * amount + 0.15f;
        return Math.Clamp(radius, 0.2f, 3.5f);
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
            emSize: GetFontPixelsPerEm(graphics, font),
            origin: new PointF(x, y),
            format: pathFormat);
        return path;
    }


    private static GraphicsPath CreateTextPath(Graphics graphics, string text, Font font, RectangleF bounds, StringFormat format)
    {
        var path = new GraphicsPath();
        using StringFormat pathFormat = (StringFormat)format.Clone();
        path.AddString(
            text,
            font.FontFamily,
            (int)font.Style,
            emSize: GetFontPixelsPerEm(graphics, font),
            layoutRect: bounds,
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


    private TimerTextLayout DrawTimerText(
        Graphics graphics,
        TimeSpan elapsed,
        TextRenderStyle mainStyle,
        TextRenderStyle millisecondsStyle,
        Rectangle bounds)
    {
        if (!settings.Columns.Timer.Show && !settings.Columns.TimerMilliseconds.Show)
        {
            return TimerTextLayout.Empty;
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
            DrawStyledString(graphics, mainText, mainFont, mainStyle, mainX, mainY, format, 1f);
        }

        if (settings.Columns.TimerMilliseconds.Show)
        {
            DrawStyledString(graphics, millisecondsText, millisecondsFont, millisecondsStyle, millisecondsX, millisecondsY, format, 1f);
        }

        float groupWidth = (settings.Columns.Timer.Show ? mainSize.Width : 0f) + gap +
            (settings.Columns.TimerMilliseconds.Show ? millisecondsSize.Width : 0f);
        RectangleF mainVisualBounds = settings.Columns.Timer.Show
            ? GetTextVisualBounds(graphics, mainText, mainFont, mainX, mainY, format)
            : RectangleF.Empty;
        float mainHeight = mainMetrics.Ascent + mainMetrics.Descent;
        float anchorTop = mainVisualBounds.Height > 0f ? mainVisualBounds.Top : settings.Columns.Timer.Show ? mainY : groupY;
        float anchorHeight = mainVisualBounds.Height > 0f ? mainVisualBounds.Height : settings.Columns.Timer.Show ? mainHeight : groupHeight;
        return new TimerTextLayout(mainX + groupWidth, anchorTop, anchorHeight);
    }


    private static void DrawMouseClickThroughIndicator(Graphics graphics, Rectangle timerBounds, TimerTextLayout timerTextLayout)
    {
        if (timerTextLayout.Right <= 0f || timerTextLayout.Height <= 0f)
        {
            return;
        }

        float diameter = Math.Clamp(timerTextLayout.Height * 0.22f, 9f, 13f);
        float gap = Math.Max(6f, diameter * 0.7f);
        float x = Math.Min(timerBounds.Right - diameter, timerTextLayout.Right + gap);
        float y = timerTextLayout.Top;
        var dotBounds = new RectangleF(x, y, diameter, diameter);

        SmoothingMode previousSmoothingMode = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            using var dotBrush = new SolidBrush(Color.FromArgb(255, 179, 92, 255));
            graphics.FillEllipse(dotBrush, dotBounds);
        }
        finally
        {
            graphics.SmoothingMode = previousSmoothingMode;
        }
    }


    private static FontMetrics GetFontMetrics(Graphics graphics, Font font)
    {
        FontFamily family = font.FontFamily;
        FontStyle style = font.Style;
        float emHeight = family.GetEmHeight(style);
        float pixelsPerEm = GetFontPixelsPerEm(graphics, font);
        float ascent = family.GetCellAscent(style) * pixelsPerEm / emHeight;
        float descent = family.GetCellDescent(style) * pixelsPerEm / emHeight;
        return new FontMetrics(ascent, descent);
    }


    private static RectangleF GetTextVisualBounds(
        Graphics graphics,
        string text,
        Font font,
        float x,
        float y,
        StringFormat format)
    {
        using GraphicsPath path = CreateTextPath(graphics, text, font, x, y, format);
        return path.PointCount > 0 ? path.GetBounds() : RectangleF.Empty;
    }


    private static float GetFontPixelsPerEm(Graphics graphics, Font font)
    {
        return font.Unit == GraphicsUnit.Pixel
            ? font.Size
            : font.SizeInPoints * graphics.DpiY / 72f;
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


    private TextRenderStyle GetTimerTextStyle(UiPalette palette, bool milliseconds)
    {
        if (runTimer.Phase == SplitTimerPhase.NotStarted)
        {
            return CreateTimerTextStyle(palette.TimerText, palette.TimerTextOutline, palette.TimerTextShadow, milliseconds);
        }

        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (TryGetCompletedMoonLordStatus(statuses, out BossSplitStatus moonLordStatus, out TimeSpan moonLordTime) &&
            settings.TryGetReferenceSplit(moonLordStatus.Definition, out TimeSpan moonLordReference))
        {
            return moonLordTime < moonLordReference
                ? CreateTimerTextStyle(
                    palette.TimerRecordText,
                    palette.TimerRecordTextOutline,
                    palette.TimerRecordTextShadow,
                    milliseconds)
                : CreateTimerTextStyle(
                    palette.TimerNoRecordText,
                    palette.TimerNoRecordTextOutline,
                    palette.TimerNoRecordTextShadow,
                    milliseconds);
        }

        if (statuses.Count > 0 && statuses[^1].Time is TimeSpan finalTime)
        {
            if (settings.TryGetReferenceSplit(statuses[^1].Definition, out TimeSpan finalReference) &&
                finalTime < finalReference)
            {
                return CreateTimerTextStyle(
                    palette.TimerRecordText,
                    palette.TimerRecordTextOutline,
                    palette.TimerRecordTextShadow,
                    milliseconds);
            }

            if (settings.TryGetReferenceSplit(statuses[^1].Definition, out finalReference) &&
                settings.EnableTimerGradientColor)
            {
                return GetTimerGradientTextStyle(finalTime - finalReference, palette, milliseconds);
            }

            return runTimer.Phase == SplitTimerPhase.Paused
                ? CreateTimerTextStyle(
                    palette.TimerPausedText,
                    palette.TimerPausedTextOutline,
                    palette.TimerPausedTextShadow,
                    milliseconds)
                : CreateTimerTextStyle(
                    palette.TimerBehindText,
                    palette.TimerBehindTextOutline,
                    palette.TimerBehindTextShadow,
                    milliseconds);
        }

        if (runTimer.Phase == SplitTimerPhase.Paused)
        {
            return CreateTimerTextStyle(
                palette.TimerPausedText,
                palette.TimerPausedTextOutline,
                palette.TimerPausedTextShadow,
                milliseconds);
        }

        if (splitTracker.CurrentIndex < statuses.Count &&
            settings.TryGetReferenceSplit(statuses[splitTracker.CurrentIndex].Definition, out TimeSpan currentReference))
        {
            if (settings.EnableTimerGradientColor)
            {
                return GetTimerGradientTextStyle(runTimer.Elapsed - currentReference, palette, milliseconds);
            }

            return runTimer.Elapsed <= currentReference
                ? CreateTimerTextStyle(
                    palette.TimerAheadText,
                    palette.TimerAheadTextOutline,
                    palette.TimerAheadTextShadow,
                    milliseconds)
                : CreateTimerTextStyle(
                    palette.TimerBehindText,
                    palette.TimerBehindTextOutline,
                    palette.TimerBehindTextShadow,
                    milliseconds);
        }

        return CreateTimerTextStyle(palette.TimerText, palette.TimerTextOutline, palette.TimerTextShadow, milliseconds);
    }


    private TextRenderStyle GetTimerGradientTextStyle(TimeSpan delta, UiPalette palette, bool milliseconds)
    {
        TextRenderStyle style = delta < TimeSpan.Zero
            ? CreateTimerTextStyle(
                palette.TimerAheadText,
                palette.TimerAheadTextOutline,
                palette.TimerAheadTextShadow,
                milliseconds)
            : delta > TimeSpan.Zero
                ? CreateTimerTextStyle(
                    palette.TimerBehindText,
                    palette.TimerBehindTextOutline,
                    palette.TimerBehindTextShadow,
                    milliseconds)
                : CreateTimerTextStyle(
                    palette.TimerText,
                    palette.TimerTextOutline,
                    palette.TimerTextShadow,
                    milliseconds);
        return style with
        {
            Fill = GetGradientDeltaColor(
                delta,
                palette.TimerAheadText,
                palette.TimerText,
                palette.TimerBehindText)
        };
    }


    private static bool TryGetCompletedMoonLordStatus(
        IReadOnlyList<BossSplitStatus> statuses,
        out BossSplitStatus moonLordStatus,
        out TimeSpan moonLordTime)
    {
        BossSplitStatus? match = statuses.FirstOrDefault(status =>
            !status.IsSkipped &&
            status.Time is not null &&
            status.Definition.BossIds.Any(bossId => string.Equals(
                bossId,
                BossSplitDefinitions.MoonLord,
                StringComparison.OrdinalIgnoreCase)));
        if (match?.Time is TimeSpan time)
        {
            moonLordStatus = match;
            moonLordTime = time;
            return true;
        }

        moonLordStatus = null!;
        moonLordTime = TimeSpan.Zero;
        return false;
    }


    private Color GetDeltaComparisonColor(
        SplitComparison comparison,
        UiPalette palette,
        bool enableGradient)
    {
        TimeSpan? delta = comparison.Delta;
        if (delta is null)
        {
            return palette.DeltaBehindText;
        }

        if (enableGradient)
        {
            return GetGradientDeltaColor(
                delta.Value,
                palette.DeltaAheadText,
                palette.TimerText,
                palette.DeltaBehindText);
        }

        if (TimeText.IsDeltaDisplayedAsZero(delta.Value, settings.EnableDynamicDeltaTimeUnits))
        {
            return palette.DeltaBehindText;
        }

        if (delta < TimeSpan.Zero)
        {
            return palette.DeltaAheadText;
        }

        if (delta > TimeSpan.Zero)
        {
            return palette.DeltaBehindText;
        }

        return palette.DeltaBehindText;
    }


    private Color GetGradientDeltaColor(TimeSpan delta, Color aheadColor, Color baseColor, Color behindColor)
    {
        if (delta == TimeSpan.Zero)
        {
            return baseColor;
        }

        float thresholdSeconds = Math.Max(1, settings.DeltaGradientThresholdSeconds);
        float magnitude = Math.Min(1f, (float)(Math.Abs(delta.TotalSeconds) / thresholdSeconds));
        float amount = DeltaGradientCurves.Evaluate(settings.DeltaGradientCurve, magnitude);
        return delta < TimeSpan.Zero
            ? BlendColor(baseColor, aheadColor, amount)
            : BlendColor(baseColor, behindColor, amount);
    }


    private static Color BlendColor(Color from, Color to, float amount)
    {
        float t = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            Lerp(from.R, to.R, t),
            Lerp(from.G, to.G, t),
            Lerp(from.B, to.B, t));
    }
}
