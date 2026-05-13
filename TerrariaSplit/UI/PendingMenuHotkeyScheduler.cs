namespace TerrariaSplit;

internal enum MenuHotkeyActionKind
{
    Reset
}

internal sealed class PendingMenuHotkeyScheduler
{
    private PendingMenuHotkeyAction? pendingAction;

    public void Queue(MenuHotkeyActionKind kind, DateTime requestedAtUtc, TimeSpan graceDuration)
    {
        pendingAction = new PendingMenuHotkeyAction(kind, requestedAtUtc + graceDuration);
    }

    public bool TryConsume(Func<MenuHotkeyActionKind, bool> canExecute, out MenuHotkeyActionKind kind)
    {
        kind = default;
        if (pendingAction is not PendingMenuHotkeyAction action)
        {
            return false;
        }

        if (DateTime.UtcNow > action.DeadlineUtc)
        {
            pendingAction = null;
            return false;
        }

        if (!canExecute(action.Kind))
        {
            return false;
        }

        pendingAction = null;
        kind = action.Kind;
        return true;
    }

    public void Clear()
    {
        pendingAction = null;
    }

    private readonly record struct PendingMenuHotkeyAction(MenuHotkeyActionKind Kind, DateTime DeadlineUtc);
}
