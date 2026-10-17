using System;
using System.Drawing;

namespace CodeImp.DoomBuilder.Rendering
{
	/// <summary>
	/// The graphics API behind <see cref="RenderDevice"/>. In UDB this was the native BuilderNative library
	/// (OpenGL via P/Invoke); it is now a managed interface so the Avalonia host can supply an OpenGL
	/// implementation (DoomBuilder.Rendering) bound to the context Avalonia owns, and tests can supply a fake.
	///
	/// State setters only record state. Anything that touches the GPU may be deferred by the implementation until a
	/// graphics context is available (the host only has one while it is rendering a frame).
	/// </summary>
	public interface IRenderBackend : IDisposable
	{
		/// <summary>Size in pixels of the surface drawn to when no render target texture is set.</summary>
		Size SurfaceSize { get; }

		/// <summary>Description of the last failure, for exceptions.</summary>
		string GetError();

		void DeclareUniform(UniformName name, string glslname, UniformType type);
		void DeclareShader(ShaderName index, string name, string vertexshader, string fragmentshader);
		void SetShader(ShaderName name);

		/// <param name="data">Raw uniform data (floats or ints).</param>
		/// <param name="count">Number of array elements (1 for non-arrays).</param>
		void SetUniform(UniformName name, ReadOnlySpan<byte> data, int count);

		void SetVertexBuffer(VertexBuffer buffer);
		void SetIndexBuffer(IndexBuffer buffer);

		void SetAlphaBlendEnable(bool value);
		void SetAlphaTestEnable(bool value);
		void SetCullMode(Cull mode);
		void SetBlendOperation(BlendOperation op);
		void SetSourceBlend(Blend blend);
		void SetDestinationBlend(Blend blend);
		void SetFillMode(FillMode mode);
		void SetMultisampleAntialias(bool value);
		void SetZEnable(bool value);
		void SetZWriteEnable(bool value);

		void SetTexture(int unit, BaseTexture texture);
		void SetSamplerFilter(int unit, TextureFilter minfilter, TextureFilter magfilter, MipmapFilter mipfilter, float maxanisotropy);
		void SetSamplerState(int unit, TextureAddress address);

		bool Draw(PrimitiveType type, int startIndex, int primitiveCount);
		bool DrawIndexed(PrimitiveType type, int startIndex, int primitiveCount);
		bool DrawData(PrimitiveType type, int startIndex, int primitiveCount, ReadOnlySpan<byte> flatvertices);

		/// <param name="backcolor">ARGB.</param>
		/// <param name="target">Texture to render into, or null for the host surface.</param>
		bool StartRendering(bool clear, int backcolor, Texture target, bool usedepthbuffer);
		bool FinishRendering();
		bool Present();
		bool ClearTexture(int backcolor, Texture texture);
		bool CopyTexture(CubeTexture dst, CubeMapFace face);

		/// <param name="data">Vertex data; empty to only reserve <paramref name="size"/> bytes.</param>
		bool SetVertexBufferData(VertexBuffer buffer, ReadOnlySpan<byte> data, long size, VertexFormat format);
		bool SetVertexBufferSubdata(VertexBuffer buffer, long destOffset, ReadOnlySpan<byte> data);
		bool SetIndexBufferData(IndexBuffer buffer, ReadOnlySpan<int> data);

		/// <summary>Uploads a full 2D texture. <paramref name="data"/> holds width*height pixels in the texture's format.</summary>
		bool SetPixels(Texture texture, ReadOnlySpan<byte> data);
		bool SetCubePixels(CubeTexture texture, CubeMapFace face, ReadOnlySpan<byte> data);

		/// <summary>Pointer to writable memory (BGRA8) that <see cref="UnmapPBO"/> uploads into the texture.</summary>
		IntPtr MapPBO(Texture texture);
		bool UnmapPBO(Texture texture);

		/// <summary>Called when a vertex buffer, index buffer or texture is disposed.</summary>
		void ReleaseResource(object backendData);
	}
}
