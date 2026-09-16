using System.Drawing;
using Moria.Core;

namespace Moria.Entities;

public abstract class Actor
{
    private Position position;
    private PointF worldPosition;

    public string Name { get; protected set; }
    public char Symbol { get; protected set; }
    public Position Position
    {
        get => position;
        set
        {
            position = value;
            worldPosition = new PointF(value.X, value.Y);
        }
    }
    public PointF WorldPosition
    {
        get => worldPosition;
        set
        {
            worldPosition = value;
            position = new Position(
                (int)MathF.Floor(value.Y + 0.5f),
                (int)MathF.Floor(value.X + 0.5f));
        }
    }
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
        this.position = position;
        worldPosition = new PointF(position.X, position.Y);
        Hp = MaxHp = hp;
        ArmorClass = armorClass;
        Attack = attack;
        Level = level;
    }
}
