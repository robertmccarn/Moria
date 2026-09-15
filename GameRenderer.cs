using System.Drawing;
using System.Drawing.Drawing2D;
using Moria.Items;
using Moria.World;

namespace Moria;

public sealed partial class Game
{
    private void DrawMap(Graphics g)
    {
        for (int y = 0; y < Dungeon.Height; y++)
        for (int x = 0; x < Dungeon.Width; x++)
        {
            Position p = new(y, x);
            Tile tile = dungeon[p];
            Rectangle rect = new(x * TileSize, y * TileSize, TileSize, TileSize);

            if (!tile.Seen)
            {
                using SolidBrush unseen = new(Color.FromArgb(7, 7, 10));
                g.FillRectangle(unseen, rect);
                continue;
            }

            DrawTile(g, tile.Type, rect);
            if (tile.Gear != null) DrawGear(g, rect, tile.Gear);
            else if (tile.HasItem) DrawPotion(g, rect);

            Monster? monster = dungeon.MonsterAt(p);
            if (monster != null) DrawMonster(g, rect, monster);
        }

        DrawPlayer(g, new Rectangle(player.Position.X * TileSize, player.Position.Y * TileSize, TileSize, TileSize), player.Alive);

        if (runOver) DrawDeathOverlay(g);
    }

    private static void DrawTile(Graphics g, TileType type, Rectangle rect)
    {
        Color fill = type switch
        {
            TileType.Floor => Color.FromArgb(43, 44, 50),
            TileType.Wall => Color.FromArgb(24, 25, 31),
            TileType.Door => Color.FromArgb(82, 62, 42),
            TileType.StairsUp or TileType.StairsDown => Color.FromArgb(46, 54, 66),
            TileType.Trap => Color.FromArgb(62, 36, 45),
            _ => Color.FromArgb(10, 11, 15)
        };

        using SolidBrush brush = new(fill);
        g.FillRectangle(brush, rect);
        using Pen grid = new(Color.FromArgb(16, 17, 21));
        g.DrawRectangle(grid, rect);

        int cx = rect.X + rect.Width / 2;
        int cy = rect.Y + rect.Height / 2;
        using Pen detail = new(Color.FromArgb(135, 140, 150), 2f);

        switch (type)
        {
            case TileType.StairsUp:
                g.DrawLine(detail, cx - 6, cy + 5, cx, cy - 5);
                g.DrawLine(detail, cx, cy - 5, cx + 6, cy + 5);
                break;
            case TileType.StairsDown:
                g.DrawLine(detail, cx - 6, cy - 5, cx, cy + 5);
                g.DrawLine(detail, cx, cy + 5, cx + 6, cy - 5);
                break;
            case TileType.Door:
                g.DrawRectangle(detail, rect.X + 6, rect.Y + 4, rect.Width - 12, rect.Height - 8);
                break;
            case TileType.Trap:
                g.DrawPolygon(detail, [new Point(cx, cy - 6), new Point(cx - 6, cy + 5), new Point(cx + 6, cy + 5)]);
                break;
        }
    }

    private static void DrawPlayer(Graphics g, Rectangle rect, bool alive)
    {
        int cx = rect.X + rect.Width / 2;
        int cy = rect.Y + rect.Height / 2;
        using SolidBrush steel = new(alive ? Color.FromArgb(205, 210, 220) : Color.FromArgb(105, 105, 110));
        using SolidBrush shadow = new(Color.FromArgb(70, 72, 80));
        using Pen outline = new(alive ? Color.FromArgb(110, 205, 255) : Color.FromArgb(110, 110, 115), 1.5f);
        using Pen sword = new(Color.FromArgb(230, 230, 235), 2f);

        g.FillEllipse(shadow, rect.X + 5, rect.Y + 4, rect.Width - 10, rect.Height - 5);
        g.FillRectangle(steel, rect.X + 7, rect.Y + 10, rect.Width - 14, rect.Height - 7);
        g.FillEllipse(steel, rect.X + 6, rect.Y + 2, rect.Width - 12, 11);
        g.DrawEllipse(outline, rect.X + 6, rect.Y + 2, rect.Width - 12, 11);
        g.DrawLine(outline, rect.X + 8, rect.Y + 12, rect.X + 14, rect.Y + 17);
        g.DrawLine(outline, rect.X + rect.Width - 8, rect.Y + 12, rect.X + 14, rect.Y + 17);
        g.DrawLine(sword, cx + 3, cy + 3, rect.Right - 2, rect.Y + 3);
        g.DrawLine(sword, rect.Right - 2, rect.Y + 3, rect.Right - 5, rect.Y + 6);
    }

    private static void DrawMonster(Graphics g, Rectangle rect, Monster monster)
    {
        Color bodyColor = monster.Level >= 5 ? Color.FromArgb(175, 70, 85) : Color.FromArgb(145, 100, 70);
        using SolidBrush body = new(bodyColor);
        Point[] shape = [
            new Point(rect.X + rect.Width / 2, rect.Y + 3),
            new Point(rect.X + rect.Width - 4, rect.Y + rect.Height / 2),
            new Point(rect.X + rect.Width / 2, rect.Y + rect.Height - 3),
            new Point(rect.X + 4, rect.Y + rect.Height / 2)
        ];
        g.FillPolygon(body, shape);
        using Pen outline = new(Color.FromArgb(235, 170, 110), 1.5f);
        g.DrawPolygon(outline, shape);
    }

    private static void DrawPotion(Graphics g, Rectangle rect)
    {
        using SolidBrush bottle = new(Color.FromArgb(90, 170, 225));
        using SolidBrush cork = new(Color.FromArgb(185, 145, 95));
        g.FillRectangle(cork, rect.X + 8, rect.Y + 3, 6, 4);
        g.FillEllipse(bottle, rect.X + 5, rect.Y + 6, 12, 13);
    }

    private static void DrawGear(Graphics g, Rectangle rect, Gear gear)
    {
        Color glow = gear.Rarity switch
        {
            1 => Color.FromArgb(165, 170, 180),
            2 => Color.FromArgb(90, 155, 235),
            3 => Color.FromArgb(185, 105, 235),
            _ => Color.FromArgb(245, 175, 65)
        };
        using SolidBrush brush = new(glow);
        using Pen outline = new(Color.FromArgb(245, 245, 245), 1f);
        Point[] diamond = [
            new Point(rect.X + rect.Width / 2, rect.Y + 3),
            new Point(rect.X + rect.Width - 4, rect.Y + rect.Height / 2),
            new Point(rect.X + rect.Width / 2, rect.Y + rect.Height - 3),
            new Point(rect.X + 4, rect.Y + rect.Height / 2)
        ];
        g.FillPolygon(brush, diamond);
        g.DrawPolygon(outline, diamond);
    }

    private void DrawDeathOverlay(Graphics g)
    {
        using SolidBrush veil = new(Color.FromArgb(155, 0, 0, 0));
        g.FillRectangle(veil, 0, 0, MapWidth, MapHeight);
        using Font title = new("Segoe UI", 28, FontStyle.Bold);
        using Font body = new("Segoe UI", 12);
        using SolidBrush text = new(Color.Gainsboro);
        using SolidBrush gold = new(Color.FromArgb(255, 215, 90));
        int reward = LastRunReward;
        g.DrawString("YOU DIED", title, text, 290, 175);
        g.DrawString($"Dungeon level {player.DungeonLevel}    Run {player.RunsCompleted}", body, text, 300, 220);
        g.DrawString($"+{reward} Legacy Gold", body, gold, 320, 245);
        g.DrawString("ENTER / SPACE / N  New Run", body, text, 295, 285);
        g.DrawString("X / ESC  Quit", body, text, 350, 310);
    }

    private void DrawStatus(Graphics g)
    {
        int y = MapHeight;
        using SolidBrush background = new(Color.FromArgb(17, 18, 23));
        g.FillRectangle(background, 0, y, ClientSize.Width, StatusHeight);
        using Font title = new("Segoe UI", 11, FontStyle.Bold);
        using Font normal = new("Segoe UI", 9);
        using Font small = new("Segoe UI", 8.5f);
        using SolidBrush text = new(Color.Gainsboro);
        using SolidBrush muted = new(Color.FromArgb(155, 160, 170));
        using SolidBrush accent = new(Color.FromArgb(100, 205, 255));
        using SolidBrush legendary = new(Color.FromArgb(245, 175, 65));

        g.DrawString($"{player.Name}   HP {Math.Max(0, player.Hp)}/{player.TotalMaxHp}   LV {player.Level}   XP {player.Experience}   ATK {player.TotalAttack}   ARM {player.TotalArmorClass}", title, text, 12, y + 8);
        g.DrawString($"Gold {player.Gold}   Legacy {player.PermanentGold}   Food {player.Food}   Dungeon {player.DungeonLevel}   Run {player.RunsCompleted + (runOver ? 0 : 1)}", normal, legendary, 12, y + 32);
        g.DrawString($"Weapon: {player.Weapon?.Name ?? "None"}     Armor: {player.Armor?.Name ?? "None"}     Ring: {player.Ring?.Name ?? "None"}", normal, accent, 12, y + 55);
        g.DrawString(message, normal, text, 12, y + 79);
        g.DrawString("Arrows/HJKL move   G loot   I inventory   R equip best   E eat   Q potion   . descend   S save   X/Esc quit", small, muted, 12, y + 105);
        g.DrawString("Loot: Common • Rare • Epic • Legendary    |    Critical hits + Legacy Gold progression", small, muted, 12, y + 124);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!started) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        DrawMap(e.Graphics);
        DrawStatus(e.Graphics);
    }
}
