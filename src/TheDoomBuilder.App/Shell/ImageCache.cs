using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace DoomBuilder.App.Shell;

/// <summary>Loads UDB's embedded toolbar/menu icons (by their Properties.Resources name) as Avalonia bitmaps.</summary>
public static class ImageCache
{
    private static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
    private static readonly Assembly core = typeof(CodeImp.DoomBuilder.General).Assembly;

    /// <summary>The icon, or null when the name is unknown.</summary>
    public static Bitmap Get(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return null;
        if (cache.TryGetValue(resourceName, out Bitmap cached)) return cached;

        Bitmap bitmap = null;
        if (FileOf(resourceName) is string file)
        {
            using Stream stream = core.GetManifestResourceStream("CodeImp.DoomBuilder.Resources." + file);
            if (stream != null) bitmap = new Bitmap(stream);
        }

        cache[resourceName] = bitmap;
        return bitmap;
    }

    private static readonly Dictionary<string, Bitmap> graycache = new Dictionary<string, Bitmap>(StringComparer.Ordinal);

    /// <summary>The icon grayed out and faded, as the toolbar shows a disabled button (a bitmap is not dimmed by the theme).</summary>
    public static Bitmap GetDisabled(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return null;
        if (graycache.TryGetValue(resourceName, out Bitmap cached)) return cached;

        Bitmap result = null;
        if (FileOf(resourceName) is string file)
        {
            using Stream stream = core.GetManifestResourceStream("CodeImp.DoomBuilder.Resources." + file);
            if (stream != null)
            {
                using SKBitmap source = SKBitmap.Decode(stream);
                if (source != null)
                {
                    SKColor[] pixels = source.Pixels;
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        SKColor c = pixels[i];
                        byte gray = (byte)(c.Red * 0.30 + c.Green * 0.59 + c.Blue * 0.11);
                        gray = (byte)(gray * 0.6 + 100 * 0.4);                 // less contrast: it must look "off"
                        pixels[i] = new SKColor(gray, gray, gray, (byte)(c.Alpha * 0.75));
                    }
                    using var gray8 = new SKBitmap(new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
                    gray8.Pixels = pixels;
                    using SKImage image = SKImage.FromBitmap(gray8);
                    using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var memory = new MemoryStream(data.ToArray());
                    result = new Bitmap(memory);
                }
            }
        }

        graycache[resourceName] = result;
        return result;
    }

    // Properties.Resources is internal to the Core (visible here through InternalsVisibleTo)
    private static string FileOf(string resourceName)
    {
        return CodeImp.DoomBuilder.Properties.Resources.Files.TryGetValue(resourceName, out string file) ? file : null;
    }
}
