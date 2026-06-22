using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class SplitSettingsPage : SettingsPageBase
{
    private void DrawRouteListItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox listBox || e.Index < 0 || e.Index >= listBox.Items.Count)
        {
            return;
        }

        if (listBox.Items[e.Index] is not RouteListItem item)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        PaintListItemBackground(e.Graphics, e.Bounds, selected);
        Color color = item.Entry.Enabled
            ? UiTheme.Text
            : UiTheme.MutedText;
        Rectangle contentBounds = GetListItemContentBounds(listBox, e.Bounds);
        Rectangle textBounds = new(contentBounds.Left + 8, contentBounds.Top, contentBounds.Width - 16, contentBounds.Height);
        Font itemFont = e.Font ?? listBox.Font;
        if (item.Entry.IsAttached)
        {
            DrawRouteListItemWithAttachedMarker(e.Graphics, item, itemFont, textBounds, color);
            return;
        }

        TextRenderer.DrawText(
            e.Graphics,
            item.ToString(),
            itemFont,
            textBounds,
            color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void DrawRouteListItemWithAttachedMarker(
        Graphics graphics,
        RouteListItem item,
        Font itemFont,
        Rectangle textBounds,
        Color color)
    {
        string name = item.ToString();
        string marker = Context.Localize("Attached group");
        using Font markerFont = new(itemFont.FontFamily, Math.Max(6f, itemFont.Size - 1f), itemFont.Style);
        const TextFormatFlags markerFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
        Size markerSize = TextRenderer.MeasureText(
            graphics,
            marker,
            markerFont,
            Size.Empty,
            markerFlags);
        int gap = 6;
        int markerWidth = Math.Min(markerSize.Width + 4, Math.Max(0, textBounds.Width));
        int nameMaxWidth = Math.Max(0, textBounds.Width - markerWidth - gap);
        Size nameSize = TextRenderer.MeasureText(
            graphics,
            name,
            itemFont,
            Size.Empty,
            TextFormatFlags.NoPadding);
        int visibleNameWidth = Math.Min(nameSize.Width, nameMaxWidth);
        Rectangle nameBounds = new(
            textBounds.Left,
            textBounds.Top,
            nameMaxWidth,
            textBounds.Height);
        Rectangle markerBounds = new(
            textBounds.Left + visibleNameWidth + gap,
            textBounds.Top,
            markerWidth,
            textBounds.Height);

        TextRenderer.DrawText(
            graphics,
            name,
            itemFont,
            nameBounds,
            color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(
            graphics,
            marker,
            markerFont,
            markerBounds,
            UiTheme.MutedText,
            markerFlags);
    }

    private static void DrawPlainListItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox listBox || e.Index < 0 || e.Index >= listBox.Items.Count)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        PaintListItemBackground(e.Graphics, e.Bounds, selected);
        Rectangle contentBounds = GetListItemContentBounds(listBox, e.Bounds);
        Rectangle textBounds = new(contentBounds.Left + 8, contentBounds.Top, contentBounds.Width - 16, contentBounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            listBox.GetItemText(listBox.Items[e.Index]),
            e.Font ?? listBox.Font,
            textBounds,
            listBox.Enabled ? UiTheme.Text : UiTheme.MutedText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static void PaintListItemBackground(Graphics graphics, Rectangle bounds, bool selected)
    {
        using var brush = new SolidBrush(selected ? UiTheme.Selection : UiTheme.Field);
        graphics.FillRectangle(brush, bounds);
    }

    private static Rectangle GetListItemContentBounds(ListBox listBox, Rectangle bounds)
    {
        return listBox is ThemedListBox themedListBox
            ? themedListBox.GetItemContentBounds(bounds)
            : bounds;
    }

    private void RouteListMouseDown(object? sender, MouseEventArgs e)
    {
        routeController.CancelDrag();
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        int index = routeList.IndexFromPoint(e.Location);
        if (index == ListBox.NoMatches)
        {
            return;
        }

        routeController.BeginDrag(index, e.Location);
    }

    private void RouteListMouseMove(object? sender, MouseEventArgs e)
    {
        if (!routeController.TryConsumeDrag(e.Button, e.Location, out int index))
        {
            return;
        }

        if (!SaveSelectedEntryFromControls())
        {
            return;
        }

        routeList.DoDragDrop(new RouteDragItem(index), DragDropEffects.Move);
    }

    private void RouteListDragOver(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(typeof(RouteDragItem)) == true
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private void RouteListDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(typeof(RouteDragItem)) is not RouteDragItem drag)
        {
            return;
        }

        Point point = routeList.PointToClient(new Point(e.X, e.Y));
        int insertionIndex = GetInsertionIndex(routeList, point);
        MoveRouteEntry(drag.Index, insertionIndex);
    }

    private void MoveRouteEntry(int sourceIndex, int insertionIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= routeEntries.Count)
        {
            return;
        }

        if (insertionIndex == sourceIndex || insertionIndex == sourceIndex + 1)
        {
            return;
        }

        SplitRouteEntry entry = routeEntries[sourceIndex];
        routeEntries.RemoveAt(sourceIndex);
        if (insertionIndex > sourceIndex)
        {
            insertionIndex--;
        }

        insertionIndex = Math.Clamp(insertionIndex, 0, routeEntries.Count);
        routeEntries.Insert(insertionIndex, entry);
        loadedRouteEntryIndex = -1;
        routeDirty = true;
        routeDraft.NormalizeAttachedRouteFlags();
        RefreshRouteList();
        routeList.SelectedIndex = insertionIndex;
    }

    private void ConditionListMouseDown(object? sender, MouseEventArgs e)
    {
        if (conditionController.AdvancedMode)
        {
            return;
        }

        conditionController.CancelDrag();
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        int index = conditionList.IndexFromPoint(e.Location);
        if (index == ListBox.NoMatches)
        {
            return;
        }

        conditionController.BeginDrag(index, e.Location);
    }

    private void ConditionListMouseMove(object? sender, MouseEventArgs e)
    {
        if (conditionController.AdvancedMode)
        {
            return;
        }

        if (!conditionController.TryConsumeDrag(e.Button, e.Location, out int index))
        {
            return;
        }

        conditionList.SelectedIndex = index;
        conditionList.DoDragDrop(new ConditionDragItem(index), DragDropEffects.Move);
    }

    private void ConditionListDragOver(object? sender, DragEventArgs e)
    {
        if (conditionController.AdvancedMode)
        {
            e.Effect = DragDropEffects.None;
            return;
        }

        e.Effect = e.Data?.GetDataPresent(typeof(ConditionDragItem)) == true
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private void ConditionListDragDrop(object? sender, DragEventArgs e)
    {
        if (conditionController.AdvancedMode)
        {
            return;
        }

        if (e.Data?.GetData(typeof(ConditionDragItem)) is not ConditionDragItem drag)
        {
            return;
        }

        Point point = conditionList.PointToClient(new Point(e.X, e.Y));
        int insertionIndex = GetInsertionIndex(conditionList, point);
        MoveConditionFact(drag.Index, insertionIndex);
    }

    private void MoveConditionFact(int sourceIndex, int insertionIndex)
    {
        if (conditionController.AdvancedMode)
        {
            return;
        }

        if (!TryGetSelectedRouteEntry(out SplitRouteEntry entry) ||
            sourceIndex < 0 ||
            sourceIndex >= conditionList.Items.Count)
        {
            return;
        }

        if (insertionIndex == sourceIndex || insertionIndex == sourceIndex + 1)
        {
            return;
        }

        List<ConditionListItem> items = conditionList.Items
            .Cast<ConditionListItem>()
            .ToList();
        ConditionListItem item = items[sourceIndex];
        items.RemoveAt(sourceIndex);
        if (insertionIndex > sourceIndex)
        {
            insertionIndex--;
        }

        insertionIndex = Math.Clamp(insertionIndex, 0, items.Count);
        items.Insert(insertionIndex, item);
        conditionList.BeginUpdate();
        try
        {
            conditionList.Items.Clear();
            foreach (ConditionListItem conditionItem in items)
            {
                conditionList.Items.Add(conditionItem);
            }
        }
        finally
        {
            conditionList.EndUpdate();
        }

        conditionList.SelectedIndex = insertionIndex;
        UseBasicConditionFromList();
        entry.Condition = GetCurrentCondition();
        entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
        SplitIconOverride previousOverride = GetCurrentIconOverride();
        RefreshIconOverrideOptions(previousOverride);
        entry.IconOverride = GetCurrentIconOverride();
        routeDirty = true;
    }

    private static int GetInsertionIndex(ListBox listBox, Point point)
    {
        int index = listBox.IndexFromPoint(point);
        if (index == ListBox.NoMatches)
        {
            return listBox.Items.Count;
        }

        Rectangle bounds = listBox.GetItemRectangle(index);
        return point.Y > bounds.Top + (bounds.Height / 2)
            ? index + 1
            : index;
    }
}
