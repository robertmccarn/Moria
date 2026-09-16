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
    private const int PlayerAttackCooldownMilliseconds = 350;
    private const float PlayerRollDistance = 2.25f;
    private const float PlayerRollDurationSeconds = 0.18f;

    private long lastPlayerAttackMilliseconds;
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
        float secondsRemaining = Math.Max(0f, (rollEndMilliseconds - now) / 1000f);
        float secondsThisFrame = Math.Min(0.05f, Math.Max(0f, secondsRemaining));
        float desiredStep = rollRemainingDistance * (secondsRemaining <= 0f ? 1f : Math.Min(1f, secondsThisFrame / secondsRemaining));
        desiredStep = Math.Min(desiredStep, rollRemainingDistance);

        if (desiredStep > 0f)
        {
            float oldX = player.WorldPosition.X;
            float oldY = player.WorldPosition.Y;
            MovePlayerWorld(rollDirectionX * desiredStep, rollDirectionY * desiredStep);
            float moved = MathF.Abs(player.WorldPosition.X - oldX) + MathF.Abs(player.WorldPosition.Y - oldY);
            rollRemainingDistance -= moved;
        }

        if (rollRemainingDistance <= 0.01f || now >= rollEndMilliseconds)
        {
            playerRolling = false;
            rollRemainingDistance = 0f;
        }
    }

    private void MovePlayerWorld(float dx, float dy)
    {
        float nextX = player.WorldPosition.X + dx;
        float nextY = player.WorldPosition.Y + dy;

        if (CanOccupy(nextX, player.WorldPosition.Y))
            player.WorldPosition = new PointF(nextX, player.WorldPosition.Y);
        if (CanOccupy(player.WorldPosition.X, nextY))
            player.WorldPosition = new PointF(player.WorldPosition.X, nextY);

        Position currentTile = player.Position;
        if (currentTile == lastMovementTile)
        {
            TryAttackFromMovement();
            return;
        }

        lastMovementTile = currentTile;
        RecalculateVisibility(7);
        HandleMovementTile(currentTile);
        TryAttackFromMovement();
    }

    private bool CanOccupy(float x, float y)
    {
        float r = PlayerCollisionRadius;
        return IsWalkableWorldPoint(x - r, y - r) &&
               IsWalkableWorldPoint(x + r, y - r) &&
               IsWalkableWorldPoint(x - r, y + r) &&
               IsWalkableWorldPoint(x + r, y + r);
    }

    private bool IsWalkableWorldPoint(float x, float y)
    {
        Position tile = WorldToTile(x, y);
        return dungeon.IsWalkable(tile);
    }

    private static Position WorldToTile(float x, float y) => new(
        (int)MathF.Floor(y + 0.5f),
        (int)MathF.Floor(x + 0.5f));

    private void TryAttackFromMovement()
    {
        Monster? monster = dungeon.MonsterAt(player.Position);
        if (monster == null || !monster.Alive)
            return;

        long elapsed = CurrentRunElapsed.Ticks / TimeSpan.TicksPerMillisecond;
        if (elapsed - lastPlayerAttackMilliseconds < PlayerAttackCooldownMilliseconds)
            return;

        lastPlayerAttackMilliseconds = elapsed;
        Attack(monster);
    }

    private void HandleMovementTile(Position tile)
    {
        Tile current = dungeon[tile];
        if (current.Type == TileType.Trap)
        {
            int damage = random.Next(1, 5);
            player.Hp -= damage;
            SetMessage($"A trap wounds you for {damage}!");
        }
        else if (tile == dungeon.DownStairs)
        {
            SetMessage("You found the stairs down. Press E to descend.");
        }

        AutoLoot();
    }
}
