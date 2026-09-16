using System.Drawing;
using System.Drawing.Drawing2D;
using Moria.Core;
using Moria.Entities;
using Moria.Rendering;
using Moria.UI;
using Moria.World;

namespace Moria;

public sealed partial class Game
{
    private readonly VirtualCanvas virtualCanvas = new();
    private readonly Camera2D camera = new(53, 21, 12);
    private readonly UiRenderer uiRenderer = new();
    private Position lastRenderedPlayerPosition;
    private Direction playerFacing = Direction.Down;

    private void DrawFrame(Graphics target)
    {
        using Bitmap frame = virtualCanvas.CreateBitmap();
        using (Graphics g = Graphics.FromImage(frame))
        {
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            DrawWorld(g);
            uiRenderer.Draw(g, player, message, runOver, victory, lastRunReward);
        }
        virtualCanvas.Present(target, frame);
    }

    private void DrawWorld(Graphics g)
    {
        camera.Follow(player.Position, dungeon.Width, dungeon.Height);
        g.Clear(DepthBackgroundColor(player.DungeonLevel));

        if (lastRenderedPlayerPosition != player.Position)
        {
            int dx = player.Position.X - lastRenderedPlayerPosition.X;
            int dy = player.Position.Y - lastRenderedPlayerPosition.Y;
            if (Math.Abs(dx) >= Math.Abs(dy) && dx != 0)
                playerFacing = dx > 0 ? Direction.Right : Direction.Left;
            else if (dy != 0)
                playerFacing = dy > 0 ? Direction.Down : Direction.Up;
            lastRenderedPlayerPosition = player.Position;
        }

        int minX = camera.X;
        int maxX = Math.Min(dungeon.Width - 1, camera.X + camera.ViewWidthTiles - 1);
        int minY = camera.Y;
        int maxY = Math.Min(dungeon.Height - 1, camera.Y + camera.ViewHeightTiles - 1);

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            Position position = new(y, x);
            Rectangle rect = camera.TileRectangle(position);
            if (!visibility.HasBeenSeen(position))
            {
                using SolidBrush black = new(Color.FromArgb(255, 3, 4, 7));
                g.FillRectangle(black, rect);
                continue;
            }

            DrawTileBlock(g, dungeon[position], rect, x, y);
            if (!visibility.IsVisible(position))
            {
                using SolidBrush fog = new(Color.FromArgb(175, 4, 5, 8));
                g.FillRectangle(fog, rect);
            }
            else
            {
                DrawDepthTint(g, rect, player.DungeonLevel);
                DrawEntities(g, position, rect);
            }
        }

        if (visibility.IsVisible(player.Position))
        {
            Rectangle playerTile = camera.TileRectangle(player.Position);
            DrawPlayerBlock(g, playerTile);
        }

        if (runOver || victory) DrawDeathOverlay(g);
    }

    private static void DrawTileBlock(Graphics g, Tile tile, Rectangle rect, int x, int y)
    {
        Color fill = tile.Type switch
        {
            TileType.Floor => Color.FromArgb(112, 112, 112),
            TileType.Wall => Color.FromArgb(34, 34, 34),
            TileType.Door => Color.FromArgb(82, 82, 82),
            TileType.StairsUp => Color.FromArgb(145, 145, 145),
            TileType.StairsDown => Color.FromArgb(170, 170, 170),
            TileType.Trap => Color.FromArgb(72, 72, 72),
            _ => Color.FromArgb(20, 20, 20)
        };

        using SolidBrush brush = new(fill);
        g.FillRectangle(brush, rect);

        if (tile.Type == TileType.Floor)
        {
            using Pen seam = new(Color.FromArgb(96, 96, 96), 1);
            if ((x + y) % 2 == 0)
                g.DrawLine(seam, rect.Left, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1);
        }
        else if (tile.Type == TileType.Wall)
        {
            using Pen edge = new(Color.FromArgb(48, 48, 48), 1);
            g.DrawRectangle(edge, rect.Left, rect.Top, rect.Width - 1, rect.Height - 1);
        }
        else if (tile.Type == TileType.StairsUp || tile.Type == TileType.StairsDown)
        {
            using Pen mark = new(Color.FromArgb(55, 55, 55), 1);
            int inset = 3;
            g.DrawRectangle(mark, rect.Left + inset, rect.Top + inset, rect.Width - inset * 2 - 1, rect.Height - inset * 2 - 1);
        }
    }

    private void DrawEntities(Graphics g, Position position, Rectangle rect)
    {
        if (position == player.Position) return;
        Monster? monster = dungeon.MonsterAt(position);
        if (monster != null)
        {
            int inset = monster.IsBoss ? 1 : 3;
            using SolidBrush block = new(monster.IsBoss ? Color.FromArgb(185, 185, 185) : Color.FromArgb(150, 150, 150));
            g.FillRectangle(block, rect.X + inset, rect.Y + inset, rect.Width - inset * 2, rect.Height - inset * 2);
            return;
        }

        Tile tile = dungeon[position];
        if (tile.GearLoot.Count > 0)
        {
            using SolidBrush loot = new(Color.FromArgb(195, 195, 195));
            g.FillRectangle(loot, rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
        }
        else if (tile.HasItem)
        {
            using SolidBrush loot = new(Color.FromArgb(175, 175, 175));
            g.FillRectangle(loot, rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
        }
    }

    private static void DrawPlayerBlock(Graphics g, Rectangle tile)
    {
        int inset = 2;
        using SolidBrush player = new(Color.FromArgb(225, 225, 225));
        g.FillRectangle(player, tile.X + inset, tile.Y + inset, tile.Width - inset * 2, tile.Height - inset * 2);
        using Pen outline = new(Color.FromArgb(245, 245, 245), 1);
        g.DrawRectangle(outline, tile.X + inset, tile.Y + inset, tile.Width - inset * 2 - 1, tile.Height - inset * 2 - 1);
    }

    private static Color DepthBackgroundColor(int level)
    {
        double progress = Math.Clamp((level - 1) / (double)(Dungeon.MaximumDepth - 1), 0.0, 1.0);
        int red = 4 + (int)Math.Round(progress * 22);
        int green = 6 - (int)Math.Round(progress * 4);
        int blue = 9 - (int)Math.Round(progress * 4);
        return Color.FromArgb(red, Math.Max(1, green), Math.Max(2, blue));
    }

    private static void DrawDepthTint(Graphics g, Rectangle rect, int level)
    {
        double progress = Math.Clamp((level - 1) / (double)(Dungeon.MaximumDepth - 1), 0.0, 1.0);
        int alpha = (int)Math.Round(progress * 35);
        if (alpha <= 0) return;
        using SolidBrush tint = new(Color.FromArgb(alpha, 90, 90, 90));
        g.FillRectangle(tint, rect);
    }

    private void DrawDeathOverlay(Graphics g)
    {
        using SolidBrush shade = new(Color.FromArgb(190, 0, 0, 0));
        g.FillRectangle(shade, 0, 0, 640, 256);
        using Font title = new("Segoe UI", 22, FontStyle.Bold);
        using Font body = new("Segoe UI", 10, FontStyle.Bold);
        using SolidBrush text = new(Color.Gainsboro);
        string heading = victory ? "MORIA CONQUERED" : "YOU DIED";
        string detail = victory ? $"Legacy Gold earned: {lastRunReward}" : $"Legacy Gold recovered: {lastRunReward}";
        g.DrawString(heading, title, text, 214, 95);
        g.DrawString(detail, body, text, 244, 132);
        g.DrawString("N: New Run    ESC: Quit", body, text, 242, 154);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!started) return;
        DrawFrame(e.Graphics);
    }
}
