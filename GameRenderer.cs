using System.Drawing;
using System.Drawing.Drawing2D;
using Moria.Art;
using Moria.Core;
using Moria.Entities;
using Moria.Items;
using Moria.World;

namespace Moria;

public sealed partial class Game
{
    private readonly AssetAtlas assets = new();
    private Position lastRenderedPlayerPosition;
    private Direction playerFacing = Direction.Down;

    private void DrawMap(Graphics g)
    {
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SmoothingMode = SmoothingMode.None;

        using SolidBrush mapBackground = new(Color.FromArgb(4, 5, 7));
        g.FillRectangle(mapBackground, 0, 0, MapWidth, MapHeight);

        if (lastRenderedPlayerPosition != player.Position)
        {
            int dy = player.Position.Y - lastRenderedPlayerPosition.Y;
            int dx = player.Position.X - lastRenderedPlayerPosition.X;
            if (Math.Abs(dx) >= Math.Abs(dy) && dx != 0)
                playerFacing = dx > 0 ? Direction.Right : Direction.Left;
            else if (dy != 0)
                playerFacing = dy > 0 ? Direction.Down : Direction.Up;
            lastRenderedPlayerPosition = player.Position;
        }

        for (int y = 0; y < Dungeon.Height; y++)
        for (int x = 0; x < Dungeon.Width; x++)
        {
            Position p = new(y, x);
            Tile tile = dungeon[p];
            Rectangle rect = new(x * TileSize, y * TileSize, TileSize, TileSize);

            if (!tile.Seen)
            {
                using SolidBrush unseen = new(Color.FromArgb(4, 5, 7));
                g.FillRectangle(unseen, rect);
                continue;
            }

            assets.DrawTile(g, tile.Type, rect, x, y);

            if (tile.GearLoot.Count > 0)
                assets.DrawGear(g, CenteredSpriteRect(rect, 25), tile.GearLoot[^1]);
            else if (tile.HasItem)
                assets.DrawPotion(g, CenteredSpriteRect(rect, 24));

            Monster? monster = dungeon.MonsterAt(p);
            if (monster != null)
            {
                assets.DrawMonster(g, CenteredSpriteRect(rect, 26), monster);
                DrawMonsterMarker(g, rect, monster);
            }
        }

        Rectangle playerRect = CenteredSpriteRect(
            new Rectangle(player.Position.X * TileSize, player.Position.Y * TileSize, TileSize, TileSize),
            28);

        DrawPlayerMarker(g, playerRect);
        assets.DrawPlayer(g, playerRect, playerFacing, player.Alive);

        if (runOver) DrawDeathOverlay(g);
    }

    private static Rectangle CenteredSpriteRect(Rectangle tile, int size)
    {
        int x = tile.X + (tile.Width - size) / 2;
        int y = tile.Y + (tile.Height - size) / 2;
        return new Rectangle(x, y, size, size);
    }

    private static void DrawPlayerMarker(Graphics g, Rectangle playerRect)
    {
        using Pen glow = new(Color.FromArgb(180, 255, 215, 70), 2);
        Rectangle marker = new(playerRect.X - 2, playerRect.Y - 2, playerRect.Width + 4, playerRect.Height + 4);
        g.DrawRectangle(glow, marker);
    }

    private static void DrawMonsterMarker(Graphics g, Rectangle tile, Monster monster)
    {
        int distance = Math.Abs(tile.X / TileSize - tile.X / TileSize);
        _ = distance;

        using SolidBrush marker = new(Color.FromArgb(210, 180, 45, 45));
        g.FillEllipse(marker, tile.X + TileSize - 7, tile.Y + 2, 5, 5);

        if (monster.Hp <= 0) return;

        using Font font = new("Segoe UI", 6.5f, FontStyle.Bold);
        using SolidBrush text = new(Color.FromArgb(235, 235, 235));
        SizeF size = g.MeasureString(monster.Name, font);
        float labelX = tile.X + (TileSize - size.Width) / 2f;
        float labelY = tile.Y - size.Height - 1;
        if (labelY >= 0)
        {
            using SolidBrush shadow = new(Color.FromArgb(190, 0, 0, 0));
            g.DrawString(monster.Name, font, shadow, labelX + 1, labelY + 1);
            g.DrawString(monster.Name, font, text, labelX, labelY);
        }
    }

    private void DrawDeathOverlay(Graphics g)
    {
        using SolidBrush veil = new(Color.FromArgb(175, 0, 0, 0));
        g.FillRectangle(veil, 0, 0, MapWidth, MapHeight);
        using Font title = new("Segoe UI", 30, FontStyle.Bold);
        using Font body = new("Segoe UI", 13);
        using SolidBrush text = new(Color.Gainsboro);
        using SolidBrush gold = new(Color.FromArgb(255, 215, 90));
        g.DrawString("YOU DIED", title, text, 290, 175);
        g.DrawString($"Dungeon level {player.DungeonLevel}    Run {player.RunsCompleted}", body, text, 300, 220);
        g.DrawString($"+{LastRunReward} Legacy Gold", body, gold, 320, 245);
        g.DrawString("ENTER / SPACE / N  New Run", body, text, 295, 285);
        g.DrawString("X / ESC  Quit", body, text, 350, 310);
    }

    private void DrawStatus(Graphics g)
    {
        int y = MapHeight;

        using SolidBrush background = new(Color.FromArgb(13, 14, 19));
        using SolidBrush panel = new(Color.FromArgb(20, 21, 27));
        using Pen border = new(Color.FromArgb(55, 58, 68));
        using SolidBrush text = new(Color.Gainsboro);
        using SolidBrush muted = new(Color.FromArgb(155, 160, 170));
        using SolidBrush accent = new(Color.FromArgb(100, 205, 255));
        using SolidBrush gold = new(Color.FromArgb(245, 195, 70));
        using SolidBrush hpFill = new(Color.FromArgb(190, 55, 55));
        using SolidBrush hpBack = new(Color.FromArgb(55, 25, 28));
        using SolidBrush xpFill = new(Color.FromArgb(80, 145, 205));
        using Font title = new("Segoe UI", 11, FontStyle.Bold);
        using Font normal = new("Segoe UI", 9.5f);
        using Font small = new("Segoe UI", 8.5f);
        using Font tiny = new("Segoe UI", 7.5f);

        g.FillRectangle(background, 0, y, ClientSize.Width, StatusHeight);

        Rectangle leftPanel = new(8, y + 8, 650, 86);
        Rectangle rightPanel = new(666, y + 8, ClientSize.Width - 674, 86);
        g.FillRectangle(panel, leftPanel);
        g.FillRectangle(panel, rightPanel);
        g.DrawRectangle(border, leftPanel);
        g.DrawRectangle(border, rightPanel);

        g.DrawString(player.Name, title, text, 18, y + 14);
        g.DrawString($"LV {player.Level}   Dungeon {player.DungeonLevel}   Run {player.RunsCompleted + (runOver ? 0 : 1)}", normal, muted, 18, y + 34);

        int hp = Math.Max(0, player.Hp);
        int maxHp = Math.Max(1, player.TotalMaxHp);
        int hpWidth = 210;
        Rectangle hpBackRect = new(18, y + 56, hpWidth, 14);
        g.FillRectangle(hpBack, hpBackRect);
        g.FillRectangle(hpFill, new Rectangle(hpBackRect.X, hpBackRect.Y, hpWidth * hp / maxHp, hpBackRect.Height));
        g.DrawRectangle(border, hpBackRect);
        g.DrawString($"HP {hp}/{maxHp}", tiny, text, hpBackRect.X + 7, hpBackRect.Y + 1);

        int xpToNext = Math.Max(1, player.Level * 100);
        int xpProgress = Math.Clamp(player.Experience % xpToNext, 0, xpToNext);
        Rectangle xpBackRect = new(245, y + 56, 165, 14);
        g.FillRectangle(hpBack, xpBackRect);
        g.FillRectangle(xpFill, new Rectangle(xpBackRect.X, xpBackRect.Y, xpBackRect.Width * xpProgress / xpToNext, xpBackRect.Height));
        g.DrawRectangle(border, xpBackRect);
        g.DrawString($"XP {player.Experience}", tiny, text, xpBackRect.X + 7, xpBackRect.Y + 1);

        g.DrawString($"ATK {player.TotalAttack}   ARM {player.TotalArmorClass}", normal, accent, 430, y + 54);
        g.DrawString($"Gold {player.Gold}   Legacy {player.PermanentGold}   Food {player.Food}", normal, gold, 18, y + 76);

        g.DrawString("CURRENT ACTION", tiny, muted, 678, y + 14);
        g.DrawString(message, normal, text, 678, y + 32);
        g.DrawString($"Weapon: {player.Weapon?.Name ?? "None"}", small, accent, 678, y + 55);
        g.DrawString($"Armor: {player.Armor?.Name ?? "None"}    Ring: {player.Ring?.Name ?? "None"}", small, accent, 678, y + 71);

        using Pen divider = new(Color.FromArgb(45, 47, 55));
        g.DrawLine(divider, 8, y + 101, ClientSize.Width - 8, y + 101);

        g.DrawString("GOAL", tiny, gold, 12, y + 108);
        g.DrawString("Find the stairs < / >, fight monsters, collect loot with G, and descend with .", small, text, 48, y + 107);
        g.DrawString("MOVE", tiny, muted, 12, y + 129);
        g.DrawString("Arrows / HJKL", small, text, 48, y + 128);
        g.DrawString("ACTIONS", tiny, muted, 155, y + 129);
        g.DrawString("G loot   I inventory   R equip   E eat   Q potion   S save", small, text, 205, y + 128);
        g.DrawString("X / Esc quit", small, muted, ClientSize.Width - 82, y + 128);
        g.DrawString("SNES art • fog of war • turn-based combat", tiny, muted, 12, y + 150);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!started) return;
        e.Graphics.SmoothingMode = SmoothingMode.None;
        DrawMap(e.Graphics);
        DrawStatus(e.Graphics);
    }
}
