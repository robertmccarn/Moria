using System.Drawing;
using System.Drawing.Drawing2D;
using Moria.Core;
using Moria.Entities;
using Moria.Items;
using Moria.World;

namespace Moria.Art;

public sealed class AssetAtlas : IDisposable
{
    private readonly Bitmap tiles;
    private readonly Bitmap warrior;
    private readonly Bitmap monsters;
    private readonly Bitmap items;
    private readonly Dictionary<string, Bitmap> cache = new();
    private readonly Dictionary<string, Bitmap> tileCache = new();

    private static string AssetPath(string file) => Path.Combine(AppContext.BaseDirectory, "assets", file);

    public AssetAtlas()
    {
        tiles = new Bitmap(AssetPath("tiles.png"));
        warrior = new Bitmap(AssetPath("warrior.png"));
        monsters = new Bitmap(AssetPath("mosters.png"));
        items = new Bitmap(AssetPath("items.png"));
    }

    public void DrawTile(Graphics g, TileType type, Rectangle destination, int x, int y)
    {
        Rectangle source = type switch
        {
            TileType.Floor => FloorRect(Math.Abs(x + y) % 8),
            TileType.Wall => WallRect(Math.Abs(x * 3 + y) % 8),
            TileType.Door => new Rectangle(25, 473, 60, 60),
            TileType.StairsUp => new Rectangle(500, 473, 60, 60),
            TileType.StairsDown => new Rectangle(580, 473, 60, 60),
            TileType.Trap => new Rectangle(870, 473, 60, 60),
            _ => new Rectangle(1360, 135, 60, 60)
        };

        string key = $"tile:{type}:{source.X}:{source.Y}:{destination.Width}:{destination.Height}";
        if (!tileCache.TryGetValue(key, out Bitmap? sprite))
        {
            sprite = ExtractScaledSprite(tiles, source, destination.Width, destination.Height);
            tileCache[key] = sprite;
        }

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImageUnscaled(sprite, destination.X, destination.Y);
    }

    public void DrawPlayer(Graphics g, Rectangle destination, Direction facing, bool alive)
    {
        destination = FitSprite(destination, 30);
        if (!alive)
        {
            DrawSprite(g, warrior, new Rectangle(1280, 125, 170, 100), destination, "warrior:dead");
            return;
        }

        int row = facing switch
        {
            Direction.Up => 1,
            Direction.Left => 2,
            Direction.Right => 3,
            _ => 0
        };
        int frame = (Environment.TickCount / 180) % 4;
        int[] centers = [145, 255, 365, 475];
        Rectangle source = new(centers[frame] - 48, 68 + row * 165, 96, 125);
        DrawSprite(g, warrior, source, destination, $"warrior:{row}:{frame}");
    }

    public void DrawMonster(Graphics g, Rectangle destination, Monster monster)
    {
        destination = FitSprite(destination, 28);
        int panel = monster.Name switch
        {
            "Kobold" => 0,
            "Orc" => 1,
            "Giant Rat" => 2,
            "Skeleton" => 3,
            "Wolf" => 4,
            "Troll" => 5,
            _ => 0
        };

        int rowBlock = panel / 3;
        int columnBlock = panel % 3;
        int[] centersX = [150, 255, 365, 470];
        int frame = (Environment.TickCount / 220) % 4;
        int centerX = columnBlock switch
        {
            0 => centersX[frame],
            1 => 650 + (frame * 105),
            _ => 1150 + (frame * 105)
        };
        int centerY = rowBlock == 0 ? 155 : 535;
        Rectangle source = new(centerX - 48, centerY - 48, 96, 88);
        DrawSprite(g, monsters, source, destination, $"monster:{monster.Name}:{frame}");
    }

    public void DrawPotion(Graphics g, Rectangle destination)
    {
        DrawSprite(g, items, new Rectangle(28, 140, 70, 90), FitSprite(destination, 24), "potion");
    }

    public void DrawGear(Graphics g, Rectangle destination, Gear gear)
    {
        Rectangle source = gear.Slot switch
        {
            GearSlot.Weapon => new Rectangle(25, 335, 80, 85),
            GearSlot.Armor => new Rectangle(590, 335, 80, 85),
            GearSlot.Ring => new Rectangle(680, 335, 75, 85),
            _ => new Rectangle(25, 335, 80, 85)
        };
        DrawSprite(g, items, source, FitSprite(destination, 25), $"gear:{gear.Slot}");
    }

    private static Rectangle FitSprite(Rectangle destination, int maxSize)
    {
        int size = Math.Min(maxSize, Math.Min(destination.Width, destination.Height));
        return new Rectangle(
            destination.X + (destination.Width - size) / 2,
            destination.Y + (destination.Height - size) / 2,
            size,
            size);
    }

    private static Rectangle FloorRect(int index) => new(25 + index * 80, 135, 64, 64);
    private static Rectangle WallRect(int index) => new(680 + index * 80, 135, 64, 64);

    private void DrawSprite(Graphics g, Bitmap sheet, Rectangle source, Rectangle destination, string key)
    {
        if (!cache.TryGetValue(key, out Bitmap? sprite))
        {
            sprite = ExtractSprite(sheet, source);
            cache[key] = sprite;
        }

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(sprite, destination);
    }

    private static Bitmap ExtractScaledSprite(Bitmap sheet, Rectangle source, int width, int height)
    {
        Bitmap crop = ExtractSprite(sheet, source);
        if (crop.Width == width && crop.Height == height) return crop;

        Bitmap scaled = new(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(crop, new Rectangle(0, 0, width, height));
        }
        crop.Dispose();
        return scaled;
    }

    private static Bitmap ExtractSprite(Bitmap sheet, Rectangle source)
    {
        Bitmap crop = new(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(crop))
            g.DrawImage(sheet, new Rectangle(0, 0, source.Width, source.Height), source, GraphicsUnit.Pixel);

        Color tl = crop.GetPixel(0, 0);
        Color tr = crop.GetPixel(crop.Width - 1, 0);
        Color bl = crop.GetPixel(0, crop.Height - 1);
        Color br = crop.GetPixel(crop.Width - 1, crop.Height - 1);

        for (int y = 0; y < crop.Height; y++)
        for (int x = 0; x < crop.Width; x++)
        {
            double u = crop.Width <= 1 ? 0 : (double)x / (crop.Width - 1);
            double v = crop.Height <= 1 ? 0 : (double)y / (crop.Height - 1);
            Color background = Bilinear(tl, tr, bl, br, u, v);
            Color pixel = crop.GetPixel(x, y);
            double distance = ColorDistance(pixel, background);
            int alpha = distance <= 18 ? 0 : distance >= 42 ? 255 : (int)((distance - 18) * 255 / 24);
            crop.SetPixel(x, y, Color.FromArgb(alpha, pixel.R, pixel.G, pixel.B));
        }

        return crop;
    }

    private static Color Bilinear(Color tl, Color tr, Color bl, Color br, double u, double v)
    {
        double r = tl.R * (1 - u) * (1 - v) + tr.R * u * (1 - v) + bl.R * (1 - u) * v + br.R * u * v;
        double g = tl.G * (1 - u) * (1 - v) + tr.G * u * (1 - v) + bl.G * (1 - u) * v + br.G * u * v;
        double b = tl.B * (1 - u) * (1 - v) + tr.B * u * (1 - v) + bl.B * (1 - u) * v + br.B * u * v;
        return Color.FromArgb((int)r, (int)g, (int)b);
    }

    private static double ColorDistance(Color a, Color b)
    {
        int r = a.R - b.R;
        int g = a.G - b.G;
        int blue = a.B - b.B;
        return Math.Sqrt(r * r + g * g + blue * blue);
    }

    public void Dispose()
    {
        foreach (Bitmap bitmap in cache.Values) bitmap.Dispose();
        foreach (Bitmap bitmap in tileCache.Values) bitmap.Dispose();
        cache.Clear();
        tileCache.Clear();
        tiles.Dispose();
        warrior.Dispose();
        monsters.Dispose();
        items.Dispose();
    }
}
