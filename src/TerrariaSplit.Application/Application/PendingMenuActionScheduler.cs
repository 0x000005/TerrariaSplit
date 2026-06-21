namespace TerrariaSplit.Application;

internal enum MenuActionKind
{
    Reset,
    CreateWorld,
    PracticeWorld
}

internal sealed class PendingMenuActionScheduler
{
    private PendingMenuAction? pendingAction;

    public void Queue(MenuActionKind kind, DateTime requestedAtUtc, TimeSpan graceDuration)
    {
        pendingAction = new PendingMenuAction(kind, requestedAtUtc + graceDuration);
    }

    public bool TryConsume(Func<MenuActionKind, bool> canExecute, out MenuActionKind kind)
    {
        kind = default;
        if (pendingAction is not PendingMenuAction action)
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

    private readonly record struct PendingMenuAction(MenuActionKind Kind, DateTime DeadlineUtc);
}
