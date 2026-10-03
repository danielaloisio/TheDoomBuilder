using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Rendering;
using DoomBuilder.Rendering;
using Silk.NET.OpenGL;
using Xunit;
using Texture = CodeImp.DoomBuilder.Rendering.Texture;
using PrimitiveType = CodeImp.DoomBuilder.Rendering.PrimitiveType;

namespace DoomBuilder.Rendering.Tests;

/// <summary>Runs the OpenGL backend against a real GL 3.3 core context (skipped when the machine has none).</summary>
[Collection("OpenGL")]
public class GlBackendTests
{
    private readonly GlHost host;

    public GlBackendTests(GlFixture fixture) => host = fixture.Host;

    private const string PassThroughVertex = @"
        in vec3 AttrPosition; in vec4 AttrColor; in vec2 AttrUV;
        out vec4 vColor; out vec2 vUV;
        void main() { vColor = AttrColor; vUV = AttrUV; gl_Position = vec4(AttrPosition, 1.0); }";
    private const string PassThroughFragment = @"
        in vec4 vColor; in vec2 vUV; out vec4 FragColor;
        uniform sampler2D texture1;
        void main() { FragColor = vColor; }";

    /// <summary>One huge triangle that covers the whole viewport.</summary>
    private static FlatVertex[] FullscreenTriangle(int color)
    {
        FlatVertex V(float x, float y) => new FlatVertex { x = x, y = y, z = 0, c = color, u = 0, v = 0 };
        return new[] { V(-1, -1), V(3, -1), V(-1, 3) };
    }

    private void WithBackend(Action<GlRenderBackend, GL> body)
    {
        Skip.IfNot(host.Available, host.UnavailableReason);
        host.Invoke(() =>
        {
            var backend = new GlRenderBackend();
            backend.AttachContext(host.Gl, false);
            try { body(backend, host.Gl); }
            finally { backend.Dispose(); }
        });
    }

    private static unsafe (byte r, byte g, byte b, byte a) ReadPixel(GL gl, int x, int y)
    {
        byte* px = stackalloc byte[4];
        gl.ReadPixels(x, y, 1, 1, Silk.NET.OpenGL.PixelFormat.Rgba, Silk.NET.OpenGL.PixelType.UnsignedByte, px);
        return (px[0], px[1], px[2], px[3]);
    }

    [SkippableFact]
    public void Attaching_a_context_reports_the_driver()
    {
        WithBackend((backend, gl) =>
        {
            Assert.True(backend.HasContext);
            Assert.Contains("GL", backend.GlInfo);
        });
    }

    [SkippableFact]
    public void Uploads_outside_a_frame_are_deferred_until_the_next_frame()
    {
        WithBackend((backend, gl) =>
        {
            var vb = new VertexBuffer();
            var tex = new Texture(4, 4, TextureFormat.Bgra8);
            backend.SetVertexBufferData(vb, MemoryMarshal.AsBytes(FullscreenTriangle(-1).AsSpan()), 3 * FlatVertex.Stride, VertexFormat.Flat);
            backend.SetPixels(tex, new byte[4 * 4 * 4]);

            Assert.Equal(2, backend.DeferredCount);   // nothing reached the GPU yet

            backend.BeginFrame(0, new Size(64, 64));
            Assert.Equal(0, backend.DeferredCount);
            Assert.NotNull(vb.BackendData);
            Assert.NotNull(tex.BackendData);
            backend.EndFrame();
        });
    }

    [SkippableFact]
    public void Draws_vertex_colors_into_a_render_target_texture()
    {
        WithBackend((backend, gl) =>
        {
            backend.DeclareShader(ShaderName.display2d_normal, "passthrough", PassThroughVertex, PassThroughFragment);
            var target = new Texture(64, 64, TextureFormat.Bgra8);
            var vb = new VertexBuffer();
            backend.SetVertexBufferData(vb, MemoryMarshal.AsBytes(FullscreenTriangle(unchecked((int)0xFFFF8000)).AsSpan()), 3 * FlatVertex.Stride, VertexFormat.Flat);

            backend.BeginFrame(0, new Size(128, 128));
            Assert.True(backend.StartRendering(true, unchecked((int)0xFF000000), target, false), backend.GetError());
            backend.SetShader(ShaderName.display2d_normal);
            backend.SetVertexBuffer(vb);
            Assert.True(backend.Draw(PrimitiveType.TriangleList, 0, 1), backend.GetError());

            var (r, g, b, a) = ReadPixel(gl, 32, 32);
            backend.EndFrame();

            // 0xFFFF8000 = A:FF R:FF G:80 B:00. Reading it back proves the BGRA vertex color swizzle.
            Assert.Equal(255, r);
            Assert.InRange((int)g, 127, 129);
            Assert.Equal(0, b);
            Assert.Equal(255, a);
        });
    }

    [SkippableFact]
    public void Indexed_draws_use_the_buffer_base_vertex()
    {
        WithBackend((backend, gl) =>
        {
            backend.DeclareShader(ShaderName.display2d_normal, "passthrough", PassThroughVertex, PassThroughFragment);
            var target = new Texture(64, 64, TextureFormat.Bgra8);

            // a decoy buffer first, so the real one does not start at vertex 0 of the shared buffer
            var decoy = new VertexBuffer();
            backend.SetVertexBufferData(decoy, MemoryMarshal.AsBytes(FullscreenTriangle(unchecked((int)0xFF0000FF)).AsSpan()), 3 * FlatVertex.Stride, VertexFormat.Flat);
            var vb = new VertexBuffer();
            backend.SetVertexBufferData(vb, MemoryMarshal.AsBytes(FullscreenTriangle(unchecked((int)0xFF00FF00)).AsSpan()), 3 * FlatVertex.Stride, VertexFormat.Flat);
            var ib = new IndexBuffer();
            backend.SetIndexBufferData(ib, new[] { 0, 1, 2 });

            backend.BeginFrame(0, new Size(64, 64));
            Assert.True(backend.StartRendering(true, unchecked((int)0xFF000000), target, false), backend.GetError());
            backend.SetShader(ShaderName.display2d_normal);
            backend.SetVertexBuffer(vb);
            backend.SetIndexBuffer(ib);
            Assert.True(backend.DrawIndexed(PrimitiveType.TriangleList, 0, 1), backend.GetError());

            var (r, g, b, _) = ReadPixel(gl, 32, 32);
            backend.EndFrame();
            Assert.Equal((0, 255, 0), ((int)r, (int)g, (int)b));   // green = the second buffer, not the decoy
        });
    }

    [SkippableFact]
    public void Growing_the_shared_buffer_keeps_existing_data_intact()
    {
        WithBackend((backend, gl) =>
        {
            backend.DeclareShader(ShaderName.display2d_normal, "passthrough", PassThroughVertex, PassThroughFragment);
            var target = new Texture(64, 64, TextureFormat.Bgra8);

            var small = new VertexBuffer();
            backend.SetVertexBufferData(small, MemoryMarshal.AsBytes(FullscreenTriangle(unchecked((int)0xFF0000FF)).AsSpan()), 3 * FlatVertex.Stride, VertexFormat.Flat);

            // bigger than the initial 16 MB shared buffer: forces a compaction/grow with a GPU-side copy
            var huge = new VertexBuffer();
            backend.SetVertexBufferData(huge, ReadOnlySpan<byte>.Empty, 20 * 1024 * 1024, VertexFormat.Flat);

            backend.BeginFrame(0, new Size(64, 64));
            Assert.True(backend.StartRendering(true, unchecked((int)0xFF000000), target, false), backend.GetError());
            backend.SetShader(ShaderName.display2d_normal);
            backend.SetVertexBuffer(small);
            Assert.True(backend.Draw(PrimitiveType.TriangleList, 0, 1), backend.GetError());

            var (r, g, b, _) = ReadPixel(gl, 32, 32);
            backend.EndFrame();
            Assert.Equal((0, 0, 255), ((int)r, (int)g, (int)b));   // still blue after the copy
        });
    }

    [SkippableFact]
    public void Draw_data_streams_vertices_without_a_buffer()
    {
        WithBackend((backend, gl) =>
        {
            backend.DeclareShader(ShaderName.display2d_normal, "passthrough", PassThroughVertex, PassThroughFragment);
            var target = new Texture(64, 64, TextureFormat.Bgra8);
            var vb = new VertexBuffer();
            backend.SetVertexBufferData(vb, MemoryMarshal.AsBytes(FullscreenTriangle(-1).AsSpan()), 3 * FlatVertex.Stride, VertexFormat.Flat);

            backend.BeginFrame(0, new Size(64, 64));
            backend.StartRendering(true, unchecked((int)0xFF000000), target, false);
            backend.SetShader(ShaderName.display2d_normal);
            backend.SetVertexBuffer(vb);
            Assert.True(backend.DrawData(PrimitiveType.TriangleList, 0, 1, MemoryMarshal.AsBytes(FullscreenTriangle(unchecked((int)0xFFFFFF00)).AsSpan())), backend.GetError());

            var (r, g, b, _) = ReadPixel(gl, 10, 10);
            backend.EndFrame();
            Assert.Equal((255, 255, 0), ((int)r, (int)g, (int)b));
        });
    }

    [SkippableFact]
    public void Shader_errors_are_reported_with_the_compiler_log()
    {
        WithBackend((backend, gl) =>
        {
            backend.DeclareShader(ShaderName.display2d_normal, "broken", PassThroughVertex, "this is not glsl");
            var vb = new VertexBuffer();
            backend.SetVertexBufferData(vb, MemoryMarshal.AsBytes(FullscreenTriangle(-1).AsSpan()), 3 * FlatVertex.Stride, VertexFormat.Flat);

            backend.BeginFrame(0, new Size(64, 64));
            backend.SetShader(ShaderName.display2d_normal);
            backend.SetVertexBuffer(vb);
            Assert.False(backend.Draw(PrimitiveType.TriangleList, 0, 1));
            Assert.Contains("broken", backend.GetError());
            backend.EndFrame();
        });
    }
}

[Collection("OpenGL")]
public class UdbShaderTests : IDisposable
{
    private readonly GlHost host;
    private readonly string appdir = TestAssets.CreateAppDirectory();

    public UdbShaderTests(GlFixture fixture) => host = fixture.Host;

    public void Dispose()
    {
        General.ShutdownHeadless();
        if (System.IO.Directory.Exists(appdir)) System.IO.Directory.Delete(appdir, true);
    }

    [SkippableFact]
    public void Every_udb_shader_compiles_and_links_in_both_alpha_test_variants()
    {
        Skip.IfNot(host.Available, host.UnavailableReason);

        General.InitializeHeadless(System.IO.Path.Combine(appdir, "UDBuilder.default.cfg"), System.IO.Path.Combine(appdir, "Compilers"));

        host.Invoke(() =>
        {
            var backend = new GlRenderBackend();
            backend.AttachContext(host.Gl, false);
            var device = new RenderDevice(backend);   // declares and parses all UDB shaders
            try
            {
                var vb = new VertexBuffer();
                device.SetBufferData(vb, FullscreenQuadVertices());

                backend.BeginFrame(0, new Size(64, 64));
                device.StartRendering(true, new Color4(0, 0, 0, 1));
                device.SetVertexBuffer(vb);

                var failures = new List<string>();
                foreach (ShaderName shader in Enum.GetValues(typeof(ShaderName)))
                {
                    if (shader.ToString().Contains("_p")) continue;   // numbering gaps the original enum keeps for alignment
                    foreach (bool alphatest in new[] { false, true })
                    {
                        device.SetAlphaTestEnable(alphatest);
                        device.SetShader(shader);
                        try { device.Draw(PrimitiveType.TriangleList, 0, 1); }
                        catch (RenderDeviceException e) { failures.Add(shader + (alphatest ? " (alpha test)" : "") + ": " + e.Message); }
                    }
                }
                backend.EndFrame();

                Assert.True(failures.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, failures));
            }
            finally { device.Dispose(); }
        });
    }

    private static FlatVertex[] FullscreenQuadVertices()
    {
        FlatVertex V(float x, float y) => new FlatVertex { x = x, y = y, z = 0, c = -1 };
        return new[] { V(-1, -1), V(3, -1), V(-1, 3) };
    }
}
