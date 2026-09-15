namespace Moria.Items;

public enum ItemKind { Weapon, Armor, Food, Potion, Scroll, Gold }

public sealed class Item
{
    public string Name { get; }
    public char Symbol { get; }
    public int Value { get; }
    public int AttackBonus { get; }
    public int ArmorBonus { get; }
    public ItemKind Kind { get; }
    public int Quantity { get; set; } = 1;

    public Item(string name, char symbol, int value, int attackBonus, int armorBonus, ItemKind kind = ItemKind.Weapon)
    {
        Name = name;
        Symbol = symbol;
        Value = value;
        AttackBonus = attackBonus;
        ArmorBonus = armorBonus;
        Kind = kind;
    }
}
