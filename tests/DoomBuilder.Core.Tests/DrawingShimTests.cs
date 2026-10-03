using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Xunit;

namespace DoomBuilder.Core.Tests;

// The Core relies on System.Drawing.Bitmap/Graphics, which only work on Windows in modern .NET.
// These tests pin the behaviour of the SkiaSharp-backed shim on every platform.
public class DrawingShimTests
{
    [Fact]
    public void LockBits_exposes_straight_BGRA_pixels()
    {
        using var bmp = new Bitmap(4, 2, PixelFormat.Format32bppArgb);
        BitmapData data = bmp.LockBits(new Rectangle(0, 0, 4, 2), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        unsafe
        {
            uint* px = (uint*)data.Scan0;
            px[0] = 0x80FF0000; // A=0x80 R=0xFF
        }
        bmp.UnlockBits(data);

        Color c = bmp.GetPixel(0, 0);
        Assert.Equal(0x80, c.A);
        Assert.Equal(0xFF, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(0, c.B);
    }

    [Fact]
    public void Png_round_trip_keeps_pixels()
    {
        using var bmp = new Bitmap(3, 3);
        bmp.SetPixel(1, 1, Color.FromArgb(255, 10, 20, 30));
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        using var back = (Bitmap)Image.FromStream(ms);
        Assert.Equal(3, back.Width);
        Assert.Equal(Color.FromArgb(255, 10, 20, 30), back.GetPixel(1, 1));
    }

    [Fact]
    public void RotateFlip_90_swaps_dimensions_and_moves_pixels()
    {
        using var bmp = new Bitmap(4, 2);
        bmp.SetPixel(0, 0, Color.Red);
        bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
        Assert.Equal(2, bmp.Width);
        Assert.Equal(4, bmp.Height);
        Assert.Equal(Color.Red.ToArgb(), bmp.GetPixel(1, 0).ToArgb());   // clockwise: top-left -> top-right
    }

    [Fact]
    public void Graphics_fills_and_keeps_alpha_straight()
    {
        using var bmp = new Bitmap(4, 4);
        using (var g = Graphics.FromImage(bmp))
            g.FillRectangle(new SolidBrush(Color.FromArgb(255, 0, 128, 0)), 0, 0, 2, 2);
        Assert.Equal(Color.FromArgb(255, 0, 128, 0), bmp.GetPixel(0, 0));
        Assert.Equal(0, bmp.GetPixel(3, 3).A);
    }

    [Fact]
    public void Embedded_UDB_resources_load()
    {
        using Bitmap failed = CodeImp.DoomBuilder.Properties.Resources.Failed;
        Assert.True(failed.Width > 0 && failed.Height > 0);
    }
}
