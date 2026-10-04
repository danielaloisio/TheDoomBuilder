using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Avalonia.Media.Imaging;

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

    // Properties.Resources is internal to the Core (visible here through InternalsVisibleTo)
    private static string FileOf(string resourceName)
    {
        return CodeImp.DoomBuilder.Properties.Resources.Files.TryGetValue(resourceName, out string file) ? file : null;
    }
}
