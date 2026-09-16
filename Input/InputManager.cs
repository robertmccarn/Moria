using System.Windows.Forms;
using Moria.Core;

namespace Moria.Input;

public static class InputManager
{
    public static GameAction Translate(Keys key) => key switch
    {
        Keys.Up or Keys.W => GameAction.MoveUp,
        Keys.Down or Keys.S => GameAction.MoveDown,
        Keys.Left or Keys.A => GameAction.MoveLeft,
        Keys.Right or Keys.D => GameAction.MoveRight,
        Keys.Q => GameAction.DrinkPotion,
        Keys.E => GameAction.Interact,
        Keys.F => GameAction.ChatLog,
        Keys.Escape => GameAction.Quit,
        Keys.Enter or Keys.Space or Keys.N => GameAction.NewRun,
        _ => GameAction.None
    };
}
