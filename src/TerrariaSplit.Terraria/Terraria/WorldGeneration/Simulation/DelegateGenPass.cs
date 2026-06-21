namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal sealed class DelegateGenPass : GenPass
{
    private readonly Action<WorldGenContext, GenerationProgress> apply;

    public DelegateGenPass(string name, double weight, Action<WorldGenContext, GenerationProgress> apply)
        : base(name, weight)
    {
        this.apply = apply;
    }

    protected override void ApplyPass(WorldGenContext context, GenerationProgress progress)
    {
        apply(context, progress);
    }
}
