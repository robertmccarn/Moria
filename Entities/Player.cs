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
    public int Mana { get; set; } = 5;
    public int MaxMana { get; set; } = 5;
    public int DungeonLevel { get; set; } = 1;
    public int RunsCompleted { get; set; }
    public int PermanentGold { get; set; }
    public List<Item> Inventory { get; } = new();
    public List<Gear> GearInventory { get; } = new();
    public Gear? Weapon { get; private set; }
    public Gear? Armor { get; private set; }
    public Gear? Ring { get; private set; }

    public int TotalAttack => Attack + (Weapon?.AttackBonus ?? 0) + (Ring?.AttackBonus ?? 0);
    public int TotalArmorClass => ArmorClass + (Armor?.ArmorBonus ?? 0) + (Ring?.ArmorBonus ?? 0);
    public int TotalMaxHp => MaxHp + (Armor?.MaxHpBonus ?? 0) + (Ring?.MaxHpBonus ?? 0);

    public Player(string name, Position position, int legacyGold = 0) : base(name, 'W', position, 24, 10, 4, 1)
    {
        PermanentGold = legacyGold;
        Attack += legacyGold / 250;
        MaxHp += (legacyGold / 500) * 2;
        Hp = MaxHp;
        Weapon = new Gear("Iron Longsword", '†', GearSlot.Weapon, 2, 0, 0, 35);
    }

    public void Equip(Gear gear)
    {
        switch (gear.Slot)
        {
            case GearSlot.Weapon: Weapon = gear; break;
            case GearSlot.Armor: Armor = gear; break;
            case GearSlot.Ring: Ring = gear; break;
        }
        Hp = Math.Min(Hp, TotalMaxHp);
    }

    public Gear? Equipped(GearSlot slot) => slot switch
    {
        GearSlot.Weapon => Weapon,
        GearSlot.Armor => Armor,
        GearSlot.Ring => Ring,
        _ => null
    };

    public void GainExperience(int amount)
    {
        Experience += amount;
        int needed = Level * 100;
        while (Experience >= needed)
        {
            Experience -= needed;
            Level++;
            MaxHp += 5;
            Hp = TotalMaxHp;
            MaxMana++;
            Mana = MaxMana;
            Attack++;
            needed = Level * 100;
        }
    }
}
