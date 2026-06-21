namespace TerrariaSplit.Terraria;

internal sealed class TerrariaUiScalePatchApplierAdapter : ITerrariaUiScalePatchApplier
{
    private readonly TerrariaUiScalePatch patch = new();

    public TerrariaUiScalePatchResult TryApply()
    {
        return patch.TryApply();
    }
}
