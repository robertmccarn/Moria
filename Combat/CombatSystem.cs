using Moria.Entities;

namespace Moria.Combat;

public sealed record CombatResult(bool Hit, bool Critical, int Damage, bool Defeated);

public sealed class CombatSystem
{
    public CombatResult PlayerAttack(Player player, Monster monster, Random random)
    {
        int attackRoll = random.Next(1, 21);
        if (attackRoll + player.TotalAttack < monster.ArmorClass)
            return new CombatResult(false, false, 0, false);

        bool critical = random.Next(100) < 8 + player.Dexterity / 5;
        int rolledDamage = random.Next(1, 7) + Math.Max(1, player.TotalAttack / 2);
        if (critical)
            rolledDamage *= 2;

        int damage = Math.Min(rolledDamage, monster.Hp);
        monster.Hp = Math.Max(0, monster.Hp - damage);

        return new CombatResult(true, critical, damage, !monster.Alive);
    }

    public CombatResult MonsterAttack(Player player, Monster monster, Random random)
    {
        if (random.Next(1, 21) + monster.Attack < player.TotalArmorClass)
            return new CombatResult(false, false, 0, false);

        int damage = random.Next(1, 5) + monster.Level / 2;
        player.Hp = Math.Max(0, player.Hp - damage);
        return new CombatResult(true, false, damage, !player.Alive);
    }
}
