namespace Moria.Items;

public enum GearSlot
{
    Weapon,
    Armor,
    Ring
}

public sealed record Gear(
    string Name,
    char Symbol,
    GearSlot Slot,
    int AttackBonus,
    int ArmorBonus,
    int MaxHpBonus,
    int Value,
    int Rarity = 1);
