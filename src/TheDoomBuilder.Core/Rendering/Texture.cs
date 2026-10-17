using System;

namespace CodeImp.DoomBuilder.Rendering
{
    public enum TextureFormat : int
    {
        Rgba8,
        Bgra8,
        Rg16f,
        Rgba16f,
        R32f,
        Rg32f,
        Rgb32f,
        Rgba32f,
        D32f_S8,
        D24_S8
    }

    /// <summary>Texture handle. The GPU object is owned by the <see cref="IRenderBackend"/> that fills it.</summary>
    public abstract class BaseTexture : IDisposable
    {
        protected BaseTexture(int width, int height, TextureFormat format, bool cube)
        {
            // The native renderer silently turned bad sizes into 16, so callers never had to care.
            Width = width < 1 ? 16 : width;
            Height = height < 1 ? 16 : height;
            Format = format;
            IsCube = cube;
        }

        ~BaseTexture()
        {
            Dispose();
        }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public TextureFormat Format { get; private set; }
        public bool IsCube { get; private set; }

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            if (!Disposed)
            {
                Disposed = true;
                Backend?.ReleaseResource(BackendData);
                BackendData = null;
            }
        }

        /// <summary>Set by the backend that owns the GPU object (backend implementations live in another assembly).</summary>
        public IRenderBackend Backend;
        public object BackendData;
    }

    public class Texture : BaseTexture
    {
        public Texture(int width, int height, TextureFormat format) : base(width, height, format, false)
        {
        }

        public Texture(RenderDevice device, System.Drawing.Bitmap bitmap) : base(bitmap.Width, bitmap.Height, TextureFormat.Bgra8, false)
        {
            device.SetPixels(this, bitmap);
        }

        public Texture(RenderDevice device, System.Drawing.Image image) : this(device, ToBitmap(image))
        {
        }

        private static System.Drawing.Bitmap ToBitmap(System.Drawing.Image image)
        {
            return new System.Drawing.Bitmap(image);
        }

        public object Tag { get; set; }
        public int UserData { get; set; }
    }

    public class CubeTexture : BaseTexture
    {
        public CubeTexture(RenderDevice device, int size) : base(size, size, TextureFormat.Bgra8, true)
        {
        }
    }

    public enum CubeMapFace : int { PositiveX, PositiveY, PositiveZ, NegativeX, NegativeY, NegativeZ }
}
