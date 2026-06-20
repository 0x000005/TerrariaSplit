using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal class ThemedDropDownList : UserControl
{
    private const int ArrowWidth = 36;
    private readonly DropDownItemCollection items;
    private ToolStripDropDown? dropDown;
    private int selectedIndex = -1;
    private bool hovered;
    private bool pressed;
    private bool suppressNextOpenFromOwnerClick;
    private bool closingFromOwnerMouseDown;

    public ThemedDropDownList()
    {
        items = new DropDownItemCollection(this);
        BackColor = UiTheme.Field;
        ForeColor = UiTheme.Text;
        Font = UiTheme.FormFont(9f);
        Height = 38;
        Margin = new Padding(0, 7, 2, 7);
        MinimumSize = new Size(0, 38);
        TabStop = true;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.Selectable,
            true);
        UpdateDisplayedValue();
    }

    public event EventHandler? SelectedIndexChanged;

    public event EventHandler? SelectionCommitted;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DropDownItemCollection Items => items;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            int normalized = value >= 0 && value < items.Count ? value : -1;
            if (selectedIndex == normalized)
            {
                return;
            }

            selectedIndex = normalized;
            UpdateDisplayedValue();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem
    {
        get => selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null;
        set => SelectedIndex = items.IndexOf(value);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = false;
        pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            pressed = true;
            Focus();
            if (suppressNextOpenFromOwnerClick)
            {
                suppressNextOpenFromOwnerClick = false;
                Invalidate();
                return;
            }

            closingFromOwnerMouseDown = dropDown is { Visible: true };
            ToggleDropDown();
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        pressed = false;
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space || e.KeyCode == Keys.Down && e.Alt)
        {
            ToggleDropDown();
            e.Handled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        bool open = dropDown is { Visible: true };
        Color fill = Enabled
            ? pressed || open
                ? UiTheme.Selection
                : hovered
                    ? UiTheme.SurfaceRaised
                    : UiTheme.Field
            : UiTheme.Field;
        Color textColor = Enabled ? UiTheme.Text : UiTheme.MutedText;
        using (var fillBrush = new SolidBrush(fill))
        {
            e.Graphics.FillRectangle(fillBrush, ClientRectangle);
        }

        Rectangle arrowBounds = new(
            Math.Max(0, ClientSize.Width - ArrowWidth),
            0,
            Math.Min(ArrowWidth, ClientSize.Width),
            ClientSize.Height);
        Rectangle textBounds = new(
            10,
            0,
            Math.Max(0, ClientSize.Width - ArrowWidth - 14),
            ClientSize.Height);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            textBounds,
            textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(
            e.Graphics,
            "\u25BE",
            Font,
            arrowBounds,
            textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);

        using var borderPen = new Pen(UiTheme.Border);
        e.Graphics.DrawRectangle(borderPen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            dropDown?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected virtual string GetItemText(object? item)
    {
        return item?.ToString() ?? string.Empty;
    }

    private void ToggleDropDown()
    {
        if (!Enabled || items.Count == 0)
        {
            return;
        }

        if (dropDown is { Visible: true })
        {
            dropDown.Close();
            return;
        }

        ThemedDropDownPopupList list = CreateList();
        var host = new ToolStripControlHost(list)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = list.Size
        };

        dropDown?.Dispose();
        dropDown = new ToolStripDropDown
        {
            AutoClose = true,
            BackColor = UiTheme.Border,
            Padding = new Padding(1)
        };
        dropDown.Items.Add(host);
        dropDown.Closing += (_, _) =>
        {
            suppressNextOpenFromOwnerClick = !closingFromOwnerMouseDown && IsCursorOverSelf();
        };
        dropDown.Closed += (_, _) =>
        {
            closingFromOwnerMouseDown = false;
            pressed = false;
            Focus();
            Invalidate();
        };
        dropDown.Show(this, new Point(0, Height));
        Invalidate();
        list.Focus();
    }

    private bool IsCursorOverSelf()
    {
        return RectangleToScreen(ClientRectangle).Contains(Cursor.Position);
    }

    private ThemedDropDownPopupList CreateList()
    {
        var list = new ThemedDropDownPopupList(items, GetItemText)
        {
            Font = UiTheme.FormFont(9f),
            SelectedIndex = selectedIndex,
            Size = ThemedDropDownPopupList.GetPreferredListSize(Math.Max(Width, 220), items.Count)
        };
        if (selectedIndex >= 0)
        {
            list.TopIndex = Math.Max(0, selectedIndex - 4);
        }

        list.SelectionCommitted += (_, index) =>
        {
            SelectedIndex = index;
            dropDown?.Close();
            SelectionCommitted?.Invoke(this, EventArgs.Empty);
        };
        list.Cancelled += (_, _) => dropDown?.Close();
        return list;
    }

    private void UpdateDisplayedValue()
    {
        Text = GetItemText(SelectedItem);
        Invalidate();
    }

    private void HandleItemsCleared()
    {
        SelectedIndex = -1;
        dropDown?.Close();
    }

    public sealed class DropDownItemCollection : IEnumerable<object>
    {
        private readonly ThemedDropDownList owner;
        private readonly List<object> values = new();

        internal DropDownItemCollection(ThemedDropDownList owner)
        {
            this.owner = owner;
        }

        public int Count => values.Count;

        public object this[int index] => values[index];

        public int Add(object item)
        {
            values.Add(item);
            owner.UpdateDisplayedValue();
            return values.Count - 1;
        }

        public void Clear()
        {
            values.Clear();
            owner.HandleItemsCleared();
        }

        public bool Contains(object? item)
        {
            return IndexOf(item) >= 0;
        }

        public int IndexOf(object? item)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (Equals(values[i], item))
                {
                    return i;
                }
            }

            return -1;
        }

        public IEnumerator<object> GetEnumerator()
        {
            return values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class ThemedDropDownPopupList : Control
    {
        private const int ItemHeight = 28;
        private const int DropDownMaxHeight = 360;
        private const int ScrollBarWidth = 12;
        private const int WheelScrollLines = 3;

        private readonly DropDownItemCollection items;
        private readonly Func<object?, string> getItemText;
        private int selectedIndex = -1;
        private int topIndex;
        private bool draggingThumb;
        private int dragStartY;
        private int dragStartTopIndex;

        public ThemedDropDownPopupList(DropDownItemCollection items, Func<object?, string> getItemText)
        {
            this.items = items;
            this.getItemText = getItemText;
            BackColor = UiTheme.Field;
            ForeColor = UiTheme.Text;
            TabStop = true;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.Selectable,
                true);
        }

        public event EventHandler<int>? SelectionCommitted;

        public event EventHandler? Cancelled;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                selectedIndex = value >= 0 && value < items.Count ? value : -1;
                EnsureSelectedVisible();
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int TopIndex
        {
            get => topIndex;
            set
            {
                topIndex = Math.Clamp(value, 0, GetMaxTopIndex());
                Invalidate();
            }
        }

        public static Size GetPreferredListSize(int width, int itemCount)
        {
            int visibleItems = Math.Max(1, Math.Min(itemCount, DropDownMaxHeight / ItemHeight));
            return new Size(width, visibleItems * ItemHeight);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys keyCode = keyData & Keys.KeyCode;
            return keyCode is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown ||
                base.IsInputKey(keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(UiTheme.Field);
            PaintItems(e.Graphics);
            PaintScrollBar(e.Graphics);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Delta == 0)
            {
                return;
            }

            TopIndex -= Math.Sign(e.Delta) * WheelScrollLines;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (GetThumbBounds().Contains(e.Location))
            {
                draggingThumb = true;
                dragStartY = e.Y;
                dragStartTopIndex = topIndex;
                Capture = true;
                return;
            }

            if (GetTrackBounds().Contains(e.Location))
            {
                TopIndex = PointToTopIndex(e.Y);
                return;
            }

            int index = topIndex + e.Y / ItemHeight;
            if (index >= 0 && index < items.Count)
            {
                SelectedIndex = index;
                CommitSelection();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!draggingThumb)
            {
                return;
            }

            Rectangle track = GetTrackBounds();
            Rectangle thumb = GetThumbBounds();
            int travel = Math.Max(1, track.Height - thumb.Height);
            int delta = e.Y - dragStartY;
            TopIndex = dragStartTopIndex + (int)Math.Round(delta * (GetMaxTopIndex() / (float)travel));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            draggingThumb = false;
            Capture = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Up:
                    MoveSelection(-1);
                    e.Handled = true;
                    break;
                case Keys.Down:
                    MoveSelection(1);
                    e.Handled = true;
                    break;
                case Keys.Home:
                    SelectedIndex = items.Count > 0 ? 0 : -1;
                    e.Handled = true;
                    break;
                case Keys.End:
                    SelectedIndex = items.Count - 1;
                    e.Handled = true;
                    break;
                case Keys.PageUp:
                    MoveSelection(-GetVisibleItemCount());
                    e.Handled = true;
                    break;
                case Keys.PageDown:
                    MoveSelection(GetVisibleItemCount());
                    e.Handled = true;
                    break;
                case Keys.Enter:
                    CommitSelection();
                    e.Handled = true;
                    break;
                case Keys.Escape:
                    Cancelled?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    break;
            }
        }

        private void PaintItems(Graphics graphics)
        {
            int itemWidth = Math.Max(0, ClientSize.Width - (ShouldShowScrollBar() ? ScrollBarWidth : 0));
            int visibleCount = GetVisibleItemCount();
            for (int row = 0; row < visibleCount; row++)
            {
                int index = topIndex + row;
                if (index >= items.Count)
                {
                    break;
                }

                var bounds = new Rectangle(0, row * ItemHeight, itemWidth, ItemHeight);
                bool selected = index == selectedIndex;
                using (var backBrush = new SolidBrush(selected ? UiTheme.Selection : UiTheme.Field))
                {
                    graphics.FillRectangle(backBrush, bounds);
                }

                TextRenderer.DrawText(
                    graphics,
                    getItemText(items[index]),
                    Font,
                    Rectangle.Inflate(bounds, -8, 0),
                    UiTheme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }
        }

        private void PaintScrollBar(Graphics graphics)
        {
            Rectangle track = GetTrackBounds();
            if (track.IsEmpty)
            {
                return;
            }

            using (var trackBrush = new SolidBrush(UiTheme.Field))
            using (var borderPen = new Pen(UiTheme.Border))
            {
                graphics.FillRectangle(trackBrush, track);
                graphics.DrawLine(borderPen, track.Left, track.Top, track.Left, track.Bottom);
            }

            Rectangle thumb = GetThumbBounds();
            if (!thumb.IsEmpty)
            {
                using var thumbBrush = new SolidBrush(UiTheme.SurfaceRaised);
                graphics.FillRectangle(thumbBrush, thumb);
            }
        }

        private void MoveSelection(int delta)
        {
            if (items.Count == 0)
            {
                SelectedIndex = -1;
                return;
            }

            int origin = selectedIndex >= 0 ? selectedIndex : 0;
            SelectedIndex = Math.Clamp(origin + delta, 0, items.Count - 1);
        }

        private void EnsureSelectedVisible()
        {
            if (selectedIndex < 0)
            {
                return;
            }

            int visibleCount = GetVisibleItemCount();
            if (selectedIndex < topIndex)
            {
                topIndex = selectedIndex;
            }
            else if (selectedIndex >= topIndex + visibleCount)
            {
                topIndex = selectedIndex - visibleCount + 1;
            }

            topIndex = Math.Clamp(topIndex, 0, GetMaxTopIndex());
        }

        private void CommitSelection()
        {
            if (selectedIndex >= 0 && selectedIndex < items.Count)
            {
                SelectionCommitted?.Invoke(this, selectedIndex);
            }
        }

        private bool ShouldShowScrollBar()
        {
            return items.Count > GetVisibleItemCount();
        }

        private int GetVisibleItemCount()
        {
            return Math.Max(1, ClientSize.Height / ItemHeight);
        }

        private int GetMaxTopIndex()
        {
            return Math.Max(0, items.Count - GetVisibleItemCount());
        }

        private Rectangle GetTrackBounds()
        {
            if (!ShouldShowScrollBar())
            {
                return Rectangle.Empty;
            }

            return new Rectangle(
                ClientSize.Width - ScrollBarWidth,
                0,
                ScrollBarWidth,
                ClientSize.Height);
        }

        private Rectangle GetThumbBounds()
        {
            Rectangle track = GetTrackBounds();
            if (track.IsEmpty)
            {
                return Rectangle.Empty;
            }

            int visibleCount = GetVisibleItemCount();
            int thumbHeight = Math.Clamp(
                (int)Math.Round(track.Height * (visibleCount / (float)Math.Max(visibleCount, items.Count))),
                32,
                track.Height);
            int travel = Math.Max(1, track.Height - thumbHeight);
            int maxTopIndex = GetMaxTopIndex();
            int thumbY = track.Y + (maxTopIndex == 0 ? 0 : (int)Math.Round(travel * (topIndex / (float)maxTopIndex)));
            return new Rectangle(track.X, thumbY, track.Width, thumbHeight);
        }

        private int PointToTopIndex(int y)
        {
            Rectangle track = GetTrackBounds();
            Rectangle thumb = GetThumbBounds();
            int travel = Math.Max(1, track.Height - thumb.Height);
            int relativeY = Math.Clamp(y - thumb.Height / 2, 0, travel);
            return (int)Math.Round(relativeY * (GetMaxTopIndex() / (float)travel));
        }
    }
}
