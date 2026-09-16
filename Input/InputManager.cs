using System.Windows.Forms;
using Moria.Core;

namespace Moria.Input;

public static class InputManager
{
    private const int DoubleTapWindowMilliseconds = 250;
    private static readonly HashSet<Keys> HeldMovementKeys = new();
    private static readonly Dictionary<Keys, long> LastMovementPress = new();
    private static Direction pendingRollDirection = Direction.None;

    public static GameAction Translate(Keys key)
    {
        if (IsMovementKey(key))
        {
            if (!HeldMovementKeys.Contains(key))
            {
                long now = Environment.TickCount64;
                if (LastMovementPress.TryGetValue(key, out long lastPress) &&
                    now - lastPress <= DoubleTapWindowMilliseconds)
                {
                    pendingRollDirection = ToDirection(key);
                    LastMovementPress.Remove(key);
                }
                else
                {
                    LastMovementPress[key] = now;
                }

                HeldMovementKeys.Add(key);
            }

            return GameAction.None;
        }

        return key switch
        {
            Keys.Q => GameAction.DrinkPotion,
            Keys.E => GameAction.Interact,
            Keys.F => GameAction.ChatLog,
            Keys.Escape => GameAction.Quit,
            Keys.Enter or Keys.Space or Keys.N => GameAction.NewRun,
            _ => GameAction.None
        };
    }

    public static Direction ConsumeRollDirection()
    {
        Direction direction = pendingRollDirection;
        pendingRollDirection = Direction.None;
        return direction;
    }

    public static void Release(Keys key) => HeldMovementKeys.Remove(key);

    public static void ClearMovementKeys()
    {
        HeldMovementKeys.Clear();
        LastMovementPress.Clear();
        pendingRollDirection = Direction.None;
    }

    public static (float X, float Y) GetMovementVector()
    {
        float x = (IsHeld(Keys.Right) || IsHeld(Keys.D) ? 1f : 0f) -
                  (IsHeld(Keys.Left) || IsHeld(Keys.A) ? 1f : 0f);
        float y = (IsHeld(Keys.Down) || IsHeld(Keys.S) ? 1f : 0f) -
                  (IsHeld(Keys.Up) || IsHeld(Keys.W) ? 1f : 0f);

        float length = MathF.Sqrt(x * x + y * y);
        return length > 0f ? (x / length, y / length) : (0f, 0f);
    }

    private static bool IsHeld(Keys key) => HeldMovementKeys.Contains(key);

    private static bool IsMovementKey(Keys key) => key is
        Keys.Up or Keys.Down or Keys.Left or Keys.Right or
        Keys.W or Keys.A or Keys.S or Keys.D;

    private static Direction ToDirection(Keys key) => key switch
    {
        Keys.Up or Keys.W => Direction.Up,
        Keys.Down or Keys.S => Direction.Down,
        Keys.Left or Keys.A => Direction.Left,
        Keys.Right or Keys.D => Direction.Right,
        _ => Direction.None
    };
}
