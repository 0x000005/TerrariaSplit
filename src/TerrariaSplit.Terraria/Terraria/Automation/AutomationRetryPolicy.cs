namespace TerrariaSplit.Terraria.Automation;

internal static class AutomationRetryPolicy
{
    public const int MaxAttempts = 3;
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(50);
}
