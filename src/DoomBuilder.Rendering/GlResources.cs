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

		/// <summary>The buffer's contents kept in CPU memory, to fill the GPU again after the GL context is lost and created anew.</summary>
		public byte[] Shadow;
	}

	/// <summary>Backend data attached to an <see cref="IndexBuffer"/>.</summary>
	internal sealed class GlIndexBuffer
	{
		public uint Buffer;
		public GlRenderBackend Owner;

		/// <summary>The indices kept in CPU memory (see <see cref="GlVertexBuffer.Shadow"/>).</summary>
		public int[] Shadow;
	}

	/// <summary>Backend data attached to a <see cref="BaseTexture"/>: GL object names, created lazily like the native renderer did.</summary>
	internal sealed class GlTexture : IDisposable
	{
		public uint Texture;
		public uint Framebuffer;
		public uint DepthRenderbuffer;
		public GlRenderBackend Owner;

		/// <summary>
		/// The pixels last uploaded to each image of the texture (one for a 2D texture, one per face for a cube map), kept in CPU
		/// memory to upload them again after the GL context is lost. Render targets and cube maps drawn on the GPU have none.
		/// </summary>
		public byte[][] Pixels = new byte[6][];
		public bool[] Mipmaps = new bool[6];

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
