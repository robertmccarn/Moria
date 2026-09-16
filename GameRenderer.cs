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
    private int lastRenderedLevel = -1;
    private long levelUpPulseStart;

    private int CameraX
    {
        get
        {
            int visibleColumns = MapWidth / TileSize;
            int maxCamera = Math.Max(0, Dungeon.Width - visibleColumns);
            int desired = player.Position.X - visibleColumns / 2;
            return Math.Clamp(desired, 0, maxCamera);
        }
    }

    private void DrawMap(Graphics g)
    {
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SmoothingMode = SmoothingMode.None;

        Color depthBackground = DepthBackgroundColor(player.DungeonLevel);
        using SolidBrush mapBackground = new(depthBackground);
        g.FillRectangle(mapBackground, 0, 0, MapWidth, MapHeight);

        if (lastRenderedLevel < 0)
            lastRenderedLevel = player.Level;
        else if (player.Level > lastRenderedLevel)
        {
            lastRenderedLevel = player.Level;
            levelUpPulseStart = Environment.TickCount64;
        }

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

        int cameraX = CameraX;
        int firstColumn = cameraX;
        int lastColumn = Math.Min(Dungeon.Width - 1, cameraX + MapWidth / TileSize);

        for (int y = 0; y < Dungeon.Height; y++)
        for (int x = firstColumn; x <= lastColumn; x++)
        {
            Position p = new(y, x);
            Tile tile = dungeon[p];
            Rectangle rect = ScreenRect(x, y, cameraX);

            if (!tile.Seen)
            {
                using SolidBrush unseen = new(depthBackground);
                g.FillRectangle(unseen, rect);
                continue;
            }

            assets.DrawTile(g, tile.Type, rect, x, y);
            DrawDepthTint(g, rect, player.DungeonLevel);

            if (tile.GearLoot.Count > 0)
                assets.DrawGear(g, CenteredSpriteRect(rect, 28), tile.GearLoot[^1]);
            else if (tile.HasItem)
                assets.DrawPotion(g, CenteredSpriteRect(rect, 26));

            Monster? monster = dungeon.MonsterAt(p);
            if (monster != null)
            {
                assets.DrawMonster(g, CenteredSpriteRect(rect, monster.IsBoss ? 48 : 28), monster);
                DrawMonsterMarker(g, rect, monster);
            }
        }

        Rectangle playerTile = ScreenRect(player.Position.X, player.Position.Y, cameraX);
        Rectangle playerRect = CenteredSpriteRect(playerTile, 30);

        DrawPlayerEnergy(g, playerTile);
        assets.DrawPlayer(g, playerRect, playerFacing, player.Alive);

        if (runOver) DrawDeathOverlay(g);
    }

    private static Color DepthBackgroundColor(int level)
    {
        double progress = Math.Clamp((level - 1) / (double)(Dungeon.MaximumDepth - 1), 0.0, 1.0);
        int red = 4 + (int)Math.Round(progress * 22);
        int green = 5 - (int)Math.Round(progress * 4);
        int blue = 7 - (int)Math.Round(progress * 4);
        return Color.FromArgb(red, Math.Max(1, green), Math.Max(2, blue));
    }

    private static void DrawDepthTint(Graphics g, Rectangle rect, int level)
    {
        double progress = Math.Clamp((level - 1) / (double)(Dungeon.MaximumDepth - 1), 0.0, 1.0);
        int alpha = (int)Math.Round(progress * 82);
        if (alpha <= 0) return;
        using SolidBrush tint = new(Color.FromArgb(alpha, 150, 10, 10));
        g.FillRectangle(tint, rect);
    }

    private static Rectangle ScreenRect(int worldX, int worldY, int cameraX) =>
        new((worldX - cameraX) * TileSize, worldY * TileSize, TileSize, TileSize);

    private void DrawPlayerEnergy(Graphics g, Rectangle tile)
    {
        long now = Environment.TickCount64;
        double pulseAge = levelUpPulseStart <= 0 ? double.MaxValue : now - levelUpPulseStart;
        double pulse = pulseAge < 1100 ? 1.0 - pulseAge / 1100.0 : 0.0;
        double pulseWave = pulse > 0 ? Math.Sin((1.0 - pulse) * Math.PI * 3.0) * pulse : 0.0;

        int level = Math.Max(1, player.Level);
        Color energy = HslToColor(EnergyHue(level), 0.76 + Math.Min(0.14, level / 60.0), 0.54 + Math.Min(0.08, level / 100.0));

        int baseWidth = 34 + Math.Min(12, level / 3);
        int baseHeight = 11 + Math.Min(5, level / 5);
        int width = (int)Math.Round(baseWidth + pulseWave * 12);
        int height = (int)Math.Round(baseHeight + pulseWave * 5);
        int centerX = tile.X + tile.Width / 2;
        int centerY = tile.Y + tile.Height - 6;

        int glowAlpha = 20 + Math.Min(24, level) + (int)(Math.Max(0, pulseWave) * 75);
        int coreAlpha = 34 + Math.Min(26, level) + (int)(Math.Max(0, pulseWave) * 105);

        using SolidBrush outer = new(Color.FromArgb(Math.Clamp(glowAlpha / 2, 8, 90), energy.R, energy.G, energy.B));
        using SolidBrush middle = new(Color.FromArgb(Math.Clamp(glowAlpha, 15, 135), energy.R, energy.G, energy.B));
        using SolidBrush core = new(Color.FromArgb(Math.Clamp(coreAlpha, 20, 180), energy.R, energy.G, energy.B));

        g.FillEllipse(outer, new Rectangle(centerX - width - 8, centerY - height / 2 - 3, width * 2 + 16, height + 7));
        g.FillEllipse(middle, new Rectangle(centerX - width, centerY - height / 2, width * 2, height));
        g.FillEllipse(core, new Rectangle(centerX - width / 2, centerY - height / 3, width, Math.Max(4, height / 2)));

        if (pulse > 0)
        {
            int ringWidth = (int)Math.Round(24 + (1.0 - pulse) * 26);
            int ringHeight = (int)Math.Round(9 + (1.0 - pulse) * 9);
            int ringAlpha = (int)Math.Round(105 * pulse);
            using Pen ring = new(Color.FromArgb(Math.Clamp(ringAlpha, 0, 120), energy.R, energy.G, energy.B), 2f);
            g.DrawEllipse(ring, new Rectangle(centerX - ringWidth, centerY - ringHeight / 2, ringWidth * 2, ringHeight));
        }
    }

    private static double EnergyHue(int level)
    {
        double progress = Math.Clamp((level - 1) / 24.0, 0.0, 1.0);
        return 195.0 + progress * 85.0;
    }

    private static Color HslToColor(double hue, double saturation, double lightness)
    {
        hue %= 360.0;
        if (hue < 0) hue += 360.0;
        double c = (1.0 - Math.Abs(2.0 * lightness - 1.0)) * saturation;
        double x = c * (1.0 - Math.Abs((hue / 60.0) % 2.0 - 1.0));
        double m = lightness - c / 2.0;
        double r;
        double g;
        double b;

        if (hue < 60) (r, g, b) = (c, x, 0);
        else if (hue < 120) (r, g, b) = (x, c, 0);
        else if (hue < 180) (r, g, b) = (0, c, x);
        else if (hue < 240) (r, g, b) = (0, x, c);
        else if (hue < 300) (r, g, b) = (x, 0, c);
        else (r, g, b) = (c, 0, x);

        return Color.FromArgb(255, (int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
    }

    private static Rectangle CenteredSpriteRect(Rectangle tile, int size)
    {
        int x = tile.X + (tile.Width - size) / 2;
        int y = tile.Y + (tile.Height - size) / 2;
        return new Rectangle(x, y, size, size);
    }

    private static void DrawMonsterMarker(Graphics g, Rectangle tile, Monster monster)
    {
        int markerSize = monster.IsBoss ? 8 : 6;
        using SolidBrush marker = new(Color.FromArgb(monster.IsBoss ? 235 : 210, 190, 35, 35));
        g.FillEllipse(marker, tile.X + TileSize - markerSize - 3, tile.Y + 3, markerSize, markerSize);

        using Font font = new("Segoe UI", monster.IsBoss ? 8.5f : 7.5f, FontStyle.Bold);
        using SolidBrush text = new(Color.FromArgb(235, 235, 235));
        SizeF size = g.MeasureString(monster.Name, font);
        float labelX = tile.X + (TileSize - size.Width) / 2f;
        float labelY = tile.Y - size.Height - 2;
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
        using Font title = new("Segoe UI", 42, FontStyle.Bold);
        using Font body = new("Segoe UI", 16);
        using SolidBrush text = new(Color.Gainsboro);
        using SolidBrush gold = new(Color.FromArgb(255, 215, 90));
        g.DrawString("YOU DIED", title, text, 720, 280);
        g.DrawString($"Dungeon level {player.DungeonLevel}    Run {player.RunsCompleted}", body, text, 710, 340);
        g.DrawString($"+{LastRunReward} Legacy Gold", body, gold, 760, 375);
        g.DrawString("ENTER / SPACE / N  New Run", body, text, 700, 430);
        g.DrawString("ESC  Quit", body, text, 820, 465);
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
        using Font title = new("Segoe UI", 12, FontStyle.Bold);
        using Font normal = new("Segoe UI", 10.5f);
        using Font small = new("Segoe UI", 9.5f);
        using Font tiny = new("Segoe UI", 8.5f);

        g.FillRectangle(background, 0, y, ClientSize.Width, StatusHeight);

        Rectangle stats = new(10, y + 10, 600, 125);
        Rectangle action = new(620, y + 10, 850, 125);
        Rectangle info = new(1480, y + 10, 430, 125);
        g.FillRectangle(panel, stats);
        g.FillRectangle(panel, action);
        g.FillRectangle(panel, info);
        g.DrawRectangle(border, stats);
        g.DrawRectangle(border, action);
        g.DrawRectangle(border, info);

        g.DrawString($"{player.Name}   LV {player.Level}", title, text, 22, y + 18);
        g.DrawString($"Dungeon {player.DungeonLevel}   Run {player.RunsCompleted + (runOver ? 0 : 1)}", small, muted, 22, y + 43);

        int hp = Math.Max(0, player.Hp);
        int maxHp = Math.Max(1, player.TotalMaxHp);
        Rectangle hpBackRect = new(22, y + 67, 220, 16);
        g.FillRectangle(hpBack, hpBackRect);
        g.FillRectangle(hpFill, new Rectangle(hpBackRect.X, hpBackRect.Y, hpBackRect.Width * hp / maxHp, hpBackRect.Height));
        g.DrawRectangle(border, hpBackRect);
        g.DrawString($"HP {hp}/{maxHp}", tiny, text, hpBackRect.X + 8, hpBackRect.Y + 2);

        int xpToNext = Math.Max(1, player.Level * 100);
        int xpProgress = Math.Clamp(player.Experience, 0, xpToNext);
        Rectangle xpBackRect = new(260, y + 67, 180, 16);
        g.FillRectangle(hpBack, xpBackRect);
        g.FillRectangle(xpFill, new Rectangle(xpBackRect.X, xpBackRect.Y, xpBackRect.Width * xpProgress / xpToNext, xpBackRect.Height));
        g.DrawRectangle(border, xpBackRect);
        g.DrawString($"XP {player.Experience}/{xpToNext}", tiny, text, xpBackRect.X + 8, xpBackRect.Y + 2);

        g.DrawString($"ATK {player.TotalAttack}   ARM {player.TotalArmorClass}", normal, accent, 22, y + 95);
        int potionCount = player.Inventory.Count(i => i.Kind == ItemKind.Potion);
        g.DrawString($"Gold {player.Gold}   Legacy {player.PermanentGold}   Potions {potionCount}", small, gold, 22, y + 114);

        g.DrawString("LATEST ACTION", tiny, muted, 636, y + 18);
        g.DrawString(message, normal, text, 636, y + 42);
        g.DrawString($"Weapon: {player.Weapon?.Name ?? "None"}", small, accent, 636, y + 70);
        g.DrawString($"Armor: {player.Armor?.Name ?? "None"}    Ring: {player.Ring?.Name ?? "None"}", small, accent, 636, y + 94);
        g.DrawString("F  Chat Log", tiny, gold, 636, y + 116);

        g.DrawString("CONTROLS", tiny, muted, 1496, y + 18);
        g.DrawString("WASD / ARROWS", small, text, 1496, y + 40);
        g.DrawString("Move", tiny, muted, 1630, y + 41);
        g.DrawString("Q", small, gold, 1496, y + 62);
        g.DrawString("Potion", tiny, muted, 1520, y + 63);
        g.DrawString("E", small, gold, 1496, y + 84);
        g.DrawString("Interact / Inventory", tiny, muted, 1520, y + 85);
        g.DrawString("F", small, gold, 1496, y + 106);
        g.DrawString("Chat Log", tiny, muted, 1520, y + 107);

        using Pen divider = new(Color.FromArgb(45, 47, 55));
        g.DrawLine(divider, 10, y + 145, ClientSize.Width - 10, y + 145);
        g.DrawString("AUTO LOOT", tiny, gold, 16, y + 154);
        g.DrawString("Loot is collected automatically and better gear equips itself.  E descends when standing on stairs.", small, text, 94, y + 153);
        g.DrawString("ESC quit", small, muted, ClientSize.Width - 72, y + 153);
        g.DrawString("SNES art • fog of war • turn-based combat • 1920 × 1080", tiny, muted, 16, y + 178);
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
