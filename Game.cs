using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Moria.Core;
using Moria.Entities;
using Moria.Items;
using Moria.World;

namespace Moria;

public sealed class Game : Form
{
    private const int TileSize = 22;
    private const int MapWidth = Dungeon.Width * TileSize;
    private const int MapHeight = Dungeon.Height * TileSize;
    private const int StatusHeight = 112;

    private readonly Random random = new();
    private readonly Timer redrawTimer;
    private Dungeon dungeon = null!;
    private Player player = null!;
    private string message = "Welcome to Moria.";
    private bool running = true;
    private bool started;

    public Game()
    {
        Text = "Moria";
        ClientSize = new Size(MapWidth, MapHeight + StatusHeight);
        BackColor = Color.FromArgb(12, 12, 16);
        ForeColor = Color.Gainsboro;
        DoubleBuffered = true;
        KeyPreview = true;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        KeyDown += OnKeyDown;
        FormClosed += (_, _) => running = false;

        redrawTimer = new Timer { Interval = 50 };
        redrawTimer.Tick += (_, _) => Invalidate();
        redrawTimer.Start();
        Shown += (_, _) => BeginGame();
    }

    private void BeginGame()
    {
        using Form dialog = new()
        {
            Text = "New Character",
            ClientSize = new Size(360, 130),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false
        };

        Label label = new() { Text = "Character name:", Left = 18, Top = 18, AutoSize = true };
        TextBox nameBox = new() { Left = 18, Top = 44, Width = 324 };
        Button button = new() { Text = "Enter Moria", Left = 230, Top = 78, Width = 112, DialogResult = DialogResult.OK };
        dialog.Controls.AddRange([label, nameBox, button]);
        dialog.AcceptButton = button;

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            Close();
            return;
        }

        string name = nameBox.Text.Trim();
        if (name.Length == 0) name = "Adventurer";

        dungeon = new Dungeon(random.Next());
        player = new Player(name, new Position(1, 1));
        dungeon.Generate(player.DungeonLevel);
        player.Position = dungeon.UpStairs;
        dungeon.RevealAround(player.Position, 8);
        started = true;
        message = $"Welcome, {player.Name}. Explore the dungeon.";
        Focus();
        Invalidate();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!started || !running || !player.Alive) return;

        Direction direction = e.KeyCode switch
        {
            Keys.Up or Keys.K => Direction.Up,
            Keys.Down or Keys.J => Direction.Down,
            Keys.Left or Keys.H => Direction.Left,
            Keys.Right or Keys.L => Direction.Right,
            Keys.Y => Direction.Up,
            Keys.U => Direction.Right,
            Keys.B => Direction.Left,
            Keys.N => Direction.Down,
            _ => Direction.None
        };

        if (direction != Direction.None)
        {
            MovePlayer(direction);
            e.Handled = true;
        }
        else
        {
            switch (e.KeyCode)
            {
                case Keys.I: ShowInventory(); break;
                case Keys.G: Pickup(); break;
                case Keys.E: Eat(); break;
                case Keys.Q: Drink(); break;
                case Keys.S: Save(); break;
                case Keys.X:
                case Keys.Escape: running = false; Close(); break;
                case Keys.OemPeriod: Descend(); break;
            }
        }

        if (running && player.Alive && e.KeyCode != Keys.I) MonstersAct();
        Invalidate();
    }

    private void MovePlayer(Direction direction)
    {
        Position target = player.Position.Step(direction);
        if (!dungeon.IsWalkable(target)) { message = "You cannot move there."; return; }
        Monster? monster = dungeon.MonsterAt(target);
        if (monster != null) { Attack(monster); return; }
        player.Position = target;
        dungeon.RevealAround(target, 6);
        if (dungeon[target].Type == TileType.Trap)
        {
            int damage = random.Next(1, 5);
            player.Hp -= damage;
            message = $"A trap wounds you for {damage}!";
        }
        else if (target == dungeon.DownStairs)
            message = "You stand on the stairs leading deeper into Moria.";
    }

    private void Attack(Monster monster)
    {
        int roll = random.Next(1, 21) + player.Attack;
        if (roll >= monster.ArmorClass)
        {
            int damage = random.Next(1, 7) + player.Attack / 2;
            monster.Hp -= damage;
            message = $"You hit the {monster.Name} for {damage}.";
            if (!monster.Alive)
            {
                player.GainExperience(monster.ExperienceValue);
                message += $" The {monster.Name} dies. +{monster.ExperienceValue} XP.";
            }
        }
        else message = $"You miss the {monster.Name}.";
    }

    private void MonstersAct()
    {
        foreach (Monster monster in dungeon.Monsters.Where(m => m.Alive).ToList())
        {
            int dy = player.Position.Y - monster.Position.Y;
            int dx = player.Position.X - monster.Position.X;
            if (Math.Abs(dy) + Math.Abs(dx) > 10) continue;

            Position target = monster.Position;
            if (Math.Abs(dy) >= Math.Abs(dx)) target = new Position(target.Y + Math.Sign(dy), target.X);
            else target = new Position(target.Y, target.X + Math.Sign(dx));

            if (target == player.Position)
            {
                int roll = random.Next(1, 21) + monster.Attack;
                if (roll >= player.ArmorClass)
                {
                    int damage = random.Next(1, 5) + monster.Level / 2;
                    player.Hp -= damage;
                    message = $"The {monster.Name} hits you for {damage}!";
                }
                else message = $"The {monster.Name} misses you.";
            }
            else if (dungeon.IsWalkable(target) && dungeon.MonsterAt(target) == null)
                monster.Position = target;
        }
    }

    private void Descend()
    {
        if (player.Position != dungeon.DownStairs) { message = "There are no stairs here."; return; }
        player.DungeonLevel++;
        dungeon.Generate(player.DungeonLevel);
        player.Position = dungeon.UpStairs;
        dungeon.RevealAround(player.Position, 8);
        message = $"You descend to dungeon level {player.DungeonLevel}.";
    }

    private void Pickup()
    {
        if (!dungeon[player.Position].HasItem) { message = "There is nothing here."; return; }
        dungeon[player.Position].HasItem = false;
        player.Inventory.Add(new Item("Potion of Healing", '!', 50, 0, 0, ItemKind.Potion));
        message = "You pick up a Potion of Healing.";
    }

    private void Eat()
    {
        if (player.Food <= 0) { message = "You have no food."; return; }
        player.Food--;
        player.Hp = Math.Min(player.MaxHp, player.Hp + 3);
        message = "You eat some food.";
    }

    private void Drink()
    {
        Item? potion = player.Inventory.FirstOrDefault(i => i.Kind == ItemKind.Potion);
        if (potion == null) { message = "You have no potions."; return; }
        player.Inventory.Remove(potion);
        player.Hp = player.MaxHp;
        message = "You drink a Potion of Healing.";
    }

    private void ShowInventory()
    {
        string inventory = player.Inventory.Count == 0
            ? "Your pack is empty."
            : string.Join(Environment.NewLine, player.Inventory.Select(i => $"{i.Name}    {i.Value} gp"));
        MessageBox.Show(this, inventory, "Inventory", MessageBoxButtons.OK, MessageBoxIcon.None);
        Focus();
    }

    private void Save()
    {
        string data = string.Join('|', player.Name, player.Level, player.Experience, player.Hp, player.MaxHp, player.Gold, player.DungeonLevel);
        File.WriteAllText("moria.sav", data);
        message = "Game saved to moria.sav.";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!started) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        DrawMap(e.Graphics);
        DrawStatus(e.Graphics);
    }

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
                using SolidBrush unseen = new(Color.FromArgb(8, 8, 11));
                g.FillRectangle(unseen, rect);
                continue;
            }

            DrawTile(g, tile.Type, rect);
            if (tile.HasItem) DrawItem(g, rect);
            Monster? monster = dungeon.MonsterAt(p);
            if (monster != null) DrawMonster(g, rect, monster);
        }

        DrawPlayer(g, new Rectangle(player.Position.X * TileSize, player.Position.Y * TileSize, TileSize, TileSize));
    }

    private static void DrawTile(Graphics g, TileType type, Rectangle rect)
    {
        Color fill = type switch
        {
            TileType.Floor => Color.FromArgb(45, 45, 50),
            TileType.Wall => Color.FromArgb(27, 28, 34),
            TileType.Door => Color.FromArgb(82, 64, 44),
            TileType.StairsUp or TileType.StairsDown => Color.FromArgb(48, 55, 65),
            TileType.Trap => Color.FromArgb(57, 38, 44),
            _ => Color.FromArgb(12, 13, 17)
        };

        using SolidBrush brush = new(fill);
        g.FillRectangle(brush, rect);
        using Pen grid = new(Color.FromArgb(18, 19, 23));
        g.DrawRectangle(grid, rect);

        int cx = rect.X + rect.Width / 2;
        int cy = rect.Y + rect.Height / 2;
        using Pen detail = new(Color.FromArgb(125, 130, 140), 2f);

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

    private static void DrawPlayer(Graphics g, Rectangle rect)
    {
        using SolidBrush body = new(Color.FromArgb(225, 225, 230));
        g.FillEllipse(body, rect.X + 4, rect.Y + 3, rect.Width - 8, rect.Height - 6);
        using Pen outline = new(Color.FromArgb(100, 205, 255), 2f);
        g.DrawEllipse(outline, rect.X + 4, rect.Y + 3, rect.Width - 8, rect.Height - 6);
        using Pen weapon = new(Color.FromArgb(220, 220, 220), 2f);
        g.DrawLine(weapon, rect.X + 14, rect.Y + 7, rect.X + 19, rect.Y + 2);
    }

    private static void DrawMonster(Graphics g, Rectangle rect, Monster monster)
    {
        Color bodyColor = monster.Level >= 5 ? Color.FromArgb(175, 75, 85) : Color.FromArgb(150, 105, 75);
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

    private static void DrawItem(Graphics g, Rectangle rect)
    {
        using SolidBrush brush = new(Color.FromArgb(90, 170, 225));
        Point[] diamond = [
            new Point(rect.X + rect.Width / 2, rect.Y + 4),
            new Point(rect.X + rect.Width - 5, rect.Y + rect.Height / 2),
            new Point(rect.X + rect.Width / 2, rect.Y + rect.Height - 4),
            new Point(rect.X + 5, rect.Y + rect.Height / 2)
        ];
        g.FillPolygon(brush, diamond);
    }

    private void DrawStatus(Graphics g)
    {
        int y = MapHeight;
        using SolidBrush background = new(Color.FromArgb(18, 19, 24));
        g.FillRectangle(background, 0, y, ClientSize.Width, StatusHeight);
        using Font title = new("Segoe UI", 12, FontStyle.Bold);
        using Font normal = new("Segoe UI", 9);
        using Font controls = new("Segoe UI", 8.5f);
        using SolidBrush text = new(Color.Gainsboro);
        using SolidBrush muted = new(Color.FromArgb(155, 160, 170));

        g.DrawString($"{player.Name}   HP {player.Hp}/{player.MaxHp}   LV {player.Level}   XP {player.Experience}   Gold {player.Gold}   Food {player.Food}   Dungeon {player.DungeonLevel}", title, text, 12, y + 10);
        g.DrawString(message, normal, text, 12, y + 38);
        g.DrawString("Arrow keys / HJKL move    G get    I inventory    E eat    Q drink    . descend    S save    X / Esc quit", controls, muted, 12, y + 66);
    }
}
