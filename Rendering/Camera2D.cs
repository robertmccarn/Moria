using System.Drawing;
using Moria.Core;

namespace Moria.Rendering;

public sealed class Camera2D
{
    public int ViewWidthTiles { get; }
    public int ViewHeightTiles { get; }
    public int TileSize { get; }
    public int X { get; private set; }
    public int Y { get; private set; }

    public Camera2D(int viewWidthTiles, int viewHeightTiles, int tileSize)
    {
        ViewWidthTiles = viewWidthTiles;
        ViewHeightTiles = viewHeightTiles;
        TileSize = tileSize;
    }

    public void Follow(Position target, int worldWidth, int worldHeight)
    {
        X = Math.Clamp(target.X - ViewWidthTiles / 2, 0, Math.Max(0, worldWidth - ViewWidthTiles));
        Y = Math.Clamp(target.Y - ViewHeightTiles / 2, 0, Math.Max(0, worldHeight - ViewHeightTiles));
    }

    public Rectangle TileRectangle(Position worldPosition) => new(
        (worldPosition.X - X) * TileSize,
        (worldPosition.Y - Y) * TileSize,
        TileSize,
        TileSize);
}
