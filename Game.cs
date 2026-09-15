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
    private const int TileSize = 22;
    private const int MapWidth = Dungeon.Width * TileSize;
    private const int MapHeight = Dungeon.Height * TileSize;
    private const int StatusHeight = 150;
    private const string MetaFile = "moria.meta";

    private readonly Random random = new();
    private readonly WinFormsTimer redrawTimer;
    private Dungeon dungeon = null!;
    private Player player = null!;
    private string message = "Welcome to Moria.";
    private bool running = true;
    private bool started;
    private bool runOver;
    private int lastRunReward;

    public int LastRunReward => lastRunReward;

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
        player.Food = 10;
        player.Experience = 0;
        dungeon.Generate(player.DungeonLevel);
        player.Position = dungeon.UpStairs;
        dungeon.RevealAround(player.Position, 8);
        runOver = false;
        message = $"Run {player.RunsCompleted + 1}: descend, loot gear, and bring home Legacy Gold.";
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!started || !running) return;

        if (runOver)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space or Keys.N) StartFreshRun();
            else if (e.KeyCode is Keys.X or Keys.Escape) { running = false; Close(); }
            return;
        }

        if (!player.Alive) { EndRun(); return; }

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
                case Keys.R: EquipBestGear(); break;
                case Keys.S: Save(); break;
                case Keys.X:
                case Keys.Escape: running = false; Close(); return;
                case Keys.OemPeriod: Descend(); break;
            }
        }

        if (running && player.Alive && e.KeyCode != Keys.I) MonstersAct();
        if (!player.Alive) EndRun();
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
        Tile tile = dungeon[target];
        if (tile.Type == TileType.Trap)
        {
            int damage = random.Next(1, 5);
            player.Hp -= damage;
            message = $"A trap wounds you for {damage}!";
        }
        else if (tile.GearLoot.Count > 0 || tile.PotionCount > 0)
        {
            int lootCount = tile.GearLoot.Count + tile.PotionCount;
            message = $"You see {lootCount} loot item{(lootCount == 1 ? "" : "s")}. Press G to collect all.";
        }
        else if (target == dungeon.DownStairs) message = "Press . to descend deeper into Moria.";
    }

    private void Attack(Monster monster)
    {
        if (random.Next(1, 21) + player.TotalAttack >= monster.ArmorClass)
        {
            bool critical = random.Next(100) < 8 + player.Dexterity / 5;
            int damage = random.Next(1, 7) + Math.Max(1, player.TotalAttack / 2);
            if (critical) damage *= 2;
            monster.Hp -= damage;
            message = critical ? $"CRITICAL! You cleave the {monster.Name} for {damage}." : $"You hit the {monster.Name} for {damage}.";
            if (!monster.Alive)
            {
                int xp = monster.ExperienceValue;
                int gold = random.Next(4, 13) + player.DungeonLevel * 2;
                player.Gold += gold;
                player.GainExperience(xp);
                message += $" +{xp} XP, +{gold} gold.";
                DropLoot(monster.Position, player.DungeonLevel);
            }
        }
        else message = $"You miss the {monster.Name}.";
    }

    private void DropLoot(Position position, int level)
    {
        Tile tile = dungeon[position];
        int roll = random.Next(100);
        if (roll < 45)
        {
            Gear gear = CreateLoot(level);
            tile.GearLoot.Add(gear);
        }
        else if (roll < 70)
        {
            tile.PotionCount++;
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
                    message = $"The {monster.Name} hits you for {damage}!";
                }
                else message = $"The {monster.Name} misses you.";
            }
            else if (dungeon.IsWalkable(target) && dungeon.MonsterAt(target) == null) monster.Position = target;
            if (!player.Alive) return;
        }
    }

    private void Descend()
    {
        if (player.Position != dungeon.DownStairs) { message = "There are no stairs here."; return; }
        player.DungeonLevel++;
        player.Gold += 15 + player.DungeonLevel * 3;
        dungeon.Generate(player.DungeonLevel);
        player.Position = dungeon.UpStairs;
        dungeon.RevealAround(player.Position, 8);
        message = $"You descend to level {player.DungeonLevel}. Enemy power rises with depth.";
    }

    private void Pickup()
    {
        Tile tile = dungeon[player.Position];
        int gearCount = tile.GearLoot.Count;
        int potionCount = tile.PotionCount;

        if (gearCount == 0 && potionCount == 0)
        {
            message = "There is nothing here.";
            return;
        }

        foreach (Gear gear in tile.GearLoot)
        {
            player.GearInventory.Add(gear);
            if (ShouldEquip(gear)) player.Equip(gear);
        }

        tile.GearLoot.Clear();

        for (int i = 0; i < potionCount; i++)
            player.Inventory.Add(new Item("Potion of Healing", '!', 50, 0, 0, ItemKind.Potion));

        tile.PotionCount = 0;

        List<string> lootParts = new();
        if (gearCount > 0) lootParts.Add($"{gearCount} gear");
        if (potionCount > 0) lootParts.Add($"{potionCount} potion{(potionCount == 1 ? "" : "s")}");
        message = $"Collected all loot: {string.Join(" and ", lootParts)}.";
    }

    private bool ShouldEquip(Gear gear)
    {
        Gear? current = player.Equipped(gear.Slot);
        return current == null || GearScore(gear) > GearScore(current);
    }

    private void EquipBestGear()
    {
        Gear? best = player.GearInventory.OrderByDescending(GearScore).FirstOrDefault(ShouldEquip);
        if (best == null) { message = "Nothing in your pack would improve your equipment."; return; }
        player.Equip(best);
        message = $"Equipped {best.Name}: {GearSummary(best)}";
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

    private void Eat()
    {
        if (player.Food <= 0) { message = "You have no food."; return; }
        player.Food--;
        player.Hp = Math.Min(player.TotalMaxHp, player.Hp + 3);
        message = "You eat some food and recover 3 HP.";
    }

    private void Drink()
    {
        Item? potion = player.Inventory.FirstOrDefault(i => i.Kind == ItemKind.Potion);
        if (potion == null) { message = "You have no potions."; return; }
        player.Inventory.Remove(potion);
        int healed = player.TotalMaxHp - player.Hp;
        player.Hp = player.TotalMaxHp;
        message = $"Potion used: +{healed} HP.";
    }

    private void ShowInventory()
    {
        string gear = player.GearInventory.Count == 0 ? "No spare gear." : string.Join(Environment.NewLine, player.GearInventory.Select(g => $"{RarityName(g.Rarity)} {g.Name} [{g.Slot}] {GearSummary(g)}"));
        string items = player.Inventory.Count == 0 ? "No consumables." : string.Join(Environment.NewLine, player.Inventory.Select(i => $"{i.Name}    {i.Value} gp"));
        string equipped = $"WEAPON: {player.Weapon?.Name ?? "None"}\nARMOR:  {player.Armor?.Name ?? "None"}\nRING:   {player.Ring?.Name ?? "None"}";
        MessageBox.Show(this, $"EQUIPPED\n{equipped}\n\nLOOT\n{gear}\n\nCONSUMABLES\n{items}", "Inventory & Gear", MessageBoxButtons.OK, MessageBoxIcon.None);
        Focus();
    }

    private static string RarityName(int rarity) => rarity switch { 1 => "Common", 2 => "Rare", 3 => "Epic", _ => "Legendary" };

    private void EndRun()
    {
        if (runOver) return;
        runOver = true;
        player.RunsCompleted++;
        lastRunReward = Math.Max(10, player.Gold / 3 + player.DungeonLevel * 10);
        player.PermanentGold += lastRunReward;
        SaveLegacyGold();
        message = $"You fell in dungeon level {player.DungeonLevel}. +{lastRunReward} Legacy Gold.";
    }

    private void StartFreshRun()
    {
        int legacy = player.PermanentGold;
        player = new Player(player.Name, new Position(1, 1), legacy);
        StartRun();
        message = $"New run begins. Legacy Gold: {legacy}.";
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

    private void Save()
    {
        File.WriteAllText("moria.sav", string.Join('|', player.Name, player.Level, player.Experience, player.Hp, player.TotalMaxHp, player.Gold, player.DungeonLevel, player.PermanentGold));
        SaveLegacyGold();
        message = "Run saved. Legacy Gold persisted.";
    }
}
