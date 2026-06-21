namespace TerrariaSplit.UI;

internal readonly record struct ModalWindowOptions(
    bool ForceTopMost,
    bool KeepForeground)
{
    public static ModalWindowOptions Default => new(false, false);

    public static ModalWindowOptions ForceTopMostForeground => new(true, true);
}
