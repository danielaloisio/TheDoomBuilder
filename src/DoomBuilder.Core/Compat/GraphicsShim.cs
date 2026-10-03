// TEMPORARY SHIM, part 2 of DrawingShim.cs: fonts, brushes, pens and a Graphics object on top of
// SkiaSharp, covering what the UDB Core draws (text labels, texture composition, camera images).
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace System.Drawing.Drawing2D
{
    public enum SmoothingMode { Default, HighSpeed, HighQuality, None, AntiAlias }
    public enum InterpolationMode { Default, Low, High, Bilinear, Bicubic, NearestNeighbor, HighQualityBilinear, HighQualityBicubic }
    public enum PixelOffsetMode { Default, HighSpeed, HighQuality, None, Half }
    public enum CompositingQuality { Default, HighSpeed, HighQuality, GammaCorrected, AssumeLinear }
    public enum CompositingMode { SourceOver, SourceCopy }

    public class GraphicsPath : IDisposable
    {
        internal SKPath Path = new SKPath();
        public void AddString(string text, FontFamily family, int style, float emSize, PointF origin, StringFormat format)
        {
            using (var font = new SKFont(family.Typeface, emSize))
                Path.AddPath(font.GetTextPath(text, new SKPoint(origin.X, origin.Y + font.Size)));
        }
        public void AddString(string text, FontFamily family, int style, float emSize, RectangleF layout, StringFormat format)
            => AddString(text, family, style, emSize, new PointF(layout.X, layout.Y), format);
        public void AddLine(float x1, float y1, float x2, float y2) { if (Path.IsEmpty) Path.MoveTo(x1, y1); else Path.LineTo(x1, y1); Path.LineTo(x2, y2); }
        public void AddArc(float x, float y, float w, float h, float start, float sweep) => Path.ArcTo(new SKRect(x, y, x + w, y + h), start, sweep, false);
        public void CloseFigure() => Path.Close();
        public void Dispose() => Path.Dispose();
    }

    public class LinearGradientBrush : Brush
    {
        private readonly RectangleF rect; private readonly Color c1, c2; private readonly float angle;
        public LinearGradientBrush(RectangleF rect, Color c1, Color c2, float angle) { this.rect = rect; this.c1 = c1; this.c2 = c2; this.angle = angle; }
        public LinearGradientBrush(Rectangle rect, Color c1, Color c2, float angle) : this((RectangleF)rect, c1, c2, angle) { }
        internal override void Apply(SKPaint paint)
        {
            double a = angle * Math.PI / 180.0;
            var dir = new SKPoint((float)Math.Cos(a), (float)Math.Sin(a));
            var center = new SKPoint(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            float half = (Math.Abs(dir.X) * rect.Width + Math.Abs(dir.Y) * rect.Height) / 2;
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(center.X - dir.X * half, center.Y - dir.Y * half),
                new SKPoint(center.X + dir.X * half, center.Y + dir.Y * half),
                new[] { Sk(c1), Sk(c2) }, null, SKShaderTileMode.Clamp);
        }
    }
}

namespace System.Drawing.Imaging
{
    public enum WrapMode { Tile, TileFlipX, TileFlipY, TileFlipXY, Clamp }
    public class ImageAttributes : IDisposable
    {
        public void SetWrapMode(WrapMode mode) { }
        public void Dispose() { }
    }
}

namespace System.Drawing
{
    public sealed class Icon : IDisposable
    {
        public static Icon ExtractAssociatedIcon(string path) => null;
        public Bitmap ToBitmap() => new Bitmap(16, 16);
        public void Dispose() { }
    }
}

namespace System.Drawing.Text
{
    public enum TextRenderingHint { SystemDefault, SingleBitPerPixelGridFit, SingleBitPerPixel, AntiAliasGridFit, AntiAlias, ClearTypeGridFit }
}

namespace System.Drawing
{
    using System.Drawing.Drawing2D;
    using System.Drawing.Text;

    [Flags] public enum FontStyle { Regular = 0, Bold = 1, Italic = 2, Underline = 4, Strikeout = 8 }
    public enum GraphicsUnit { World, Display, Pixel, Point, Inch, Document, Millimeter }
    public enum StringAlignment { Near, Center, Far }

    public class FontFamily : IDisposable
    {
        public string Name { get; }
        internal SKTypeface Typeface { get; }
        public FontFamily(string name) { Name = name; Typeface = SKTypeface.FromFamilyName(name) ?? SKTypeface.Default; }
        public static FontFamily GenericSansSerif => new FontFamily("sans-serif");
        public void Dispose() { }
    }

    public class Font : IDisposable
    {
        public string Name { get; }
        public FontStyle Style { get; }
        public GraphicsUnit Unit { get; }
        public float Size { get; }
        public float SizeInPoints => Unit == GraphicsUnit.Pixel ? Size * 72f / 96f : Size;
        public float SizeInPixels => Unit == GraphicsUnit.Pixel ? Size : Size * 96f / 72f;
        public int Height => (int)Math.Ceiling(SizeInPixels * 1.25f);
        public FontFamily FontFamily => new FontFamily(Name);
        internal SKTypeface Typeface { get; }

        public Font(string family, float size) : this(family, size, FontStyle.Regular, GraphicsUnit.Point) { }
        public Font(string family, float size, FontStyle style) : this(family, size, style, GraphicsUnit.Point) { }
        public Font(FontFamily family, float size, FontStyle style, GraphicsUnit unit) : this(family.Name, size, style, unit) { }
        public Font(FontFamily family, float size, FontStyle style) : this(family.Name, size, style, GraphicsUnit.Point) { }
        public Font(FontFamily family, float size) : this(family.Name, size, FontStyle.Regular, GraphicsUnit.Point) { }
        public Font(Font prototype, FontStyle style) : this(prototype.Name, prototype.Size, style, prototype.Unit) { }
        public Font(string family, float size, FontStyle style, GraphicsUnit unit)
        {
            Name = family; Size = size; Style = style; Unit = unit;
            Typeface = SKTypeface.FromFamilyName(family,
                (style & FontStyle.Bold) != 0 ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                (style & FontStyle.Italic) != 0 ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright) ?? SKTypeface.Default;
        }
        public void Dispose() { }
    }

    public static class SystemFonts
    {
        public static Font DefaultFont => new Font("sans-serif", 9f);
        public static Font MessageBoxFont => new Font("sans-serif", 9f);
    }

    [Flags] public enum StringFormatFlags { DirectionRightToLeft = 1, DirectionVertical = 2, FitBlackBox = 4, DisplayFormatControl = 0x20, NoFontFallback = 0x400, MeasureTrailingSpaces = 0x800, NoWrap = 0x1000, LineLimit = 0x2000, NoClip = 0x4000 }

    public class StringFormat : IDisposable
    {
        public StringFormatFlags FormatFlags { get; set; }
        public StringAlignment Alignment { get; set; }
        public StringAlignment LineAlignment { get; set; }
        public static StringFormat GenericTypographic => new StringFormat();
        public static StringFormat GenericDefault => new StringFormat();
        public void Dispose() { }
    }

    public abstract class Brush : IDisposable
    {
        internal abstract void Apply(SKPaint paint);
        internal static SKColor Sk(Color c) => new SKColor(c.R, c.G, c.B, c.A);
        public void Dispose() { }
    }

    public class SolidBrush : Brush
    {
        public Color Color { get; set; }
        public SolidBrush(Color c) { Color = c; }
        internal override void Apply(SKPaint paint) { paint.Color = Sk(Color); }
    }

    public static class Brushes
    {
        public static Brush Black => new SolidBrush(Color.Black);
        public static Brush White => new SolidBrush(Color.White);
        public static Brush Transparent => new SolidBrush(Color.Transparent);
    }

    public class Pen : IDisposable
    {
        public Color Color { get; set; }
        public float Width { get; set; }
        public Pen(Color c) : this(c, 1f) { }
        public Pen(Color c, float w) { Color = c; Width = w; }
        public Pen(Brush b, float w) : this(b is SolidBrush sb ? sb.Color : Color.Black, w) { }
        public void Dispose() { }
    }

    public sealed class Graphics : IDisposable
    {
        private readonly Bitmap target;
        private SKBitmap surface;   // premultiplied working copy (Skia cannot draw on unpremultiplied targets)
        private SKCanvas canvas;
        private SKSamplingOptions sampling = new SKSamplingOptions(SKFilterMode.Linear);
        private bool antialias = true;

        public SmoothingMode SmoothingMode { get => antialias ? SmoothingMode.AntiAlias : SmoothingMode.None; set => antialias = value != SmoothingMode.None; }
        public InterpolationMode InterpolationMode
        {
            set => sampling = value == InterpolationMode.NearestNeighbor ? new SKSamplingOptions(SKFilterMode.Nearest)
                : value == InterpolationMode.HighQualityBicubic ? new SKSamplingOptions(SKCubicResampler.Mitchell)
                : new SKSamplingOptions(SKFilterMode.Linear);
        }
        public PixelOffsetMode PixelOffsetMode { get; set; }
        public GraphicsUnit PageUnit { get; set; }
        public CompositingQuality CompositingQuality { get; set; }
        public CompositingMode CompositingMode { get; set; }
        public TextRenderingHint TextRenderingHint { get; set; }

        private Graphics(Bitmap bmp)
        {
            target = bmp;
            var premul = new SKImageInfo(bmp.Width, bmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            surface = new SKBitmap(premul);
            bmp.Native.PeekPixels().ReadPixels(premul, surface.GetPixels(), surface.RowBytes);
            canvas = new SKCanvas(surface);
        }

        public static Graphics FromImage(Image image) => new Graphics((Bitmap)image);

        public void Clear(Color c) => canvas.Clear(Brush.Sk(c));

        private SKPaint Paint(Brush b)
        {
            var p = new SKPaint { IsAntialias = antialias };
            b.Apply(p);
            return p;
        }

        public void FillRectangle(Brush brush, float x, float y, float w, float h)
        { using (var p = Paint(brush)) canvas.DrawRect(x, y, w, h, p); }
        public void FillRectangle(Brush brush, Rectangle r) => FillRectangle(brush, r.X, r.Y, r.Width, r.Height);
        public void FillRectangle(Brush brush, RectangleF r) => FillRectangle(brush, r.X, r.Y, r.Width, r.Height);

        public void DrawLines(Pen pen, Point[] points)
        {
            using (var p = new SKPaint { IsAntialias = antialias, Style = SKPaintStyle.Stroke, StrokeWidth = pen.Width, Color = Brush.Sk(pen.Color) })
                for (int i = 0; i + 1 < points.Length; i++)
                    canvas.DrawLine(points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y, p);
        }

        public void FillPath(Brush brush, GraphicsPath path) { using (var p = Paint(brush)) canvas.DrawPath(path.Path, p); }
        public void DrawPath(Pen pen, GraphicsPath path)
        {
            using (var p = new SKPaint { IsAntialias = antialias, Style = SKPaintStyle.Stroke, StrokeWidth = pen.Width, Color = Brush.Sk(pen.Color) })
                canvas.DrawPath(path.Path, p);
        }

        // ---- images
        private void Draw(Image img, SKRect src, SKRect dst)
        {
            using (var paint = new SKPaint { IsAntialias = false })
            using (var image = SKImage.FromBitmap(((Bitmap)img).Native))
                canvas.DrawImage(image, src, dst, sampling, paint);
        }
        public void DrawImage(Image img, int x, int y) => DrawImage(img, x, y, img.Width, img.Height);
        public void DrawImage(Image img, Point p) => DrawImage(img, p.X, p.Y, img.Width, img.Height);
        public void DrawImage(Image img, int x, int y, int w, int h) => Draw(img, new SKRect(0, 0, img.Width, img.Height), new SKRect(x, y, x + w, y + h));
        public void DrawImage(Image img, Rectangle r) => DrawImage(img, r.X, r.Y, r.Width, r.Height);
        public void DrawImage(Image img, RectangleF r) => Draw(img, new SKRect(0, 0, img.Width, img.Height), new SKRect(r.X, r.Y, r.Right, r.Bottom));
        public void DrawImage(Image img, int x, int y, Rectangle srcRect, GraphicsUnit unit)
            => Draw(img, new SKRect(srcRect.X, srcRect.Y, srcRect.Right, srcRect.Bottom), new SKRect(x, y, x + srcRect.Width, y + srcRect.Height));
        public void DrawImage(Image img, Rectangle dest, int sx, int sy, int sw, int sh, GraphicsUnit unit)
            => Draw(img, new SKRect(sx, sy, sx + sw, sy + sh), new SKRect(dest.X, dest.Y, dest.Right, dest.Bottom));
        public void DrawImage(Image img, Rectangle dest, Rectangle src, GraphicsUnit unit)
            => Draw(img, new SKRect(src.X, src.Y, src.Right, src.Bottom), new SKRect(dest.X, dest.Y, dest.Right, dest.Bottom));
        public void DrawImage(Image img, Rectangle dest, int sx, int sy, int sw, int sh, GraphicsUnit unit, System.Drawing.Imaging.ImageAttributes attrs)
            => DrawImage(img, dest, sx, sy, sw, sh, unit);
        public void DrawImageUnscaled(Image img, int x, int y) => DrawImage(img, x, y, img.Width, img.Height);
        public void DrawImageUnscaled(Image img, Point p) => DrawImage(img, p.X, p.Y, img.Width, img.Height);
        public void DrawImageUnscaled(Image img, Rectangle r) => DrawImage(img, r.X, r.Y, img.Width, img.Height);
        public void DrawImageUnscaledAndClipped(Image img, Rectangle r)
        {
            canvas.Save();
            canvas.ClipRect(new SKRect(r.X, r.Y, r.Right, r.Bottom));
            DrawImage(img, r.X, r.Y, img.Width, img.Height);
            canvas.Restore();
        }

        // ---- text
        private SKFont SkFont(Font f) => new SKFont(f.Typeface, f.SizeInPixels) { Edging = SKFontEdging.Antialias, Subpixel = true };

        public SizeF MeasureString(string text, Font font)
        {
            using (var f = SkFont(font))
            {
                f.GetFontMetrics(out SKFontMetrics m);
                return new SizeF(f.MeasureText(text), m.Descent - m.Ascent + m.Leading);
            }
        }

        public void DrawString(string text, Font font, Brush brush, float x, float y)
        {
            using (var f = SkFont(font))
            using (var p = Paint(brush))
            {
                f.GetFontMetrics(out SKFontMetrics m);
                canvas.DrawText(text, x, y - m.Ascent, f, p);
            }
        }
        public void DrawString(string text, Font font, Brush brush, PointF p) => DrawString(text, font, brush, p.X, p.Y);
        public void DrawString(string text, Font font, Brush brush, RectangleF layout) => DrawString(text, font, brush, layout, null);
        public void DrawString(string text, Font font, Brush brush, RectangleF layout, StringFormat format)
        {
            using (var f = SkFont(font))
            using (var p = Paint(brush))
            {
                f.GetFontMetrics(out SKFontMetrics m);
                float w = f.MeasureText(text), h = m.Descent - m.Ascent;
                float x = layout.X, y = layout.Y;
                if (format != null)
                {
                    if (format.Alignment == StringAlignment.Center) x += (layout.Width - w) / 2;
                    else if (format.Alignment == StringAlignment.Far) x += layout.Width - w;
                    if (format.LineAlignment == StringAlignment.Center) y += (layout.Height - h) / 2;
                    else if (format.LineAlignment == StringAlignment.Far) y += layout.Height - h;
                }
                canvas.DrawText(text, x, y - m.Ascent, f, p);
            }
        }

        public void Dispose()
        {
            if (canvas == null) return;
            canvas.Dispose(); canvas = null;
            // write back, converting premultiplied -> straight alpha
            var straight = new SKImageInfo(target.Width, target.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            surface.PeekPixels().ReadPixels(straight, target.Native.GetPixels(), target.Native.RowBytes);
            surface.Dispose(); surface = null;
        }
    }
}
