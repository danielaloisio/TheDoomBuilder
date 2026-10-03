using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using CodeImp.DoomBuilder.Rendering;
using Silk.NET.OpenGL;
using Blend = CodeImp.DoomBuilder.Rendering.Blend;
using BlendOperation = CodeImp.DoomBuilder.Rendering.BlendOperation;
using Cull = CodeImp.DoomBuilder.Rendering.Cull;
using FillMode = CodeImp.DoomBuilder.Rendering.FillMode;
using MipmapFilter = CodeImp.DoomBuilder.Rendering.MipmapFilter;
using PrimitiveType = CodeImp.DoomBuilder.Rendering.PrimitiveType;
using TextureAddress = CodeImp.DoomBuilder.Rendering.TextureAddress;
using TextureFilter = CodeImp.DoomBuilder.Rendering.TextureFilter;
using UniformType = CodeImp.DoomBuilder.Rendering.UniformType;
using VertexFormat = CodeImp.DoomBuilder.Rendering.VertexFormat;
using CubeMapFace = CodeImp.DoomBuilder.Rendering.CubeMapFace;
using Texture = CodeImp.DoomBuilder.Rendering.Texture;
using PixelFormat = Silk.NET.OpenGL.PixelFormat;

namespace DoomBuilder.Rendering
{
	/// <summary>
	/// OpenGL implementation of <see cref="IRenderBackend"/> on Silk.NET: a managed port of UDB's BuilderNative renderer
	/// (GLRenderDevice.cpp, Magnus Norddahl, zlib license). It targets OpenGL 3.3 core (the version macOS offers) and falls
	/// back to OpenGL ES 3.0 when the host only has ANGLE.
	///
	/// The host owns the GL context and only has it while rendering a frame, so the lifecycle is:
	/// <see cref="AttachContext"/> once the context exists, then for every frame <see cref="BeginFrame"/> ... <see cref="EndFrame"/>.
	/// Anything that touches the GPU outside a frame (buffer and texture uploads while the user edits) is queued and
	/// replayed, in order, at the start of the next frame. Draw calls outside a frame are dropped.
	/// </summary>
	public sealed unsafe class GlRenderBackend : IRenderBackend
	{
		private const int MaxTextureUnits = 10;
		private const int InitialSharedBufferSize = 16 * 1024 * 1024;
		private const uint GL_TEXTURE_MAX_ANISOTROPY_EXT = 0x84FE;
		private const int GL_BGRA = 0x80E1;

		private sealed class UniformInfo
		{
			public string Name = string.Empty;
			public UniformType Type;
			public int LastUpdate;
			public int Count;
			public byte[] Data = Array.Empty<byte>();
		}

		private struct UnitState
		{
			public GlTexture Tex;
			public bool HasTex;
			public TextureAddress WrapMode;
			public uint SamplerHandle;
			public TextureFilter MinFilter, MagFilter;
			public MipmapFilter MipFilter;
			public float MaxAnisotropy;
		}

		private readonly struct SamplerKey : IEquatable<SamplerKey>
		{
			public readonly int Min, Mag;
			public readonly float Aniso;
			public SamplerKey(int min, int mag, float aniso) { Min = min; Mag = mag; Aniso = aniso; }
			public bool Equals(SamplerKey o) { return Min == o.Min && Mag == o.Mag && Aniso == o.Aniso; }
			public override bool Equals(object o) { return o is SamplerKey k && Equals(k); }
			public override int GetHashCode() { return HashCode.Combine(Min, Mag, Aniso); }
		}

		// ---- context / frame
		private GL gl;
		private bool gles;
		private bool inFrame;
		private int defaultFramebuffer;
		private Size surfacesize = new Size(1, 1);
		private string error = string.Empty;
		private readonly Queue<Action> deferred = new Queue<Action>();
		private readonly ConcurrentQueue<Action> pendingreleases = new ConcurrentQueue<Action>();
		private readonly object statelock = new object();

		// ---- GL objects owned by the device
		private uint streamvao, streamvbo;
		private readonly VertexArena[] arenas = new VertexArena[2];
		private readonly uint[] arenabuffers = new uint[2];
		private readonly uint[] arenavaos = new uint[2];
		private readonly Dictionary<SamplerKey, uint[]> samplers = new Dictionary<SamplerKey, uint[]>();
		private readonly GlShader[] shaders = new GlShader[64];
		private readonly GlShader[] alphatestshaders = new GlShader[64];
		private readonly List<UniformInfo> uniforms = new List<UniformInfo>();
		private readonly List<string> uniformnames = new List<string>();

		// ---- device state
		private readonly UnitState[] units = new UnitState[MaxTextureUnits];
		private GlVertexBuffer vertexbuffer;
		private GlIndexBuffer indexbuffer;
		private ShaderName shadername;
		private Cull cullmode = Cull.None;
		private FillMode fillmode = FillMode.Solid;
		private bool alphatest, alphablend, depthtest, depthwrite;
		private BlendOperation blendop = BlendOperation.Add;
		private Blend srcblend = Blend.SourceAlpha, dstblend = Blend.InverseSourceAlpha;
		private int viewportwidth, viewportheight;

		private bool needapply = true, shaderchanged = true, uniformschanged = true, textureschanged = true;
		private bool indexbufferchanged = true, vertexbufferchanged = true, depthstatechanged = true, blendstatechanged = true, rasterizerstatechanged = true;

		public GlRenderBackend()
		{
			for(int i = 0; i < units.Length; i++)
			{
				units[i].WrapMode = TextureAddress.Wrap;
				units[i].MinFilter = TextureFilter.Nearest;
				units[i].MagFilter = TextureFilter.Nearest;
				units[i].MipFilter = MipmapFilter.None;
				units[i].MaxAnisotropy = 1;
			}
			for(int i = 0; i < shaders.Length; i++) { shaders[i] = new GlShader(); alphatestshaders[i] = new GlShader(); }
		}

		#region ================== Host interface (context and frames)

		/// <summary>True once a GL context is attached.</summary>
		public bool HasContext { get { return gl != null; } }

		/// <summary>Number of operations waiting for the next frame (diagnostics and tests).</summary>
		public int DeferredCount { get { return deferred.Count; } }

		public string GlInfo { get; private set; }

		/// <summary>Called by the host when its GL context exists and is current.</summary>
		public void AttachContext(GL gl, bool gles)
		{
			this.gl = gl;
			this.gles = gles;
			GlInfo = gl.GetStringS(StringName.Renderer) + " | GL " + gl.GetStringS(StringName.Version) + " | GLSL " + gl.GetStringS(StringName.ShadingLanguageVersion);

			streamvao = gl.GenVertexArray();
			streamvbo = gl.GenBuffer();
			gl.BindVertexArray(streamvao);
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, streamvbo);
			SetupVertexLayout(VertexFormat.Flat);

			for(int i = 0; i < arenas.Length; i++)
			{
				arenas[i] = new VertexArena((VertexFormat)i, InitialSharedBufferSize);
				arenabuffers[i] = gl.GenBuffer();
				gl.BindBuffer(BufferTargetARB.ArrayBuffer, arenabuffers[i]);
				gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)InitialSharedBufferSize, null, BufferUsageARB.StaticDraw);
			}
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
			CheckGLError();
		}

		/// <summary>Called by the host when its GL context is about to go away. GPU data is lost.</summary>
		public void DetachContext()
		{
			gl = null;
			inFrame = false;
		}

		/// <summary>Updates the size reported by <see cref="SurfaceSize"/> (the host calls this when its viewport is resized).</summary>
		public void SetSurfaceSize(Size size)
		{
			surfacesize = size;
		}

		/// <summary>Starts a frame. The context must be current. Replays queued work and frees released resources.</summary>
		/// <param name="framebuffer">Framebuffer the host wants the frame drawn to (never assume 0).</param>
		public void BeginFrame(int framebuffer, Size size)
		{
			if(gl == null) throw new InvalidOperationException("No GL context attached");
			defaultframebufferSet(framebuffer);
			surfacesize = size;
			inFrame = true;
			FlushDeferred();
			ProcessReleases();
		}

		public void EndFrame()
		{
			inFrame = false;
		}

		private void defaultframebufferSet(int framebuffer) { defaultFramebuffer = framebuffer; }

		private void FlushDeferred()
		{
			while(deferred.Count > 0) deferred.Dequeue()();
		}

		private void ProcessReleases()
		{
			while(pendingreleases.TryDequeue(out Action release)) release();
		}

		/// <summary>Runs GPU work now when a frame is active, otherwise queues it for the next frame.</summary>
		private void Run(Action work)
		{
			if(inFrame && gl != null) work();
			else deferred.Enqueue(work);
		}

		private static byte[] Copy(ReadOnlySpan<byte> data) { return data.ToArray(); }

		#endregion

		#region ================== IRenderBackend: state

		public Size SurfaceSize { get { return surfacesize; } }

		public string GetError() { return error; }

		private void SetError(string message) { error = message; }

		public void DeclareUniform(UniformName name, string glslname, UniformType type)
		{
			int index = (int)name;
			while(uniforms.Count <= index) { uniforms.Add(new UniformInfo()); uniformnames.Add(string.Empty); }
			uniforms[index].Name = glslname;
			uniforms[index].Type = type;
			uniformnames[index] = glslname;
		}

		public void DeclareShader(ShaderName index, string name, string vertexshader, string fragmentshader)
		{
			shaders[(int)index].Setup(name, vertexshader, fragmentshader, false);
			alphatestshaders[(int)index].Setup(name, vertexshader, fragmentshader, true);
		}

		private void SetShaderNow(ShaderName name)
		{
			if(name != shadername)
			{
				shadername = name;
				needapply = true;
				shaderchanged = true;
				uniformschanged = true;
			}
		}

		private void SetUniformNow(UniformName name, ReadOnlySpan<byte> data, int count)
		{
			UniformInfo info = uniforms[(int)name];
			info.Count = count;
			if(info.Data.Length != data.Length) info.Data = new byte[data.Length];
			if(!data.SequenceEqual(info.Data))
			{
				data.CopyTo(info.Data);
				info.LastUpdate++;
				needapply = true;
				uniformschanged = true;
			}
		}

		private void SetVertexBufferNow(VertexBuffer buffer)
		{
			GlVertexBuffer data = buffer != null ? buffer.BackendData as GlVertexBuffer : null;
			if(vertexbuffer != data)
			{
				vertexbuffer = data;
				needapply = true;
				vertexbufferchanged = true;
			}
		}

		private void SetIndexBufferNow(IndexBuffer buffer)
		{
			GlIndexBuffer data = buffer != null ? buffer.BackendData as GlIndexBuffer : null;
			if(indexbuffer != data)
			{
				indexbuffer = data;
				needapply = true;
				indexbufferchanged = true;
			}
		}

		private void SetAlphaBlendEnableNow(bool value) { if(alphablend != value) { alphablend = value; needapply = true; blendstatechanged = true; } }
		private void SetAlphaTestEnableNow(bool value) { if(alphatest != value) { alphatest = value; needapply = true; shaderchanged = true; uniformschanged = true; } }
		private void SetCullModeNow(Cull mode) { if(cullmode != mode) { cullmode = mode; needapply = true; rasterizerstatechanged = true; } }
		private void SetBlendOperationNow(BlendOperation op) { if(blendop != op) { blendop = op; needapply = true; blendstatechanged = true; } }
		private void SetSourceBlendNow(Blend blend) { if(srcblend != blend) { srcblend = blend; needapply = true; blendstatechanged = true; } }
		private void SetDestinationBlendNow(Blend blend) { if(dstblend != blend) { dstblend = blend; needapply = true; blendstatechanged = true; } }
		private void SetFillModeNow(FillMode mode) { if(fillmode != mode) { fillmode = mode; needapply = true; rasterizerstatechanged = true; } }
		public void SetMultisampleAntialias(bool value) { }
		private void SetZEnableNow(bool value) { if(depthtest != value) { depthtest = value; needapply = true; depthstatechanged = true; } }
		private void SetZWriteEnableNow(bool value) { if(depthwrite != value) { depthwrite = value; needapply = true; depthstatechanged = true; } }

		private void SetTextureNow(int unit, BaseTexture texture)
		{
			GlTexture data = texture != null ? EnsureTextureData(texture) : null;
			if(units[unit].Tex != data)
			{
				units[unit].Tex = data;
				needapply = true;
				textureschanged = true;
			}
		}

		private void SetSamplerFilterNow(int unit, TextureFilter minfilter, TextureFilter magfilter, MipmapFilter mipfilter, float maxanisotropy)
		{
			bool dirty = false;
			if(units[unit].MinFilter != minfilter) { units[unit].MinFilter = minfilter; dirty = true; }
			if(units[unit].MagFilter != magfilter) { units[unit].MagFilter = magfilter; dirty = true; }
			if(units[unit].MipFilter != mipfilter) { units[unit].MipFilter = mipfilter; dirty = true; }
			if(units[unit].MaxAnisotropy != maxanisotropy) { units[unit].MaxAnisotropy = maxanisotropy; dirty = true; }
			if(dirty) { needapply = true; textureschanged = true; }
		}

		private void SetSamplerStateNow(int unit, TextureAddress address)
		{
			if(units[unit].WrapMode != address)
			{
				units[unit].WrapMode = address;
				needapply = true;
				textureschanged = true;
			}
		}

		#endregion

		#region ================== Resources (the backend data hanging off the handles)

		private GlTexture EnsureTextureData(BaseTexture texture)
		{
			GlTexture data = texture.BackendData as GlTexture;
			if(data == null)
			{
				data = new GlTexture { Owner = this };
				texture.Backend = this;
				texture.BackendData = data;
				texturedimensions[data] = texture;
			}
			return data;
		}

		// GlTexture does not know its size or format; keep the handle that does (weak not needed: released explicitly)
		private readonly Dictionary<GlTexture, BaseTexture> texturedimensions = new Dictionary<GlTexture, BaseTexture>();

		public void ReleaseResource(object backendData)
		{
			if(backendData == null) return;

			// May be called from a finalizer thread: only queue, the GL work happens in the next frame
			pendingreleases.Enqueue(() => ReleaseNow(backendData));
		}

		private void ReleaseNow(object backendData)
		{
			if(backendData is GlVertexBuffer vb)
			{
				if(vb.Range != null) arenas[(int)vb.Format].Free(vb.Range);
			}
			else if(backendData is GlIndexBuffer ib)
			{
				if(ib.Buffer != 0) gl.DeleteBuffer(ib.Buffer);
				ib.Buffer = 0;
			}
			else if(backendData is GlTexture tex)
			{
				if(tex.DepthRenderbuffer != 0) gl.DeleteRenderbuffer(tex.DepthRenderbuffer);
				if(tex.Framebuffer != 0) gl.DeleteFramebuffer(tex.Framebuffer);
				if(tex.Texture != 0) gl.DeleteTexture(tex.Texture);
				tex.DepthRenderbuffer = 0; tex.Framebuffer = 0; tex.Texture = 0;
				tex.Dispose();
				texturedimensions.Remove(tex);
				for(int i = 0; i < units.Length; i++) if(units[i].Tex == tex) units[i].Tex = null;
			}
		}

		#endregion

		#region ================== Vertex / index buffers

		public bool SetVertexBufferData(VertexBuffer buffer, ReadOnlySpan<byte> data, long size, VertexFormat format)
		{
			byte[] copy = data.IsEmpty ? null : Copy(data);
			int length = (int)size;
			Run(() => SetVertexBufferDataNow(buffer, copy, length, format));
			return true;
		}

		private void SetVertexBufferDataNow(VertexBuffer buffer, byte[] data, int size, VertexFormat format)
		{
			GlVertexBuffer vb = buffer.BackendData as GlVertexBuffer;
			if(vb == null)
			{
				vb = new GlVertexBuffer { Owner = this };
				buffer.Backend = this;
				buffer.BackendData = vb;
			}
			else if(vb.Range != null)
			{
				// A buffer being refilled gets a new range at the end (the old one is simply compacted away)
				arenas[(int)vb.Format].Free(vb.Range);
				vb.Range = null;
			}

			MakeRoom(format, size);
			VertexArena arena = arenas[(int)format];

			vb.Format = format;
			vb.Range = arena.Allocate(size, vb);

			if(data != null)
			{
				gl.BindBuffer(BufferTargetARB.ArrayBuffer, arenabuffers[(int)format]);
				fixed(byte* p = data)
					gl.BufferSubData(BufferTargetARB.ArrayBuffer, vb.Range.Offset, (nuint)size, p);
				gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
			}
			vertexbufferchanged = true;
			needapply = true;
			CheckGLError();
		}

		public bool SetVertexBufferSubdata(VertexBuffer buffer, long destOffset, ReadOnlySpan<byte> data)
		{
			byte[] copy = Copy(data);
			Run(() =>
			{
				GlVertexBuffer vb = buffer.BackendData as GlVertexBuffer;
				if(vb == null || vb.Range == null) return;
				gl.BindBuffer(BufferTargetARB.ArrayBuffer, arenabuffers[(int)vb.Format]);
				fixed(byte* p = copy)
					gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)(vb.Range.Offset + destOffset), (nuint)copy.Length, p);
				gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
				CheckGLError();
			});
			return true;
		}

		/// <summary>Makes sure the shared buffer of this format has room, compacting or growing it on the GPU when it is full.</summary>
		private void MakeRoom(VertexFormat format, int size)
		{
			int f = (int)format;
			VertexArena old = arenas[f];
			if(old.HasRoom(size)) return;

			VertexArena next = old.Compact(size, out List<VertexArena.CopyRun> copies);
			uint oldbuffer = arenabuffers[f];
			uint oldvao = arenavaos[f];

			uint newbuffer = gl.GenBuffer();
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, newbuffer);
			gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)next.Size, null, BufferUsageARB.StaticDraw);

			gl.BindBuffer(BufferTargetARB.CopyReadBuffer, oldbuffer);
			foreach(VertexArena.CopyRun run in copies)
				gl.CopyBufferSubData(CopyBufferSubDataTarget.CopyReadBuffer, CopyBufferSubDataTarget.ArrayBuffer, run.ReadOffset, run.WriteOffset, (nuint)run.Size);
			gl.BindBuffer(BufferTargetARB.CopyReadBuffer, 0);
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

			if(oldvao != 0) gl.DeleteVertexArray(oldvao);
			gl.DeleteBuffer(oldbuffer);

			arenas[f] = next;
			arenabuffers[f] = newbuffer;
			arenavaos[f] = 0;   // rebuilt lazily around the new buffer
			vertexbufferchanged = true;
			needapply = true;
		}

		public bool SetIndexBufferData(IndexBuffer buffer, ReadOnlySpan<int> data)
		{
			int[] copy = data.ToArray();
			Run(() =>
			{
				GlIndexBuffer ib = buffer.BackendData as GlIndexBuffer;
				if(ib == null)
				{
					ib = new GlIndexBuffer { Owner = this };
					buffer.Backend = this;
					buffer.BackendData = ib;
				}
				if(ib.Buffer == 0) ib.Buffer = gl.GenBuffer();

				gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ib.Buffer);
				fixed(int* p = copy)
					gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(copy.Length * sizeof(int)), p, BufferUsageARB.StaticDraw);
				gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
				indexbufferchanged = true;
				needapply = true;
				CheckGLError();
			});
			return true;
		}

		private uint GetArenaVao(int f)
		{
			if(arenavaos[f] == 0)
			{
				arenavaos[f] = gl.GenVertexArray();
				gl.BindVertexArray(arenavaos[f]);
				gl.BindBuffer(BufferTargetARB.ArrayBuffer, arenabuffers[f]);
				SetupVertexLayout((VertexFormat)f);
				gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
			}
			return arenavaos[f];
		}

		private void SetupVertexLayout(VertexFormat format)
		{
			uint stride = (uint)(format == VertexFormat.Flat ? FlatVertex.Stride : WorldVertex.Stride);
			gl.EnableVertexAttribArray(GlShader.AttrPosition);
			gl.EnableVertexAttribArray(GlShader.AttrColor);
			gl.EnableVertexAttribArray(GlShader.AttrUV);
			gl.VertexAttribPointer(GlShader.AttrPosition, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
			// Colors are stored as BGRA bytes; GL_BGRA as the component count swizzles them (core since GL 3.2)
			gl.VertexAttribPointer(GlShader.AttrColor, gles ? 4 : GL_BGRA, VertexAttribPointerType.UnsignedByte, true, stride, (void*)12);
			gl.VertexAttribPointer(GlShader.AttrUV, 2, VertexAttribPointerType.Float, false, stride, (void*)16);
			if(format == VertexFormat.World)
			{
				gl.EnableVertexAttribArray(GlShader.AttrNormal);
				gl.VertexAttribPointer(GlShader.AttrNormal, 3, VertexAttribPointerType.Float, false, stride, (void*)24);
			}
		}

		#endregion

		#region ================== Textures

		private static InternalFormat ToInternalFormat(TextureFormat f)
		{
			switch(f)
			{
				case TextureFormat.Rgba8: case TextureFormat.Bgra8: return InternalFormat.Rgba8;
				case TextureFormat.Rg16f: return InternalFormat.RG16f;
				case TextureFormat.Rgba16f: return InternalFormat.Rgba16f;
				case TextureFormat.R32f: return InternalFormat.R32f;
				case TextureFormat.Rg32f: return InternalFormat.RG32f;
				case TextureFormat.Rgb32f: return InternalFormat.Rgb32f;
				case TextureFormat.Rgba32f: return InternalFormat.Rgba32f;
				case TextureFormat.D32f_S8: return InternalFormat.Depth32fStencil8;
				default: return InternalFormat.Depth24Stencil8;
			}
		}

		private PixelFormat ToDataFormat(TextureFormat f)
		{
			switch(f)
			{
				case TextureFormat.Rgba8: return PixelFormat.Rgba;
				case TextureFormat.Bgra8: return gles ? PixelFormat.Rgba : PixelFormat.Bgra;
				case TextureFormat.Rg16f: case TextureFormat.Rg32f: return PixelFormat.RG;
				case TextureFormat.Rgba16f: case TextureFormat.Rgba32f: return PixelFormat.Rgba;
				case TextureFormat.R32f: return PixelFormat.Red;
				case TextureFormat.Rgb32f: return PixelFormat.Rgb;
				default: return PixelFormat.DepthStencil;
			}
		}

		private static PixelType ToDataType(TextureFormat f)
		{
			switch(f)
			{
				case TextureFormat.Rgba8: case TextureFormat.Bgra8: return PixelType.UnsignedByte;
				case TextureFormat.D32f_S8: return PixelType.Float32UnsignedInt248Rev;
				case TextureFormat.D24_S8: return PixelType.UnsignedInt248;
				default: return PixelType.Float;
			}
		}

		private static readonly TextureTarget[] CubeFaces =
		{
			TextureTarget.TextureCubeMapPositiveX, TextureTarget.TextureCubeMapPositiveY, TextureTarget.TextureCubeMapPositiveZ,
			TextureTarget.TextureCubeMapNegativeX, TextureTarget.TextureCubeMapNegativeY, TextureTarget.TextureCubeMapNegativeZ
		};

		private BaseTexture TextureInfo(GlTexture data) { return texturedimensions[data]; }

		/// <summary>Creates the GL texture on first use (empty), exactly like the native GetTexture().</summary>
		private uint GetTexture(GlTexture data)
		{
			if(data.Texture != 0) return data.Texture;

			BaseTexture info = TextureInfo(data);
			gl.ActiveTexture(Silk.NET.OpenGL.TextureUnit.Texture0);
			data.Texture = gl.GenTexture();
			gl.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);

			InternalFormat internalformat = ToInternalFormat(info.Format);
			PixelFormat dataformat = ToDataFormat(info.Format);
			PixelType datatype = ToDataType(info.Format);

			if(!info.IsCube)
			{
				gl.BindTexture(TextureTarget.Texture2D, data.Texture);
				gl.TexImage2D(TextureTarget.Texture2D, 0, internalformat, (uint)info.Width, (uint)info.Height, 0, dataformat, datatype, null);
				gl.BindTexture(TextureTarget.Texture2D, 0);
			}
			else
			{
				gl.BindTexture(TextureTarget.TextureCubeMap, data.Texture);
				foreach(TextureTarget face in CubeFaces)
					gl.TexImage2D(face, 0, internalformat, (uint)info.Width, (uint)info.Height, 0, dataformat, datatype, null);
				gl.BindTexture(TextureTarget.TextureCubeMap, 0);
			}
			units[0].SamplerHandle = 0;   // we touched unit 0's binding
			textureschanged = true;
			needapply = true;
			return data.Texture;
		}

		public bool SetPixels(Texture texture, ReadOnlySpan<byte> pixels)
		{
			byte[] copy = Copy(pixels);
			Run(() =>
			{
				GlTexture data = EnsureTextureData(texture);
				UploadPixels(data, texture, TextureTarget.Texture2D, TextureTarget.Texture2D, copy, true);
			});
			return true;
		}

		public bool SetCubePixels(CubeTexture texture, CubeMapFace face, ReadOnlySpan<byte> pixels)
		{
			byte[] copy = Copy(pixels);
			Run(() =>
			{
				GlTexture data = EnsureTextureData(texture);
				UploadPixels(data, texture, TextureTarget.TextureCubeMap, CubeFaces[(int)face], copy, face == CubeMapFace.NegativeZ);
			});
			return true;
		}

		private void UploadPixels(GlTexture data, BaseTexture info, TextureTarget bindtarget, TextureTarget imagetarget, byte[] pixels, bool mipmaps)
		{
			GetTexture(data);
			gl.ActiveTexture(Silk.NET.OpenGL.TextureUnit.Texture0);
			gl.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);
			gl.BindTexture(bindtarget, data.Texture);
			fixed(byte* p = pixels)
				gl.TexImage2D(imagetarget, 0, ToInternalFormat(info.Format), (uint)info.Width, (uint)info.Height, 0, ToDataFormat(info.Format), ToDataType(info.Format), p);
			if(mipmaps) gl.GenerateMipmap(bindtarget);
			gl.BindTexture(bindtarget, 0);
			textureschanged = true;
			needapply = true;
			CheckGLError();
		}

		public IntPtr MapPBO(Texture texture)
		{
			// The host cannot guarantee a context here, so hand out CPU memory; UnmapPBO uploads it.
			GlTexture data = EnsureTextureData(texture);
			if(data.Staging == IntPtr.Zero)
				data.Staging = Marshal.AllocHGlobal(texture.Width * texture.Height * 4);
			return data.Staging;
		}

		public bool UnmapPBO(Texture texture)
		{
			GlTexture data = EnsureTextureData(texture);
			if(data.Staging == IntPtr.Zero) return false;
			byte[] copy = new byte[texture.Width * texture.Height * 4];
			Marshal.Copy(data.Staging, copy, 0, copy.Length);
			Run(() => UploadPixels(data, texture, TextureTarget.Texture2D, TextureTarget.Texture2D, copy, false));
			return true;
		}

		private uint GetFramebuffer(GlTexture data, bool usedepthbuffer)
		{
			BaseTexture info = TextureInfo(data);
			if(!usedepthbuffer)
			{
				if(data.Framebuffer == 0)
				{
					uint texture = GetTexture(data);
					data.Framebuffer = gl.GenFramebuffer();
					gl.BindFramebuffer(FramebufferTarget.Framebuffer, data.Framebuffer);
					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
					if(gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
						throw new InvalidOperationException("glCheckFramebufferStatus did not return GL_FRAMEBUFFER_COMPLETE");
				}
				return data.Framebuffer;
			}

			if(data.DepthRenderbuffer == 0)
			{
				data.DepthRenderbuffer = gl.GenRenderbuffer();
				gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, data.DepthRenderbuffer);
				gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)info.Width, (uint)info.Height);
				gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
			}

			if(data.Framebuffer == 0)
			{
				uint texture = GetTexture(data);
				data.Framebuffer = gl.GenFramebuffer();
				gl.BindFramebuffer(FramebufferTarget.Framebuffer, data.Framebuffer);
				gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
				gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, data.DepthRenderbuffer);
				if(gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
					throw new InvalidOperationException("glCheckFramebufferStatus did not return GL_FRAMEBUFFER_COMPLETE");
				gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			}
			return data.Framebuffer;
		}

		#endregion

		#region ================== Rendering

		private static readonly Silk.NET.OpenGL.PrimitiveType[] Modes =
		{
			Silk.NET.OpenGL.PrimitiveType.Lines, Silk.NET.OpenGL.PrimitiveType.Triangles, Silk.NET.OpenGL.PrimitiveType.TriangleStrip
		};
		private static readonly int[] ToVertexCount = { 2, 3, 1 };
		private static readonly int[] ToVertexStart = { 0, 0, 2 };

		private int VertexBufferStartIndex { get { return vertexbuffer != null && vertexbuffer.Range != null ? vertexbuffer.Range.StartIndex : 0; } }

		private bool DrawNow(PrimitiveType type, int startIndex, int primitiveCount)
		{
			if(needapply && !ApplyChanges()) return false;
			gl.DrawArrays(Modes[(int)type], VertexBufferStartIndex + startIndex, (uint)(ToVertexStart[(int)type] + primitiveCount * ToVertexCount[(int)type]));
			return CheckGLError();
		}

		private bool DrawIndexedNow(PrimitiveType type, int startIndex, int primitiveCount)
		{
			if(needapply && !ApplyChanges()) return false;
			gl.DrawElementsBaseVertex(Modes[(int)type], (uint)(ToVertexStart[(int)type] + primitiveCount * ToVertexCount[(int)type]),
				DrawElementsType.UnsignedInt, (void*)(startIndex * sizeof(uint)), VertexBufferStartIndex);
			return CheckGLError();
		}

		private bool DrawDataNow(PrimitiveType type, int startIndex, int primitiveCount, ReadOnlySpan<byte> flatvertices)
		{
			int vertcount = ToVertexStart[(int)type] + primitiveCount * ToVertexCount[(int)type];
			if(needapply && !ApplyChanges()) return false;

			gl.BindBuffer(BufferTargetARB.ArrayBuffer, streamvbo);
			fixed(byte* p = flatvertices)
				gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertcount * FlatVertex.Stride), p + startIndex * FlatVertex.Stride, BufferUsageARB.StreamDraw);
			gl.BindVertexArray(streamvao);
			gl.DrawArrays(Modes[(int)type], 0, (uint)vertcount);
			if(!CheckGLError()) return false;

			return ApplyVertexBuffer();
		}

		private bool StartRenderingNow(bool clear, int backcolor, Texture target, bool usedepthbuffer)
		{
			if(target != null)
			{
				try
				{
					uint framebuffer = GetFramebuffer(EnsureTextureData(target), usedepthbuffer);
					gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
				}
				catch(InvalidOperationException e)
				{
					SetError("Error setting render target: " + e.Message);
					return false;
				}
				viewportwidth = target.Width;
				viewportheight = target.Height;
			}
			else
			{
				gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)defaultFramebuffer);
				viewportwidth = surfacesize.Width;
				viewportheight = surfacesize.Height;
			}
			gl.Viewport(0, 0, (uint)viewportwidth, (uint)viewportheight);

			float a = ((backcolor >> 24) & 0xFF) / 255.0f, r = ((backcolor >> 16) & 0xFF) / 255.0f, g = ((backcolor >> 8) & 0xFF) / 255.0f, b = (backcolor & 0xFF) / 255.0f;
			if(clear && usedepthbuffer)
			{
				gl.Enable(EnableCap.DepthTest);
				gl.DepthMask(true);
				gl.ClearColor(r, g, b, a);
				gl.ClearDepth(1.0f);
				gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
			}
			else if(clear)
			{
				gl.ClearColor(r, g, b, a);
				gl.Clear((uint)ClearBufferMask.ColorBufferBit);
			}

			needapply = shaderchanged = uniformschanged = textureschanged = true;
			indexbufferchanged = vertexbufferchanged = depthstatechanged = blendstatechanged = rasterizerstatechanged = true;
			return CheckGLError();
		}

		private void FinishRenderingNow() { }
		public bool FinishRendering() { Run(FinishRenderingNow); return true; }

		/// <summary>The host swaps buffers itself; this only recycles released resources.</summary>
		public bool Present()
		{
			if(inFrame) ProcessReleases();
			return true;
		}

		/// <summary>Failures of commands that were queued (the caller already got "true"). Also written to stderr.</summary>
		public event Action<string> CommandFailed;

		private void ReportDeferredFailure(string what)
		{
			string message = what + ": " + error;
			Console.Error.WriteLine("[GL] " + message);
			CommandFailed?.Invoke(message);
		}

		private bool RunCommand(string what, Func<bool> work)
		{
			if(inFrame && gl != null) return work();
			deferred.Enqueue(() => { if(!work()) ReportDeferredFailure(what); });
			return true;
		}

		// ---- the ordered commands: executed now inside a frame, queued (and replayed in order) outside one

		public void SetShader(ShaderName name) { Run(() => SetShaderNow(name)); }

		public void SetUniform(UniformName name, ReadOnlySpan<byte> data, int count)
		{
			if(inFrame && gl != null) { SetUniformNow(name, data, count); return; }
			byte[] copy = Copy(data);
			deferred.Enqueue(() => SetUniformNow(name, copy, count));
		}

		public void SetVertexBuffer(VertexBuffer buffer) { Run(() => SetVertexBufferNow(buffer)); }
		public void SetIndexBuffer(IndexBuffer buffer) { Run(() => SetIndexBufferNow(buffer)); }
		public void SetAlphaBlendEnable(bool value) { Run(() => SetAlphaBlendEnableNow(value)); }
		public void SetAlphaTestEnable(bool value) { Run(() => SetAlphaTestEnableNow(value)); }
		public void SetCullMode(Cull mode) { Run(() => SetCullModeNow(mode)); }
		public void SetBlendOperation(BlendOperation op) { Run(() => SetBlendOperationNow(op)); }
		public void SetSourceBlend(Blend blend) { Run(() => SetSourceBlendNow(blend)); }
		public void SetDestinationBlend(Blend blend) { Run(() => SetDestinationBlendNow(blend)); }
		public void SetFillMode(FillMode mode) { Run(() => SetFillModeNow(mode)); }
		public void SetZEnable(bool value) { Run(() => SetZEnableNow(value)); }
		public void SetZWriteEnable(bool value) { Run(() => SetZWriteEnableNow(value)); }
		public void SetTexture(int unit, BaseTexture texture) { Run(() => SetTextureNow(unit, texture)); }
		public void SetSamplerFilter(int unit, TextureFilter minfilter, TextureFilter magfilter, MipmapFilter mipfilter, float maxanisotropy) { Run(() => SetSamplerFilterNow(unit, minfilter, magfilter, mipfilter, maxanisotropy)); }
		public void SetSamplerState(int unit, TextureAddress address) { Run(() => SetSamplerStateNow(unit, address)); }

		public bool Draw(PrimitiveType type, int startIndex, int primitiveCount) { return RunCommand("Draw", () => DrawNow(type, startIndex, primitiveCount)); }
		public bool DrawIndexed(PrimitiveType type, int startIndex, int primitiveCount) { return RunCommand("DrawIndexed", () => DrawIndexedNow(type, startIndex, primitiveCount)); }

		public bool DrawData(PrimitiveType type, int startIndex, int primitiveCount, ReadOnlySpan<byte> flatvertices)
		{
			if(inFrame && gl != null) return DrawDataNow(type, startIndex, primitiveCount, flatvertices);
			byte[] copy = Copy(flatvertices);
			return RunCommand("DrawData", () => DrawDataNow(type, startIndex, primitiveCount, copy));
		}

		public bool StartRendering(bool clear, int backcolor, Texture target, bool usedepthbuffer)
		{
			return RunCommand("StartRendering", () => StartRenderingNow(clear, backcolor, target, usedepthbuffer));
		}

		public bool CopyTexture(CubeTexture dst, CubeMapFace face) { return RunCommand("CopyTexture", () => CopyTextureNow(dst, face)); }

		public bool ClearTexture(int backcolor, Texture texture)
		{
			// A self-contained command, so unlike draws it can wait for the next frame
			Run(() =>
			{
				if(!StartRenderingNow(true, backcolor, texture, false)) throw new InvalidOperationException(error);
			});
			return true;
		}

		private bool CopyTextureNow(CubeTexture dst, CubeMapFace face)
		{
			GlTexture data = EnsureTextureData(dst);
			gl.BindTexture(TextureTarget.TextureCubeMap, GetTexture(data));
			gl.CopyTexSubImage2D(CubeFaces[(int)face], 0, 0, 0, 0, 0, (uint)dst.Width, (uint)dst.Height);
			if(face == CubeMapFace.NegativeZ) gl.GenerateMipmap(TextureTarget.TextureCubeMap);
			gl.BindTexture(TextureTarget.TextureCubeMap, 0);
			textureschanged = true;
			needapply = true;
			return CheckGLError();
		}

		private GlShader ActiveShader { get { return alphatest ? alphatestshaders[(int)shadername] : shaders[(int)shadername]; } }

		private bool ApplyChanges()
		{
			if(shaderchanged && !ApplyShader()) return false;
			if(vertexbufferchanged && !ApplyVertexBuffer()) return false;
			if(indexbufferchanged && !ApplyIndexBuffer()) return false;
			if(uniformschanged && !ApplyUniforms()) return false;
			if(textureschanged && !ApplyTextures()) return false;
			if(rasterizerstatechanged && !ApplyRasterizerState()) return false;
			if(blendstatechanged && !ApplyBlendState()) return false;
			if(depthstatechanged && !ApplyDepthState()) return false;
			needapply = false;
			return true;
		}

		private bool ApplyShader()
		{
			GlShader shader = ActiveShader;
			if(!shader.CheckCompile(gl, gles, uniformnames))
			{
				SetError("Failed to bind shader:\r\n" + shader.GetCompileError());
				return false;
			}
			shader.Bind(gl);
			shaderchanged = false;
			return CheckGLError();
		}

		private bool ApplyRasterizerState()
		{
			if(cullmode == Cull.None)
			{
				gl.Disable(EnableCap.CullFace);
			}
			else
			{
				gl.Enable(EnableCap.CullFace);
				gl.FrontFace(FrontFaceDirection.CW);
			}

			// ES has no glPolygonMode: wireframe is a desktop-only feature
			if(!gles) gl.PolygonMode(TriangleFace.FrontAndBack, fillmode == FillMode.Solid ? Silk.NET.OpenGL.PolygonMode.Fill : Silk.NET.OpenGL.PolygonMode.Line);

			rasterizerstatechanged = false;
			return CheckGLError();
		}

		private static BlendingFactor ToBlendFactor(Blend b)
		{
			switch(b)
			{
				case Blend.InverseSourceAlpha: return BlendingFactor.OneMinusSrcAlpha;
				case Blend.SourceAlpha: return BlendingFactor.SrcAlpha;
				default: return BlendingFactor.One;
			}
		}

		private bool ApplyBlendState()
		{
			if(alphablend)
			{
				gl.Enable(EnableCap.Blend);
				gl.BlendEquation(blendop == BlendOperation.Add ? BlendEquationModeEXT.FuncAdd : BlendEquationModeEXT.FuncReverseSubtract);
				gl.BlendFunc(ToBlendFactor(srcblend), ToBlendFactor(dstblend));
			}
			else
			{
				gl.Disable(EnableCap.Blend);
			}
			blendstatechanged = false;
			return CheckGLError();
		}

		private bool ApplyDepthState()
		{
			if(depthtest)
			{
				gl.Enable(EnableCap.DepthTest);
				gl.DepthFunc(DepthFunction.Lequal);
				gl.DepthMask(depthwrite);
			}
			else
			{
				gl.Disable(EnableCap.DepthTest);
			}
			depthstatechanged = false;
			return CheckGLError();
		}

		private bool ApplyIndexBuffer()
		{
			gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexbuffer != null ? indexbuffer.Buffer : 0);
			indexbufferchanged = false;
			return CheckGLError();
		}

		private bool ApplyVertexBuffer()
		{
			if(vertexbuffer != null)
				gl.BindVertexArray(GetArenaVao((int)vertexbuffer.Format));
			vertexbufferchanged = false;
			return CheckGLError();
		}

		private bool ApplyUniforms()
		{
			GlShader shader = ActiveShader;
			int count = Math.Min(uniforms.Count, shader.UniformLocations.Length);
			for(int i = 0; i < count; i++)
			{
				UniformInfo info = uniforms[i];
				if(shader.UniformLastUpdates[i] == info.LastUpdate) continue;

				int location = shader.UniformLocations[i];
				if(location >= 0 && info.Data.Length > 0)
				{
					fixed(byte* bytes = info.Data)
					{
						float* f = (float*)bytes;
						int* n = (int*)bytes;
						switch(info.Type)
						{
							case UniformType.Vec4f: gl.Uniform4(location, 1, f); break;
							case UniformType.Vec3f: gl.Uniform3(location, 1, f); break;
							case UniformType.Vec2f: gl.Uniform2(location, 1, f); break;
							case UniformType.Float: gl.Uniform1(location, 1, f); break;
							case UniformType.Mat4: gl.UniformMatrix4(location, 1, false, f); break;
							case UniformType.Vec4i: gl.Uniform4(location, 1, n); break;
							case UniformType.Vec3i: gl.Uniform3(location, 1, n); break;
							case UniformType.Vec2i: gl.Uniform2(location, 1, n); break;
							case UniformType.Int: gl.Uniform1(location, 1, n); break;
							case UniformType.Vec4fArray: gl.Uniform4(location, (uint)info.Count, f); break;
							case UniformType.Vec3fArray: gl.Uniform3(location, (uint)info.Count, f); break;
							case UniformType.Vec2fArray: gl.Uniform2(location, (uint)info.Count, f); break;
						}
					}
				}
				shader.UniformLastUpdates[i] = info.LastUpdate;
			}
			uniformschanged = false;
			return CheckGLError();
		}

		private static int ToGlMinFilter(TextureFilter filter, MipmapFilter mip)
		{
			bool linear = filter == TextureFilter.Linear;
			switch(mip)
			{
				case MipmapFilter.Linear: return (int)(linear ? GLEnum.LinearMipmapLinear : GLEnum.NearestMipmapLinear);
				case MipmapFilter.Nearest: return (int)(linear ? GLEnum.LinearMipmapNearest : GLEnum.NearestMipmapNearest);
				default: return (int)(linear ? GLEnum.Linear : GLEnum.Nearest);
			}
		}

		private bool ApplyTextures()
		{
			bool ok = true;
			for(int index = 0; index < MaxTextureUnits; index++)
			{
				ref UnitState unit = ref units[index];
				if(unit.Tex != null)
				{
					gl.ActiveTexture((Silk.NET.OpenGL.TextureUnit)((int)Silk.NET.OpenGL.TextureUnit.Texture0 + index));
					TextureTarget target = TextureInfo(unit.Tex).IsCube ? TextureTarget.TextureCubeMap : TextureTarget.Texture2D;
					gl.BindTexture(target, GetTexture(unit.Tex));

					// Samplers are cached per (filter, anisotropy) with one object per wrap mode
					SamplerKey key = new SamplerKey(ToGlMinFilter(unit.MagFilter, unit.MipFilter), unit.MagFilter == TextureFilter.Linear ? (int)GLEnum.Linear : (int)GLEnum.Nearest, unit.MaxAnisotropy);
					if(!samplers.TryGetValue(key, out uint[] wraps)) samplers[key] = wraps = new uint[2];

					uint handle = wraps[(int)unit.WrapMode];
					if(handle == 0)
					{
						int wrap = unit.WrapMode == TextureAddress.Wrap ? (int)GLEnum.Repeat : (int)GLEnum.ClampToEdge;
						handle = gl.GenSampler();
						gl.SamplerParameter(handle, SamplerParameterI.MinFilter, key.Min);
						gl.SamplerParameter(handle, SamplerParameterI.MagFilter, key.Mag);
						gl.SamplerParameter(handle, SamplerParameterI.WrapS, wrap);
						gl.SamplerParameter(handle, SamplerParameterI.WrapT, wrap);
						gl.SamplerParameter(handle, SamplerParameterI.WrapR, wrap);
						if(key.Aniso > 0.0f && !gles)
							gl.SamplerParameter(handle, (SamplerParameterF)GL_TEXTURE_MAX_ANISOTROPY_EXT, key.Aniso);
						wraps[(int)unit.WrapMode] = handle;
					}

					if(unit.SamplerHandle != handle)
					{
						unit.SamplerHandle = handle;
						gl.BindSampler((uint)index, handle);
					}
				}
				ok &= CheckGLError();
			}
			textureschanged = false;
			return ok;
		}

		private bool CheckGLError()
		{
			GLEnum code = gl.GetError();
			if(code == GLEnum.NoError) return true;
			SetError("OpenGL error: " + code);
			return false;
		}

		#endregion

		public void Dispose()
		{
			if(gl != null)
			{
				ProcessReleases();
				foreach(GlShader s in shaders) s.ReleaseResources(gl);
				foreach(GlShader s in alphatestshaders) s.ReleaseResources(gl);
				foreach(uint[] wraps in samplers.Values) foreach(uint s in wraps) if(s != 0) gl.DeleteSampler(s);
				for(int i = 0; i < arenas.Length; i++)
				{
					if(arenabuffers[i] != 0) gl.DeleteBuffer(arenabuffers[i]);
					if(arenavaos[i] != 0) gl.DeleteVertexArray(arenavaos[i]);
				}
				gl.DeleteBuffer(streamvbo);
				gl.DeleteVertexArray(streamvao);
				gl = null;
			}
		}
	}
}
