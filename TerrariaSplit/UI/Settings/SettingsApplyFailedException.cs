namespace TerrariaSplit;

internal sealed class SettingsApplyFailedException : Exception
{
    public SettingsApplyFailedException(string message)
        : base(message)
    {
    }
}
