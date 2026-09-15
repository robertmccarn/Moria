using Moria.Core;

namespace Moria.Entities;

public abstract class Actor
{
    public string Name { get; protected set; }
    public char Symbol { get; protected set; }
    public Position Position { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; protected set; }
    public int ArmorClass { get; protected set; }
    public int Attack { get; protected set; }
    public int Level { get; protected set; }
    public int Experience { get; set; }
    public bool Alive => Hp > 0;

    protected Actor(string name, char symbol, Position position, int hp, int armorClass, int attack, int level)
    {
        Name = name;
        Symbol = symbol;
        Position = position;
        Hp = MaxHp = hp;
        ArmorClass = armorClass;
        Attack = attack;
        Level = level;
    }
}
