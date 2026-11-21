
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections.Generic;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Geometry;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Reflection;
using System.IO;
using System.Text;
using CodeImp.DoomBuilder.Rendering.Shaders;

#endregion

namespace CodeImp.DoomBuilder.Rendering
{
    public class RenderDeviceException : Exception
    {
        public RenderDeviceException(string message) : base(message) { }
    }

    /// <summary>
    /// Thin, API-neutral front end that the renderers talk to. The actual graphics API is an <see cref="IRenderBackend"/>.
    /// </summary>
    public class RenderDevice : IDisposable
    {
        private IRenderBackend backend;

        public RenderDevice(IRenderBackend backend)
        {
            this.backend = backend;

            DeclareUniform(UniformName.rendersettings, "rendersettings", UniformType.Vec4f);
            DeclareUniform(UniformName.projection, "projection", UniformType.Mat4);
            DeclareUniform(UniformName.desaturation, "desaturation", UniformType.Float);
            DeclareUniform(UniformName.highlightcolor, "highlightcolor", UniformType.Vec4f);
            DeclareUniform(UniformName.view, "view", UniformType.Mat4);
            DeclareUniform(UniformName.world, "world", UniformType.Mat4);
            DeclareUniform(UniformName.modelnormal, "modelnormal", UniformType.Mat4);
            DeclareUniform(UniformName.FillColor, "fillColor", UniformType.Vec4f);
            DeclareUniform(UniformName.vertexColor, "vertexColor", UniformType.Vec4f);
            DeclareUniform(UniformName.stencilColor, "stencilColor", UniformType.Vec4f);
            DeclareUniform(UniformName.lightPosAndRadius, "lightPosAndRadius", UniformType.Vec4fArray);
            DeclareUniform(UniformName.lightOrientation, "lightOrientation", UniformType.Vec4fArray);
            DeclareUniform(UniformName.light2Radius, "light2Radius", UniformType.Vec2fArray);
            DeclareUniform(UniformName.lightColor, "lightColor", UniformType.Vec4fArray);
            DeclareUniform(UniformName.ignoreNormals, "ignoreNormals", UniformType.Float);
            DeclareUniform(UniformName.spotLight, "spotLight", UniformType.Float);
            DeclareUniform(UniformName.campos, "campos", UniformType.Vec4f);
            DeclareUniform(UniformName.fogsettings, "fogsettings", UniformType.Vec4f);
            DeclareUniform(UniformName.fogcolor, "fogcolor", UniformType.Vec4f);
            DeclareUniform(UniformName.sectorfogcolor, "sectorfogcolor", UniformType.Vec4f);
            DeclareUniform(UniformName.lightsEnabled, "lightsEnabled", UniformType.Float);
			DeclareUniform(UniformName.slopeHandleLength, "slopeHandleLength", UniformType.Float);

			// vkdoom lights
            DeclareUniform(UniformName.lightStrengthAndLinearity, "lightStrengthAndLinearity", UniformType.Vec2fArray);
            DeclareUniform(UniformName.useLightStrength, "useLightStrength", UniformType.Float);
            
            // volte: classic rendering
            DeclareUniform(UniformName.drawPaletted, "drawPaletted", UniformType.Int);
            DeclareUniform(UniformName.colormapSize, "colormapSize", UniformType.Vec2i);
            DeclareUniform(UniformName.doomlightlevels, "doomlightlevels", UniformType.Int);
            DeclareUniform(UniformName.sectorLightLevel, "sectorLightLevel", UniformType.Int);

            DeclareUniform(UniformName.skew, "skew", UniformType.Vec2f);

            // 2d fsaa
            CompileShader(ShaderName.display2d_fsaa, "display2d.shader", "display2d_fsaa");
            
            // 2d normal
            CompileShader(ShaderName.display2d_normal, "display2d.shader", "display2d_normal");
            CompileShader(ShaderName.display2d_fullbright, "display2d.shader", "display2d_fullbright");

            // 2d things
            CompileShader(ShaderName.things2d_thing, "things2d.shader", "things2d_thing");
            CompileShader(ShaderName.things2d_sprite, "things2d.shader", "things2d_sprite");
            CompileShader(ShaderName.things2d_fill, "things2d.shader", "things2d_fill");

            // non-fog 3d shaders
            CompileShader(ShaderName.world3d_main, "world3d.shader", "world3d_main");
            CompileShader(ShaderName.world3d_fullbright, "world3d.shader", "world3d_fullbright");
            CompileShader(ShaderName.world3d_main_highlight, "world3d.shader", "world3d_main_highlight");
            CompileShader(ShaderName.world3d_fullbright_highlight, "world3d.shader", "world3d_fullbright_highlight");
            CompileShader(ShaderName.world3d_vertex_color, "world3d.shader", "world3d_vertex_color");
            CompileShader(ShaderName.world3d_main_vertexcolor, "world3d.shader", "world3d_main_vertexcolor");
            CompileShader(ShaderName.world3d_constant_color, "world3d.shader", "world3d_constant_color");
            
            // classic rendering
            CompileShader(ShaderName.world3d_classic, "world3d.shader", "world3d_classic");
            CompileShader(ShaderName.world3d_classic_highlight, "world3d.shader", "world3d_classic_highlight");

            // skybox shader
            CompileShader(ShaderName.world3d_skybox, "world3d_skybox.shader", "world3d_skybox");

            // fog 3d shaders
            CompileShader(ShaderName.world3d_main_fog, "world3d.shader", "world3d_main_fog");
            CompileShader(ShaderName.world3d_main_highlight_vertexcolor, "world3d.shader", "world3d_highlight_vertexcolor");
            CompileShader(ShaderName.world3d_main_highlight_fog, "world3d.shader", "world3d_main_highlight_fog");
            CompileShader(ShaderName.world3d_main_fog_vertexcolor, "world3d.shader", "world3d_main_fog_vertexcolor");
            CompileShader(ShaderName.world3d_main_highlight_fog_vertexcolor, "world3d.shader", "world3d_main_highlight_fog_vertexcolor");

			// Slope handle
			CompileShader(ShaderName.world3d_slope_handle, "world3d.shader", "world3d_slope_handle");

            SetupSettings();
        }

        ~RenderDevice()
        {
            Dispose();
        }

        /// <summary>The backend this device draws with.</summary>
        public IRenderBackend Backend { get { return backend; } }

        /// <summary>Size in pixels of the surface drawn to when no render target texture is set.</summary>
        public Size ClientSize { get { return backend.SurfaceSize; } }

        public bool Disposed { get { return backend == null; } }

        void ThrowIfFailed(bool result)
        {
            if (!result)
                throw new RenderDeviceException(backend.GetError());
        }

        public void Dispose()
        {
            if (!Disposed)
            {
                backend.Dispose();
                backend = null;
            }
        }

        public void DeclareUniform(UniformName name, string variablename, UniformType type)
        {
            backend.DeclareUniform(name, variablename, type);
        }

        public void DeclareShader(ShaderName name, string vertResourceName, string fragResourceName)
        {
            backend.DeclareShader(name, name.ToString(), GetResourceText(vertResourceName), GetResourceText(fragResourceName));
        }

        // save precompiled shaders -- don't build from scratch every time
        private static Dictionary<string, ShaderGroup> precompiledGroups = new Dictionary<string, ShaderGroup>();
        public void CompileShader(ShaderName internalName, string groupName, string shaderName)
        {
            ShaderGroup sg;

            if (precompiledGroups.ContainsKey(groupName))
                sg = precompiledGroups[groupName];
            else sg = ShaderCompiler.Compile(GetResourceText(groupName));

            Shader s = sg.GetShader(shaderName);

            if (s == null)
                throw new RenderDeviceException(string.Format("Shader {0}::{1} not found", groupName, shaderName));

            backend.DeclareShader(internalName, internalName.ToString(), s.GetVertexSource(), s.GetFragmentSource());
        }

        static string GetResourceText(string name)
        {
            string fullname = string.Format("CodeImp.DoomBuilder.Resources.{0}", name);
            using (Stream stream = typeof(RenderDevice).Assembly.GetManifestResourceStream(fullname))
            {
                if (stream == null)
                    throw new Exception(string.Format("Resource {0} not found!", fullname));
                byte[] data = new byte[stream.Length];
                stream.ReadExactly(data, 0, data.Length);
                int start = 0;
                if (data.Length >= 3 && data[0] == 0xef && data[1] == 0xbb && data[2] == 0xbf)
                    start = 3;
                return Encoding.UTF8.GetString(data, start, data.Length - start);
            }
        }

        public void SetShader(ShaderName shader)
        {
            backend.SetShader(shader);
        }

        private void SetUniformFloats(UniformName uniform, ReadOnlySpan<float> data, int count)
        {
            backend.SetUniform(uniform, MemoryMarshal.AsBytes(data), count);
        }

        private void SetUniformInts(UniformName uniform, ReadOnlySpan<int> data, int count)
        {
            backend.SetUniform(uniform, MemoryMarshal.AsBytes(data), count);
        }

        public void SetUniform(UniformName uniform, bool value)
        {
            SetUniformFloats(uniform, stackalloc float[] { value ? 1.0f : 0.0f }, 1);
        }

        public void SetUniform(UniformName uniform, float value)
        {
            SetUniformFloats(uniform, stackalloc float[] { value }, 1);
        }

        public void SetUniform(UniformName uniform, Vector2f value)
        {
            SetUniformFloats(uniform, stackalloc float[] { value.X, value.Y }, 1);
        }

        public void SetUniform(UniformName uniform, Vector3f value)
        {
            SetUniformFloats(uniform, stackalloc float[] { value.X, value.Y, value.Z }, 1);
        }

        public void SetUniform(UniformName uniform, Vector4f value)
        {
            SetUniformFloats(uniform, stackalloc float[] { value.X, value.Y, value.Z, value.W }, 1);
        }

        public void SetUniform(UniformName uniform, Color4 value)
        {
            SetUniformFloats(uniform, stackalloc float[] { value.Red, value.Green, value.Blue, value.Alpha }, 1);
        }

        public void SetUniform(UniformName uniform, Matrix matrix)
        {
            SetUniform(uniform, ref matrix);
        }

        public void SetUniform(UniformName uniform, ref Matrix matrix)
        {
            backend.SetUniform(uniform, MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref matrix, 1)), 1);
        }

        public void SetUniform(UniformName uniform, int value)
        {
            SetUniformInts(uniform, stackalloc int[] { value }, 1);
        }

        public void SetUniform(UniformName uniform, Vector2i value)
        {
            SetUniformInts(uniform, stackalloc int[] { value.X, value.Y }, 1);
        }

        public void SetUniform(UniformName uniform, Vector3i value)
        {
            SetUniformInts(uniform, stackalloc int[] { value.X, value.Y, value.Z }, 1);
        }

        public void SetUniform(UniformName uniform, Vector4i value)
        {
            SetUniformInts(uniform, stackalloc int[] { value.X, value.Y, value.Z, value.W }, 1);
        }

        public void SetUniform(UniformName uniform, Vector2f[] value)
        {
            float[] conv = new float[value.Length * 2];
            for (int i = 0; i < value.Length; i++)
            {
                conv[i * 2] = value[i].X;
                conv[i * 2 + 1] = value[i].Y;
            }
            SetUniformFloats(uniform, conv, value.Length);
        }

        public void SetUniform(UniformName uniform, Vector3f[] value)
        {
            float[] conv = new float[value.Length * 3];
            for (int i = 0; i < value.Length; i++)
            {
                conv[i * 3] = value[i].X;
                conv[i * 3 + 1] = value[i].Y;
                conv[i * 3 + 2] = value[i].Z;
            }
            SetUniformFloats(uniform, conv, value.Length);
        }

        public void SetUniform(UniformName uniform, Vector4f[] value)
        {
            float[] conv = new float[value.Length * 4];
            for (int i = 0; i < value.Length; i++)
            {
                conv[i * 4] = value[i].X;
                conv[i * 4 + 1] = value[i].Y;
                conv[i * 4 + 2] = value[i].Z;
                conv[i * 4 + 3] = value[i].W;
            }
            SetUniformFloats(uniform, conv, value.Length);
        }

        public void SetVertexBuffer(VertexBuffer buffer) { backend.SetVertexBuffer(buffer); }
        public void SetIndexBuffer(IndexBuffer buffer) { backend.SetIndexBuffer(buffer); }
        public void SetAlphaBlendEnable(bool value) { backend.SetAlphaBlendEnable(value); }
        public void SetAlphaTestEnable(bool value) { backend.SetAlphaTestEnable(value); }
        public void SetCullMode(Cull mode) { backend.SetCullMode(mode); }
        public void SetBlendOperation(BlendOperation op) { backend.SetBlendOperation(op); }
        public void SetSourceBlend(Blend blend) { backend.SetSourceBlend(blend); }
        public void SetDestinationBlend(Blend blend) { backend.SetDestinationBlend(blend); }
        public void SetFillMode(FillMode mode) { backend.SetFillMode(mode); }
        public void SetMultisampleAntialias(bool value) { backend.SetMultisampleAntialias(value); }
        public void SetZEnable(bool value) { backend.SetZEnable(value); }
        public void SetZWriteEnable(bool value) { backend.SetZWriteEnable(value); }

        public void SetTexture(BaseTexture value, int unit = 0)
        {
            backend.SetTexture(unit, value);
        }

        public void SetSamplerFilter(TextureFilter filter, int unit = 0)
        {
            SetSamplerFilter(filter, filter, MipmapFilter.None, 0.0f, unit);
        }

        public void SetSamplerFilter(TextureFilter minfilter, TextureFilter magfilter, MipmapFilter mipfilter, float maxanisotropy, int unit = 0)
        {
            backend.SetSamplerFilter(unit, minfilter, magfilter, mipfilter, maxanisotropy);
        }

        public void SetSamplerState(TextureAddress address, int unit = 0)
        {
            backend.SetSamplerState(unit, address);
        }

        public void DrawIndexed(PrimitiveType type, int startIndex, int primitiveCount)
        {
            ThrowIfFailed(backend.DrawIndexed(type, startIndex, primitiveCount));
        }

        public void Draw(PrimitiveType type, int startIndex, int primitiveCount)
        {
            ThrowIfFailed(backend.Draw(type, startIndex, primitiveCount));
        }

        public void Draw(PrimitiveType type, int startIndex, int primitiveCount, FlatVertex[] data)
        {
            ThrowIfFailed(backend.DrawData(type, startIndex, primitiveCount, MemoryMarshal.AsBytes(data.AsSpan())));
        }

        public void StartRendering(bool clear, Color4 backcolor)
        {
            ThrowIfFailed(backend.StartRendering(clear, backcolor.ToArgb(), null, true));
        }

        public void StartRendering(bool clear, Color4 backcolor, Texture target, bool usedepthbuffer)
        {
            ThrowIfFailed(backend.StartRendering(clear, backcolor.ToArgb(), target, usedepthbuffer));
        }

        public void FinishRendering()
        {
            ThrowIfFailed(backend.FinishRendering());
        }

        public void Present()
        {
            ThrowIfFailed(backend.Present());
        }

        public void ClearTexture(Color4 backcolor, Texture texture)
        {
            ThrowIfFailed(backend.ClearTexture(backcolor.ToArgb(), texture));
        }

        public void CopyTexture(CubeTexture dst, CubeMapFace face)
        {
            ThrowIfFailed(backend.CopyTexture(dst, face));
        }

        public void SetBufferData(IndexBuffer buffer, int[] data)
        {
            ThrowIfFailed(backend.SetIndexBufferData(buffer, data));
        }

        public void SetBufferData(VertexBuffer buffer, int length, VertexFormat format)
        {
            int stride = (format == VertexFormat.Flat) ? FlatVertex.Stride : WorldVertex.Stride;
            ThrowIfFailed(backend.SetVertexBufferData(buffer, ReadOnlySpan<byte>.Empty, (long)length * stride, format));
        }

        public void SetBufferData(VertexBuffer buffer, FlatVertex[] data)
        {
            ThrowIfFailed(backend.SetVertexBufferData(buffer, MemoryMarshal.AsBytes(data.AsSpan()), (long)data.Length * FlatVertex.Stride, VertexFormat.Flat));
        }

        public void SetBufferData(VertexBuffer buffer, WorldVertex[] data)
        {
            ThrowIfFailed(backend.SetVertexBufferData(buffer, MemoryMarshal.AsBytes(data.AsSpan()), (long)data.Length * WorldVertex.Stride, VertexFormat.World));
        }

        public void SetBufferSubdata(VertexBuffer buffer, long destOffset, FlatVertex[] data)
        {
            ThrowIfFailed(backend.SetVertexBufferSubdata(buffer, destOffset * FlatVertex.Stride, MemoryMarshal.AsBytes(data.AsSpan())));
        }

        public void SetBufferSubdata(VertexBuffer buffer, long destOffset, WorldVertex[] data)
        {
            ThrowIfFailed(backend.SetVertexBufferSubdata(buffer, destOffset * WorldVertex.Stride, MemoryMarshal.AsBytes(data.AsSpan())));
        }

        public void SetBufferSubdata(VertexBuffer buffer, FlatVertex[] data, long size)
        {
            if (size < 0 || size > data.Length) throw new ArgumentOutOfRangeException("size");
            ThrowIfFailed(backend.SetVertexBufferSubdata(buffer, 0, MemoryMarshal.AsBytes(data.AsSpan(0, (int)size))));
        }

        public unsafe void SetPixels(Texture texture, System.Drawing.Bitmap bitmap)
        {
            System.Drawing.Imaging.BitmapData bmpdata = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Size.Width, bitmap.Size.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                int length = bitmap.Width * bitmap.Height * 4;
                ThrowIfFailed(backend.SetPixels(texture, new ReadOnlySpan<byte>(bmpdata.Scan0.ToPointer(), length)));
            }
            finally
            {
                bitmap.UnlockBits(bmpdata);
            }
        }

        public unsafe void SetPixels(CubeTexture texture, CubeMapFace face, System.Drawing.Bitmap bitmap)
        {
            System.Drawing.Imaging.BitmapData bmpdata = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Size.Width, bitmap.Size.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                int length = bitmap.Width * bitmap.Height * 4;
                ThrowIfFailed(backend.SetCubePixels(texture, face, new ReadOnlySpan<byte>(bmpdata.Scan0.ToPointer(), length)));
            }
            finally
            {
                bitmap.UnlockBits(bmpdata);
            }
        }

        public unsafe void SetPixels(Texture texture, uint* pixeldata)
        {
            ThrowIfFailed(backend.SetPixels(texture, new ReadOnlySpan<byte>(pixeldata, texture.Width * texture.Height * 4)));
        }

        public unsafe void* MapPBO(Texture texture)
        {
            void* ptr = backend.MapPBO(texture).ToPointer();
            ThrowIfFailed(ptr != null);
            return ptr;
        }

        public void UnmapPBO(Texture texture)
        {
            ThrowIfFailed(backend.UnmapPBO(texture));
        }

        internal void RegisterResource(IRenderResource res)
        {
        }

        internal void UnregisterResource(IRenderResource res)
        {
        }

        public void SetupSettings()
		{
			// Setup renderstates
			SetAlphaBlendEnable(false);
			SetAlphaTestEnable(false);
			SetCullMode(Cull.None);
			SetDestinationBlend(Blend.InverseSourceAlpha);
			SetFillMode(FillMode.Solid);
			SetMultisampleAntialias((General.Settings.AntiAliasingSamples > 0));
			SetSourceBlend(Blend.SourceAlpha);
			SetZEnable(false);
			SetZWriteEnable(false);
			
			// Texture addressing
			SetSamplerState(TextureAddress.Wrap);
			
            //mxd. It's still nice to have anisotropic filtering when texture filtering is disabled
            TextureFilter magminfilter = (General.Settings.VisualBilinear ? TextureFilter.Linear : TextureFilter.Nearest);
            SetSamplerFilter(
                magminfilter,
                magminfilter,
                General.Settings.VisualBilinear ? MipmapFilter.Linear : MipmapFilter.Nearest,
                General.Settings.FilterAnisotropy);

            // Initialize presentations
            Presentation.Initialize();
		}

        //mxd. Anisotropic filtering steps
        public static readonly List<float> AF_STEPS = new List<float> { 1.0f, 2.0f, 4.0f, 8.0f, 16.0f };

        //mxd. Antialiasing steps
        public static readonly List<int> AA_STEPS = new List<int> { 0, 2, 4, 8 };


		// This makes a Vector3 from Vector3D
		public static Vector3f V3(Vector3D v3d)
		{
			return new Vector3f((float)v3d.x, (float)v3d.y, (float)v3d.z);
		}

		// This makes a Vector3D from Vector3
		public static Vector3D V3D(Vector3f v3)
		{
			return new Vector3D(v3.X, v3.Y, v3.Z);
		}

		// This makes a Vector2 from Vector2D
		public static Vector2f V2(Vector2D v2d)
		{
			return new Vector2f((float)v2d.x, (float)v2d.y);
		}

		// This makes a Vector2D from Vector2
		public static Vector2D V2D(Vector2f v2)
		{
			return new Vector2D(v2.X, v2.Y);
		}
    }

    public enum ShaderName : int
    {
        display2d_fsaa,
        display2d_normal,
        display2d_fullbright,
        things2d_thing,
        things2d_sprite,
        things2d_fill,
        world3d_main,
        world3d_fullbright,
        world3d_main_highlight,
        world3d_fullbright_highlight,
        world3d_main_vertexcolor,
        world3d_skybox,
        world3d_main_highlight_vertexcolor,
        world3d_p7,
        world3d_main_fog,
        world3d_p9,
        world3d_main_highlight_fog,
        world3d_p11,
        world3d_main_fog_vertexcolor,
        world3d_p13,
        world3d_main_highlight_fog_vertexcolor,
        world3d_vertex_color,
        world3d_constant_color,
		world3d_slope_handle,
        world3d_classic,
        world3d_p19,
        world3d_classic_highlight
    }

    public enum UniformType : int
    {
        Vec4f,
        Vec3f,
        Vec2f,
        Float,
        Mat4,
        Vec4i,
        Vec3i,
        Vec2i,
        Int,
        Vec4fArray,
        Vec3fArray,
        Vec2fArray
    }

    public enum UniformName : int
    {
        rendersettings,
        projection,
        desaturation,
        highlightcolor,
        view,
        world,
        modelnormal,
        FillColor,
        vertexColor,
        stencilColor,
        lightPosAndRadius,
        lightOrientation,
        light2Radius,
        lightColor,
        ignoreNormals,
        spotLight,
        campos,
        fogsettings,
        fogcolor,
        sectorfogcolor,
        lightsEnabled,
		slopeHandleLength,
        drawPaletted,
        colormapSize,
        sectorLightLevel,
        doomlightlevels,
        skew,
		lightStrengthAndLinearity,
		useLightStrength
    }

    public enum VertexFormat : int { Flat, World }
    public enum Cull : int { None, Clockwise }
    public enum Blend : int { InverseSourceAlpha, SourceAlpha, One }
    public enum BlendOperation : int { Add, ReverseSubtract }
    public enum FillMode : int { Solid, Wireframe }
    public enum TextureAddress : int { Wrap, Clamp }
    public enum PrimitiveType : int { LineList, TriangleList, TriangleStrip }
    public enum TextureFilter : int { Nearest, Linear }
    public enum MipmapFilter : int { None, Nearest, Linear}
}
