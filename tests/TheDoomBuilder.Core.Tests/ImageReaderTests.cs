using System.Drawing;
using System.IO;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.IO;
using Xunit;

namespace DoomBuilder.Core.Tests;

/// <summary>Palette, flat and patch decoding: pure data code, but it goes through the Bitmap shim.</summary>
[Collection("General static state")]
public class ImageReaderTests : MapIoTestBase
{
    /// <summary>Palette entry i = (i, 255 - i, i / 2).</summary>
    private static Playpal GradientPalette()
    {
        var bytes = new byte[768];
        for (int i = 0; i < 256; i++) { bytes[i * 3] = (byte)i; bytes[i * 3 + 1] = (byte)(255 - i); bytes[i * 3 + 2] = (byte)(i / 2); }
        return new Playpal(new MemoryStream(bytes));
    }

    [Fact]
    public void Playpal_reads_rgb_triplets()
    {
        Playpal pal = GradientPalette();
        Assert.Equal(256, pal.Length);
        Assert.Equal(10, pal[10].r);
        Assert.Equal(245, pal[10].g);
        Assert.Equal(5, pal[10].b);
        Assert.Equal(255, pal[10].a);
    }

    [Fact]
    public void Flat_reader_maps_palette_indices_to_pixels()
    {
        var data = new byte[64 * 64];
        data[0] = 10;                 // top-left
        data[63] = 200;               // top-right
        data[63 * 64] = 77;           // bottom-left

        var reader = new DoomFlatReader(GradientPalette());
        Assert.True(reader.Validate(new MemoryStream(data)));

        using Bitmap bmp = reader.ReadAsBitmap(new MemoryStream(data), out int ox, out int oy);
        Assert.Equal(64, bmp.Width);
        Assert.Equal(64, bmp.Height);
        Assert.Equal(Color.FromArgb(255, 10, 245, 5), bmp.GetPixel(0, 0));
        Assert.Equal(Color.FromArgb(255, 200, 55, 100), bmp.GetPixel(63, 0));
        Assert.Equal(Color.FromArgb(255, 77, 178, 38), bmp.GetPixel(0, 63));
    }

    [Fact]
    public void Flat_reader_rejects_empty_data()
        => Assert.False(new DoomFlatReader(GradientPalette()).Validate(new MemoryStream(new byte[0])));

    /// <summary>2x2 patch, one post per column, with draw offsets.</summary>
    private static byte[] TinyPatch()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write((short)2); w.Write((short)2);     // width, height
        w.Write((short)1); w.Write((short)-3);    // left offset, top offset
        w.Write(16); w.Write(23);                 // column addresses
        // column 0: rows 0..1 = palette 10, 20
        w.Write((byte)0); w.Write((byte)2); w.Write((byte)0); w.Write((byte)10); w.Write((byte)20); w.Write((byte)0); w.Write((byte)0xFF);
        // column 1: rows 0..1 = palette 30, 40
        w.Write((byte)0); w.Write((byte)2); w.Write((byte)0); w.Write((byte)30); w.Write((byte)40); w.Write((byte)0); w.Write((byte)0xFF);
        return ms.ToArray();
    }

    [Fact]
    public void Patch_reader_decodes_columns_posts_and_offsets()
    {
        var reader = new DoomPictureReader(GradientPalette());
        Assert.True(reader.Validate(new MemoryStream(TinyPatch())));

        using Bitmap bmp = reader.ReadAsBitmap(new MemoryStream(TinyPatch()), out int ox, out int oy);
        Assert.Equal(2, bmp.Width);
        Assert.Equal(2, bmp.Height);
        Assert.Equal(1, ox);
        Assert.Equal(-3, oy);
        Assert.Equal(Color.FromArgb(255, 10, 245, 5), bmp.GetPixel(0, 0));
        Assert.Equal(Color.FromArgb(255, 20, 235, 10), bmp.GetPixel(0, 1));
        Assert.Equal(Color.FromArgb(255, 30, 225, 15), bmp.GetPixel(1, 0));
        Assert.Equal(Color.FromArgb(255, 40, 215, 20), bmp.GetPixel(1, 1));
    }

    [Fact]
    public void Patch_reader_rejects_garbage()
        => Assert.False(new DoomPictureReader(GradientPalette()).Validate(new MemoryStream(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 })));
}
