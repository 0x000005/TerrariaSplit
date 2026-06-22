namespace TerrariaSplit.Application;

public readonly record struct TerrariaUiScalePatchResult(
    TerrariaUiScalePatchStatus Status,
    int? ProcessId,
    string Message)
{
    public bool IsSuccess => Status is TerrariaUiScalePatchStatus.Applied or TerrariaUiScalePatchStatus.AlreadyApplied;

    public static TerrariaUiScalePatchResult NoProcess()
    {
        return new TerrariaUiScalePatchResult(
            TerrariaUiScalePatchStatus.NoProcess,
            null,
            "Terraria process is not running.");
    }

    public static TerrariaUiScalePatchResult Unsupported(int processId, string message)
    {
        return new TerrariaUiScalePatchResult(TerrariaUiScalePatchStatus.Unsupported, processId, message);
    }

    public static TerrariaUiScalePatchResult Failed(int processId, string message)
    {
        return new TerrariaUiScalePatchResult(TerrariaUiScalePatchStatus.Failed, processId, message);
    }

    public static TerrariaUiScalePatchResult Applied(int processId, string message)
    {
        return new TerrariaUiScalePatchResult(TerrariaUiScalePatchStatus.Applied, processId, message);
    }

    public static TerrariaUiScalePatchResult AlreadyApplied(int processId, string message)
    {
        return new TerrariaUiScalePatchResult(TerrariaUiScalePatchStatus.AlreadyApplied, processId, message);
    }
}

public enum TerrariaUiScalePatchStatus
{
    NoProcess,
    Unsupported,
    Failed,
    Applied,
    AlreadyApplied
}
