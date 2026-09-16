using System.Drawing;
using System.Windows.Forms;
using Moria.Combat;
using Moria.Core;
using Moria.Entities;
using Moria.Input;
using Moria.Items;
using Moria.World;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace Moria;

public sealed partial class Game : Form
{
    private const int TileSize = 16;
    private const int MapWidth = 640;
    private const int MapHeight = 256;
    private const int StatusHeight = 104;
    private const string MetaFile = "moria.meta";

    private readonly Random random = new();
    private readonly WinFormsTimer redrawTimer;
    private readonly List<string> chatLog = new();
    private readonly FieldOfView fieldOfView = new();
    private readonly CombatSystem combat = new();
    private Dungeon dungeon = null!;
    private Player player = null!;
    private VisibilityMap visibility = null!;
    private string message = "Welcome to Moria.";
    private bool running = true;
    private bool started;
    private bool runOver;
    private bool victory;
    private int lastRunReward;

    public int LastRunReward => lastRunReward;
    public IReadOnlyList<string> ChatLog => chatLog;

    public Game()
    {
        Text = "Moria — Roguelite";
        ClientSize = new Size(1920, 1080);
        BackColor = Color.Black;
        ForeColor = Color.Gainsboro;
        DoubleBuffered = true;
        KeyPreview = true;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        KeyDown += OnKeyDown;
        FormClosed += (_, _) =>
        {
            running = false;
            redrawTimer.Stop();
            redrawTimer.Dispose();
        };

        redrawTimer = new WinFormsTimer { Interval = 50 };
        redrawTimer.Tick += (_, _) => Invalidate();
        redrawTimer.Start();
        Shown += (_, _) => BeginGame();
    }

    private void BeginGame()
    {
        string name = AskForName();
        if (name.Length == 0)
        {
            Close();
            return;
        }

        Player newPlayer = new(name, new Position(1, 1), (int)Math.Min(LoadLegacyGold(), int.MaxValue));
        player = newPlayer;
        StartRun(newPlayer);
        started = true;
        Focus();
        Invalidate();
    }

    private string AskForName()
    {
        using Form dialog = new()
        {
            Text = "New Adventurer",
            ClientSize = new Size(390, 150),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false
        };
        Label label = new() { Text = "Warrior name:", Left = 18, Top = 18, AutoSize = true };
        TextBox nameBox = new() { Left = 18, Top = 44, Width = 350 };
        Button button = new() { Text = "Enter Moria", Left = 252, Top = 88, Width = 116, DialogResult = DialogResult.OK };
        dialog.Controls.AddRange([label, nameBox, button]);
        dialog.AcceptButton = button;
        return dialog.ShowDialog(this) == DialogResult.OK ? nameBox.Text.Trim() : string.Empty;
    }

    private void StartRun(Player currentPlayer)
    {
        dungeon = new Dungeon(random.Next());
        currentPlayer.DungeonLevel = 1;
        currentPlayer.Hp = currentPlayer.TotalMaxHp;
        currentPlayer.Mana = currentPlayer.MaxMana;
        currentPlayer.Gold = 100;
        currentPlayer.Experience = 0;
        dungeon.Generate(currentPlayer.DungeonLevel);
        visibility = new VisibilityMap(dungeon.Width, dungeon.Height);
        currentPlayer.Position = dungeon.UpStairs;
        RecalculateVisibility(8);
        runOver = false;
        victory = false;
        chatLog.Clear();
        SetMessage($"Depth {currentPlayer.DungeonLevel}. Find the stairs down.");
    }
