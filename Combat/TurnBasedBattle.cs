using Moria.Entities;
using Moria.Items;

namespace Moria.Combat;

public enum BattlePhase
{
    PlayerTurn,
    EnemyTurn,
    Victory,
    Defeat,
    Fled
}

public enum BattleAction
{
    Attack,
    PowerStrike,
    Potion,
    Defend,
    Flee
}

public sealed class TurnBasedBattle
{
    private readonly CombatSystem combat;
    private readonly Random random;
    private bool defending;

    public Player Player { get; }
    public Monster Enemy { get; }
    public BattlePhase Phase { get; private set; } = BattlePhase.PlayerTurn;
    public string Message { get; private set; } = "Choose an action.";
    public bool Finished => Phase is BattlePhase.Victory or BattlePhase.Defeat or BattlePhase.Fled;

    public TurnBasedBattle(Player player, Monster enemy, CombatSystem combat, Random random)
    {
        Player = player;
        Enemy = enemy;
        this.combat = combat;
        this.random = random;
    }

    public void Execute(BattleAction action)
    {
        if (Finished || Phase != BattlePhase.PlayerTurn)
            return;

        switch (action)
        {
            case BattleAction.Attack:
                Attack();
                break;
            case BattleAction.PowerStrike:
                PowerStrike();
                break;
            case BattleAction.Potion:
                Potion();
                break;
            case BattleAction.Defend:
                defending = true;
                Message = "You raise your guard.";
                EnemyTurn();
                break;
            case BattleAction.Flee:
                Flee();
                break;
        }
    }

    private void Attack()
    {
        CombatResult result = combat.PlayerAttack(Player, Enemy, random);
        Message = result.Hit
            ? result.Critical ? $"CRITICAL HIT! {result.Damage} damage." : $"You strike for {result.Damage} damage."
            : "Your attack misses.";
        if (result.Defeated)
        {
            Phase = BattlePhase.Victory;
            Message += $" {Enemy.Name} is defeated!";
            return;
        }
        EnemyTurn();
    }

    private void PowerStrike()
    {
        int attackBonus = Math.Max(2, Player.TotalAttack / 2);
        bool hit = random.Next(1, 21) + Player.TotalAttack + attackBonus >= Enemy.ArmorClass;
        if (!hit)
        {
            Message = "POWER STRIKE misses!";
            EnemyTurn();
            return;
        }

        int damage = random.Next(5, 11) + Player.TotalAttack + attackBonus;
        if (random.Next(100) < 10 + Player.Dexterity / 4)
            damage *= 2;
        Enemy.Hp -= damage;
        Message = $"POWER STRIKE! {damage} damage.";
        if (!Enemy.Alive)
        {
            Phase = BattlePhase.Victory;
            Message += $" {Enemy.Name} is defeated!";
            return;
        }
        EnemyTurn();
    }

    private void Potion()
    {
        Item? potion = Player.Inventory.FirstOrDefault(i => i.Kind == ItemKind.Potion);
        if (potion == null)
        {
            Message = "You have no healing potions.";
            return;
        }

        Player.Inventory.Remove(potion);
        int healed = Player.TotalMaxHp - Player.Hp;
        Player.Hp = Player.TotalMaxHp;
        Message = healed > 0 ? $"You recover {healed} HP." : "You are already at full HP.";
        EnemyTurn();
    }

    private void Flee()
    {
        if (Enemy.IsBoss)
        {
            Message = "There is no escape from this foe!";
            EnemyTurn();
            return;
        }

        int chance = Math.Clamp(45 + Player.Dexterity * 3, 45, 85);
        if (random.Next(100) < chance)
        {
            Phase = BattlePhase.Fled;
            Message = "You escape from the battle.";
            return;
        }

        Message = "You fail to escape!";
        EnemyTurn();
    }

    private void EnemyTurn()
    {
        Phase = BattlePhase.EnemyTurn;
        CombatResult result = combat.MonsterAttack(Player, Enemy, random);
        if (result.Hit)
        {
            int damage = result.Damage;
            if (defending)
            {
                int blocked = Math.Max(1, damage / 2);
                Player.Hp += blocked;
                damage -= blocked;
                Message += $" {Enemy.Name} attacks. Guard blocks {blocked} damage. {damage} gets through.";
            }
            else
            {
                Message += $" {Enemy.Name} hits you for {damage} damage.";
            }
        }
        else
        {
            Message += $" {Enemy.Name} misses.";
        }

        defending = false;
        if (!Player.Alive)
        {
            Phase = BattlePhase.Defeat;
            Message += " You fall.";
            return;
        }

        Phase = BattlePhase.PlayerTurn;
    }
}
