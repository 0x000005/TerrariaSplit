using Microsoft.AspNetCore.SignalR.Client;

namespace TerrariaSplit.Race.Client;

public sealed class RaceReconnectRetryPolicy : IRetryPolicy
{
    public static RaceReconnectRetryPolicy Instance { get; } = new();

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        int exponent = (int)Math.Min(retryContext.PreviousRetryCount, 10);
        int seconds = 1 << exponent;
        return TimeSpan.FromSeconds(seconds);
    }
}
