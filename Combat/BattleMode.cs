using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Moria.Art;
using Moria.Combat;
using Moria.Core;
using Moria.Entities;
using Moria.Input;
using Moria.UI;
using Moria.World;

namespace Moria;

public sealed partial class Game
{
    private const int BattleGroupRadius = 5;
    private const int BattleGroupLimit = 6;

    internal TurnBasedBattle? CurrentBattle { get; private set; }
    internal int BattleMenuIndex => battleMenuIndex;
    private BattleOverlayControl? battleOverlay;
    private int battleMenuIndex;

    private void ProcessMonsterTurn()
    {
        foreach (Monster monster in dungeon.Monsters.Where(m => m.Alive).ToList())
        {
            if (monster.IsBoss && monster.Position == dungeon.DownStairs)
                continue;

            int dy = player.Position.Y - monster.Position.Y;
            int dx = player.Position.X - monster.Position.X;
            if (Math.Abs(dy) + Math.Abs(dx) > 10)
                continue;

            Position target = monster.Position;
            if (Math.Abs(dy) >= Math.Abs(dx))
                target = new Position(target.Y + Math.Sign(dy), target.X);
            else
                target = new Position(target.Y, target.X + Math.Sign(dx));

            if (target == player.Position)
            {
                BeginBattle(monster);
                return;
            }

            if (dungeon.IsWalkable(target) && dungeon.MonsterAt(target) == null)
                monster.Position = target;
        }
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        if (battleOverlay != null)
            return;

        battleOverlay = new BattleOverlayControl(this)
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            TabStop = false
        };
        Controls.Add(battleOverlay);
        battleOverlay.BringToFront();
        battleOverlay.Visible = false;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (CurrentBattle == null)
            return base.ProcessCmdKey(ref msg, keyData);

        if (battleOverlay?.IsAnimating == true)
            return true;

        Keys key = keyData & Keys.KeyCode;
        if (key is Keys.D1 or Keys.NumPad1) return ExecuteBattleMenu(0);
        if (key is Keys.D2 or Keys.NumPad2) return ExecuteBattleMenu(1);
        if (key is Keys.D3 or Keys.NumPad3) return ExecuteBattleMenu(2);
        if (key is Keys.D4 or Keys.NumPad4) return ExecuteBattleMenu(3);
        if (key is Keys.D5 or Keys.NumPad5) return ExecuteBattleMenu(4);

        if (key == Keys.Left) { CurrentBattle.CycleTarget(-1); battleOverlay?.Invalidate(); return true; }
        if (key == Keys.Right) { CurrentBattle.CycleTarget(1); battleOverlay?.Invalidate(); return true; }
        if (key == Keys.Up)
        {
            battleMenuIndex = (battleMenuIndex + 4) % 5;
            battleOverlay?.Invalidate();
            return true;
        }
        if (key == Keys.Down)
        {
            battleMenuIndex = (battleMenuIndex + 1) % 5;
            battleOverlay?.Invalidate();
            return true;
        }
        if (key is Keys.Enter or Keys.Space)
            return ExecuteBattleMenu(battleMenuIndex);

        return true;
    }

    private bool ExecuteBattleMenu(int index)
    {
        if (CurrentBattle == null || battleOverlay?.IsAnimating == true)
            return false;

        battleMenuIndex = Math.Clamp(index, 0, 4);
        BattleAction action = (BattleAction)battleMenuIndex;

        Dictionary<Monster, int> enemyHpBefore = CurrentBattle.Enemies.ToDictionary(enemy => enemy, enemy => enemy.Hp);
        int playerHpBefore = player.Hp;

        CurrentBattle.Execute(action);
        SetMessage(CurrentBattle.Message);

        Dictionary<Monster, int> enemyDamage = CurrentBattle.Enemies
            .ToDictionary(enemy => enemy, enemy => Math.Max(0, enemyHpBefore[enemy] - Math.Max(0, enemy.Hp)));
        int playerDamage = Math.Max(0, playerHpBefore - Math.Max(0, player.Hp));

        battleOverlay?.PlayActionAnimation(action, CurrentBattle, enemyDamage, playerDamage);
        return true;
    }

    private void BeginBattle(Monster monster)
    {
        if (CurrentBattle != null || !monster.Alive)
            return;

        List<Monster> group = dungeon.Monsters
            .Where(enemy => enemy.Alive)
            .Where(enemy => enemy == monster || Math.Abs(enemy.Position.Y - player.Position.Y) + Math.Abs(enemy.Position.X - player.Position.X) <= BattleGroupRadius)
            .OrderBy(enemy => enemy == monster ? -1 : Math.Abs(enemy.Position.Y - player.Position.Y) + Math.Abs(enemy.Position.X - player.Position.X))
            .Take(BattleGroupLimit)
            .ToList();

        playerRolling = false;
        InputManager.ClearMovementKeys();
        CurrentBattle = new TurnBasedBattle(player, group, combat, random);
        battleMenuIndex = 0;
        battleOverlay!.Visible = true;
        battleOverlay.BringToFront();
        SetMessage(group.Count == 1 ? $"A {monster.Name} blocks your path!" : $"{group.Count} enemies surround you!");
        battleOverlay.Invalidate();
    }

    internal void CompleteBattleAnimation(TurnBasedBattle battle)
    {
        if (CurrentBattle != battle)
            return;

        if (battle.Finished)
            FinishBattle(battle);
        else
            battleOverlay?.Invalidate();
    }

    private void FinishBattle(TurnBasedBattle battle)
    {
        if (battle.Phase == BattlePhase.Victory)
        {
            int totalXp = 0;
            int totalGold = 0;
            int oldLevel = player.Level;

            foreach (Monster monster in battle.Enemies.Where(enemy => !enemy.Alive))
            {
                int xp = monster.ExperienceValue;
                int gold = random.Next(4, 13) + player.DungeonLevel * 2;
                totalXp += xp;
                totalGold += gold;
                player.Gold += gold;
                DropLoot(monster.Position, player.DungeonLevel);
            }

            player.GainExperience(totalXp);
            AutoLoot();
            SetMessage($"Victory! +{totalXp} XP, +{totalGold} gold. {battle.Enemies.Count} enemies defeated.");
            if (player.Level > oldLevel)
                SetMessage($"LEVEL UP! You are now level {player.Level}.");
            if (player.DungeonLevel == Dungeon.MaximumDepth && battle.Enemies.Any(enemy => enemy.IsBoss && !enemy.Alive))
                WinRun();
        }
        else if (battle.Phase == BattlePhase.Fled)
        {
            EscapeBattlePosition(battle.CurrentEnemy.Position);
            SetMessage("You escape the battle.");
        }
        else if (battle.Phase == BattlePhase.Defeat)
        {
            EndRun();
        }

        CurrentBattle = null;
        battleOverlay!.Visible = false;
        battleOverlay.Invalidate();
    }

    private void EscapeBattlePosition(Position enemyPosition)
    {
        int dy = player.Position.Y - enemyPosition.Y;
        int dx = player.Position.X - enemyPosition.X;
        Position[] candidates =
        [
            new(player.Position.Y + Math.Sign(dy), player.Position.X + Math.Sign(dx)),
            new(player.Position.Y - Math.Sign(dy), player.Position.X),
            new(player.Position.Y, player.Position.X - Math.Sign(dx)),
            new(player.Position.Y + 1, player.Position.X),
            new(player.Position.Y - 1, player.Position.X)
        ];

        foreach (Position candidate in candidates)
        {
            if (candidate == enemyPosition || !dungeon.IsWalkable(candidate))
                continue;
            player.WorldPosition = new PointF(candidate.X, candidate.Y);
            RecalculateVisibility(7);
            return;
        }
    }
}

internal sealed class BattleOverlayControl : Control
{
    private readonly Game game;
    private readonly AssetAtlas assets = new();
    private readonly System.Windows.Forms.Timer animationTimer;

    private static readonly string[] MenuLabels =
    [
        "ATTACK",
        "POWER STRIKE",
        "POTION",
        "DEFEND",
        "FLEE"
    ];

    private TurnBasedBattle? animationBattle;
    private Dictionary<Monster, int> animationDamage = new();
    private int animationPlayerDamage;
    private BattleAction animationAction;
    private int animationFrame;
    private int animationEnemyIndex;
    private bool animationPlayerPhase;

    internal bool IsAnimating => animationTimer.Enabled;

    public BattleOverlayControl(Game game)
    {
        this.game = game;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        animationTimer = new System.Windows.Forms.Timer { Interval = 40 };
        animationTimer.Tick += (_, _) => AdvanceAnimation();
    }

    protected override bool ShowFocusCues => false;

    internal void PlayActionAnimation(BattleAction action, TurnBasedBattle battle, Dictionary<Monster, int> enemyDamage, int playerDamage)
    {
        animationBattle = battle;
        animationDamage = enemyDamage;
        animationPlayerDamage = playerDamage;
        animationAction = action;
        animationFrame = 0;
        animationEnemyIndex = 0;
        animationPlayerPhase = action is BattleAction.Attack or BattleAction.PowerStrike;
        animationTimer.Start();
        Invalidate();
    }

    private void AdvanceAnimation()
    {
        animationFrame++;

        if (animationPlayerPhase && animationFrame >= 22)
        {
            animationPlayerPhase = false;
            animationFrame = 0;
            animationEnemyIndex = 0;
        }

        List<Monster> attackers = animationBattle?.Enemies
            .Where(enemy => animationDamage.GetValueOrDefault(enemy) > 0)
            .ToList() ?? new List<Monster>();

        if (!animationPlayerPhase && animationFrame >= 18)
        {
            animationFrame = 0;
            animationEnemyIndex++;
        }

        if (!animationPlayerPhase && animationEnemyIndex >= Math.Max(1, attackers.Count))
        {
            animationTimer.Stop();
            TurnBasedBattle? completedBattle = animationBattle;
            animationBattle = null;
            if (completedBattle != null)
                game.CompleteBattleAnimation(completedBattle);
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        TurnBasedBattle? battle = game.CurrentBattle;
        if (battle == null)
            return;

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.Clear(Color.FromArgb(3, 8, 17));

        DrawBattleBackdrop(g);
        DrawEnemies(g, battle);
        DrawPlayer(g, battle);
        DrawStatus(g, battle);
        DrawCommandWindow(g);
        DrawAnimation(g, battle);
    }

    private void DrawBattleBackdrop(Graphics g)
    {
        using SolidBrush sky = new(Color.FromArgb(12, 27, 55));
        using SolidBrush ground = new(Color.FromArgb(8, 14, 27));
        g.FillRectangle(sky, ClientRectangle);
        g.FillRectangle(ground, new Rectangle(0, 515, 1920, 565));

        using Pen horizon = new(Color.FromArgb(55, 77, 112), 2);
        g.DrawLine(horizon, 0, 515, 1920, 515);

        for (int i = 0; i < 18; i++)
        {
            int x = 30 + i * 113;
            int y = 80 + (i % 4) * 62;
            using SolidBrush star = new(Color.FromArgb(90, 142, 157, 185));
            g.FillRectangle(star, x, y, 2, 2);
        }

        using Pen floor = new(Color.FromArgb(28, 45, 69), 1);
        for (int x = 0; x < 1920; x += 80)
            g.DrawLine(floor, x, 515, x - 160, 760);
    }

    private void DrawEnemies(Graphics g, TurnBasedBattle battle)
    {
        using Font name = new("Segoe UI", 11, FontStyle.Bold);
        using Font details = new("Segoe UI", 8.5f, FontStyle.Bold);
        using SolidBrush text = new(Color.White);
        using SolidBrush muted = new(Color.FromArgb(170, 187, 210));
        using SolidBrush target = new(Color.FromArgb(75, 40, 83, 125));

        for (int i = 0; i < battle.Enemies.Count; i++)
        {
            Monster enemy = battle.Enemies[i];
            int column = i % 3;
            int row = i / 3;
            int x = 130 + column * 315;
            int y = row == 0 ? 75 : 285;
            Rectangle card = new(x, y, 290, 195);

            if (i == battle.TargetIndex && enemy.Alive)
                g.FillRectangle(target, card);

            Rectangle sprite = new(x + 65, y + 4, 160, 160);
            assets.DrawMonster(g, sprite, enemy);
            g.DrawString($"{i + 1}. {enemy.Name.ToUpperInvariant()}", name, enemy.Alive ? text : muted, x + 12, y + 150);
            g.DrawString($"LV {enemy.Level}   HP {Math.Max(0, enemy.Hp)}/{enemy.MaxHp}", details, muted, x + 12, y + 171);
            DrawBar(g, new Rectangle(x + 12, y + 186, 265, 7), enemy.Hp, Math.Max(1, enemy.MaxHp));
        }
    }

    private void DrawPlayer(Graphics g, TurnBasedBattle battle)
    {
        Rectangle panel = new(1300, 80, 470, 440);
        DrawWindow(g, panel, Color.FromArgb(18, 31, 55));

        Rectangle sprite = new(1435, 110, 180, 180);
        assets.DrawPlayer(g, sprite, Direction.Left, battle.Player.Alive);

        using Font name = new("Segoe UI", 16, FontStyle.Bold);
        using Font details = new("Segoe UI", 11, FontStyle.Bold);
        using SolidBrush text = new(Color.White);
        using SolidBrush muted = new(Color.FromArgb(178, 195, 220));
        g.DrawString(battle.Player.Name.ToUpperInvariant(), name, text, 1370, 315);
        g.DrawString($"LV {battle.Player.Level}    HP {Math.Max(0, battle.Player.Hp)}/{battle.Player.TotalMaxHp}", details, muted, 1370, 347);
        DrawBar(g, new Rectangle(1370, 378, 320, 14), battle.Player.Hp, Math.Max(1, battle.Player.TotalMaxHp));
        g.DrawString($"MP {battle.Player.Mana}/{battle.Player.MaxMana}", details, muted, 1370, 408);
        g.DrawString("TARGET", details, muted, 1370, 450);
        using SolidBrush targetText = new(UiTheme.Gold);
        g.DrawString(battle.CurrentEnemy.Name.ToUpperInvariant(), details, targetText, 1450, 450);
    }

    private static void DrawStatus(Graphics g, TurnBasedBattle battle)
    {
        using Font phase = new("Segoe UI", 11, FontStyle.Bold);
        using Font message = new("Segoe UI", 9.5f, FontStyle.Bold);
        using SolidBrush gold = new(UiTheme.Gold);
        using SolidBrush text = new(Color.White);
        string heading = battle.Phase == BattlePhase.PlayerTurn ? "YOUR TURN" : "ENEMY TURN";
        g.DrawString(heading, phase, gold, 785, 535);
        g.DrawString(battle.Message, message, text, 785, 560);
    }

    private void DrawCommandWindow(Graphics g)
    {
        Rectangle menu = new(180, 700, 1120, 270);
        DrawWindow(g, menu, Color.FromArgb(13, 24, 45));

        using Font heading = new("Segoe UI", 11, FontStyle.Bold);
        using SolidBrush gold = new(UiTheme.Gold);
        g.DrawString("COMMAND", heading, gold, menu.X + 26, menu.Y + 20);

        for (int i = 0; i < MenuLabels.Length; i++)
        {
            int x = menu.X + 38 + (i % 2) * 500;
            int y = menu.Y + 68 + (i / 2) * 58;
            bool selected = i == game.BattleMenuIndex;
            if (selected)
            {
                using SolidBrush highlight = new(Color.FromArgb(100, 40, 83, 125));
                g.FillRectangle(highlight, x - 12, y - 6, 440, 39);
            }

            using SolidBrush number = new(UiTheme.Gold);
            using SolidBrush label = new(selected ? Color.White : Color.FromArgb(190, 201, 218));
            g.DrawString($"{i + 1}", heading, number, x, y);
            g.DrawString(MenuLabels[i], heading, label, x + 30, y);
        }

        using Font hint = new("Segoe UI", 8.5f, FontStyle.Bold);
        using SolidBrush muted = new(UiTheme.Muted);
        g.DrawString("1-5 SELECT    ↑/↓ COMMAND    ←/→ TARGET    ENTER CONFIRM", hint, muted, menu.X + 26, menu.Bottom - 28);
    }

    private static void DrawWindow(Graphics g, Rectangle rectangle, Color fill)
    {
        using SolidBrush brush = new(fill);
        using Pen outer = new(Color.FromArgb(105, 130, 160), 3);
        using Pen inner = new(Color.FromArgb(35, 54, 82), 1);
        g.FillRectangle(brush, rectangle);
        g.DrawRectangle(outer, rectangle);
        Rectangle inset = Rectangle.Inflate(rectangle, -7, -7);
        g.DrawRectangle(inner, inset);
    }

    private void DrawAnimation(Graphics g, TurnBasedBattle battle)
    {
        if (!IsAnimating || animationBattle != battle)
            return;

        if (animationPlayerPhase)
        {
            Monster target = battle.CurrentEnemy;
            Point start = new(1525, 290);
            Rectangle targetRect = GetEnemySpriteRectangle(target, battle);
            Point end = new(targetRect.X + targetRect.Width / 2, targetRect.Y + targetRect.Height / 2);
            DrawProjectile(g, start, end, animationFrame / 22f, Color.FromArgb(245, 222, 169));

            if (animationFrame >= 16)
            {
                int damage = animationDamage.GetValueOrDefault(target);
                DrawImpact(g, end, animationFrame - 16);
                DrawDamageNumber(g, end, damage, animationFrame - 16);
            }
        }
        else
        {
            List<Monster> attackers = animationBattle.Enemies
                .Where(enemy => animationDamage.GetValueOrDefault(enemy) > 0)
                .ToList();

            if (animationEnemyIndex < attackers.Count)
            {
                Monster attacker = attackers[animationEnemyIndex];
                Rectangle sourceRect = GetEnemySpriteRectangle(attacker, battle);
                Point start = new(sourceRect.X + sourceRect.Width / 2, sourceRect.Y + sourceRect.Height / 2);
                Point end = new(1525, 200);
                DrawProjectile(g, start, end, animationFrame / 18f, Color.FromArgb(215, 115, 106));
                if (animationFrame >= 12)
                {
                    DrawImpact(g, end, animationFrame - 12);
                    DrawDamageNumber(g, end, animationPlayerDamage, animationFrame - 12);
                }
            }
        }
    }

    private Rectangle GetEnemySpriteRectangle(Monster enemy, TurnBasedBattle battle)
    {
        int index = battle.Enemies.IndexOf(enemy);
        int column = index % 3;
        int row = index / 3;
        int x = 130 + column * 315;
        int y = row == 0 ? 75 : 285;
        return new Rectangle(x + 65, y + 4, 160, 160);
    }

    private static void DrawProjectile(Graphics g, Point start, Point end, float progress, Color color)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        Point point = new(
            start.X + (int)((end.X - start.X) * progress),
            start.Y + (int)((end.Y - start.Y) * progress));
        using Pen trail = new(Color.FromArgb(190, color), 5);
        g.DrawLine(trail, start, point);
        using SolidBrush core = new(color);
        g.FillRectangle(core, point.X - 5, point.Y - 5, 10, 10);
    }

    private static void DrawImpact(Graphics g, Point center, int frame)
    {
        int radius = 10 + frame * 5;
        using Pen ring = new(Color.FromArgb(Math.Max(30, 210 - frame * 22), 235, 216, 150), 4);
        g.DrawEllipse(ring, center.X - radius, center.Y - radius, radius * 2, radius * 2);
    }

    private static void DrawDamageNumber(Graphics g, Point center, int damage, int frame)
    {
        if (damage <= 0)
            return;

        using Font font = new("Segoe UI", 18, FontStyle.Bold);
        using SolidBrush shadow = new(Color.FromArgb(210, 0, 0, 0));
        using SolidBrush text = new(Color.White);
        int y = center.Y - 35 - frame * 2;
        string value = $"-{damage}";
        g.DrawString(value, font, shadow, center.X - 20 + 2, y + 2);
        g.DrawString(value, font, text, center.X - 20, y);
    }

    private static void DrawBar(Graphics g, Rectangle rect, int value, int maximum)
    {
        using SolidBrush background = new(Color.FromArgb(25, 37, 55));
        using Pen border = new(Color.FromArgb(100, 126, 153), 1);
        g.FillRectangle(background, rect);
        g.DrawRectangle(border, rect);
        int width = Math.Clamp(rect.Width * Math.Max(0, value) / Math.Max(1, maximum), 0, rect.Width);
        if (width > 0)
        {
            using SolidBrush fill = new(UiTheme.Hp);
            g.FillRectangle(fill, rect.X + 1, rect.Y + 1, Math.Max(1, width - 1), Math.Max(1, rect.Height - 1));
        }
    }
}
