namespace Moria.Core;

public readonly record struct Position(int Y, int X)
{
    public Position Step(Direction direction) => direction switch
    {
        Direction.Up => new(Y - 1, X),
        Direction.Down => new(Y + 1, X),
        Direction.Left => new(Y, X - 1),
        Direction.Right => new(Y, X + 1),
        _ => this
    };
}

public enum Direction { None, Up, Down, Left, Right }
