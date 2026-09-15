using System.Collections.Generic;

namespace Moria.World;

public class Dungeon
{
    public const int Width = 80;
    public const int Height = 24;

    private readonly CaveTile[,] tiles = new CaveTile[Height, Width];

    private readonly Random random = new();

    private readonly List<Room> rooms = new();

    public Dungeon()
    {
        FillWithRock();
    }

    public void GenerateCave()
    {
        FillWithRock();
        rooms.Clear();

        GenerateRooms();
        ConnectRooms();
        PlaceBoundary();
    }

    private void GenerateRooms()
    {
        for (int i = 0; i < 10; i++)
        {
            int y = RandomInt(15) + 4;
            int x = RandomInt(56) + 11;

            BuildRoom(y, x);

            rooms.Add(new Room(y, x));
        }
    }

    private void ConnectRooms()
    {
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            ConnectRoom(
                rooms[i].Y,
                rooms[i].X,
                rooms[i + 1].Y,
                rooms[i + 1].X);
        }
    }

    private void ConnectRoom(int y1, int x1, int y2, int x2)
    {
        int currentY = y1;
        int currentX = x1;

        while (currentY != y2 || currentX != x2)
        {
            Direction direction = CorrectDirection(
                currentY,
                currentX,
                y2,
                x2);

            int nextY = currentY;
            int nextX = currentX;

            Move(ref nextY, ref nextX, direction);

            if (!IsInsideDungeon(nextY, nextX))
            {
                break;
            }

            currentY = nextY;
            currentX = nextX;

            CarveTile(currentY, currentX);
        }
    }

    private void FillWithRock()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                tiles[y, x] = new CaveTile
                {
                    IsOpen = false,
                    IsMemorized = false,
                    IsLit = false,
                    Symbol = '#'
                };
            }
        }
    }

    private void BuildRoom(int y, int x)
    {
        int yHeight = y - RandomInt(4);
        int yDepth = y + RandomInt(3);

        int xLeft = x - RandomInt(11);
        int xRight = x + RandomInt(11);

        for (int currentY = yHeight; currentY <= yDepth; currentY++)
        {
            for (int currentX = xLeft; currentX <= xRight; currentX++)
            {
                CarveTile(currentY, currentX);
            }
        }

        for (int currentY = yHeight - 1; currentY <= yDepth + 1; currentY++)
        {
            tiles[currentY, xLeft - 1].Symbol = '#';
            tiles[currentY, xRight + 1].Symbol = '#';
        }

        for (int currentX = xLeft; currentX <= xRight; currentX++)
        {
            tiles[yHeight - 1, currentX].Symbol = '#';
            tiles[yDepth + 1, currentX].Symbol = '#';
        }
    }

    private void CarveTile(int y, int x)
    {
        tiles[y, x].IsOpen = true;
        tiles[y, x].IsLit = true;
        tiles[y, x].Symbol = '.';
    }

    private Direction CorrectDirection(
        int y1,
        int x1,
        int y2,
        int x2)
    {
        Direction verticalDirection;
        Direction horizontalDirection;

        if (y1 < y2)
        {
            verticalDirection = Direction.Down;
        }
        else if (y1 == y2)
        {
            verticalDirection = Direction.None;
        }
        else
        {
            verticalDirection = Direction.Up;
        }

        if (x1 < x2)
        {
            horizontalDirection = Direction.Right;
        }
        else if (x1 == x2)
        {
            horizontalDirection = Direction.None;
        }
        else
        {
            horizontalDirection = Direction.Left;
        }

        if (verticalDirection == Direction.None)
        {
            return horizontalDirection;
        }

        if (horizontalDirection == Direction.None)
        {
            return verticalDirection;
        }

        if (random.Next(2) == 0)
        {
            return verticalDirection;
        }

        return horizontalDirection;
    }

    private void Move(
        ref int y,
        ref int x,
        Direction direction)
    {
        if (direction == Direction.Up)
        {
            y--;
        }
        else if (direction == Direction.Down)
        {
            y++;
        }
        else if (direction == Direction.Left)
        {
            x--;
        }
        else if (direction == Direction.Right)
        {
            x++;
        }
    }

    private bool IsInsideDungeon(int y, int x)
    {
        return y >= 0 &&
               y < Height &&
               x >= 0 &&
               x < Width;
    }

    private void PlaceBoundary()
    {
        for (int y = 0; y < Height; y++)
        {
            tiles[y, 0].Symbol = '#';
            tiles[y, Width - 1].Symbol = '#';
        }

        for (int x = 0; x < Width; x++)
        {
            tiles[0, x].Symbol = '#';
            tiles[Height - 1, x].Symbol = '#';
        }
    }

    private int RandomInt(int maximum)
    {
        return random.Next(1, maximum + 1);
    }

    public void Draw()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Console.Write(tiles[y, x].Symbol);
            }

            Console.WriteLine();
        }
    }
}
