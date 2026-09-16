using System.Drawing;
using System.Drawing.Drawing2D;
using Moria.Art;
using Moria.Core;
using Moria.Entities;
using Moria.Rendering;
using Moria.UI;
using Moria.World;

namespace Moria;

public sealed partial class Game
{
    private readonly AssetAtlas assets = new();
    private readonly VirtualCanvas virtualCanvas = new();
    private readonly Camera2D camera = new(40, 16, TileSize);
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
            uiRenderer.Draw(g, player, message, runOver, lastRunReward);
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

            assets.DrawTile(g, dungeon[position].Type, rect, x, y);
            bool lit = visibility.IsVisible(position);
            if (!lit)
            {
                using SolidBrush fog = new(Color.FromArgb(170, 4, 5, 8));
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
            Rectangle playerRect = camera.TileRectangle(player.Position);
            DrawPlayerEnergy(g, playerRect);
            assets.DrawPlayer(g, CenteredSpriteRect(playerRect, 15), playerFacing, player.Alive);
        }

        if (runOver)
            DrawDeathOverlay(g);
    }

    private void DrawEntities(Graphics g, Position position, Rectangle rect)
    {
        if (position == player.Position) return;
        Monster? monster = dungeon.MonsterAt(position);
        if (monster != null)
        {
            int size = monster.IsBoss ? 28 : 15;
            assets.DrawMonster(g, CenteredSpriteRect(rect, size), monster);
            DrawMonsterMarker(g, rect, monster);
            return;
        }

        Tile tile = dungeon[position];
        if (tile.GearLoot.Count > 0)
            assets.DrawGear(g, CenteredSpriteRect(rect, 14), tile.GearLoot[^1]);
        else if (tile.HasItem)
            assets.DrawPotion(g, CenteredSpriteRect(rect, 14));
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
        int alpha = (int)Math.Round(progress * 70);
        if (alpha <= 0) return;
        using SolidBrush tint = new(Color.FromArgb(alpha, 145, 12, 12));
        g.FillRectangle(tint, rect);
    }

    private static Rectangle CenteredSpriteRect(Rectangle tile, int size)
    {
        return new Rectangle(tile.X + (tile.Width - size) / 2, tile.Y + (tile.Height - size) / 2, size, size);
    }

    private static void DrawMonsterMarker(Graphics g, Rectangle tile, Monster monster)
    {
        int size = monster.IsBoss ? 4 : 3;
        using SolidBrush marker = new(Color.FromArgb(monster.IsBoss ? 235 : 205, 190, 45, 40));
        g.FillEllipse(marker, tile.Right - size - 1, tile.Y + 1, size, size);
    }

    private static void DrawPlayerEnergy(Graphics g, Rectangle tile)
    {
        using SolidBrush glow = new(Color.FromArgb(80, 75, 165, 205));
        g.FillEllipse(glow, tile.X + 1, tile.Bottom - 5, tile.Width - 2, 4);
    }

    private void DrawDeathOverlay(Graphics g)
    {
        using SolidBrush veil = new(Color.FromArgb(175, 0, 0, 0));
        g.FillRectangle(veil, 0, 0, VirtualCanvas.Width, MapHeight);
        using Font title = new("Segoe UI", 22, FontStyle.Bold);
        using SolidBrush text = new(Color.Gainsboro);
        using SolidBrush gold = new(Color.FromArgb(255, 215, 90));
        g.DrawString("YOU DIED", title, text, 265, 112);
        using Font body = new("Segoe UI", 8, FontStyle.Bold);
        g.DrawString($"DEPTH {player.DungeonLevel}    +{LastRunReward} LEGACY GOLD", body, gold, 225, 140);
        g.DrawString("ENTER / SPACE / N  NEW RUN", body, text, 235, 158);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!started) return;
        DrawFrame(e.Graphics);
    }
}
