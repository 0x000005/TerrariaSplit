namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal sealed class GenerationProgress
{
    public double CurrentProgress { get; private set; }

    public double TotalProgress { get; private set; }

    public string Message { get; set; } = string.Empty;

    public void Start(double weight)
    {
        CurrentProgress = 0d;
    }

    public void Set(double value)
    {
        CurrentProgress = value;
    }

    public void End()
    {
        CurrentProgress = 1d;
    }

    public void SetTotal(double value)
    {
        TotalProgress = value;
    }
}
