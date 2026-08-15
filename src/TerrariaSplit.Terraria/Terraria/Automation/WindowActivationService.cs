using System.Drawing;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class WindowActivationService
{
    private readonly TerrariaAutomationContext automation;
    private readonly string workflowName;

    public WindowActivationService(TerrariaAutomationContext automation, string workflowName)
    {
        this.automation = automation;
        this.workflowName = workflowName;
    }

    public async Task<WindowActivationResult> ActivateAsync(CancellationToken cancellationToken)
    {
        Size clientSize = Size.Empty;
        bool succeeded = await automation.RunStepAsync(
            "activate Terraria window",
            _ =>
            {
                if (!automation.TryActivate(out Size activatedSize))
                {
                    FileAppLogger.Instance.Info($"{workflowName} automation could not activate Terraria window.");
                    return Task.FromResult(false);
                }

                clientSize = activatedSize;
                return Task.FromResult(true);
            },
            cancellationToken);
        return succeeded
            ? WindowActivationResult.Success(clientSize)
            : WindowActivationResult.Failed(
                "Could not activate the Terraria window.",
                $"{workflowName} automation could not activate Terraria window.");
    }

    public bool TryReactivate(string detail, int activationDelayMilliseconds)
    {
        if (automation.TryActivate(out _, activationDelayMilliseconds))
        {
            return true;
        }

        FileAppLogger.Instance.Info($"{workflowName} automation could not reactivate Terraria {detail}.");
        return false;
    }
}

internal readonly record struct WindowActivationResult(
    bool Succeeded,
    Size ClientSize,
    string UserMessage,
    string DiagnosticMessage)
{
    public static WindowActivationResult Success(Size clientSize)
    {
        return new WindowActivationResult(true, clientSize, string.Empty, string.Empty);
    }

    public static WindowActivationResult Failed(string userMessage, string diagnosticMessage)
    {
        return new WindowActivationResult(false, Size.Empty, userMessage, diagnosticMessage);
    }
}
