using System.Drawing;
using System.Windows.Forms;
using Moria.Core;
using Moria.Input;
using Moria.Entities;
using Moria.World;

namespace Moria;

public sealed partial class Game
{
    // Tuned toward the brisk, responsive feel of Link's movement rather than
    // the slower pace of the original tile-by-tile roguelike movement.
    private const float PlayerMoveSpeed = 7.5f;
    private const float PlayerCollisionRadius = 0.28f;
    private const float PlayerRollDistance = 2.25f;
    private const float PlayerRollDurationSeconds = 0.18f;

    private Position lastMovementTile;
    private bool playerRolling;
    private float rollRemainingDistance;
    private float rollDirectionX;
    private float rollDirectionY;
    private long rollEndMilliseconds;

    protected override void OnKeyUp(KeyEventArgs e)
    {
        InputManager.Release(e.KeyCode);
        base.OnKeyUp(e);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        InputManager.ClearMovementKeys();
        playerRolling = false;
        base.OnDeactivate(e);
    }

    private void ProcessPlayerMovement()
    {
        if (playerRolling)
        {
            ProcessRoll();
            return;
        }

        Direction rollDirection = InputManager.ConsumeRollDirection();
        if (rollDirection != Direction.None)
        {
            StartRoll(rollDirection);
            ProcessRoll();
            return;
        }

        (float dx, float dy) = InputManager.GetMovementVector();
        if (dx == 0f && dy == 0f)
            return;

        float step = PlayerMoveSpeed * 0.05f;
        MovePlayerWorld(dx * step, dy * step);
    }

    private void StartRoll(Direction direction)
    {
        playerRolling = true;
        rollRemainingDistance = PlayerRollDistance;
        (rollDirectionX, rollDirectionY) = direction switch
        {
            Direction.Up => (0f, -1f),
            Direction.Down => (0f, 1f),
            Direction.Left => (-1f, 0f),
            Direction.Right => (1f, 0f),
            _ => (0f, 0f)
        };

        long now = CurrentRunElapsed.Ticks / TimeSpan.TicksPerMillisecond;
        rollEndMilliseconds = now + (long)(PlayerRollDurationSeconds * 1000f);
        SetMessage("Roll!");
    }

    private void ProcessRoll()
    {
        long now = CurrentRunElapsed.Ticks / TimeSpan.TicksPerMillisecond;
        if (now >= rollEndMilliseconds || rollRemainingDistance <= 0f)
        {
            playerRolling = false;
            return;
        }

        float totalDuration = PlayerRollDurationSeconds;
        float remainingMilliseconds = rollEndMilliseconds - now;
        float distanceThisFrame = PlayerRollDistance / totalDuration * 0.05f;
        distanceThisFrame = Math.Min(distanceThisFrame, rollRemainingDistance);
        MovePlayerWorld(rollDirectionX * distanceThisFrame, rollDirectionY * distanceThisFrame);
        rollRemainingDistance -= distanceThisFrame;

        if (CurrentBattle != null)
            playerRolling = false;
    }

    private void MovePlayerWorld(float dx, float dy)
    {
        if (CurrentBattle != null)
            return;

        PointF current = player.WorldPosition;
        PointF next = new(current.X + dx, current.Y + dy);
        int targetX = (int)MathF.Round(next.X);
        int targetY = (int)MathF.Round(next.Y);
        Position targetTile = new(targetY, targetX);

        if (!dungeon.IsWalkable(targetTile))
            return;

        Monster? monster = dungeon.MonsterAt(targetTile);
        if (monster != null)
        {
            TryAttackFromMovement(monster);
            return;
        }

        player.WorldPosition = next;
        Position currentTile = new((int)MathF.Round(next.Y), (int)MathF.Round(next.X));
        if (currentTile != lastMovementTile)
        {
            lastMovementTile = currentTile;
            RecalculateVisibility(7);
            Tile tile = dungeon[currentTile];
            if (tile.Type == TileType.Trap)
            {
                int damage = new Random().Next(1, 5);
                player.Hp -= damage;
                SetMessage($"A trap wounds you for {damage}!");
            }
            else if (currentTile == dungeon.DownStairs)
            {
                SetMessage("You found the stairs down. Press E to descend.");
            }
            AutoLoot();
        }
    }

    private void TryAttackFromMovement(Monster monster)
    {
        if (CurrentBattle != null)
            return;

        BeginBattle(monster);
    }
}
