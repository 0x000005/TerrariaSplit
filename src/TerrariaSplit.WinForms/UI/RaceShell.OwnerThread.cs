namespace TerrariaSplit.UI;

internal sealed partial class RaceShell
{
    private bool DispatchOwnerThreadIfRequired(Action action)
    {
        if (owner.IsDisposed || !owner.InvokeRequired)
        {
            return false;
        }

        _ = PostOwnerThread(action);
        return true;
    }

    private bool PostOwnerThread(Action action)
    {
        try
        {
            if (owner.IsHandleCreated)
            {
                owner.BeginInvoke(action);
                return true;
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return false;
    }

    private Task InvokeOwnerThreadAsync(Action action)
    {
        if (owner.IsDisposed)
        {
            return Task.FromException(new ObjectDisposedException(owner.Name));
        }

        if (!owner.InvokeRequired)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!PostOwnerThread(() =>
            {
                try
                {
                    action();
                    completion.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            }))
        {
            completion.TrySetException(
                new InvalidOperationException("The Race UI owner thread is unavailable."));
        }

        return completion.Task;
    }
}
