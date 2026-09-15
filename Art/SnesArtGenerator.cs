using System.Drawing;
using System.Drawing.Imaging;

namespace Moria.Art;

/// <summary>Procedural SNES-inspired art pipeline: RGB555, 8x8 tiles, 16-color palettes, dithering and deduplication.</summary>
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
    public int Count => colors.Count;

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

/// <summary>An 8x8 SNES-style pixel tile.</summary>
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
            for (int x = 0; x < Width; x++) hash.Add(pixels[y, x]);
        return hash.ToHashCode();
    }

    public Bitmap ToBitmap()
    {
        Bitmap bitmap = new(Width, Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) bitmap.SetPixel(x, y, pixels[y, x]);
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
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
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
    public Tilemap(int width, int height) { Width = width; Height = height; indices = new int[height, width]; }
    public int this[int x, int y] { get => indices[y, x]; set => indices[y, x] = value; }
}

public enum TerrainType { Grass, Stone, Water, Wood }

public static class SnesDither
{
    /// <summary>Floyd-Steinberg dithering against RGB555 precision.</summary>
    public static Bitmap Dither(Bitmap source)
    {
        Bitmap result = new(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(result)) g.DrawImageUnscaled(source, 0, 0);
        double[,,] work = new double[result.Height, result.Width, 3];
        for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++)
            {
                Color c = result.GetPixel(x, y);
                if (c.A == 0) continue;
                work[y, x, 0] = c.R; work[y, x, 1] = c.G; work[y, x, 2] = c.B;
            }
        for (int y = 0; y < result.Height; y++)
        for (int x = 0; x < result.Width; x++)
        {
            Color current = result.GetPixel(x, y);
            if (current.A == 0) continue;
            Color original = Color.FromArgb(current.A, Clamp(work[y, x, 0]), Clamp(work[y, x, 1]), Clamp(work[y, x, 2]));
            Color snapped = SnesColor.ToRgb555(original);
            result.SetPixel(x, y, snapped);
            double er = original.R - snapped.R, eg = original.G - snapped.G, eb = original.B - snapped.B;
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
        work[y, x, 0] += r * weight; work[y, x, 1] += g * weight; work[y, x, 2] += b * weight;
    }
    private static int Clamp(double value) => Math.Clamp((int)Math.Round(value), 0, 255);
}

public sealed class TerrainGenerator
{
    private readonly Random random;
    public TerrainGenerator(int seed) => random = new(seed);

    public PixelTile GenerateTerrainTile(TerrainType type)
    {
        Color[] palette = type switch
        {
            TerrainType.Grass => [Color.FromArgb(24, 48, 30), Color.FromArgb(40, 72, 38), Color.FromArgb(64, 96, 45), Color.FromArgb(92, 112, 55)],
            TerrainType.Water => [Color.FromArgb(16, 32, 56), Color.FromArgb(24, 56, 88), Color.FromArgb(40, 80, 112), Color.FromArgb(72, 112, 136)],
            TerrainType.Wood => [Color.FromArgb(40, 24, 18), Color.FromArgb(72, 42, 24), Color.FromArgb(104, 64, 32), Color.FromArgb(136, 88, 44)],
            _ => [Color.FromArgb(28, 28, 32), Color.FromArgb(48, 48, 52), Color.FromArgb(72, 72, 76), Color.FromArgb(104, 104, 104)]
        };
        PixelTile tile = new();
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int index = Math.Clamp(random.Next(palette.Length) + (random.Next(100) < 55 ? 0 : -1), 0, palette.Length - 1);
                tile.SetPixel(x, y, palette[index]);
            }
        return QuantizeTileToPalette(tile, palette);
    }

    public static PixelTile QuantizeTileToPalette(PixelTile tile, IReadOnlyList<Color> palette)
    {
        Palette target = new(Color.Transparent);
        foreach (Color color in palette.Take(15)) target.AddColor(color);
        PixelTile result = new();
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++) result.SetPixel(x, y, target.GetNearestColor(tile.GetPixel(x, y)));
        return result;
    }
}

public sealed class SpriteGenerator
{
    private readonly Random random;
    public SpriteGenerator(int seed) => random = new(seed);

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

    public Bitmap GeneratePlayerSprite(bool alive) => GenerateSilhouette(16, 16, alive ? Color.FromArgb(168, 176, 184) : Color.FromArgb(80, 80, 84), Color.FromArgb(54, 58, 64), Color.FromArgb(216, 220, 204));
    public Bitmap GenerateMonsterSprite(int level)
    {
        Color body = level >= 5 ? Color.FromArgb(144, 48, 56) : Color.FromArgb(120, 76, 48);
        return GenerateSilhouette(16, 16, body, Color.FromArgb(52, 32, 28), Color.FromArgb(200, 132, 72));
    }

    public Bitmap GeneratePotionSprite()
    {
        Bitmap bitmap = new(8, 8, PixelFormat.Format32bppArgb);
        Color liquid = Color.FromArgb(64, 144, 200), glass = Color.FromArgb(176, 208, 216), cork = Color.FromArgb(136, 96, 56);
        for (int y = 2; y < 8; y++)
            for (int x = 1; x < 7; x++)
                if (x is > 1 and < 6 && y > 2) bitmap.SetPixel(x, y, liquid);
        for (int x = 2; x < 6; x++) bitmap.SetPixel(x, 1, cork);
        bitmap.SetPixel(1, 3, glass); bitmap.SetPixel(6, 3, glass);
        return SnesDither.Dither(bitmap);
    }

    public Bitmap GenerateGearSprite(int rarity)
    {
        Bitmap bitmap = new(8, 8, PixelFormat.Format32bppArgb);
        Color metal = rarity switch
        {
            1 => Color.FromArgb(152, 160, 168),
            2 => Color.FromArgb(72, 128, 200),
            3 => Color.FromArgb(168, 88, 208),
            _ => Color.FromArgb(232, 160, 56)
        };
        Color dark = Color.FromArgb(Math.Max(0, metal.R - 48), Math.Max(0, metal.G - 48), Math.Max(0, metal.B - 48));
        for (int y = 1; y < 7; y++)
            for (int x = 1; x < 7; x++)
            {
                int distance = Math.Abs(x - 3) + Math.Abs(y - 3);
                if (distance <= 3) bitmap.SetPixel(x, y, distance <= 1 ? metal : dark);
            }
        bitmap.SetPixel(3, 0, metal); bitmap.SetPixel(4, 0, metal);
        bitmap.SetPixel(0, 3, metal); bitmap.SetPixel(0, 4, metal);
        bitmap.SetPixel(7, 3, metal); bitmap.SetPixel(7, 4, metal);
        bitmap.SetPixel(3, 7, metal); bitmap.SetPixel(4, 7, metal);
        return SnesDither.Dither(bitmap);
    }
}

/// <summary>High-level procedural asset orchestrator used by the renderer.</summary>
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
        if (!spriteCache.TryGetValue("potion", out Bitmap? bitmap))
        {
            bitmap = SpriteGenerator.GeneratePotionSprite();
            spriteCache["potion"] = bitmap;
        }
        return bitmap;
    }

    public Bitmap GenerateGearSprite(int rarity)
    {
        string key = $"gear:{rarity}";
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
        for (int rarity = 1; rarity <= 4; rarity++) _ = GenerateGearSprite(rarity);
    }
}
