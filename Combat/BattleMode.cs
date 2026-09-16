using System.Drawing;
using System.Windows.Forms;
using Moria.Art;
using Moria.Combat;
using Moria.Core;
using Moria.Entities;
using Moria.Input;
using Moria.UI;

namespace Moria;

public sealed partial class Game
{
    internal TurnBasedBattle? CurrentBattle { get; private set; }
    internal int BattleMenuIndex => battleMenuIndex;
    private BattleOverlayControl? battleOverlay;
    private int battleMenuIndex;

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

        playerRolling = false;
        InputManager.ClearMovementKeys();
        CurrentBattle = new TurnBasedBattle(player, monster, combat, random);
        battleMenuIndex = 0;
        battleOverlay!.Visible = true;
        battleOverlay.BringToFront();
        SetMessage($"A {monster.Name} blocks your path!");
        battleOverlay.Invalidate();
    }

    private void FinishBattle(TurnBasedBattle battle)
    {
        if (battle.Phase == BattlePhase.Victory)
        {
            Monster monster = battle.Enemy;
            int xp = monster.ExperienceValue;
            int gold = random.Next(4, 13) + player.DungeonLevel * 2;
            player.Gold += gold;
            int oldLevel = player.Level;
            player.GainExperience(xp);
            DropLoot(monster.Position, player.DungeonLevel);
            AutoLoot();
            SetMessage($"Victory! +{xp} XP, +{gold} gold.");
            if (player.Level > oldLevel)
                SetMessage($"LEVEL UP! You are now level {player.Level}.");
            if (player.DungeonLevel == Dungeon.MaximumDepth && monster.IsBoss)
                WinRun();
        }
        else if (battle.Phase == BattlePhase.Fled)
        {
            EscapeBattlePosition(battle.Enemy.Position);
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

        Rectangle arena = new(180, 105, 1560, 560);
        using SolidBrush arenaBrush = new(Color.FromArgb(235, 10, 11, 15));
        using Pen arenaBorder = new(Color.FromArgb(150, 112, 91, 47), 3);
        g.FillRectangle(arenaBrush, arena);
        g.DrawRectangle(arenaBorder, arena);

        DrawEnemy(g, battle);
        DrawPlayer(g, battle);
        DrawStatus(g, battle);
        DrawCommandWindow(g, battle);
    }

    private void DrawEnemy(Graphics g, TurnBasedBattle battle)
    {
        Rectangle sprite = new(430, 185, battle.Enemy.IsBoss ? 260 : 190, battle.Enemy.IsBoss ? 260 : 190);
        assets.DrawMonster(g, sprite, battle.Enemy);

        using Font name = new("Segoe UI", 16, FontStyle.Bold);
        using SolidBrush text = new(Color.Gainsboro);
        using SolidBrush muted = new(Color.FromArgb(170, 175, 185));
        g.DrawString(battle.Enemy.Name.ToUpperInvariant(), name, text, 395, 460);
        g.DrawString($"LV {battle.Enemy.Level}    HP {Math.Max(0, battle.Enemy.Hp)}", new Font("Segoe UI", 11, FontStyle.Bold), muted, 395, 487);
        DrawBar(g, new Rectangle(395, 515, 330, 16), battle.Enemy.Hp, Math.Max(1, battle.Enemy.MaxHp));
    }

    private void DrawPlayer(Graphics g, TurnBasedBattle battle)
    {
        Rectangle sprite = new(1120, 190, 180, 180);
        assets.DrawPlayer(g, sprite, Direction.Left, battle.Player.Alive);

        using Font name = new("Segoe UI", 16, FontStyle.Bold);
        using SolidBrush text = new(Color.Gainsboro);
        using SolidBrush muted = new(Color.FromArgb(170, 175, 185));
        g.DrawString(battle.Player.Name.ToUpperInvariant(), name, text, 1065, 460);
        g.DrawString($"LV {battle.Player.Level}    HP {Math.Max(0, battle.Player.Hp)}/{battle.Player.TotalMaxHp}", new Font("Segoe UI", 11, FontStyle.Bold), muted, 1065, 487);
        DrawBar(g, new Rectangle(1065, 515, 330, 16), battle.Player.Hp, Math.Max(1, battle.Player.TotalMaxHp));
    }

    private static void DrawStatus(Graphics g, TurnBasedBattle battle)
    {
        using Font phase = new("Segoe UI", 11, FontStyle.Bold);
        using SolidBrush gold = new(UiTheme.Gold);
        using SolidBrush text = new(UiTheme.Text);
        string heading = battle.Phase == BattlePhase.PlayerTurn ? "YOUR TURN" : "ENEMY TURN";
        g.DrawString(heading, phase, gold, 820, 125);
        using Font message = new("Segoe UI", 10, FontStyle.Bold);
        g.DrawString(battle.Message, message, text, 820, 150);
    }

    private void DrawCommandWindow(Graphics g, TurnBasedBattle battle)
    {
        Rectangle menu = new(500, 690, 920, 250);
        using SolidBrush panel = new(Color.FromArgb(245, 8, 9, 13));
        using Pen border = new(Color.FromArgb(175, 112, 91, 47), 3);
        g.FillRectangle(panel, menu);
        g.DrawRectangle(border, menu);

        using Font heading = new("Segoe UI", 11, FontStyle.Bold);
        using SolidBrush gold = new(UiTheme.Gold);
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
        g.DrawString("1-5 SELECT    ↑/↓ MOVE    ENTER CONFIRM", hint, muted, menu.X + 24, menu.Bottom - 30);
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
