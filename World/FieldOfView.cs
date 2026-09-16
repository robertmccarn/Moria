using Moria.Core;

namespace Moria.World;

public sealed class FieldOfView
{
    public void Recalculate(Dungeon dungeon, Position origin, int radius, VisibilityMap visibility)
    {
        visibility.ClearVisible();
        visibility.Reveal(origin);

        for (int y = origin.Y - radius; y <= origin.Y + radius; y++)
        for (int x = origin.X - radius; x <= origin.X + radius; x++)
        {
            Position target = new(y, x);
            if (!dungeon.IsInside(target)) continue;
            if (Math.Abs(target.Y - origin.Y) + Math.Abs(target.X - origin.X) > radius) continue;
            if (HasLineOfSight(dungeon, origin, target)) visibility.Reveal(target);
        }
    }

    private static bool HasLineOfSight(Dungeon dungeon, Position start, Position end)
    {
        int x0 = start.X;
        int y0 = start.Y;
        int x1 = end.X;
        int y1 = end.Y;
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            Position p = new(y0, x0);
            if (p != start && p != end && dungeon[p].Type is TileType.Wall or TileType.Rock)
                return false;
            if (x0 == x1 && y0 == y1) return true;

            int e2 = 2 * error;
            if (e2 >= dy) { error += dy; x0 += sx; }
            if (e2 <= dx) { error += dx; y0 += sy; }
        }
    }
}
