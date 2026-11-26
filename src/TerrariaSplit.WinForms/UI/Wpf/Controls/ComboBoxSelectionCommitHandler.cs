using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace TerrariaSplit.UI.Wpf.Controls;

// WPF has no SelectionChangeCommitted event. Distinguish actual input from
// loading settings, and run modal actions after the selection event has finished.
internal sealed class ComboBoxSelectionCommitHandler
{
    private readonly WpfComboBox combo;
    private readonly Func<bool> canCommit;
    private readonly Action committed;
    private bool closedKeyboardSelection;
    private bool dropDownSelectionCommitted;
    private bool callbackQueued;
    private object? requestedSelection;

    public ComboBoxSelectionCommitHandler(WpfComboBox combo, Func<bool> canCommit, Action committed)
    {
        this.combo = combo;
        this.canCommit = canCommit;
        this.committed = committed;
        combo.DropDownOpened += (_, _) => dropDownSelectionCommitted = false;
        combo.DropDownClosed += (_, _) =>
        {
            if (dropDownSelectionCommitted)
            {
                QueueCommit();
            }

            dropDownSelectionCommitted = false;
        };
        combo.PreviewKeyDown += (_, e) =>
        {
            closedKeyboardSelection = !combo.IsDropDownOpen && e.Key is
                Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown;
            if (combo.IsDropDownOpen)
            {
                dropDownSelectionCommitted = e.Key is Key.Enter or Key.Space;
            }
        };
        combo.PreviewKeyUp += (_, _) => closedKeyboardSelection = false;
        combo.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler((_, e) =>
        {
            closedKeyboardSelection = false;
            dropDownSelectionCommitted = e.ChangedButton == MouseButton.Left && combo.IsDropDownOpen &&
                e.OriginalSource is DependencyObject source &&
                ItemsControl.ContainerFromElement(combo, source) is ComboBoxItem;
        }), handledEventsToo: true);
        combo.SelectionChanged += (_, _) =>
        {
            if (!canCommit())
            {
                requestedSelection = null;
            }
            else if (closedKeyboardSelection && !combo.IsDropDownOpen)
            {
                QueueCommit();
            }
        };
    }

    private void QueueCommit()
    {
        if (!canCommit())
        {
            return;
        }

        requestedSelection = combo.SelectedItem;
        closedKeyboardSelection = false;
        if (callbackQueued)
        {
            return;
        }

        callbackQueued = true;
        _ = combo.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            callbackQueued = false;
            if (requestedSelection is not null && ReferenceEquals(requestedSelection, combo.SelectedItem) && canCommit())
            {
                committed();
            }
        }));
    }
}
