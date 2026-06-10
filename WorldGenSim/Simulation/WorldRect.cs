namespace WorldGenSim.Simulation;

internal readonly record struct WorldRect(int X, int Y, int Width, int Height)
{
    public int Left => X;

    public int Top => Y;

    public int Right => X + Width;

    public int Bottom => Y + Height;

    public int CenterX => X + Width / 2;

    public int CenterY => Y + Height / 2;

    public static WorldRect Empty => new(0, 0, 0, 0);

    public bool Contains(int x, int y)
    {
        return x >= Left && x < Right && y >= Top && y < Bottom;
    }

    public bool Intersects(WorldRect other)
    {
        return other.Left < Right &&
            Left < other.Right &&
            other.Top < Bottom &&
            Top < other.Bottom;
    }

    public WorldRect Inflated(int horizontal, int vertical)
    {
        return new WorldRect(X - horizontal, Y - vertical, Width + horizontal * 2, Height + vertical * 2);
    }
}
