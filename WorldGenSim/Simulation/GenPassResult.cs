namespace WorldGenSim.Simulation;

internal readonly record struct GenPassResult(
    string Name,
    bool Skipped,
    int DurationMs,
    int RandNext);
