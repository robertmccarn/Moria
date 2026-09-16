using System.Drawing;
using System.Drawing.Drawing2D;

namespace Moria.Rendering;

public sealed class VirtualCanvas
{
    public const int Width = 640;
    public const int Height = 360;

    public Bitmap CreateBitmap() => new(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

    public void Present(Graphics target, Bitmap source)
    {
        target.Clear(Color.Black);
        target.InterpolationMode = InterpolationMode.NearestNeighbor;
        target.PixelOffsetMode = PixelOffsetMode.Half;
        target.SmoothingMode = SmoothingMode.None;

        double scale = Math.Min((double)target.VisibleClipBounds.Width / Width, (double)target.VisibleClipBounds.Height / Height);
        int scaledWidth = Math.Max(1, (int)Math.Floor(Width * scale));
        int scaledHeight = Math.Max(1, (int)Math.Floor(Height * scale));
        int x = ((int)target.VisibleClipBounds.Width - scaledWidth) / 2;
        int y = ((int)target.VisibleClipBounds.Height - scaledHeight) / 2;
        target.DrawImage(source, new Rectangle(x, y, scaledWidth, scaledHeight));
    }
}
