using Moria.Core;

namespace Moria.Entities;

public sealed class Monster : Actor
{
    public int ExperienceValue { get; }
    public bool Hostile { get; set; } = true;
    public bool IsBoss { get; }

    public Monster(string name, char symbol, Position position, int hp, int armorClass, int attack, int level, int experienceValue, bool isBoss = false)
        : base(name, symbol, position, hp, armorClass, attack, level)
    {
        ExperienceValue = experienceValue;
        IsBoss = isBoss;
    }
}
