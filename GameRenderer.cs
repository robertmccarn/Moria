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
                using SolidBrush unseen = new(Color.FromArgb(5, 7, 8));
                g.FillRectangle(unseen, rect);
                continue;
            }

            assets.DrawTile(g, tile.Type, rect, x, y);

            if (tile.GearLoot.Count > 0)
                assets.DrawGear(g, rect, tile.GearLoot[^1]);
            else if (tile.HasItem)
                assets.DrawPotion(g, rect);

            Monster? monster = dungeon.MonsterAt(p);
            if (monster != null)
                assets.DrawMonster(g, rect, monster);
        }

        assets.DrawPlayer(
            g,
            new Rectangle(player.Position.X * TileSize, player.Position.Y * TileSize, TileSize, TileSize),
            playerFacing,
            player.Alive);

        if (runOver) DrawDeathOverlay(g);
    }

    private void DrawDeathOverlay(Graphics g)
    {
        using SolidBrush veil = new(Color.FromArgb(155, 0, 0, 0));
        g.FillRectangle(veil, 0, 0, MapWidth, MapHeight);
        using Font title = new("Segoe UI", 28, FontStyle.Bold);
        using Font body = new("Segoe UI", 12);
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
        g.DrawString("Authored SNES assets • 8x8 dungeon tiles • 32x32 character art • nearest-neighbor rendering", small, muted, 12, y + 124);
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
