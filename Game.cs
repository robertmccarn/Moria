using System.Drawing;
using System.Windows.Forms;
using Moria.Core;
using Moria.Entities;
using Moria.Items;
using Moria.World;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace Moria;

public sealed partial class Game : Form
{
    private const int TileSize = 40;
    private const int MapWidth = 1920;
    private const int MapHeight = 880;
    private const int StatusHeight = 200;
    private const string MetaFile = "moria.meta";

    private readonly Random random = new();
    private readonly WinFormsTimer redrawTimer;
    private readonly List<string> chatLog = new();
    private Dungeon dungeon = null!;
    private Player player = null!;
    private string message = "Welcome to Moria.";
    private bool running = true;
    private bool started;
    private bool runOver;
    private int lastRunReward;

    public int LastRunReward => lastRunReward;
    public IReadOnlyList<string> ChatLog => chatLog;

    public Game()
    {
        Text = "Moria — Roguelite";
        ClientSize = new Size(MapWidth, MapHeight + StatusHeight);
        BackColor = Color.FromArgb(9, 9, 12);
        ForeColor = Color.Gainsboro;
        DoubleBuffered = true;
        KeyPreview = true;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        KeyDown += OnKeyDown;
        FormClosed += (_, _) => running = false;

        redrawTimer = new WinFormsTimer { Interval = 50 };
        redrawTimer.Tick += (_, _) => Invalidate();
        redrawTimer.Start();
        Shown += (_, _) => BeginGame();
    }

    private void BeginGame()
    {
        string name = AskForName();
        if (name.Length == 0)
        {
            Close();
            return;
        }

        player = new Player(name, new Position(1, 1), (int)Math.Min(LoadLegacyGold(), int.MaxValue));
        StartRun();
        started = true;
        Focus();
        Invalidate();
    }

    private string AskForName()
    {
        using Form dialog = new()
        {
            Text = "New Adventurer",
            ClientSize = new Size(390, 150),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false
        };
        Label label = new() { Text = "Warrior name:", Left = 18, Top = 18, AutoSize = true };
        TextBox nameBox = new() { Left = 18, Top = 44, Width = 350 };
        Button button = new() { Text = "Enter Moria", Left = 252, Top = 88, Width = 116, DialogResult = DialogResult.OK };
        dialog.Controls.AddRange([label, nameBox, button]);
        dialog.AcceptButton = button;
        return dialog.ShowDialog(this) == DialogResult.OK ? nameBox.Text.Trim() : string.Empty;
    }

    private void StartRun()
    {
        dungeon = new Dungeon(random.Next());
        player.DungeonLevel = 1;
        player.Hp = player.TotalMaxHp;
        player.Mana = player.MaxMana;
        player.Gold = 100;
        player.Experience = 0;
        dungeon.Generate(player.DungeonLevel);
        player.Position = dungeon.UpStairs;
        dungeon.RevealAround(player.Position, 8);
        runOver = false;
        chatLog.Clear();
        SetMessage($"Run {player.RunsCompleted + 1} begins. Descend into Moria and survive.");
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!started || !running) return;

        if (runOver)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space or Keys.N) StartFreshRun();
            else if (e.KeyCode is Keys.Escape) { running = false; Close(); }
            return;
        }

        if (!player.Alive) { EndRun(); return; }

        Direction direction = e.KeyCode switch
        {
            Keys.Up or Keys.W => Direction.Up,
            Keys.Down or Keys.S => Direction.Down,
            Keys.Left or Keys.A => Direction.Left,
            Keys.Right or Keys.D => Direction.Right,
            _ => Direction.None
        };

        if (direction != Direction.None)
        {
            MovePlayer(direction);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else
        {
            switch (e.KeyCode)
            {
                case Keys.Q:
                    Drink();
                    break;
                case Keys.E:
                    Interact();
                    break;
                case Keys.F:
                    ShowChatLog();
                    break;
                case Keys.Escape:
                    running = false;
                    Close();
                    return;
            }
        }

        if (running && player.Alive && direction != Direction.None || e.KeyCode == Keys.Q || e.KeyCode == Keys.E)
        {
            if (running && player.Alive && (direction != Direction.None || e.KeyCode == Keys.Q || e.KeyCode == Keys.E))
                MonstersAct();
        }

        if (!player.Alive) EndRun();
        Invalidate();
    }

    private void MovePlayer(Direction direction)
    {
        Position target = player.Position.Step(direction);
        if (!dungeon.IsWalkable(target))
        {
            SetMessage("You cannot move there.");
            return;
        }

        Monster? monster = dungeon.MonsterAt(target);
        if (monster != null)
        {
            Attack(monster);
            return;
        }

        player.Position = target;
        dungeon.RevealAround(target, 6);

        Tile tile = dungeon[target];
        if (tile.Type == TileType.Trap)
        {
            int damage = random.Next(1, 5);
            player.Hp -= damage;
            SetMessage($"A trap wounds you for {damage}!");
        }
        else if (target == dungeon.DownStairs)
        {
            SetMessage("You found the stairs down. Press E to descend.");
        }

        AutoLoot();
    }

    private void Attack(Monster monster)
    {
        if (random.Next(1, 21) + player.TotalAttack >= monster.ArmorClass)
        {
            bool critical = random.Next(100) < 8 + player.Dexterity / 5;
            int damage = random.Next(1, 7) + Math.Max(1, player.TotalAttack / 2);
            if (critical) damage *= 2;
            monster.Hp -= damage;
            SetMessage(critical ? $"CRITICAL! You cleave the {monster.Name} for {damage}." : $"You hit the {monster.Name} for {damage}.");
            if (!monster.Alive)
            {
                int xp = monster.ExperienceValue;
                int gold = random.Next(4, 13) + player.DungeonLevel * 2;
                player.Gold += gold;
                int oldLevel = player.Level;
                player.GainExperience(xp);
                SetMessage($"The {monster.Name} dies. +{xp} XP, +{gold} gold.");
                if (player.Level > oldLevel)
                    SetMessage($"LEVEL UP! You are now level {player.Level}.");
                DropLoot(monster.Position, player.DungeonLevel);
                AutoLoot();
            }
        }
        else SetMessage($"You miss the {monster.Name}.");
    }

    private void DropLoot(Position position, int level)
    {
        Tile tile = dungeon[position];
        int roll = random.Next(100);
        if (roll < 45)
        {
            Gear gear = CreateLoot(level);
            tile.GearLoot.Add(gear);
            SetMessage($"Loot dropped: {RarityName(gear.Rarity)} {gear.Name}.");
        }
        else if (roll < 70)
        {
            tile.PotionCount++;
            SetMessage("A healing potion drops.");
        }
    }

    private Gear CreateLoot(int level)
    {
        int rarityRoll = random.Next(100);
        int rarity = rarityRoll < 60 ? 1 : rarityRoll < 88 ? 2 : rarityRoll < 98 ? 3 : 4;
        int power = Math.Max(1, level + rarity - 1);
        GearSlot slot = (GearSlot)random.Next(3);
        string adjective = rarity switch { 1 => "Worn", 2 => "Fine", 3 => "Runed", _ => "Mythic" };

        if (slot == GearSlot.Weapon)
            return new Gear($"{adjective} {WeaponName()}", '†', slot, 2 + power + rarity, 0, 0, 25 * power * rarity, rarity);
        if (slot == GearSlot.Armor)
            return new Gear($"{adjective} {ArmorName()}", '[', slot, 0, 1 + power / 2 + rarity, rarity >= 2 ? power * rarity : 0, 30 * power * rarity, rarity);
        return new Gear($"{adjective} Ring of the Depths", 'o', slot, rarity >= 3 ? rarity : random.Next(0, 2), random.Next(0, 2) + (rarity >= 2 ? 1 : 0), power * rarity, 40 * power * rarity, rarity);
    }

    private string WeaponName() => new[] { "War Axe", "Steel Falchion", "Goblin Cleaver", "Deepfang", "Starblade" }[random.Next(5)];
    private string ArmorName() => new[] { "Chain Hauberk", "Iron Cuirass", "Runed Mail", "Deepguard Plate", "Wyrmhide Armor" }[random.Next(5)];

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
                if (random.Next(1, 21) + monster.Attack >= player.TotalArmorClass)
                {
                    int damage = random.Next(1, 5) + monster.Level / 2;
                    player.Hp -= damage;
                    SetMessage($"The {monster.Name} hits you for {damage}!");
                }
                else SetMessage($"The {monster.Name} misses you.");
            }
            else if (dungeon.IsWalkable(target) && dungeon.MonsterAt(target) == null) monster.Position = target;
            if (!player.Alive) return;
        }
    }

    private void Interact()
    {
        if (player.Position == dungeon.DownStairs)
        {
            Descend();
            return;
        }

        ShowInventory();
    }

    private void Descend()
    {
        player.DungeonLevel++;
        player.Gold += 15 + player.DungeonLevel * 3;
        dungeon.Generate(player.DungeonLevel);
        player.Position = dungeon.UpStairs;
        dungeon.RevealAround(player.Position, 8);
        SaveProgress();
        SetMessage($"You descend to level {player.DungeonLevel}. Enemy power rises with depth.");
    }

    private void AutoLoot()
    {
        Tile tile = dungeon[player.Position];
        int gearCount = tile.GearLoot.Count;
        int potionCount = tile.PotionCount;
        if (gearCount == 0 && potionCount == 0) return;

        foreach (Gear gear in tile.GearLoot)
        {
            player.GearInventory.Add(gear);
            if (ShouldEquip(gear))
            {
                player.Equip(gear);
                SetMessage($"Auto-equipped {gear.Name}: {GearSummary(gear)}");
            }
        }
        tile.GearLoot.Clear();

        for (int i = 0; i < potionCount; i++)
            player.Inventory.Add(new Item("Potion of Healing", '!', 50, 0, 0, ItemKind.Potion));
        tile.PotionCount = 0;

        if (gearCount > 0 || potionCount > 0)
            SetMessage($"Auto-looted {gearCount + potionCount} item{(gearCount + potionCount == 1 ? "" : "s")}.");
    }

    private bool ShouldEquip(Gear gear)
    {
        Gear? current = player.Equipped(gear.Slot);
        return current == null || GearScore(gear) > GearScore(current);
    }

    private static int GearScore(Gear gear) => gear.AttackBonus * 5 + gear.ArmorBonus * 5 + gear.MaxHpBonus + gear.Rarity * 2;

    private static string GearSummary(Gear gear)
    {
        List<string> parts = new();
        if (gear.AttackBonus > 0) parts.Add($"+{gear.AttackBonus} ATK");
        if (gear.ArmorBonus > 0) parts.Add($"+{gear.ArmorBonus} ARM");
        if (gear.MaxHpBonus > 0) parts.Add($"+{gear.MaxHpBonus} HP");
        return string.Join(", ", parts);
    }

    private void Drink()
    {
        Item? potion = player.Inventory.FirstOrDefault(i => i.Kind == ItemKind.Potion);
        if (potion == null)
        {
            SetMessage("You have no potions.");
            return;
        }

        player.Inventory.Remove(potion);
        int healed = player.TotalMaxHp - player.Hp;
        player.Hp = player.TotalMaxHp;
        SetMessage(healed > 0 ? $"You drink a healing potion and recover {healed} HP." : "You drink a healing potion, but you are already at full health.");
    }

    private void ShowInventory()
    {
        string gear = player.GearInventory.Count == 0 ? "No spare gear." : string.Join(Environment.NewLine, player.GearInventory.Select(g => $"{RarityName(g.Rarity)} {g.Name} [{g.Slot}] {GearSummary(g)}"));
        string items = player.Inventory.Count == 0 ? "No potions." : string.Join(Environment.NewLine, player.Inventory.Select(i => $"{i.Name}    {i.Value} gp"));
        string equipped = $"WEAPON: {player.Weapon?.Name ?? "None"}\nARMOR:  {player.Armor?.Name ?? "None"}\nRING:   {player.Ring?.Name ?? "None"}";
        MessageBox.Show(this, $"EQUIPPED\n{equipped}\n\nLOOT\n{gear}\n\nPOTIONS\n{items}", "Inventory & Gear", MessageBoxButtons.OK, MessageBoxIcon.None);
        Focus();
    }

    private void ShowChatLog()
    {
        string text = chatLog.Count == 0 ? "No actions yet." : string.Join(Environment.NewLine, chatLog);
        using Form dialog = new()
        {
            Text = "Moria — Chat Log",
            ClientSize = new Size(760, 620),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false
        };
        TextBox log = new()
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(12, 13, 17),
            ForeColor = Color.Gainsboro,
            Font = new Font("Consolas", 10),
            Text = text
        };
        dialog.Controls.Add(log);
        dialog.ShowDialog(this);
        Focus();
    }

    private static string RarityName(int rarity) => rarity switch { 1 => "Common", 2 => "Rare", 3 => "Epic", _ => "Legendary" };

    private void SetMessage(string text)
    {
        message = text;
        chatLog.Add(text);
        if (chatLog.Count > 250) chatLog.RemoveAt(0);
    }

    private void EndRun()
    {
        if (runOver) return;
        runOver = true;
        player.RunsCompleted++;
        lastRunReward = Math.Max(10, player.Gold / 3 + player.DungeonLevel * 10);
        player.PermanentGold += lastRunReward;
        SaveLegacyGold();
        SetMessage($"You fell in dungeon level {player.DungeonLevel}. +{lastRunReward} Legacy Gold.");
    }

    private void StartFreshRun()
    {
        int legacy = player.PermanentGold;
        player = new Player(player.Name, new Position(1, 1), legacy);
        StartRun();
        SetMessage($"New run begins. Legacy Gold: {legacy}.");
        Focus();
        Invalidate();
    }

    private long LoadLegacyGold()
    {
        try
        {
            if (!File.Exists(MetaFile)) return 0;
            string value = File.ReadAllText(MetaFile).Split('|')[0];
            return long.TryParse(value, out long gold) ? Math.Max(0, gold) : 0;
        }
        catch { return 0; }
    }

    private void SaveLegacyGold() => File.WriteAllText(MetaFile, $"{player.PermanentGold}|{player.RunsCompleted}");

    private void SaveProgress()
    {
        File.WriteAllText("moria.sav", string.Join('|', player.Name, player.Level, player.Experience, player.Hp, player.TotalMaxHp, player.Gold, player.DungeonLevel, player.PermanentGold));
        SaveLegacyGold();
    }
}
