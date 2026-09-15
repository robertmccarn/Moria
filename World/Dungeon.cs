using Moria.Core;
using Moria.Entities;

namespace Moria.World;

public sealed class Dungeon
{
    public const int Width = 80;
    public const int Height = 22;

    private readonly Tile[,] tiles = new Tile[Height, Width];
    private readonly Random random;
    private readonly List<Room> rooms = new();
    public List<Monster> Monsters { get; } = new();
    public Position UpStairs { get; private set; }
    public Position DownStairs { get; private set; }

    public Dungeon(int seed)
    {
        random = new Random(seed);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                tiles[y, x] = new Tile();
    }

    public Tile this[Position p] => tiles[p.Y, p.X];
    public bool IsInside(Position p) => p.Y > 0 && p.Y < Height - 1 && p.X > 0 && p.X < Width - 1;
    public bool IsWalkable(Position p) => IsInside(p) && this[p].Type is TileType.Floor or TileType.Door or TileType.StairsUp or TileType.StairsDown or TileType.Trap;
    public Monster? MonsterAt(Position p) => Monsters.FirstOrDefault(m => m.Alive && m.Position == p);

    public void Generate(int level)
    {
        rooms.Clear();
        Monsters.Clear();
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                tiles[y, x] = new Tile();

        for (int i = 0; i < 12; i++)
        {
            int w = random.Next(5, 14);
            int h = random.Next(3, 7);
            int x = random.Next(2, Width - w - 2);
            int y = random.Next(2, Height - h - 2);
            Room room = new(y, x, h, w);
            if (rooms.Any(r => r.Intersects(room))) continue;
            CarveRoom(room);
            if (rooms.Count > 0) Connect(rooms[^1].Center, room.Center);
            rooms.Add(room);
        }

        if (rooms.Count == 0) throw new InvalidOperationException("Dungeon generation failed.");
        UpStairs = rooms[0].Center;
        DownStairs = rooms[^1].Center;
        this[UpStairs].Type = TileType.StairsUp;
        this[DownStairs].Type = TileType.StairsDown;

        int monsterCount = Math.Min(4 + level * 2, 18);
        for (int i = 0; i < monsterCount; i++) SpawnMonster(level);
        RevealAround(UpStairs, 8);
    }

    private void CarveRoom(Room r)
    {
        for (int y = r.Y; y < r.Y + r.Height; y++)
            for (int x = r.X; x < r.X + r.Width; x++)
                tiles[y, x].Type = TileType.Floor;
        for (int y = r.Y - 1; y <= r.Y + r.Height; y++)
            for (int x = r.X - 1; x <= r.X + r.Width; x++)
                if (IsInside(new Position(y, x)) && tiles[y, x].Type == TileType.Rock)
                    tiles[y, x].Type = TileType.Wall;
    }

    private void Connect(Position a, Position b)
    {
        Position p = a;
        bool horizontalFirst = random.Next(2) == 0;
        if (horizontalFirst)
        {
            while (p.X != b.X) { p = new Position(p.Y, p.X + Math.Sign(b.X - p.X)); CarveCorridor(p); }
            while (p.Y != b.Y) { p = new Position(p.Y + Math.Sign(b.Y - p.Y), p.X); CarveCorridor(p); }
        }
        else
        {
            while (p.Y != b.Y) { p = new Position(p.Y + Math.Sign(b.Y - p.Y), p.X); CarveCorridor(p); }
            while (p.X != b.X) { p = new Position(p.Y, p.X + Math.Sign(b.X - p.X)); CarveCorridor(p); }
        }
    }

    private void CarveCorridor(Position p)
    {
        if (!IsInside(p)) return;
        this[p].Type = TileType.Floor;
        foreach (Position n in Neighbors(p))
            if (IsInside(n) && this[n].Type == TileType.Rock) this[n].Type = TileType.Wall;
    }

    private void SpawnMonster(int level)
    {
        for (int tries = 0; tries < 100; tries++)
        {
            Room r = rooms[random.Next(rooms.Count)];
            Position p = new(random.Next(r.Y, r.Y + r.Height), random.Next(r.X, r.X + r.Width));
            if (p == UpStairs || p == DownStairs || MonsterAt(p) != null) continue;
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
                if (IsInside(p) && Math.Abs(y - center.Y) + Math.Abs(x - center.X) <= radius) this[p].Seen = true;
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
    public bool Intersects(Room other) => X - 1 < other.X + other.Width && X + Width + 1 > other.X && Y - 1 < other.Y + other.Height && Y + Height + 1 > other.Y;
}
