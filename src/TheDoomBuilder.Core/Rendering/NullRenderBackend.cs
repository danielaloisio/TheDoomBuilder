using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace CodeImp.DoomBuilder.Rendering
{
	/// <summary>
	/// Backend that draws nothing. Used by the headless shell (tests, command-line tools) so that code that creates
	/// textures and buffers can run without a GPU. It keeps just enough state to behave like a device:
	/// pixel uploads are accepted, MapPBO hands out real memory, and calls are counted for assertions.
	/// </summary>
	public sealed class NullRenderBackend : IRenderBackend
	{
		private sealed class Staging : IDisposable
		{
			public IntPtr Memory;
			public void Dispose() { if(Memory != IntPtr.Zero) { Marshal.FreeHGlobal(Memory); Memory = IntPtr.Zero; } }
		}

		public NullRenderBackend() : this(new Size(800, 600)) { }
		public NullRenderBackend(Size surfacesize) { SurfaceSize = surfacesize; }

		public Size SurfaceSize { get; set; }
		public int DrawCalls { get; private set; }
		public int PixelUploads { get; private set; }
		public int ShadersDeclared { get; private set; }
		public string GetError() { return string.Empty; }

		public void DeclareUniform(UniformName name, string glslname, UniformType type) { }
		public void DeclareShader(ShaderName index, string name, string vertexshader, string fragmentshader) { ShadersDeclared++; }
		public void SetShader(ShaderName name) { }
		public void SetUniform(UniformName name, ReadOnlySpan<byte> data, int count) { }
		public void SetVertexBuffer(VertexBuffer buffer) { }
		public void SetIndexBuffer(IndexBuffer buffer) { }
		public void SetAlphaBlendEnable(bool value) { }
		public void SetAlphaTestEnable(bool value) { }
		public void SetCullMode(Cull mode) { }
		public void SetBlendOperation(BlendOperation op) { }
		public void SetSourceBlend(Blend blend) { }
		public void SetDestinationBlend(Blend blend) { }
		public void SetFillMode(FillMode mode) { }
		public void SetMultisampleAntialias(bool value) { }
		public void SetZEnable(bool value) { }
		public void SetZWriteEnable(bool value) { }
		public void SetTexture(int unit, BaseTexture texture) { }
		public void SetSamplerFilter(int unit, TextureFilter minfilter, TextureFilter magfilter, MipmapFilter mipfilter, float maxanisotropy) { }
		public void SetSamplerState(int unit, TextureAddress address) { }

		public bool Draw(PrimitiveType type, int startIndex, int primitiveCount) { DrawCalls++; return true; }
		public bool DrawIndexed(PrimitiveType type, int startIndex, int primitiveCount) { DrawCalls++; return true; }
		public bool DrawData(PrimitiveType type, int startIndex, int primitiveCount, ReadOnlySpan<byte> flatvertices) { DrawCalls++; return true; }

		public bool StartRendering(bool clear, int backcolor, Texture target, bool usedepthbuffer) { return true; }
		public bool FinishRendering() { return true; }
		public bool Present() { return true; }
		public bool ClearTexture(int backcolor, Texture texture) { return true; }
		public bool CopyTexture(CubeTexture dst, CubeMapFace face) { return true; }

		public bool SetVertexBufferData(VertexBuffer buffer, ReadOnlySpan<byte> data, long size, VertexFormat format) { return true; }
		public bool SetVertexBufferSubdata(VertexBuffer buffer, long destOffset, ReadOnlySpan<byte> data) { return true; }
		public bool SetIndexBufferData(IndexBuffer buffer, ReadOnlySpan<int> data) { return true; }

		public bool SetPixels(Texture texture, ReadOnlySpan<byte> data) { PixelUploads++; return true; }
		public bool SetCubePixels(CubeTexture texture, CubeMapFace face, ReadOnlySpan<byte> data) { PixelUploads++; return true; }

		public IntPtr MapPBO(Texture texture)
		{
			Staging staging = texture.BackendData as Staging;
			if(staging == null)
			{
				staging = new Staging { Memory = Marshal.AllocHGlobal(texture.Width * texture.Height * 4) };
				texture.Backend = this;
				texture.BackendData = staging;
			}
			return staging.Memory;
		}

		public bool UnmapPBO(Texture texture) { PixelUploads++; return true; }

		public void ReleaseResource(object backendData)
		{
			IDisposable disposable = backendData as IDisposable;
			if(disposable != null) disposable.Dispose();
		}

		public void Dispose() { }
	}
}
