using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Moria.Art;

public static class SnesColor
{
    public static Color ToRgb555(Color c) => Color.FromArgb(c.A, (c.R >> 3) << 3, (c.G >> 3) << 3, (c.B >> 3) << 3);

    public static double Distance(Color a, Color b)
    {
        int r = a.R - b.R, g = a.G - b.G, blue = a.B - b.B;
        return Math.Sqrt(r * r + g * g + blue * blue);
    }
}

public sealed class Palette
{
    private readonly List<Color> colors = new();
    public IReadOnlyList<Color> Colors => colors;

    public Palette(Color transparent) => colors.Add(SnesColor.ToRgb555(transparent));

    public int AddColor(Color color)
    {
        color = SnesColor.ToRgb555(color);
        int existing = colors.IndexOf(color);
        if (existing >= 0) return existing;
        if (colors.Count >= 16) return GetIndex(color);
        colors.Add(color);
        return colors.Count - 1;
    }

    public Color GetNearestColor(Color color)
    {
        if (colors.Count == 0) return Color.Black;
        Color best = colors[0];
        double distance = double.MaxValue;
        foreach (Color candidate in colors)
        {
            double d = SnesColor.Distance(color, candidate);
            if (d < distance) { distance = d; best = candidate; }
        }
        return best;
    }

    public int GetIndex(Color color)
    {
        color = SnesColor.ToRgb555(color);
        int best = 0;
        double distance = double.MaxValue;
        for (int i = 0; i < colors.Count; i++)
        {
            double d = SnesColor.Distance(color, colors[i]);
            if (d < distance) { distance = d; best = i; }
        }
        return best;
    }
}

public sealed class PixelTile : IEquatable<PixelTile>
{
    public const int Width = 8;
    public const int Height = 8;
    private readonly Color[,] pixels = new Color[Height, Width];

    public Color GetPixel(int x, int y) => pixels[y, x];
    public void SetPixel(int x, int y, Color color) => pixels[y, x] = SnesColor.ToRgb555(color);
    public int CountColors() => pixels.Cast<Color>().Distinct().Count();

    public bool Equals(PixelTile? other)
    {
        if (other is null) return false;
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (pixels[y, x] != other.pixels[y, x]) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is PixelTile other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hash = new();
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                hash.Add(pixels[y, x]);
        return hash.ToHashCode();
    }

    public Bitmap ToBitmap()
    {
        Bitmap bitmap = new(Width, Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                bitmap.SetPixel(x, y, pixels[y, x]);
        return bitmap;
    }
}

public sealed class TileSet
{
    private readonly List<PixelTile> tiles = new();
    private readonly Dictionary<PixelTile, int> indices = new();
    public IReadOnlyList<PixelTile> Tiles => tiles;

    public int GetOrAddTile(PixelTile tile)
    {
        if (indices.TryGetValue(tile, out int index)) return index;
        index = tiles.Count;
        tiles.Add(tile);
        indices.Add(tile, index);
        return index;
    }
}

public sealed class Sprite
{
    public int WidthInTiles { get; }
    public int HeightInTiles { get; }
    public IReadOnlyList<PixelTile> Tiles { get; }

    public Sprite(int widthInTiles, int heightInTiles, IReadOnlyList<PixelTile> tiles)
    {
        WidthInTiles = widthInTiles;
        HeightInTiles = heightInTiles;
        Tiles = tiles;
    }

    public Bitmap Assemble()
    {
        Bitmap bitmap = new(WidthInTiles * 8, HeightInTiles * 8, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bitmap);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        for (int i = 0; i < Tiles.Count; i++)
        {
            using Bitmap tile = Tiles[i].ToBitmap();
            g.DrawImage(tile, (i % WidthInTiles) * 8, (i / WidthInTiles) * 8, 8, 8);
        }
        return bitmap;
    }
}

public sealed class Tilemap
{
    private readonly int[,] indices;
    public int Width { get; }
    public int Height { get; }

    public Tilemap(int width, int height)
    {
        Width = width;
        Height = height;
        indices = new int[height, width];
    }

    public int this[int x, int y] { get => indices[y, x]; set => indices[y, x] = value; }
}

public enum TerrainType
{
    Floor,
    Stone,
    Wall,
    Water,
    Wood
}

public static class SnesDither
{
    public static Bitmap Dither(Bitmap source)
    {
        Bitmap result = new(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(result);
        g.DrawImageUnscaled(source, 0, 0);

        double[,,] work = new double[result.Height, result.Width, 3];
        for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++)
            {
                Color c = result.GetPixel(x, y);
                work[y, x, 0] = c.R;
                work[y, x, 1] = c.G;
                work[y, x, 2] = c.B;
            }

        for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++)
            {
                Color original = Color.FromArgb(Clamp(work[y, x, 0]), Clamp(work[y, x, 1]), Clamp(work[y, x, 2]));
                Color snapped = SnesColor.ToRgb555(original);
                result.SetPixel(x, y, snapped);
                double er = original.R - snapped.R;
                double eg = original.G - snapped.G;
                double eb = original.B - snapped.B;
                Add(work, x + 1, y, er, eg, eb, 7.0 / 16);
                Add(work, x - 1, y + 1, er, eg, eb, 3.0 / 16);
                Add(work, x, y + 1, er, eg, eb, 5.0 / 16);
                Add(work, x + 1, y + 1, er, eg, eb, 1.0 / 16);
            }
        return result;
    }

    private static void Add(double[,,] work, int x, int y, double r, double g, double b, double weight)
    {
        if (y < 0 || y >= work.GetLength(0) || x < 0 || x >= work.GetLength(1)) return;
        work[y, x, 0] += r * weight;
        work[y, x, 1] += g * weight;
        work[y, x, 2] += b * weight;
    }

    private static int Clamp(double value) => Math.Clamp((int)Math.Round(value), 0, 255);
}

public sealed class TerrainGenerator
{
    private readonly Random random;
    public TerrainGenerator(int seed) => random = new(seed);

    public PixelTile GenerateTerrainTile(TerrainType type)
    {
        return type switch
        {
            TerrainType.Floor => GenerateFloorTile(),
            TerrainType.Wall => GenerateWallTile(),
            TerrainType.Wood => GenerateWoodTile(),
            TerrainType.Water => GenerateWaterTile(),
            _ => GenerateStoneTile()
        };
    }

    private PixelTile GenerateFloorTile()
    {
        Color baseColor = Color.FromArgb(36, 38, 43);
        Color dark = Color.FromArgb(25, 27, 31);
        Color light = Color.FromArgb(55, 57, 61);
        PixelTile tile = new();
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                tile.SetPixel(x, y, baseColor);

        for (int i = 0; i < 7; i++)
        {
            int x = random.Next(1, 7);
            int y = random.Next(1, 7);
            tile.SetPixel(x, y, i % 3 == 0 ? light : dark);
            if (i % 2 == 0 && x < 7) tile.SetPixel(x + 1, y, dark);
        }
        return tile;
    }

    private PixelTile GenerateWallTile()
    {
        Color mortar = Color.FromArgb(30, 31, 35);
        Color stone = Color.FromArgb(83, 85, 91);
        Color highlight = Color.FromArgb(121, 122, 125);
        Color shadow = Color.FromArgb(56, 57, 62);
        PixelTile tile = new();
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                tile.SetPixel(x, y, stone);

        for (int y = 0; y < 8; y++) tile.SetPixel(0, y, shadow);
        for (int x = 0; x < 8; x++) tile.SetPixel(x, 0, highlight);
        for (int x = 0; x < 8; x++) tile.SetPixel(x, 7, mortar);
        for (int y = 0; y < 8; y++) tile.SetPixel(7, y, mortar);
        int seam = random.Next(2, 5);
        for (int x = seam; x < 8; x++) tile.SetPixel(x, 3, mortar);
        return tile;
    }

    private PixelTile GenerateStoneTile()
    {
        PixelTile tile = new();
        Color dark = Color.FromArgb(13, 15, 18);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                tile.SetPixel(x, y, dark);
        return tile;
    }

    private PixelTile GenerateWoodTile()
    {
        PixelTile tile = new();
        Color baseColor = Color.FromArgb(69, 43, 27);
        Color light = Color.FromArgb(118, 76, 40);
        Color dark = Color.FromArgb(40, 25, 19);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                tile.SetPixel(x, y, baseColor);
        for (int y = 1; y < 8; y += 3)
            for (int x = 0; x < 8; x++)
                tile.SetPixel(x, y, dark);
        tile.SetPixel(2, 2, light);
        tile.SetPixel(5, 5, light);
        return tile;
    }

    private PixelTile GenerateWaterTile()
    {
        PixelTile tile = new();
        Color baseColor = Color.FromArgb(20, 38, 55);
        Color light = Color.FromArgb(45, 76, 96);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                tile.SetPixel(x, y, baseColor);
        int wave = random.Next(1, 5);
        for (int x = 1; x < 7; x++) tile.SetPixel(x, wave, light);
        return tile;
    }

    public static PixelTile QuantizeTileToPalette(PixelTile tile, IReadOnlyList<Color> palette)
    {
        Palette target = new(Color.Transparent);
        foreach (Color color in palette.Take(15)) target.AddColor(color);
        PixelTile result = new();
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                result.SetPixel(x, y, target.GetNearestColor(tile.GetPixel(x, y)));
        return result;
    }
}

public sealed class SpriteGenerator
{
    private static readonly Color Transparent = Color.FromArgb(0, 0, 0, 0);
    private readonly Random random;

    public SpriteGenerator(int seed) => random = new(seed);

    private Bitmap PixelSprite(string[] rows, Dictionary<char, Color> palette)
    {
        Bitmap bitmap = new(16, 16, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                char code = rows[y][x];
                bitmap.SetPixel(x, y, palette.TryGetValue(code, out Color color) ? color : Transparent);
            }
        return bitmap;
    }

    public Bitmap GeneratePlayerSprite(bool alive)
    {
        if (!alive)
        {
            return PixelSprite([
                "................",
                "................",
                ".......HH.......",
                "......HssH......",
                ".....HssssH.....",
                ".....HssssH.....",
                "......HssH......",
                ".......HH.......",
                "......d..d......",
                ".....dd..dd.....",
                "....d......d....",
                "...d........d...",
                "................",
                "................",
                "................",
                "................"
            ], DeathPalette());
        }

        return PixelSprite([
            "................",
            ".....HHHHHH.....",
            "....HhhhhhhH....",
            "....HhsssshH....",
            "....HhsssshH....",
            ".....HhhhhhH....",
            "......AAAA......",
            ".....AaaaAA.....",
            "....AaaaAaSS....",
            "...AaaaaaaSSS...",
            "...AaaaaaaSSS...",
            "....AaaaAA......",
            ".....AaaA.......",
            ".....AAAA.......",
            "....BB..BB......",
            "....B....B......"
        ], WarriorPalette());
    }

    private static Dictionary<char, Color> WarriorPalette() => new()
    {
        ['H'] = Color.FromArgb(210, 214, 208),
        ['h'] = Color.FromArgb(150, 156, 156),
        ['s'] = Color.FromArgb(218, 166, 120),
        ['A'] = Color.FromArgb(112, 124, 136),
        ['a'] = Color.FromArgb(178, 184, 188),
        ['S'] = Color.FromArgb(228, 178, 54),
        ['B'] = Color.FromArgb(66, 72, 82)
    };

    private static Dictionary<char, Color> DeathPalette() => new()
    {
        ['H'] = Color.FromArgb(100, 104, 108),
        ['s'] = Color.FromArgb(62, 64, 68),
        ['d'] = Color.FromArgb(48, 50, 54)
    };

    public Bitmap GenerateMonsterSprite(int level)
    {
        bool elite = level >= 5;
        return PixelSprite([
            "................",
            ".....G....G.....",
            "....GGGGGGGG....",
            "...GggggggggG...",
            "..GggGggggGggG..",
            "..GggggggggggG..",
            "...GggggggggG...",
            "....GggggggG....",
            ".....GGGGGG.....",
            "....GGggggGG....",
            "...GGggggggGG...",
            "...GggggggggG...",
            "....GggggggG....",
            "....GG....GG....",
            "...GG......GG...",
            "................"
        ], new Dictionary<char, Color>
        {
            ['G'] = elite ? Color.FromArgb(176, 52, 60) : Color.FromArgb(108, 72, 48),
            ['g'] = elite ? Color.FromArgb(112, 32, 44) : Color.FromArgb(156, 104, 64)
        });
    }

    public Bitmap GeneratePotionSprite()
    {
        return PixelSprite([
            "................",
            "......NNNN......",
            "......NnnN......",
            "......NnnN......",
            ".....PPPPPP.....",
            "....PppppppP....",
            "....PppppppP....",
            "....PppppppP....",
            ".....PppppP.....",
            ".....PPPPPP.....",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................"
        ], new Dictionary<char, Color>
        {
            ['N'] = Color.FromArgb(190, 194, 190),
            ['n'] = Color.FromArgb(100, 106, 104),
            ['P'] = Color.FromArgb(52, 66, 78),
            ['p'] = Color.FromArgb(50, 126, 196)
        });
    }

    public Bitmap GenerateGearSprite(int rarity)
    {
        Color body = rarity switch
        {
            1 => Color.FromArgb(156, 162, 168),
            2 => Color.FromArgb(70, 124, 200),
            3 => Color.FromArgb(166, 84, 196),
            _ => Color.FromArgb(226, 164, 48)
        };
        Color shadow = Color.FromArgb(48, 50, 58);
        Color highlight = Color.FromArgb(238, 238, 226);
        Bitmap bitmap = new(16, 16, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.None;
        using SolidBrush b = new(body);
        using SolidBrush s = new(shadow);
        using SolidBrush h = new(highlight);
        g.FillRectangle(s, 6, 1, 4, 14);
        g.FillRectangle(b, 7, 2, 2, 12);
        g.FillRectangle(b, 3, 6, 10, 4);
        g.FillRectangle(h, 8, 2, 1, 8);
        g.FillRectangle(h, 3, 7, 2, 1);
        return bitmap;
    }

    public Bitmap GenerateSilhouette(int width, int height, Color body, Color shadow, Color highlight)
    {
        Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < (width + 1) / 2; x++)
            {
                double nx = (x - width / 2.0) / (width / 2.0);
                double ny = (y - height / 2.0) / (height / 2.0);
                bool inside = nx * nx + ny * ny < 0.78 + random.NextDouble() * 0.12;
                if (!inside) continue;
                Color color = y > height * .68 ? shadow : (y < height * .35 ? highlight : body);
                bitmap.SetPixel(x, y, color);
                bitmap.SetPixel(width - 1 - x, y, color);
            }
        return SnesDither.Dither(bitmap);
    }
}

public sealed class SnesArtGenerator
{
    public TerrainGenerator TerrainGenerator { get; }
    public SpriteGenerator SpriteGenerator { get; }
    private readonly Dictionary<TerrainType, Bitmap> terrainCache = new();
    private readonly Dictionary<string, Bitmap> spriteCache = new();

    public SnesArtGenerator(int seed = 1337)
    {
        TerrainGenerator = new TerrainGenerator(seed);
        SpriteGenerator = new SpriteGenerator(seed ^ 0x5A17);
    }

    public Bitmap GenerateTerrainTile(TerrainType type)
    {
        if (!terrainCache.TryGetValue(type, out Bitmap? bitmap))
        {
            using Bitmap source = TerrainGenerator.GenerateTerrainTile(type).ToBitmap();
            bitmap = new Bitmap(source);
            terrainCache[type] = bitmap;
        }
        return bitmap;
    }

    public Bitmap GeneratePlayerSprite(bool alive)
    {
        string key = $"player:{alive}";
        if (!spriteCache.TryGetValue(key, out Bitmap? bitmap))
        {
            bitmap = SpriteGenerator.GeneratePlayerSprite(alive);
            spriteCache[key] = bitmap;
        }
        return bitmap;
    }

    public Bitmap GenerateMonsterSprite(int level)
    {
        string key = $"monster:{Math.Min(level, 5)}";
        if (!spriteCache.TryGetValue(key, out Bitmap? bitmap))
        {
            bitmap = SpriteGenerator.GenerateMonsterSprite(level);
            spriteCache[key] = bitmap;
        }
        return bitmap;
    }

    public Bitmap GeneratePotionSprite()
    {
        const string key = "potion";
        if (!spriteCache.TryGetValue(key, out Bitmap? bitmap))
        {
            bitmap = SpriteGenerator.GeneratePotionSprite();
            spriteCache[key] = bitmap;
        }
        return bitmap;
    }

    public Bitmap GenerateGearSprite(int rarity)
    {
        string key = $"gear:{Math.Clamp(rarity, 1, 4)}";
        if (!spriteCache.TryGetValue(key, out Bitmap? bitmap))
        {
            bitmap = SpriteGenerator.GenerateGearSprite(rarity);
            spriteCache[key] = bitmap;
        }
        return bitmap;
    }

    public void GenerateAllAssets()
    {
        foreach (TerrainType type in Enum.GetValues<TerrainType>()) _ = GenerateTerrainTile(type);
        _ = GeneratePlayerSprite(true);
        _ = GenerateMonsterSprite(1);
        _ = GeneratePotionSprite();
        _ = GenerateGearSprite(1);
    }
}
