using System.Drawing;
using Moria.Entities;
using Moria.Items;

namespace Moria.UI;

public sealed class UiRenderer
{
    public const int Height = 104;

    public void Draw(Graphics g, Player player, string message, bool runOver, bool victory, int lastRunReward)
    {
        int y = 256;
        using SolidBrush background = new(UiTheme.Background);
        g.FillRectangle(background, 0, y, 640, Height);

        Rectangle left = new(6, y + 6, 258, Height - 12);
        Rectangle center = new(270, y + 6, 240, Height - 12);
        Rectangle right = new(516, y + 6, 118, Height - 12);
        DrawPanel(g, left, "PLAYER");
        DrawPanel(g, center, "LATEST ACTION");
        DrawPanel(g, right, "CONTROLS");

        using Font title = new("Segoe UI", 8.5f, FontStyle.Bold);
        using Font body = new("Segoe UI", 8.2f, FontStyle.Bold);
        using Font small = new("Segoe UI", 6.8f);
        using SolidBrush text = new(UiTheme.Text);
        using SolidBrush muted = new(UiTheme.Muted);
        using SolidBrush gold = new(UiTheme.Gold);

        g.DrawString($"{player.Name}   LV {player.Level}", body, text, left.X + 10, left.Y + 23);
        g.DrawString($"DEPTH {player.DungeonLevel}   RUN {player.RunsCompleted + (runOver || victory ? 0 : 1)}", small, muted, left.X + 10, left.Y + 38);
        DrawBar(g, new Rectangle(left.X + 10, left.Y + 51, 116, 14), player.Hp, Math.Max(1, player.TotalMaxHp), UiTheme.Hp, "HP");
        DrawBar(g, new Rectangle(left.X + 132, left.Y + 51, 116, 14), player.Experience, Math.Max(1, player.Level * 100), UiTheme.Xp, "XP");
        DrawStat(g, left.X + 10, left.Y + 72, "ATK", player.TotalAttack, UiTheme.Accent, body);
        DrawStat(g, left.X + 70, left.Y + 72, "ARM", player.TotalArmorClass, UiTheme.Accent, body);
        DrawStat(g, left.X + 130, left.Y + 72, "GOLD", player.Gold, UiTheme.Gold, body);
        DrawStat(g, left.X + 202, left.Y + 72, "POT", player.Inventory.Count(i => i.Kind == ItemKind.Potion), UiTheme.Good, body);

        DrawAction(g, center, message, small, title, text, gold);
        DrawControls(g, right, small, gold, text);

        if (runOver || victory)
        {
            using SolidBrush overlay = new(Color.FromArgb(95, 0, 0, 0));
            g.FillRectangle(overlay, 0, 0, 640, 360);
            using Font result = new("Segoe UI", 18, FontStyle.Bold);
            using SolidBrush resultBrush = new(victory ? UiTheme.Gold : UiTheme.Negative);
            g.DrawString(victory ? "MORIA CONQUERED" : "YOU DIED", result, resultBrush, victory ? 232 : 260, 112);
            using Font hint = new("Segoe UI", 8, FontStyle.Bold);
            using SolidBrush hintBrush = new(UiTheme.Text);
            string footer = victory ? "BALROG SLAIN   •   ENTER FOR NEW RUN   •   ESC TO QUIT" : $"+{lastRunReward} LEGACY GOLD   •   ENTER FOR NEW RUN   •   ESC TO QUIT";
            g.DrawString(footer, hint, hintBrush, victory ? 215 : 184, 145);
        }
    }

    private static void DrawPanel(Graphics g, Rectangle rect, string title)
    {
        using SolidBrush panel = new(UiTheme.Panel);
        using Pen border = new(UiTheme.Border);
        using SolidBrush gold = new(UiTheme.Gold);
        using Font font = new("Segoe UI", 8.5f, FontStyle.Bold);
        g.FillRectangle(panel, rect);
        g.DrawRectangle(border, rect);
        g.DrawString(title, font, gold, rect.X + 10, rect.Y + 7);
        using Pen line = new(Color.FromArgb(75, 67, 49));
        g.DrawLine(line, rect.X + 10, rect.Y + 21, rect.Right - 10, rect.Y + 21);
    }

    private static void DrawBar(Graphics g, Rectangle rect, int value, int maximum, Color fill, string label)
    {
        using SolidBrush inset = new(UiTheme.PanelInset);
        using Pen border = new(UiTheme.Border);
        g.FillRectangle(inset, rect);
        g.DrawRectangle(border, rect);
        int width = Math.Clamp(rect.Width * value / Math.Max(1, maximum), 0, rect.Width);
        if (width > 0)
        {
            using SolidBrush brush = new(Color.FromArgb(170, fill.R, fill.G, fill.B));
            g.FillRectangle(brush, rect.X + 1, rect.Y + 1, Math.Max(1, width - 1), rect.Height - 2);
        }
        using Font font = new("Segoe UI", 6.8f, FontStyle.Bold);
        using SolidBrush text = new(UiTheme.Text);
        g.DrawString($"{label} {value}/{maximum}", font, text, rect.X + 5, rect.Y + 2);
    }

    private static void DrawStat(Graphics g, int x, int y, string label, int value, Color color, Font font)
    {
        DrawIcon(g, x, y + 2, label, color);
        using SolidBrush text = new(UiTheme.Text);
        g.DrawString($"{label}  {value}", font, text, x + 15, y);
    }

    private static void DrawIcon(Graphics g, int x, int y, string type, Color color)
    {
        using SolidBrush brush = new(color);
        switch (type)
        {
            case "ATK":
                using (Pen pen = new(color, 2))
                {
                    g.DrawLine(pen, x + 1, y + 7, x + 8, y);
                    g.DrawLine(pen, x + 6, y + 2, x + 9, y + 5);
                }
                break;
            case "ARM":
                g.FillRectangle(brush, x + 2, y, 7, 9);
                break;
            case "GOLD":
                g.FillEllipse(brush, x + 1, y + 1, 8, 8);
                break;
            default:
                g.FillRectangle(brush, x + 2, y + 1, 6, 8);
                break;
        }
    }

    private static void DrawAction(Graphics g, Rectangle rect, string message, Font small, Font title, SolidBrush text, SolidBrush gold)
    {
        string display = message.Length > 48 ? message[..45] + "..." : message;
        using SolidBrush accent = new(ActionColor(message));
        using SolidBrush muted = new(UiTheme.Muted);
        g.FillRectangle(accent, rect.X + 12, rect.Y + 28, 4, 30);
        g.DrawString(display, title, text, rect.X + 24, rect.Y + 27);
        g.DrawString("AUTO LOOT", small, gold, rect.X + 12, rect.Y + 69);
        g.DrawString("Gear auto-equips", small, muted, rect.X + 76, rect.Y + 69);
    }

    private static void DrawControls(Graphics g, Rectangle rect, Font small, SolidBrush gold, SolidBrush text)
    {
        string[] keys = ["WASD", "Q", "E", "F", "ESC"];
        string[] actions = ["MOVE", "POTION", "INTERACT", "LOG", "QUIT"];
        for (int i = 0; i < keys.Length; i++)
        {
            int y = rect.Y + 25 + i * 13;
            g.DrawString(keys[i], small, gold, rect.X + 8, y);
            g.DrawString(actions[i], small, text, rect.X + 48, y);
        }
    }

    private static Color ActionColor(string message)
    {
        string lower = message.ToLowerInvariant();
        if (lower.Contains("hit") || lower.Contains("damage") || lower.Contains("miss")) return UiTheme.Negative;
        if (lower.Contains("loot") || lower.Contains("gold") || lower.Contains("equip")) return UiTheme.Gold;
        if (lower.Contains("recover") || lower.Contains("healing")) return UiTheme.Good;
        return UiTheme.Accent;
    }
}
