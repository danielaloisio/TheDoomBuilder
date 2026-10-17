using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;

namespace DoomBuilder.UI;

/// <summary>The plugins' images are the Core's System.Drawing stand-ins (Skia bitmaps); the Avalonia controls need their own bitmap type.</summary>
public static class ImageConvert
{
    private static readonly ConditionalWeakTable<System.Drawing.Image, Bitmap> cache = new ConditionalWeakTable<System.Drawing.Image, Bitmap>();

    /// <summary>The image as an Avalonia bitmap (null for null or an image that cannot be encoded).</summary>
    public static Bitmap ToAvalonia(System.Drawing.Image image)
    {
        if (!(image is System.Drawing.Bitmap source)) return null;
        if (cache.TryGetValue(source, out Bitmap cached)) return cached;

        try
        {
            using var stream = new MemoryStream();
            source.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            var bitmap = new Bitmap(stream);
            cache.Add(source, bitmap);
            return bitmap;
        }
        catch (System.Exception) { return null; }
    }
}
