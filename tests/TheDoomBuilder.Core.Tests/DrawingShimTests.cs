using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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

    // What the image exporter does: tile a texture over a polygon through a transform
    [Fact]
    public void A_texture_brush_tiles_the_image_through_its_transform()
    {
        using var tile = new Bitmap(2, 1);
        tile.SetPixel(0, 0, Color.FromArgb(255, 255, 0, 0));
        tile.SetPixel(1, 0, Color.FromArgb(255, 0, 0, 255));

        using var bmp = new Bitmap(8, 4);
        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        using (var brush = new TextureBrush(tile))
        using (var g = Graphics.FromImage(bmp))
        {
            path.AddLine(0, 0, 8, 0);
            path.AddLine(8, 0, 8, 4);
            path.AddLine(8, 4, 0, 4);
            path.CloseFigure();
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.FillPath(brush, path);
        }
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), bmp.GetPixel(0, 0));
        Assert.Equal(Color.FromArgb(255, 0, 0, 255), bmp.GetPixel(1, 0));
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), bmp.GetPixel(6, 3));       // repeats

        // Translated by one pixel: the pattern moves
        using var moved = new Bitmap(8, 4);
        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        using (var brush = new TextureBrush(tile))
        using (var g = Graphics.FromImage(moved))
        {
            var matrix = new System.Drawing.Drawing2D.Matrix();
            matrix.Translate(1, 0);
            brush.Transform = matrix;
            path.AddLine(0, 0, 8, 0);
            path.AddLine(8, 0, 8, 4);
            path.AddLine(8, 4, 0, 4);
            path.CloseFigure();
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.FillPath(brush, path);
        }
        Assert.Equal(Color.FromArgb(255, 0, 0, 255), moved.GetPixel(0, 0));
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), moved.GetPixel(1, 0));
    }

    [Fact]
    public void A_color_matrix_scales_the_channels_while_drawing()
    {
        using var src = new Bitmap(2, 2);
        for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) src.SetPixel(x, y, Color.FromArgb(255, 200, 100, 50));

        var half = new ColorMatrix(new float[][] {
            new float[] { 0.5f, 0, 0, 0, 0 },
            new float[] { 0, 0.5f, 0, 0, 0 },
            new float[] { 0, 0, 0.5f, 0, 0 },
            new float[] { 0, 0, 0, 1, 0 },
            new float[] { 0, 0, 0, 0, 1 } });
        var attributes = new ImageAttributes();
        attributes.SetColorMatrix(half);

        using var result = new Bitmap(2, 2);
        using (var g = Graphics.FromImage(result))
            g.DrawImage(src, new[] { new Point(0, 0), new Point(2, 0), new Point(0, 2) }, new Rectangle(0, 0, 2, 2), GraphicsUnit.Pixel, attributes);

        Color c = result.GetPixel(0, 0);
        Assert.InRange(c.R, 99, 101);
        Assert.InRange(c.G, 49, 51);
        Assert.InRange(c.B, 24, 26);
        Assert.Equal(255, c.A);
    }

    [Fact]
    public void Save_by_file_name_picks_the_format_from_the_extension()
    {
        using var bmp = new Bitmap(2, 2);
        string dir = Path.Combine(Path.GetTempPath(), "udb-shim-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            bmp.Save(Path.Combine(dir, "a.png"));
            bmp.Save(Path.Combine(dir, "b.jpg"));
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, File.ReadAllBytes(Path.Combine(dir, "a.png")).Take(4).ToArray());
            Assert.Equal(new byte[] { 0xFF, 0xD8 }, File.ReadAllBytes(Path.Combine(dir, "b.jpg")).Take(2).ToArray());
        }
        finally { Directory.Delete(dir, true); }
    }
}
