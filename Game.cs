using Moria.Core;
using Moria.Entities;
using Moria.Items;
using Moria.World;

namespace Moria;

public sealed class Game
{
    private readonly Random random = new();
    private Dungeon dungeon = null!;
    private Player player = null!;
    private string message = "Welcome to Moria.";
    private bool running = true;

    public void Run()
    {
        Console.CursorVisible = false;
        CreateCharacter();
        while (running && player.Alive)
        {
            dungeon.Draw(player);
            DrawStatus();
            ConsoleKey key = Console.ReadKey(true).Key;
            HandleInput(key);
            if (running && player.Alive) MonstersAct();
        }
        Console.Clear();
        Console.CursorVisible = true;
        Console.WriteLine(player.Alive ? "You escaped Moria." : "You have died in Moria.");
        Console.WriteLine(message);
    }

    private void CreateCharacter()
    {
        Console.Clear();
        Console.WriteLine("MORIA");
        Console.WriteLine("=====");
        Console.Write("Name: ");
        string name = Console.ReadLine()?.Trim() ?? "Adventurer";
        if (name.Length == 0) name = "Adventurer";

        dungeon = new Dungeon(random.Next());
        player = new Player(name, new Position(1, 1));
        dungeon.Generate(player.DungeonLevel);
        player.Position = dungeon.UpStairs;
    }

    private void HandleInput(ConsoleKey key)
    {
        Direction direction = key switch
        {
            ConsoleKey.UpArrow or ConsoleKey.K => Direction.Up,
            ConsoleKey.DownArrow or ConsoleKey.J => Direction.Down,
            ConsoleKey.LeftArrow or ConsoleKey.H => Direction.Left,
            ConsoleKey.RightArrow or ConsoleKey.L => Direction.Right,
            ConsoleKey.Y => Direction.Up,
            ConsoleKey.U => Direction.Right,
            ConsoleKey.B => Direction.Left,
            ConsoleKey.N => Direction.Down,
            _ => Direction.None
        };

        if (direction != Direction.None) { MovePlayer(direction); return; }
        switch (key)
        {
            case ConsoleKey.I: ShowInventory(); break;
            case ConsoleKey.G: Pickup(); break;
            case ConsoleKey.E: Eat(); break;
            case ConsoleKey.Q: Drink(); break;
            case ConsoleKey.S: Save(); break;
            case ConsoleKey.X: running = false; break;
            case ConsoleKey.OemPeriod: Descend(); break;
        }
    }

    private void MovePlayer(Direction direction)
    {
        Position target = player.Position.Step(direction);
        if (!dungeon.IsWalkable(target)) { message = "You cannot move there."; return; }
        Monster? monster = dungeon.MonsterAt(target);
        if (monster != null) { Attack(monster); return; }
        player.Position = target;
        dungeon.RevealAround(target, 6);
        if (dungeon[target].Type == TileType.Trap) { player.Hp -= random.Next(1, 5); message = "A trap wounds you!"; }
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
            else if (dungeon.IsWalkable(target) && dungeon.MonsterAt(target) == null) monster.Position = target;
        }
    }

    private void Descend()
    {
        if (player.Position != dungeon.DownStairs) { message = "There are no stairs here."; return; }
        player.DungeonLevel++;
        dungeon.Generate(player.DungeonLevel);
        player.Position = dungeon.UpStairs;
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
        Console.Clear();
        Console.WriteLine("INVENTORY");
        Console.WriteLine("----------");
        foreach (Item item in player.Inventory) Console.WriteLine($"{item.Symbol} {item.Name} ({item.Value} gp)");
        Console.WriteLine();
        Console.WriteLine("Press any key...");
        Console.ReadKey(true);
    }

    private void Save()
    {
        string data = string.Join('|', player.Name, player.Level, player.Experience, player.Hp, player.MaxHp, player.Gold, player.DungeonLevel);
        File.WriteAllText("moria.sav", data);
        message = "Game saved to moria.sav.";
    }

    private void DrawStatus()
    {
        Console.WriteLine($"HP {player.Hp}/{player.MaxHp}  LV {player.Level}  XP {player.Experience}  Gold {player.Gold}  Food {player.Food}  Dungeon {player.DungeonLevel}");
        Console.WriteLine(message.PadRight(Dungeon.Width));
        Console.WriteLine("Arrows/HJKL move | G get | I inventory | E eat | Q drink | . descend | S save | X quit");
    }
}
