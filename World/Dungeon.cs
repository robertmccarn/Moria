using Moria.Core;
using Moria.Entities;

namespace Moria.World;

public sealed class Dungeon
{
    public const int StartingWidth = 160;
    public const int StartingHeight = 44;
    public const int MaximumWidth = 360;
    public const int MaximumHeight = 54;
    public const int MaximumDepth = 50;

    private Tile[,] tiles = null!;
    private readonly Random random;
    private readonly List<Room> rooms = new();

    public int Width { get; private set; } = StartingWidth;
    public int Height { get; private set; } = StartingHeight;
    public List<Monster> Monsters { get; } = new();
    public Position UpStairs { get; private set; }
    public Position DownStairs { get; private set; }
    public Monster? Boss { get; private set; }

    public Dungeon(int seed)
    {
        random = new Random(seed);
        AllocateTiles();
    }

    public Tile this[Position p] => tiles[p.Y, p.X];
    public bool IsInside(Position p) => p.Y > 0 && p.Y < Height - 1 && p.X > 0 && p.X < Width - 1;
    public bool IsWalkable(Position p) => IsInside(p) && this[p].Type is TileType.Floor or TileType.Door or TileType.StairsUp or TileType.StairsDown or TileType.Trap;
    public Monster? MonsterAt(Position p) => Monsters.FirstOrDefault(m => m.Alive && m.Position == p);

    public void Generate(int level)
    {
        level = Math.Clamp(level, 1, MaximumDepth);
        SetDimensions(level);
        AllocateTiles();
        rooms.Clear();
        Monsters.Clear();
        Boss = null;

        int roomTarget = Math.Min(50, 18 + level / 2);
        int attempts = roomTarget * 8;

        for (int i = 0; i < attempts && rooms.Count < roomTarget; i++)
        {
            int maxRoomWidth = Math.Min(18, Math.Max(8, Width / 6));
            int maxRoomHeight = Math.Min(10, Math.Max(6, Height / 4));
            int w = random.Next(5, maxRoomWidth + 1);
            int h = random.Next(4, maxRoomHeight + 1);
            int x = random.Next(2, Width - w - 2);
            int y = random.Next(2, Height - h - 2);
            Room room = new(y, x, h, w);
            if (rooms.Any(r => r.Intersects(room))) continue;

            CarveRoom(room);
            if (rooms.Count > 0)
            {
                Room anchor = FindNearestRoom(room);
                Connect(anchor.Center, room.Center);
            }
            rooms.Add(room);
        }

        if (rooms.Count < 2)
            throw new InvalidOperationException("Dungeon generation failed to create enough rooms.");

        AddLoopConnections(level);
        CarveWindingTunnels(level);

        UpStairs = rooms[0].Center;
        DownStairs = rooms.OrderByDescending(r => Distance(UpStairs, r.Center)).First().Center;
        this[UpStairs].Type = TileType.StairsUp;
        this[DownStairs].Type = TileType.StairsDown;

        int monsterCount = Math.Min(12 + level * 2, 50);
        for (int i = 0; i < monsterCount; i++) SpawnMonster(level);

        SpawnBoss(level);
        RevealAround(UpStairs, 8);
    }

    public static bool HasBoss(int level) => level is 10 or 25 or 40 or 50;

    private void SpawnBoss(int level)
    {
        if (!HasBoss(level)) return;

        (string name, char symbol) = level switch
        {
            10 => ("Orc Warlord", 'W'),
            25 => ("Stone Colossus", 'C'),
            40 => ("Demon Lord", 'D'),
            _ => ("Balrog", 'B')
        };

        int hp = 55 + level * 7;
        int armor = 12 + level / 2;
        int attack = 5 + level / 2;
        int experience = 150 * level;
        Boss = new Monster(name, symbol, DownStairs, hp, armor, attack, level, experience, true);
        Monsters.Add(Boss);
    }

    private void SetDimensions(int level)
    {
        double progress = (level - 1) / (double)(MaximumDepth - 1);
        Width = StartingWidth + (int)Math.Round((MaximumWidth - StartingWidth) * progress);
        Height = StartingHeight + (int)Math.Round((MaximumHeight - StartingHeight) * progress);
    }

    private void AllocateTiles()
    {
        tiles = new Tile[Height, Width];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            tiles[y, x] = new Tile();
    }

    private Room FindNearestRoom(Room room) => rooms.OrderBy(r => Distance(room.Center, r.Center)).First();

    private void AddLoopConnections(int level)
    {
        int loops = Math.Min(24, 5 + level / 2);
        for (int i = 0; i < loops; i++)
        {
            Room a = rooms[random.Next(rooms.Count)];
            Room b = rooms[random.Next(rooms.Count)];
            if (a != b) Connect(a.Center, b.Center);
        }
    }

    private void CarveWindingTunnels(int level)
    {
        int tunnelCount = Math.Min(30, 8 + level / 2);
        for (int i = 0; i < tunnelCount; i++)
        {
            Room room = rooms[random.Next(rooms.Count)];
            Direction direction = (Direction)random.Next(1, 5);
            Position p = StepFromRoom(room, direction);
            int length = random.Next(8, 18 + level / 2);
            int stepsSinceTurn = 0;

            for (int step = 0; step < length; step++)
            {
                if (!IsInside(p) || this[p].Type == TileType.Floor) break;
                CarveCorridor(p);
                p = p.Step(direction);
                stepsSinceTurn++;

                if (stepsSinceTurn >= 2 && random.Next(100) < 38)
                {
                    direction = Turn(direction, random.Next(2) == 0);
                    stepsSinceTurn = 0;
                }
            }
        }
    }

    private Position StepFromRoom(Room room, Direction direction) => direction switch
    {
        Direction.Up => new Position(room.Y - 1, room.X + room.Width / 2),
        Direction.Down => new Position(room.Y + room.Height, room.X + room.Width / 2),
        Direction.Left => new Position(room.Y + room.Height / 2, room.X - 1),
        Direction.Right => new Position(room.Y + room.Height / 2, room.X + room.Width),
        _ => room.Center
    };

    private static Direction Turn(Direction direction, bool clockwise) => direction switch
    {
        Direction.Up => clockwise ? Direction.Right : Direction.Left,
        Direction.Right => clockwise ? Direction.Down : Direction.Up,
        Direction.Down => clockwise ? Direction.Left : Direction.Right,
        Direction.Left => clockwise ? Direction.Up : Direction.Down,
        _ => direction
    };

    private static int Distance(Position a, Position b) => Math.Abs(a.Y - b.Y) + Math.Abs(a.X - b.X);

    private void CarveRoom(Room r)
    {
        for (int y = r.Y; y < r.Y + r.Height; y++)
        for (int x = r.X; x < r.X + r.Width; x++)
            tiles[y, x].Type = TileType.Floor;

        for (int y = r.Y - 1; y <= r.Y + r.Height; y++)
        for (int x = r.X - 1; x <= r.X + r.Width; x++)
        {
            Position p = new(y, x);
            if (IsInside(p) && tiles[y, x].Type == TileType.Rock)
                tiles[y, x].Type = TileType.Wall;
        }
    }

    private void Connect(Position a, Position b)
    {
        Position p = a;
        bool horizontalFirst = random.Next(2) == 0;

        if (horizontalFirst)
        {
            while (p.X != b.X)
            {
                p = new Position(p.Y, p.X + Math.Sign(b.X - p.X));
                CarveCorridor(p);
            }
            while (p.Y != b.Y)
            {
                p = new Position(p.Y + Math.Sign(b.Y - p.Y), p.X);
                CarveCorridor(p);
            }
        }
        else
        {
            while (p.Y != b.Y)
            {
                p = new Position(p.Y + Math.Sign(b.Y - p.Y), p.X);
                CarveCorridor(p);
            }
            while (p.X != b.X)
            {
                p = new Position(p.Y, p.X + Math.Sign(b.X - p.X));
                CarveCorridor(p);
            }
        }
    }

    private void CarveCorridor(Position p)
    {
        if (!IsInside(p)) return;
        this[p].Type = TileType.Floor;
        foreach (Position n in Neighbors(p))
            if (IsInside(n) && this[n].Type == TileType.Rock)
                this[n].Type = TileType.Wall;
    }

    private void SpawnMonster(int level)
    {
        for (int tries = 0; tries < 200; tries++)
        {
            Position p = new(random.Next(1, Height - 1), random.Next(1, Width - 1));
            if (!IsWalkable(p) || p == UpStairs || p == DownStairs || MonsterAt(p) != null) continue;

            string[] names = { "Kobold", "Orc", "Giant Rat", "Skeleton", "Wolf", "Troll" };
            string name = names[Math.Min(level / 3, names.Length - 1)];
            char symbol = name[0] == 'G' ? 'r' : char.ToLower(name[0]);
            int hp = 4 + level * 2 + random.Next(level + 3);
            Monsters.Add(new Monster(name, symbol, p, hp, 8 + level, 2 + level, level, 10 * level));
            return;
        }
    }

    public void RevealAround(Position center, int radius)
    {
        for (int y = center.Y - radius; y <= center.Y + radius; y++)
        for (int x = center.X - radius; x <= center.X + radius; x++)
        {
            Position p = new(y, x);
            if (IsInside(p) && Math.Abs(y - center.Y) + Math.Abs(x - center.X) <= radius)
                this[p].Seen = true;
        }
    }

    public IEnumerable<Position> Neighbors(Position p)
    {
        yield return new Position(p.Y - 1, p.X);
        yield return new Position(p.Y + 1, p.X);
        yield return new Position(p.Y, p.X - 1);
        yield return new Position(p.Y, p.X + 1);
    }

    public void Draw(Player player)
    {
        Console.SetCursorPosition(0, 0);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Position p = new(y, x);
                char c = tiles[y, x].Seen ? tiles[y, x].Symbol : ' ';
                if (player.Position == p) c = '@';
                else if (MonsterAt(p) is Monster m) c = m.Symbol;
                Console.Write(c);
            }
            Console.WriteLine();
        }
    }
}

public sealed record Room(int Y, int X, int Height, int Width)
{
    public Position Center => new(Y + Height / 2, X + Width / 2);
    public bool Intersects(Room other) =>
        X - 1 < other.X + other.Width &&
        X + Width + 1 > other.X &&
        Y - 1 < other.Y + other.Height &&
        Y + Height + 1 > other.Y;
}
