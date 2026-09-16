using Moria.Entities;
using Moria.Items;

namespace Moria.Combat;

public enum BattlePhase { PlayerTurn, EnemyTurn, Victory, Defeat, Fled }
public enum BattleAction { Attack, PowerStrike, Potion, Defend, Flee }

public sealed class TurnBasedBattle
{
    private readonly CombatSystem combat;
    private readonly Random random;
    private bool defending;
    private int targetIndex;

    public Player Player { get; }
    public IReadOnlyList<Monster> Enemies { get; }
    public Monster Enemy => CurrentEnemy;
    public Monster CurrentEnemy => Enemies[Math.Clamp(targetIndex, 0, Enemies.Count - 1)];
    public int TargetIndex => targetIndex;
    public BattlePhase Phase { get; private set; } = BattlePhase.EnemyTurn;
    public string Message { get; private set; } = "The enemies advance.";
    public bool Finished => Phase is BattlePhase.Victory or BattlePhase.Defeat or BattlePhase.Fled;
    public bool AllEnemiesDefeated => Enemies.All(e => !e.Alive);

    public TurnBasedBattle(Player player, IEnumerable<Monster> enemies, CombatSystem combat, Random random)
    {
        Player = player;
        this.combat = combat;
        this.random = random;
        Enemies = enemies.Where(e => e.Alive).Distinct().ToList();
        if (Enemies.Count == 0) throw new ArgumentException("A battle requires at least one living enemy.", nameof(enemies));
    }

    public void StartEnemyTurn()
    {
        if (Finished || Phase != BattlePhase.EnemyTurn)
            return;

        EnemyTurn();
    }

    public void CycleTarget(int direction)
    {
        if (Finished || Phase != BattlePhase.PlayerTurn || Enemies.Count == 0) return;
        int start = targetIndex;
        do
        {
            targetIndex = (targetIndex + Math.Sign(direction) + Enemies.Count) % Enemies.Count;
            if (Enemies[targetIndex].Alive) return;
        } while (targetIndex != start);
    }

    public void Execute(BattleAction action)
    {
        if (Finished || Phase != BattlePhase.PlayerTurn) return;
        switch (action)
        {
            case BattleAction.Attack: Attack(); break;
            case BattleAction.PowerStrike: PowerStrike(); break;
            case BattleAction.Potion: Potion(); break;
            case BattleAction.Defend: defending = true; Message = "You raise your guard."; EnemyTurn(); break;
            case BattleAction.Flee: Flee(); break;
        }
    }

    private void Attack()
    {
        Monster target = CurrentEnemy;
        CombatResult result = combat.PlayerAttack(Player, target, random);
        Message = result.Hit
            ? result.Critical ? $"CRITICAL HIT! {target.Name} takes {result.Damage} damage." : $"You strike {target.Name} for {result.Damage} damage."
            : $"Your attack misses {target.Name}.";
        if (AllEnemiesDefeated) { Phase = BattlePhase.Victory; Message += " All enemies are defeated!"; return; }
        EnemyTurn();
    }

    private void PowerStrike()
    {
        Monster target = CurrentEnemy;
        int bonus = Math.Max(2, Player.TotalAttack / 2);
        if (random.Next(1, 21) + Player.TotalAttack + bonus < target.ArmorClass)
        {
            Message = $"POWER STRIKE misses {target.Name}!";
            EnemyTurn();
            return;
        }
        int damage = random.Next(5, 11) + Player.TotalAttack + bonus;
        if (random.Next(100) < 10 + Player.Dexterity / 4) damage *= 2;
        target.Hp -= damage;
        Message = $"POWER STRIKE! {target.Name} takes {damage} damage.";
        if (AllEnemiesDefeated) { Phase = BattlePhase.Victory; Message += " All enemies are defeated!"; return; }
        EnemyTurn();
    }

    private void Potion()
    {
        Item? potion = Player.Inventory.FirstOrDefault(i => i.Kind == ItemKind.Potion);
        if (potion == null) { Message = "You have no healing potions."; return; }
        Player.Inventory.Remove(potion);
        int healed = Player.TotalMaxHp - Player.Hp;
        Player.Hp = Player.TotalMaxHp;
        Message = healed > 0 ? $"You recover {healed} HP." : "You are already at full HP.";
        EnemyTurn();
    }

    private void Flee()
    {
        if (Enemies.Any(e => e.IsBoss && e.Alive)) { Message = "There is no escape from this group!"; EnemyTurn(); return; }
        int chance = Math.Clamp(45 + Player.Dexterity * 3, 45, 85);
        if (random.Next(100) < chance) { Phase = BattlePhase.Fled; Message = "You escape from the battle."; return; }
        Message = "You fail to escape!";
        EnemyTurn();
    }

    private void EnemyTurn()
    {
        Phase = BattlePhase.EnemyTurn;
        List<string> attacks = new();
        foreach (Monster enemy in Enemies.Where(e => e.Alive).ToList())
        {
            CombatResult result = combat.MonsterAttack(Player, enemy, random);
            if (result.Hit)
            {
                int damage = result.Damage;
                if (defending)
                {
                    int blocked = Math.Max(1, damage / 2);
                    Player.Hp += blocked;
                    damage -= blocked;
                    attacks.Add($"{enemy.Name} hits for {damage} ({blocked} blocked)");
                }
                else attacks.Add($"{enemy.Name} hits for {damage}");
            }
            else attacks.Add($"{enemy.Name} misses");
            if (!Player.Alive) { Phase = BattlePhase.Defeat; Message = string.Join(". ", attacks) + ". You fall."; return; }
        }
        defending = false;
        Phase = BattlePhase.PlayerTurn;
        Message = attacks.Count == 0 ? "The enemies hesitate. Your turn." : string.Join(". ", attacks) + ". Your turn.";
        EnsureLivingTarget();
    }

    private void EnsureLivingTarget()
    {
        if (Enemies[targetIndex].Alive) return;
        int start = targetIndex;
        do { targetIndex = (targetIndex + 1) % Enemies.Count; if (Enemies[targetIndex].Alive) return; } while (targetIndex != start);
    }
}
