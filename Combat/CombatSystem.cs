using Moria.Entities;

namespace Moria.Combat;

public sealed record CombatResult(bool Hit, bool Critical, int Damage, bool Defeated);

public sealed class CombatSystem
{
    public CombatResult PlayerAttack(Player player, Monster monster, Random random)
    {
        if (random.Next(1, 21) + player.TotalAttack < monster.ArmorClass)
            return new CombatResult(false, false, 0, false);

        bool critical = random.Next(100) < 8 + player.Dexterity / 5;
        int damage = random.Next(1, 7) + Math.Max(1, player.TotalAttack / 2);
        if (critical) damage *= 2;
        monster.Hp -= damage;
        return new CombatResult(true, critical, damage, !monster.Alive);
    }

    public CombatResult MonsterAttack(Player player, Monster monster, Random random)
    {
        if (random.Next(1, 21) + monster.Attack < player.TotalArmorClass)
            return new CombatResult(false, false, 0, false);

        int damage = random.Next(1, 5) + monster.Level / 2;
        player.Hp -= damage;
        return new CombatResult(true, false, damage, !player.Alive);
    }
}
