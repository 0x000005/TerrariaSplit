namespace TerrariaSplit.Application.Ports;

public interface ITerrariaUiScalePatchApplier
{
    TerrariaUiScalePatchResult TryApply();
}
