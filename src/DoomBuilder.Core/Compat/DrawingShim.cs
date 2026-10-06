// TEMPORARY SHIM. System.Drawing.Common (Bitmap/Image/Graphics) only works on Windows in
// modern .NET. The UDB Core uses a small part of it (LockBits, FromStream, RotateFlip...),
// so that subset is re-implemented here on top of SkiaSharp, keeping the ported sources
// unchanged and working on Linux, macOS and Windows.
// Only the 32bpp ARGB pixel format is supported (it is the only one the Core relies on).
using System;
using System.IO;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace System.Drawing.Imaging
{
    public enum PixelFormat { Undefined = 0, Format16bppRgb555 = 0x21005, Format24bppRgb = 0x21808, Format32bppRgb = 0x22009, Format32bppArgb = 0x26200A, Format32bppPArgb = 0xE200B }

    [Flags]
    public enum ImageLockMode { ReadOnly = 1, WriteOnly = 2, ReadWrite = 3, UserInputBuffer = 4 }

    public sealed class ImageFormat
    {
        internal string Name { get; }
        private ImageFormat(string name) { Name = name; }
        public static readonly ImageFormat Png = new ImageFormat("png");
        public static readonly ImageFormat Bmp = new ImageFormat("bmp");
        public static readonly ImageFormat Jpeg = new ImageFormat("jpeg");
        public static readonly ImageFormat Gif = new ImageFormat("gif");
    }

    public class BitmapData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Stride { get; set; }
        public PixelFormat PixelFormat { get; set; }
        public IntPtr Scan0 { get; set; }
        public int Reserved { get; set; }
    }

    public class ColorPalette
    {
        public Color[] Entries { get; } = new Color[256];
        public int Flags { get; set; }
    }
}

namespace System.Drawing
{
    using System.Drawing.Imaging;

    public enum RotateFlipType
    {
        RotateNoneFlipNone = 0, Rotate90FlipNone = 1, Rotate180FlipNone = 2, Rotate270FlipNone = 3,
        RotateNoneFlipX = 4, Rotate90FlipX = 5, Rotate180FlipX = 6, Rotate270FlipX = 7,
        Rotate180FlipXY = 0, Rotate270FlipXY = 1, Rotate90FlipXY = 3,
        RotateNoneFlipY = 6, Rotate90FlipY = 7, Rotate180FlipY = 4, Rotate270FlipY = 5,
        RotateNoneFlipXY = 2,
    }

    public abstract class Image : IDisposable, ICloneable
    {
        public abstract int Width { get; }
        public abstract int Height { get; }
        public Size Size => new Size(Width, Height);
        public virtual PixelFormat PixelFormat => PixelFormat.Format32bppArgb;
        public int Flags { get; set; }
        public ColorPalette Palette { get; set; } = new ColorPalette();
        public float HorizontalResolution => 96f;
        public float VerticalResolution => 96f;

        public static Image FromStream(Stream stream) => Bitmap.Decode(stream);
        public static Image FromFile(string filename) { using (var fs = File.OpenRead(filename)) return Bitmap.Decode(fs); }

        public abstract object Clone();
        public abstract void Dispose();
    }

    public sealed class Bitmap : Image
    {
        private SKBitmap bmp;
        private GCHandle pin;           // wrapped external memory, if any
        private bool pinned;
        private IntPtr lockedPtr;

        public SKBitmap Native => bmp;
        public override int Width => bmp.Width;
        public override int Height => bmp.Height;

        private static SKImageInfo Info(int w, int h) => new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Unpremul);

        public Bitmap(int width, int height) { bmp = new SKBitmap(Info(width, height)); bmp.Erase(SKColors.Transparent); }
        public Bitmap(int width, int height, PixelFormat format) : this(width, height) { }
        public Bitmap(Size size) : this(size.Width, size.Height) { }
        public Bitmap(Image source) { bmp = ((Bitmap)source).bmp.Copy(); }
        public Bitmap(Image source, int width, int height)
        {
            bmp = new SKBitmap(Info(width, height));
            ((Bitmap)source).bmp.ScalePixels(bmp, SKSamplingOptions.Default);
        }
        public Bitmap(Stream stream) { bmp = Decode(stream).bmp; }
        private Bitmap(SKBitmap b) { bmp = b; }

        /// <summary>Wraps (copies) external pixel memory, like the WinForms stride/scan0 constructor.</summary>
        public Bitmap(int width, int height, int stride, PixelFormat format, IntPtr scan0)
        {
            bmp = new SKBitmap(Info(width, height));
            unsafe
            {
                for (int y = 0; y < height; y++)
                    Buffer.MemoryCopy((void*)(scan0 + y * stride),
                        (void*)(bmp.GetPixels() + y * bmp.RowBytes), bmp.RowBytes, width * 4);
            }
        }

        internal static Bitmap Decode(Stream stream)
        {
            using (SKCodec codec = SKCodec.Create(stream))
            {
                if (codec == null) throw new ArgumentException("Parameter is not valid.");
                SKImageInfo info = codec.Info.WithColorType(SKColorType.Bgra8888).WithAlphaType(SKAlphaType.Unpremul);
                var decoded = new SKBitmap(info);
                SKCodecResult result = codec.GetPixels(info, decoded.GetPixels());
                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                {
                    decoded.Dispose();
                    throw new ArgumentException("Parameter is not valid.");
                }
                return new Bitmap(decoded);
            }
        }

        public Color GetPixel(int x, int y)
        {
            SKColor c = bmp.GetPixel(x, y);
            return Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);
        }

        public void SetPixel(int x, int y, Color c) => bmp.SetPixel(x, y, new SKColor(c.R, c.G, c.B, c.A));

        public void SetResolution(float xdpi, float ydpi) { }
        public void MakeTransparent() { }

        public BitmapData LockBits(Rectangle rect, ImageLockMode mode, PixelFormat format)
        {
            if (format != PixelFormat.Format32bppArgb && format != PixelFormat.Format32bppRgb)
                throw new NotSupportedException("Only 32bpp pixel formats are supported.");
            IntPtr basePtr = bmp.GetPixels();
            lockedPtr = basePtr;
            return new BitmapData
            {
                Width = rect.Width, Height = rect.Height, Stride = bmp.RowBytes, PixelFormat = format,
                Scan0 = basePtr + rect.Y * bmp.RowBytes + rect.X * 4,
            };
        }

        public void UnlockBits(BitmapData data) { lockedPtr = IntPtr.Zero; }

        public void RotateFlip(RotateFlipType type)
        {
            int t = (int)type;
            int rot = t & 3;
            bool flipx = t >= 4;   // value 4..7 = flip X applied after the rotation
            SKBitmap src = bmp;
            bool swap = (rot & 1) == 1;
            SKBitmap dst = new SKBitmap(Info(swap ? src.Height : src.Width, swap ? src.Width : src.Height));
            using (var canvas = new SKCanvas(dst))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Translate(dst.Width / 2f, dst.Height / 2f);
                if (flipx) canvas.Scale(-1, 1);
                canvas.RotateDegrees(rot * 90);
                canvas.Translate(-src.Width / 2f, -src.Height / 2f);
                canvas.DrawBitmap(src, 0, 0);
            }
            bmp.Dispose();
            bmp = dst;
        }

        public Bitmap Clone(Rectangle rect, PixelFormat format)
        {
            var sub = new SKBitmap(Info(rect.Width, rect.Height));
            using (var canvas = new SKCanvas(sub))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(bmp, new SKRect(rect.X, rect.Y, rect.Right, rect.Bottom), new SKRect(0, 0, rect.Width, rect.Height));
            }
            return new Bitmap(sub);
        }

        public override object Clone() => new Bitmap(bmp.Copy());

        public void Save(Stream stream, ImageFormat format)
        {
            SKEncodedImageFormat f = format == ImageFormat.Jpeg ? SKEncodedImageFormat.Jpeg
                : format == ImageFormat.Bmp ? SKEncodedImageFormat.Bmp
                : format == ImageFormat.Gif ? SKEncodedImageFormat.Gif : SKEncodedImageFormat.Png;
            using (var img = SKImage.FromBitmap(bmp))
            using (var data = img.Encode(f, 100))
                data.SaveTo(stream);
        }

        public void Save(string filename, ImageFormat format)
        {
            using (var fs = File.Create(filename)) Save(fs, format);
        }

        /// <summary>Saves in the format the file extension names (PNG for anything else).</summary>
        public void Save(string filename)
        {
            string ext = Path.GetExtension(filename).ToLowerInvariant();
            Save(filename, ext == ".jpg" || ext == ".jpeg" ? ImageFormat.Jpeg : ext == ".bmp" ? ImageFormat.Bmp : ext == ".gif" ? ImageFormat.Gif : ImageFormat.Png);
        }

        public override void Dispose()
        {
            bmp?.Dispose();
            bmp = null;
        }
    }
}
