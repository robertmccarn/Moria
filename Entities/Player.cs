using Moria.Core;
using Moria.Items;

namespace Moria.Entities;

public sealed class Player : Actor
{
    public int Strength { get; set; } = 10;
    public int Dexterity { get; set; } = 10;
    public int Constitution { get; set; } = 10;
    public int Intelligence { get; set; } = 10;
    public int Wisdom { get; set; } = 10;
    public int Gold { get; set; } = 100;
    public int Food { get; set; } = 10;
    public int Mana { get; set; } = 5;
    public int MaxMana { get; set; } = 5;
    public int DungeonLevel { get; set; } = 1;
    public List<Item> Inventory { get; } = new();

    public Player(string name, Position position) : base(name, '@', position, 20, 10, 4, 1)
    {
        Inventory.Add(new Item("Short Sword", '/', 12, 1, 5));
        Inventory.Add(new Item("Rations", '%', 5, 0, 0, ItemKind.Food));
    }

    public void GainExperience(int amount)
    {
        Experience += amount;
        int needed = Level * 100;
        if (Experience >= needed)
        {
            Experience -= needed;
            Level++;
            MaxHp += 5;
            Hp = MaxHp;
            MaxMana++;
            Mana = MaxMana;
            Attack++;
        }
    }
}
