using System;
using System.Runtime.InteropServices;
using CodeImp.DoomBuilder.Rendering;

namespace DoomBuilder.Rendering
{
	/// <summary>Backend data attached to a <see cref="VertexBuffer"/>: its range inside the shared buffer.</summary>
	internal sealed class GlVertexBuffer
	{
		public VertexArena.Range Range;
		public VertexFormat Format;
		public GlRenderBackend Owner;
	}

	/// <summary>Backend data attached to an <see cref="IndexBuffer"/>.</summary>
	internal sealed class GlIndexBuffer
	{
		public uint Buffer;
		public GlRenderBackend Owner;
	}

	/// <summary>Backend data attached to a <see cref="BaseTexture"/>: GL object names, created lazily like the native renderer did.</summary>
	internal sealed class GlTexture : IDisposable
	{
		public uint Texture;
		public uint Framebuffer;
		public uint DepthRenderbuffer;
		public GlRenderBackend Owner;

		/// <summary>CPU memory handed out by MapPBO; uploaded to the texture on UnmapPBO.</summary>
		public IntPtr Staging;

		public void Dispose()
		{
			if(Staging != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(Staging);
				Staging = IntPtr.Zero;
			}
		}
	}
}
