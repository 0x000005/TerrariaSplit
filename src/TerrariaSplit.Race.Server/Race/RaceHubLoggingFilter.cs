using Microsoft.AspNetCore.SignalR;
using TerrariaSplit.Infrastructure;

namespace TerrariaSplit.Race.Server;

internal sealed class RaceHubLoggingFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        string name = "Race hub " + context.HubMethodName;
        using var operation = AppLog.BeginOperation(name, new
        {
            context.Context.ConnectionId, method = context.HubMethodName
        });
        try
        {
            object? result = await next(context);
            AppLog.Event(LogLevel.Information, "Race.Server", "server.hub.completed", new { method = context.HubMethodName });
            return result;
        }
        catch (Exception exception)
        {
            // SignalR owns the final error record. Preserve this invocation's identity
            // after its operation scope unwinds instead of logging and rethrowing.
            exception.Data["OperationId"] = AppLog.OperationId;
            exception.Data["Operation"] = name;
            exception.Data["AutomationStage"] = context.HubMethodName;
            throw;
        }
    }
}
