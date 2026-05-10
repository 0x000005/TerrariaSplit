namespace TerrariaSplit;

internal sealed class TerrariaCreateWorldAutomation : IDisposable
{
    private readonly CreateWorldWorkflow workflow = new();

    public bool IsRunning => workflow.IsRunning;

    public bool IsAtMainMenu()
    {
        return workflow.IsAtMainMenu();
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        return workflow.RunAsync(cancellationToken);
    }

    public Task RunAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        return workflow.RunAsync(settings, cancellationToken);
    }

    public void Dispose()
    {
        workflow.Dispose();
    }
}
