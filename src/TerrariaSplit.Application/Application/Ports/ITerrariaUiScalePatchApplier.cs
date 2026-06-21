namespace TerrariaSplit.Application.Ports;

internal interface ITerrariaUiScalePatchApplier
{
    TerrariaUiScalePatchResult TryApply();
}
