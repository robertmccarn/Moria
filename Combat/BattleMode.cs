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
    private const int LogicalWidth = 1920;
    private const int LogicalHeight = 1080;

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

        float scaleX = ClientSize.Width / (float)LogicalWidth;
        float scaleY = ClientSize.Height / (float)LogicalHeight;
        float scale = Math.Min(scaleX, scaleY);
        float offsetX = (ClientSize.Width - LogicalWidth * scale) / 2f;
        float offsetY = (ClientSize.Height - LogicalHeight * scale) / 2f;
        g.TranslateTransform(offsetX, offsetY);
        g.ScaleTransform(scale, scale);

        g.Clear(Color.FromArgb(12, 10, 9));
        DrawBattleBackdrop(g);
        DrawEnemies(g, battle);
        DrawPlayer(g, battle);
        DrawTurnOrder(g, battle);
        DrawMessageWindow(g, battle);
        DrawCommandWindow(g, battle);
        DrawAnimation(g, battle);
    }

    private void DrawBattleBackdrop(Graphics g)
    {
        using SolidBrush darkness = new(Color.FromArgb(20, 17, 15));
        g.FillRectangle(darkness, 0, 0, LogicalWidth, LogicalHeight);

        using SolidBrush stone = new(Color.FromArgb(48, 43, 38));
        g.FillRectangle(stone, 0, 0, LogicalWidth, 690);

        using SolidBrush floor = new(Color.FromArgb(35, 31, 28));
        g.FillRectangle(floor, 0, 360, LogicalWidth, 330);

        using Pen mortar = new(Color.FromArgb(25, 22, 20), 3);
        for (int y = 20; y < 420; y += 58)
        {
            g.DrawLine(mortar, 0, y, LogicalWidth, y);
            int offset = (y / 58 % 2) * 90;
            for (int x = -offset; x < LogicalWidth; x += 180)
                g.DrawLine(mortar, x, y, x, y + 58);
        }

        using SolidBrush arch = new(Color.FromArgb(14, 12, 11));
        g.FillRectangle(arch, 690, 35, 540, 300);
        g.FillEllipse(arch, 690, -235, 540, 540);

        using SolidBrush warmGlow = new(Color.FromArgb(50, 120, 82, 35));
        g.FillEllipse(warmGlow, 45, 95, 280, 250);
        g.FillEllipse(warmGlow, 1595, 95, 280, 250);

        DrawTorch(g, 135, 165);
        DrawTorch(g, 1785, 165);

        using Pen floorLine = new(Color.FromArgb(62, 54, 47), 2);
        for (int x = -100; x < LogicalWidth + 100; x += 100)
            g.DrawLine(floorLine, x, 420, x + 220, 690);
        for (int y = 450; y < 700; y += 48)
            g.DrawLine(floorLine, 0, y, LogicalWidth, y);
    }

    private static void DrawTorch(Graphics g, int x, int y)
    {
        using SolidBrush bracket = new(Color.FromArgb(72, 58, 43));
        using SolidBrush flame = new(Color.FromArgb(222, 135, 45));
        using SolidBrush core = new(Color.FromArgb(255, 211, 103));
        g.FillRectangle(bracket, x - 7, y, 14, 85);
        g.FillRectangle(bracket, x - 24, y + 70, 48, 9);
        g.FillEllipse(flame, x - 22, y - 42, 44, 60);
        g.FillEllipse(core, x - 9, y - 27, 18, 38);
    }

    private void DrawEnemies(Graphics g, TurnBasedBattle battle)
    {
        using Font nameFont = new("Segoe UI", 18, FontStyle.Bold);
        using Font hpFont = new("Segoe UI", 13, FontStyle.Bold);
        using SolidBrush text = new(Color.FromArgb(244, 239, 229));
        using SolidBrush muted = new(Color.FromArgb(156, 149, 139));

        for (int i = 0; i < battle.Enemies.Count; i++)
        {
            Monster enemy = battle.Enemies[i];
            int column = i % 3;
            int row = i / 3;
            int x = 120 + column * 445;
            int y = row == 0 ? 42 : 285;
            Rectangle sprite = new(x + 35, y + 42, 280, 210);

            if (i == battle.TargetIndex && enemy.Alive)
                DrawTargetMarker(g, new Rectangle(x + 15, y + 24, 350, 250));

            if (!enemy.Alive)
            {
                using SolidBrush defeated = new(Color.FromArgb(70, 10, 9, 8));
                g.FillEllipse(defeated, sprite);
            }

            assets.DrawMonster(g, sprite, enemy);

            string label = enemy.Name.ToUpperInvariant();
            SizeF labelSize = g.MeasureString(label, nameFont);
            float labelX = x + 190 - labelSize.Width / 2;
            g.DrawString(label, nameFont, enemy.Alive ? text : muted, labelX, y - 4);

            DrawHpBar(g, new Rectangle(x + 88, y + 22, 240, 20), enemy.Hp, Math.Max(1, enemy.MaxHp));
            g.DrawString("HP", hpFont, text, x + 42, y + 17);
        }
    }

    private static void DrawTargetMarker(Graphics g, Rectangle bounds)
    {
        using Pen marker = new(Color.FromArgb(218, 181, 108), 4);
        int s = 22;
        g.DrawLine(marker, bounds.Left, bounds.Top, bounds.Left + s, bounds.Top);
        g.DrawLine(marker, bounds.Left, bounds.Top, bounds.Left, bounds.Top + s);
        g.DrawLine(marker, bounds.Right, bounds.Top, bounds.Right - s, bounds.Top);
        g.DrawLine(marker, bounds.Right, bounds.Top, bounds.Right, bounds.Top + s);
        g.DrawLine(marker, bounds.Left, bounds.Bottom, bounds.Left + s, bounds.Bottom);
        g.DrawLine(marker, bounds.Left, bounds.Bottom, bounds.Left, bounds.Bottom - s);
        g.DrawLine(marker, bounds.Right, bounds.Bottom, bounds.Right - s, bounds.Bottom);
        g.DrawLine(marker, bounds.Right, bounds.Bottom, bounds.Right, bounds.Bottom - s);
    }

    private void DrawPlayer(Graphics g, TurnBasedBattle battle)
    {
        Rectangle sprite = new(120, 420, 350, 260);
        assets.DrawPlayer(g, sprite, Direction.Right, battle.Player.Alive);

        using Font name = new("Segoe UI", 18, FontStyle.Bold);
        using Font stats = new("Segoe UI", 13, FontStyle.Bold);
        using SolidBrush text = new(Color.FromArgb(245, 240, 230));
        using SolidBrush muted = new(Color.FromArgb(176, 168, 156));

        g.DrawString(battle.Player.Name.ToUpperInvariant(), name, text, 60, 636);
        g.DrawString($"LV {battle.Player.Level}", stats, muted, 60, 666);
    }

    private void DrawTurnOrder(Graphics g, TurnBasedBattle battle)
    {
        Rectangle panel = new(540, 715, 700, 125);
        DrawWindow(g, panel, Color.FromArgb(26, 23, 20));
        DrawSectionTitle(g, "TURN ORDER", panel.X + 24, panel.Y + 13);

        int x = panel.X + 32;
        int y = panel.Y + 48;
        int size = 62;

        DrawTurnPortrait(g, new Rectangle(x, y, size, size), battle.Player, true);
        x += 78;

        foreach (Monster enemy in battle.Enemies)
        {
            DrawTurnPortrait(g, new Rectangle(x, y, size, size), enemy, enemy.Alive);
            x += 78;
            if (x + size > panel.Right - 20)
                break;
        }
    }

    private void DrawTurnPortrait(Graphics g, Rectangle destination, Player actor, bool alive)
    {
        assets.DrawPlayer(g, destination, Direction.Right, alive);
        using Pen border = new(Color.FromArgb(181, 160, 119), 3);
        g.DrawRectangle(border, destination);
    }

    private void DrawTurnPortrait(Graphics g, Rectangle destination, Monster monster, bool alive)
    {
        assets.DrawMonster(g, destination, monster);
        using Pen border = new(alive ? Color.FromArgb(181, 160, 119) : Color.FromArgb(78, 72, 65), 3);
        g.DrawRectangle(border, destination);
    }

    private void DrawMessageWindow(Graphics g, TurnBasedBattle battle)
    {
        Rectangle panel = new(540, 850, 700, 180);
        DrawWindow(g, panel, Color.FromArgb(22, 20, 18));

        using Font heading = new("Segoe UI", 15, FontStyle.Bold);
        using Font message = new("Segoe UI", 16, FontStyle.Bold);
        using SolidBrush gold = new(Color.FromArgb(211, 177, 109));
        using SolidBrush text = new(Color.FromArgb(245, 240, 230));

        string title = battle.Phase == BattlePhase.PlayerTurn ? "YOUR TURN" : "ENEMY TURN";
        g.DrawString(title, heading, gold, panel.X + 26, panel.Y + 18);
        g.DrawString(battle.Message, message, text, panel.X + 26, panel.Y + 64, new StringFormat { Trimming = StringTrimming.EllipsisCharacter });
    }

    private void DrawCommandWindow(Graphics g, TurnBasedBattle battle)
    {
        Rectangle menu = new(1290, 505, 490, 525);
        DrawWindow(g, menu, Color.FromArgb(22, 20, 18));
        DrawSectionTitle(g, "COMMAND", menu.X + 28, menu.Y + 24);

        using Font itemFont = new("Segoe UI", 20, FontStyle.Bold);
        using SolidBrush text = new(Color.FromArgb(239, 234, 224));
        using SolidBrush selected = new(Color.FromArgb(211, 177, 109));
        using SolidBrush selector = new(Color.FromArgb(226, 216, 194));

        int y = menu.Y + 82;
        for (int i = 0; i < MenuLabels.Length; i++)
        {
            bool active = i == game.BattleMenuIndex;
            if (active)
            {
                using SolidBrush highlight = new(Color.FromArgb(58, 52, 45));
                g.FillRectangle(highlight, menu.X + 24, y - 8, menu.Width - 48, 62);
                Point[] arrow =
                [
                    new(menu.X + 32, y + 18),
                    new(menu.X + 50, y + 7),
                    new(menu.X + 50, y + 29)
                ];
                g.FillPolygon(selector, arrow);
            }

            g.DrawString(MenuLabels[i], itemFont, active ? selected : text, menu.X + 72, y);
            y += 78;
        }
    }

    private static void DrawSectionTitle(Graphics g, string title, int x, int y)
    {
        using Font font = new("Segoe UI", 14, FontStyle.Bold);
        using SolidBrush brush = new(Color.FromArgb(219, 190, 131));
        using Pen line = new(Color.FromArgb(100, 89, 72), 2);
        g.DrawString(title, font, brush, x, y);
        float width = g.MeasureString(title, font).Width;
        g.DrawLine(line, x + width + 16, y + 12, x + 320, y + 12);
    }

    private static void DrawWindow(Graphics g, Rectangle rectangle, Color fill)
    {
        using SolidBrush brush = new(fill);
        using Pen outer = new(Color.FromArgb(191, 163, 111), 4);
        using Pen inner = new(Color.FromArgb(72, 64, 54), 2);
        g.FillRectangle(brush, rectangle);
        g.DrawRectangle(outer, rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1);
        g.DrawRectangle(inner, rectangle.X + 8, rectangle.Y + 8, rectangle.Width - 17, rectangle.Height - 17);
    }

    private static void DrawHpBar(Graphics g, Rectangle rectangle, int hp, int maxHp)
    {
        float ratio = Math.Clamp(hp / (float)Math.Max(1, maxHp), 0f, 1f);
        using SolidBrush empty = new(Color.FromArgb(18, 16, 14));
        using SolidBrush fill = new(Color.FromArgb(177, 45, 35));
        using Pen border = new(Color.FromArgb(210, 196, 167), 2);
        g.FillRectangle(empty, rectangle);
        if (ratio > 0)
            g.FillRectangle(fill, rectangle.X + 3, rectangle.Y + 3, Math.Max(1, (int)((rectangle.Width - 6) * ratio)), rectangle.Height - 6);
        g.DrawRectangle(border, rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1);
    }

    private void DrawAnimation(Graphics g, TurnBasedBattle battle)
    {
        if (!IsAnimating || animationBattle != battle)
            return;

        if (animationPlayerPhase)
        {
            float progress = Math.Min(1f, animationFrame / 22f);
            float pulse = MathF.Sin(progress * MathF.PI);
            Monster target = battle.CurrentEnemy;
            Rectangle baseRect = GetEnemySpriteRectangle(target, battle);
            using SolidBrush flash = new(Color.FromArgb((int)(90 * pulse), 235, 65, 45));
            g.FillEllipse(flash, baseRect.X - 10, baseRect.Y - 10, baseRect.Width + 20, baseRect.Height + 20);

            using Font damageFont = new("Segoe UI", 22, FontStyle.Bold);
            using SolidBrush damageBrush = new(Color.FromArgb(255, 230, 190));
            if (animationDamage.TryGetValue(target, out int damage) && damage > 0)
                g.DrawString($"-{damage}", damageFont, damageBrush, baseRect.X + 70, baseRect.Y - 15 - (int)(pulse * 35));
        }
        else
        {
            List<Monster> attackers = animationBattle.Enemies
                .Where(enemy => animationDamage.GetValueOrDefault(enemy) > 0)
                .ToList();
            if (animationEnemyIndex < attackers.Count)
            {
                Monster attacker = attackers[animationEnemyIndex];
                Rectangle rect = GetEnemySpriteRectangle(attacker, battle);
                float pulse = MathF.Sin(Math.Min(1f, animationFrame / 18f) * MathF.PI);
                using SolidBrush flash = new(Color.FromArgb((int)(90 * pulse), 220, 75, 45));
                g.FillEllipse(flash, rect.X - 8, rect.Y - 8, rect.Width + 16, rect.Height + 16);
            }

            if (animationPlayerDamage > 0)
            {
                using Font damageFont = new("Segoe UI", 22, FontStyle.Bold);
                using SolidBrush damageBrush = new(Color.FromArgb(255, 210, 170));
                g.DrawString($"-{animationPlayerDamage}", damageFont, damageBrush, 390, 430 - animationFrame);
            }
        }
    }

    private Rectangle GetEnemySpriteRectangle(Monster enemy, TurnBasedBattle battle)
    {
        int index = battle.Enemies.IndexOf(enemy);
        int column = index % 3;
        int row = index / 3;
        int x = 120 + column * 445;
        int y = row == 0 ? 42 : 285;
        return new Rectangle(x + 35, y + 42, 280, 210);
    }
}
