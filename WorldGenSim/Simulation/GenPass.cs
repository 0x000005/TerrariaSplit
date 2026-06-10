namespace WorldGenSim.Simulation;

internal abstract class GenPass
{
    protected GenPass(string name, double weight)
    {
        Name = name;
        Weight = weight;
    }

    public string Name { get; }

    public double Weight { get; }

    public bool Enabled { get; private set; } = true;

    public void Disable()
    {
        Enabled = false;
    }

    public void Apply(WorldGenContext context, GenerationProgress progress)
    {
        ApplyPass(context, progress);
    }

    protected abstract void ApplyPass(WorldGenContext context, GenerationProgress progress);
}
