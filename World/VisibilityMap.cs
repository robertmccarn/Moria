using Moria.Core;

namespace Moria.World;

public sealed class VisibilityMap
{
    private readonly bool[,] visible;
    private readonly bool[,] seen;

    public int Width { get; }
    public int Height { get; }

    public VisibilityMap(int width, int height)
    {
        Width = width;
        Height = height;
        visible = new bool[height, width];
        seen = new bool[height, width];
    }

    public bool IsVisible(Position p) => Inside(p) && visible[p.Y, p.X];
    public bool HasBeenSeen(Position p) => Inside(p) && seen[p.Y, p.X];
    public void ClearVisible() => Array.Clear(visible, 0, visible.Length);

    public void Reveal(Position p)
    {
        if (!Inside(p)) return;
        visible[p.Y, p.X] = true;
        seen[p.Y, p.X] = true;
    }

    private bool Inside(Position p) => p.Y >= 0 && p.Y < Height && p.X >= 0 && p.X < Width;
}
