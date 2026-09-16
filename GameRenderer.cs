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
    private int lastRenderedHp = -1;
    private int lastRenderedXp = -1;
    private long hpFeedbackStart;
    private long xpFeedbackStart;

    private static readonly Color HudBackground = Color.FromArgb(10, 11, 16);
    private static readonly Color HudPanel = Color.FromArgb(20, 22, 29);
    private static readonly Color HudPanelInset = Color.FromArgb(16, 18, 24);
    private static readonly Color HudBorder = Color.FromArgb(57, 61, 73);
    private static readonly Color HudBorderBright = Color.FromArgb(82, 76, 61);
    private static readonly Color HudText = Color.FromArgb(226, 225, 216);
    private static readonly Color HudMuted = Color.FromArgb(145, 150, 162);
    private static readonly Color HudGold = Color.FromArgb(226, 183, 76);
    private static readonly Color HudHp = Color.FromArgb(194, 70, 65);
    private static readonly Color HudXp = Color.FromArgb(86, 151, 207);
    private static readonly Color HudAccent = Color.FromArgb(112, 194, 214);
    private static readonly Color HudGood = Color.FromArgb(101, 190, 121);

    private int CameraX
    {
        get
        {
            int visibleColumns = MapWidth / TileSize;
            int maxCamera = Math.Max(0, dungeon.Width - visibleColumns);
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

        if (lastRenderedHp < 0)
            lastRenderedHp = player.Hp;
        else if (player.Hp != lastRenderedHp)
        {
            lastRenderedHp = player.Hp;
            hpFeedbackStart = Environment.TickCount64;
        }

        if (lastRenderedXp < 0)
            lastRenderedXp = player.Experience;
        else if (player.Experience != lastRenderedXp)
        {
            lastRenderedXp = player.Experience;
            xpFeedbackStart = Environment.TickCount64;
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
        int lastColumn = Math.Min(dungeon.Width - 1, cameraX + MapWidth / TileSize);

        for (int y = 0; y < dungeon.Height; y++)
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
        int margin = 12;
        int gap = 10;
        int hudHeight = StatusHeight - 24;
        int panelWidth = (ClientSize.Width - margin * 2 - gap * 2);
        int leftWidth = 550;
        int rightWidth = 360;
        int centerWidth = panelWidth - leftWidth - rightWidth;

        using SolidBrush background = new(HudBackground);
        using SolidBrush panel = new(HudPanel);
        using SolidBrush inset = new(HudPanelInset);
        using Pen border = new(HudBorder, 1f);
        using Pen brightBorder = new(HudBorderBright, 1f);
        using SolidBrush text = new(HudText);
        using SolidBrush muted = new(HudMuted);
        using SolidBrush gold = new(HudGold);
        using SolidBrush accent = new(HudAccent);
        using Font header = new("Segoe UI", 12.5f, FontStyle.Bold);
        using Font body = new("Segoe UI", 12f);
        using Font bodyBold = new("Segoe UI", 12f, FontStyle.Bold);
        using Font small = new("Segoe UI", 10.5f);
        using Font smallBold = new("Segoe UI", 10.5f, FontStyle.Bold);

        g.FillRectangle(background, 0, y, ClientSize.Width, StatusHeight);

        Rectangle left = new(margin, y + 8, leftWidth, hudHeight);
        Rectangle center = new(left.Right + gap, y + 8, centerWidth, hudHeight);
        Rectangle right = new(center.Right + gap, y + 8, rightWidth, hudHeight);

        DrawHudPanel(g, left, "PLAYER", panel, border, brightBorder);
        DrawHudPanel(g, center, "LATEST ACTION", panel, border, brightBorder);
        DrawHudPanel(g, right, "CONTROLS", panel, border, brightBorder);

        int x = left.X + 16;
        int contentY = left.Y + 34;
        g.DrawString($"{player.Name}   LV {player.Level}", bodyBold, text, x, contentY);
        g.DrawString($"DUNGEON {player.DungeonLevel}   •   RUN {player.RunsCompleted + (runOver ? 0 : 1)}", smallBold, muted, x, contentY + 25);

        int hp = Math.Max(0, player.Hp);
        int maxHp = Math.Max(1, player.TotalMaxHp);
        int xpToNext = Math.Max(1, player.Level * 100);
        DrawHudBar(g, x, contentY + 52, 245, 24, hp, maxHp, HudHp, "♥", $"HP  {hp} / {maxHp}", text, inset, border, bodyBold);
        DrawHudBar(g, x + 258, contentY + 52, 245, 24, Math.Clamp(player.Experience, 0, xpToNext), xpToNext, HudXp, "◆", $"XP  {player.Experience} / {xpToNext}", text, inset, border, bodyBold);

        long now = Environment.TickCount64;
        DrawFeedbackStat(g, x, contentY + 88, "⚔", "ATK", player.TotalAttack.ToString(), HudAccent, body, bodyBold, now - xpFeedbackStart < 650);
        DrawFeedbackStat(g, x + 130, contentY + 88, "◆", "ARM", player.TotalArmorClass.ToString(), HudAccent, body, bodyBold, false);
        DrawFeedbackStat(g, x + 270, contentY + 88, "●", "GOLD", player.Gold.ToString(), HudGold, body, bodyBold, false);
        DrawFeedbackStat(g, x + 405, contentY + 88, "!", "POTIONS", player.Inventory.Count(i => i.Kind == ItemKind.Potion).ToString(), HudGood, body, bodyBold, false);

        int centerX = center.X + 18;
        int centerY = center.Y + 35;
        Color actionColor = ActionColor(message);
        string icon = ActionIcon(message);
        using SolidBrush actionAccent = new(actionColor);
        g.DrawString(icon, header, actionAccent, centerX, centerY);

        Rectangle actionTextRect = new(centerX + 32, centerY - 1, center.Width - 52, 58);
        using StringFormat actionFormat = new() { Trimming = StringTrimming.EllipsisCharacter, Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
        g.DrawString(message, bodyBold, text, actionTextRect, actionFormat);

        using Pen divider = new(Color.FromArgb(45, 48, 58));
        g.DrawLine(divider, centerX, centerY + 56, center.Right - 18, centerY + 56);
        g.DrawString($"⚔  {player.Weapon?.Name ?? "No weapon"}", small, accent, centerX, centerY + 68);
        g.DrawString($"◆  {player.Armor?.Name ?? "No armor"}", small, accent, centerX, centerY + 91);
        g.DrawString($"○  {player.Ring?.Name ?? "No ring"}", small, accent, centerX, centerY + 114);
        g.DrawString("F  OPEN CHAT LOG", smallBold, gold, centerX, centerY + 137);

        int controlX = right.X + 16;
        int controlY = right.Y + 35;
        DrawControl(g, controlX, controlY, "WASD / ARROWS", "Move", text, muted, gold, smallBold, small);
        DrawControl(g, controlX, controlY + 27, "Q", "Drink Potion", text, muted, gold, smallBold, small);
        DrawControl(g, controlX, controlY + 54, "E", "Interact / Inventory", text, muted, gold, smallBold, small);
        DrawControl(g, controlX, controlY + 81, "F", "Chat Log", text, muted, gold, smallBold, small);
        DrawControl(g, controlX, controlY + 108, "ESC", "Quit", text, muted, gold, smallBold, small);

        using Pen rule = new(Color.FromArgb(49, 52, 62));
        g.DrawLine(rule, right.X + 16, right.Bottom - 45, right.Right - 16, right.Bottom - 45);
        g.DrawString("AUTO LOOT", smallBold, gold, controlX, right.Bottom - 36);
        g.DrawString("Loot is collected automatically.", small, muted, controlX, right.Bottom - 20);
        g.DrawString("Better gear equips itself.", small, muted, controlX + 174, right.Bottom - 20);

        using SolidBrush footerBrush = new(Color.FromArgb(105, 109, 120));
        using Font footerFont = new("Segoe UI", 8.5f, FontStyle.Bold);
        g.DrawString("1920 × 1080  •  TURN-BASED  •  FOG OF WAR", footerFont, footerBrush, 16, ClientSize.Height - 14);
    }

    private static void DrawHudPanel(Graphics g, Rectangle rect, string title, SolidBrush panel, Pen border, Pen brightBorder)
    {
        g.FillRectangle(panel, rect);
        g.DrawRectangle(border, rect);
        Rectangle inner = new(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6);
        g.DrawRectangle(new Pen(Color.FromArgb(31, 34, 43), 1f), inner);
        using Font font = new("Segoe UI", 12.5f, FontStyle.Bold);
        using SolidBrush titleBrush = new(HudGold);
        g.DrawString(title, font, titleBrush, rect.X + 16, rect.Y + 10);
        using Pen line = new(Color.FromArgb(76, 68, 52), 1f);
        g.DrawLine(line, rect.X + 16, rect.Y + 29, rect.Right - 16, rect.Y + 29);
    }

    private static void DrawHudBar(Graphics g, int x, int y, int width, int height, int value, int maximum, Color fill, string icon, string label, SolidBrush text, SolidBrush inset, Pen border, Font font)
    {
        Rectangle outer = new(x, y, width, height);
        g.FillRectangle(inset, outer);
        g.DrawRectangle(border, outer);
        int fillWidth = maximum <= 0 ? 0 : Math.Clamp(width * value / maximum, 0, width);
        if (fillWidth > 0)
        {
            using SolidBrush bar = new(Color.FromArgb(150, fill.R, fill.G, fill.B));
            g.FillRectangle(bar, x + 1, y + 1, Math.Max(1, fillWidth - 1), height - 2);
        }
        g.DrawString($"{icon}  {label}", font, text, x + 8, y + 3);
    }

    private static void DrawFeedbackStat(Graphics g, int x, int y, string icon, string label, string value, Color color, Font body, Font bold, bool pulse)
    {
        using SolidBrush brush = new(color);
        g.DrawString(icon, body, brush, x, y);
        g.DrawString(label, new Font(body, FontStyle.Bold), brush, x + 21, y);
        int offset = label.Length >= 5 ? 74 : 58;
        if (pulse)
        {
            using SolidBrush flash = new(Color.FromArgb(100, 235, 235, 235));
            g.DrawString(value, bold, flash, x + offset, y - 1);
        }
        else
            g.DrawString(value, bold, HudTextBrush(), x + offset, y - 1);
    }

    private static SolidBrush HudTextBrush() => new(HudText);

    private static void DrawControl(Graphics g, int x, int y, string key, string action, SolidBrush text, SolidBrush muted, SolidBrush gold, Font keyFont, Font actionFont)
    {
        g.DrawString(key, keyFont, gold, x, y);
        g.DrawString("—", actionFont, muted, x + 112, y + 1);
        g.DrawString(action, actionFont, text, x + 132, y + 1);
    }

    private static string ActionIcon(string action)
    {
        string lower = action.ToLowerInvariant();
        if (lower.Contains("hit") || lower.Contains("cleave") || lower.Contains("miss") || lower.Contains("attack")) return "⚔";
        if (lower.Contains("loot") || lower.Contains("equip") || lower.Contains("gold")) return "◆";
        if (lower.Contains("potion") || lower.Contains("recover")) return "!";
        if (lower.Contains("cannot") || lower.Contains("wall") || lower.Contains("trap")) return "✖";
        if (lower.Contains("level up")) return "★";
        if (lower.Contains("descend") || lower.Contains("stairs")) return "▼";
        return "•";
    }

    private static Color ActionColor(string action)
    {
        string lower = action.ToLowerInvariant();
        if (lower.Contains("hit") || lower.Contains("cleave") || lower.Contains("damage") || lower.Contains("trap")) return HudHp;
        if (lower.Contains("loot") || lower.Contains("equip") || lower.Contains("gold")) return HudGold;
        if (lower.Contains("recover") || lower.Contains("healing")) return HudGood;
        if (lower.Contains("level up")) return HudGold;
        if (lower.Contains("cannot") || lower.Contains("wall") || lower.Contains("miss")) return HudMuted;
        return HudAccent;
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