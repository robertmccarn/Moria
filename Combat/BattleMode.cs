using System.Drawing;
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
        if (CurrentBattle == null)
            return false;

        battleMenuIndex = Math.Clamp(index, 0, 4);
        BattleAction action = (BattleAction)battleMenuIndex;
        CurrentBattle.Execute(action);
        SetMessage(CurrentBattle.Message);
        battleOverlay?.Invalidate();

        if (CurrentBattle.Finished)
            FinishBattle(CurrentBattle);

        return true;
    }

    private void BeginBattle(Monster monster)
    {
        if (CurrentBattle != null || !monster.Alive)
            return;

        List<Monster> group = dungeon.Monsters
            .Where(enemy => enemy.Alive)
            .Where(enemy => Math.Abs(enemy.Position.Y - player.Position.Y) + Math.Abs(enemy.Position.X - player.Position.X) <= BattleGroupRadius)
            .OrderBy(enemy => Math.Abs(enemy.Position.Y - player.Position.Y) + Math.Abs(enemy.Position.X - player.Position.X))
            .Take(BattleGroupLimit)
            .ToList();

        if (!group.Contains(monster))
            group.Insert(0, monster);

        playerRolling = false;
        InputManager.ClearMovementKeys();
        CurrentBattle = new TurnBasedBattle(player, group, combat, random);
        battleMenuIndex = 0;
        battleOverlay!.Visible = true;
        battleOverlay.BringToFront();
        SetMessage(group.Count == 1 ? $"A {monster.Name} blocks your path!" : $"{group.Count} enemies surround you!");
        battleOverlay.Invalidate();
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

    private static readonly string[] MenuLabels =
    [
        "ATTACK",
        "POWER STRIKE",
        "POTION",
        "DEFEND",
        "FLEE"
    ];

    public BattleOverlayControl(Game game)
    {
        this.game = game;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override bool ShowFocusCues => false;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        TurnBasedBattle? battle = game.CurrentBattle;
        if (battle == null)
            return;

        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.Clear(Color.Transparent);

        using SolidBrush shade = new(Color.FromArgb(205, 0, 0, 0));
        g.FillRectangle(shade, ClientRectangle);

        Rectangle arena = new(140, 90, 1640, 590);
        using SolidBrush arenaBrush = new(Color.FromArgb(235, 10, 11, 15));
        using Pen arenaBorder = new(Color.FromArgb(150, 112, 91, 47), 3);
        g.FillRectangle(arenaBrush, arena);
        g.DrawRectangle(arenaBorder, arena);

        DrawEnemies(g, battle);
        DrawPlayer(g, battle);
        DrawStatus(g, battle);
        DrawCommandWindow(g);
    }

    private void DrawEnemies(Graphics g, TurnBasedBattle battle)
    {
        using Font name = new("Segoe UI", 11, FontStyle.Bold);
        using Font details = new("Segoe UI", 8.5f, FontStyle.Bold);
        using SolidBrush text = new(Color.Gainsboro);
        using SolidBrush muted = new(Color.FromArgb(165, 175, 185));
        using SolidBrush targetBrush = new(Color.FromArgb(70, 112, 91, 47));

        for (int i = 0; i < battle.Enemies.Count; i++)
        {
            Monster enemy = battle.Enemies[i];
            int column = i % 3;
            int row = i / 3;
            int x = 205 + column * 285;
            int y = 145 + row * 235;
            Rectangle card = new(x, y, 255, 215);

            if (i == battle.TargetIndex && enemy.Alive)
                g.FillRectangle(targetBrush, card);

            Rectangle sprite = new(x + 55, y + 8, 145, 145);
            assets.DrawMonster(g, sprite, enemy);
            g.DrawString($"{i + 1}. {enemy.Name.ToUpperInvariant()}", name, enemy.Alive ? text : muted, x + 12, y + 155);
            g.DrawString($"LV {enemy.Level}    HP {Math.Max(0, enemy.Hp)}/{enemy.MaxHp}", details, muted, x + 12, y + 177);
            DrawBar(g, new Rectangle(x + 12, y + 194, 225, 10), enemy.Hp, Math.Max(1, enemy.MaxHp));
        }
    }

    private void DrawPlayer(Graphics g, TurnBasedBattle battle)
    {
        Rectangle panel = new(1175, 120, 470, 475);
        using SolidBrush panelBrush = new(Color.FromArgb(105, 0, 0, 0));
        using Pen border = new(Color.FromArgb(100, 112, 91, 47), 2);
        g.FillRectangle(panelBrush, panel);
        g.DrawRectangle(border, panel);

        Rectangle sprite = new(1320, 160, 180, 180);
        assets.DrawPlayer(g, sprite, Direction.Left, battle.Player.Alive);

        using Font name = new("Segoe UI", 16, FontStyle.Bold);
        using Font details = new("Segoe UI", 11, FontStyle.Bold);
        using SolidBrush text = new(Color.Gainsboro);
        using SolidBrush muted = new(Color.FromArgb(170, 175, 185));
        g.DrawString(battle.Player.Name.ToUpperInvariant(), name, text, 1250, 365);
        g.DrawString($"LV {battle.Player.Level}    HP {Math.Max(0, battle.Player.Hp)}/{battle.Player.TotalMaxHp}", details, muted, 1250, 393);
        DrawBar(g, new Rectangle(1250, 423, 320, 16), battle.Player.Hp, Math.Max(1, battle.Player.TotalMaxHp));
        g.DrawString("LEFT / RIGHT: TARGET", details, muted, 1250, 465);
        g.DrawString("Enemies act in turn order after your action.", details, muted, 1250, 492);
    }

    private static void DrawStatus(Graphics g, TurnBasedBattle battle)
    {
        using Font phase = new("Segoe UI", 11, FontStyle.Bold);
        using Font message = new("Segoe UI", 9.5f, FontStyle.Bold);
        using SolidBrush gold = new(UiTheme.Gold);
        using SolidBrush text = new(UiTheme.Text);
        string heading = battle.Phase == BattlePhase.PlayerTurn ? "YOUR TURN" : "ENEMY TURN";
        g.DrawString(heading, phase, gold, 800, 105);
        g.DrawString(battle.Message, message, text, 800, 132);
    }

    private void DrawCommandWindow(Graphics g)
    {
        Rectangle menu = new(500, 700, 920, 250);
        using SolidBrush panel = new(Color.FromArgb(245, 8, 9, 13));
        using Pen border = new(Color.FromArgb(175, 112, 91, 47), 3);
        using Font heading = new("Segoe UI", 11, FontStyle.Bold);
        using SolidBrush gold = new(UiTheme.Gold);
        g.FillRectangle(panel, menu);
        g.DrawRectangle(border, menu);
        g.DrawString("COMMAND", heading, gold, menu.X + 24, menu.Y + 18);

        for (int i = 0; i < MenuLabels.Length; i++)
        {
            int x = menu.X + 34 + (i % 3) * 285;
            int y = menu.Y + 62 + (i / 3) * 72;
            bool selected = i == game.BattleMenuIndex;
            if (selected)
            {
                using SolidBrush highlight = new(Color.FromArgb(100, 112, 91, 47));
                g.FillRectangle(highlight, x - 12, y - 7, 245, 42);
            }

            using SolidBrush number = new(UiTheme.Gold);
            using SolidBrush label = new(selected ? Color.White : Color.FromArgb(185, 188, 198));
            g.DrawString($"{i + 1}", heading, number, x, y);
            g.DrawString(MenuLabels[i], heading, label, x + 28, y);
        }

        using Font hint = new("Segoe UI", 8.5f, FontStyle.Bold);
        using SolidBrush muted = new(UiTheme.Muted);
        g.DrawString("1-5 SELECT    ↑/↓ COMMAND    ←/→ TARGET    ENTER CONFIRM", hint, muted, menu.X + 24, menu.Bottom - 30);
    }

    private static void DrawBar(Graphics g, Rectangle rect, int value, int maximum)
    {
        using SolidBrush background = new(Color.FromArgb(50, 55, 62));
        using Pen border = new(Color.FromArgb(130, 100, 104, 112));
        g.FillRectangle(background, rect);
        g.DrawRectangle(border, rect);
        int width = Math.Clamp(rect.Width * Math.Max(0, value) / Math.Max(1, maximum), 0, rect.Width);
        if (width > 0)
        {
            using SolidBrush fill = new(Color.FromArgb(190, 145, 50, 45));
            g.FillRectangle(fill, rect.X + 1, rect.Y + 1, Math.Max(1, width - 1), rect.Height - 2);
        }
    }
}
