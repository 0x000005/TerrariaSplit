namespace TerrariaSplit.Terraria.Automation;

internal sealed class TerrariaWorldAutomation : IDisposable
{
    private readonly CreateWorldWorkflow createWorldWorkflow;
    private readonly EnterWorldWorkflow enterWorldWorkflow = new();
    private readonly AutomationRunner<AppSettings> createWorldRunner;
    private readonly AutomationRunner<EnterWorldAutomationRequest> enterWorldRunner;

    public TerrariaWorldAutomation(WorldPoolStore? worldPool = null, IAppLogger? logger = null)
    {
        logger ??= NullAppLogger.Instance;
        createWorldWorkflow = new CreateWorldWorkflow(worldPool);
        createWorldRunner = new AutomationRunner<AppSettings>(
            "Create world",
            createWorldWorkflow.RunAsync,
            createWorldWorkflow.Dispose,
            logger);
        enterWorldRunner = new AutomationRunner<EnterWorldAutomationRequest>(
            "Enter world",
            (request, cancellationToken) => enterWorldWorkflow.RunAsync(
                request.Settings,
                request.Slot,
                cancellationToken),
            enterWorldWorkflow.Dispose,
            logger);
    }

    public bool IsRunning => IsCreateWorldRunning || IsEnterWorldRunning;

    public bool IsCreateWorldRunning => createWorldRunner.IsRunning;

    public bool IsEnterWorldRunning => enterWorldRunner.IsRunning;

    public Task StartCreateWorldAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        return createWorldRunner.StartAsync(settings, cancellationToken);
    }

    public Task StartEnterWorldAsync(
        AppSettings settings,
        PracticeWorldSlot slot,
        CancellationToken cancellationToken = default)
    {
        return enterWorldRunner.StartAsync(
            new EnterWorldAutomationRequest(settings, slot),
            cancellationToken);
    }

    public bool CancelCreateWorld()
    {
        return createWorldRunner.Cancel();
    }

    public bool CancelEnterWorld()
    {
        return enterWorldRunner.Cancel();
    }

    public bool Cancel()
    {
        bool cancelledCreateWorld = CancelCreateWorld();
        bool cancelledEnterWorld = CancelEnterWorld();
        return cancelledCreateWorld || cancelledEnterWorld;
    }

    public void Dispose()
    {
        createWorldRunner.Dispose();
        enterWorldRunner.Dispose();
    }

    private readonly record struct EnterWorldAutomationRequest(
        AppSettings Settings,
        PracticeWorldSlot Slot);
}
