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

    private long lastPlayerAttackMilliseconds;
    private Position lastMovementTile;

    protected override void OnKeyUp(KeyEventArgs e)
    {
        InputManager.Release(e.KeyCode);
        base.OnKeyUp(e);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        InputManager.ClearMovementKeys();
        base.OnDeactivate(e);
    }

    private void ProcessPlayerMovement()
    {
        (float dx, float dy) = InputManager.GetMovementVector();
        if (dx == 0f && dy == 0f)
            return;

        float step = PlayerMoveSpeed * 0.05f;
        float nextX = player.WorldPosition.X + dx * step;
        float nextY = player.WorldPosition.Y + dy * step;

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
