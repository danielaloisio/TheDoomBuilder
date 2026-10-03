using Silk.NET.OpenGL;

namespace DoomBuilder.Rendering;

/// <summary>
/// Fase 0 prototype: draws a single colored triangle to validate the GL pipeline
/// (shader compile, VAO/VBO, draw) on every platform. Replaced by the real
/// IRenderDevice in Phase 2.
/// </summary>
public sealed class TriangleRenderer : IDisposable
{
    private readonly GL gl;
    private uint program, vao, vbo;

    public string Info { get; }

    public TriangleRenderer(GL gl, bool gles)
    {
        this.gl = gl;
        string header = gles ? "#version 300 es\nprecision mediump float;\n" : "#version 330 core\n";

        string vs = header + @"
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec3 aColor;
out vec3 vColor;
void main() { vColor = aColor; gl_Position = vec4(aPos, 0.0, 1.0); }";
        string fs = header + @"
in vec3 vColor;
out vec4 outColor;
void main() { outColor = vec4(vColor, 1.0); }";

        program = Link(Compile(ShaderType.VertexShader, vs), Compile(ShaderType.FragmentShader, fs));

        float[] data =
        {
            //  x      y      r  g  b
            -0.6f, -0.6f,   1, 0, 0,
             0.6f, -0.6f,   0, 1, 0,
             0.0f,  0.6f,   0, 0, 1,
        };

        vao = gl.GenVertexArray();
        gl.BindVertexArray(vao);
        vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        gl.BufferData<float>(BufferTargetARB.ArrayBuffer, data, BufferUsageARB.StaticDraw);
        const uint stride = 5 * sizeof(float);
        gl.EnableVertexAttribArray(0);
        unsafe
        {
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
        }
        gl.BindVertexArray(0);

        Info = $"{gl.GetStringS(StringName.Renderer)} | GL {gl.GetStringS(StringName.Version)} | GLSL {gl.GetStringS(StringName.ShadingLanguageVersion)}";
    }

    public void Render(int width, int height)
    {
        gl.Viewport(0, 0, (uint)Math.Max(width, 1), (uint)Math.Max(height, 1));
        gl.ClearColor(0.10f, 0.10f, 0.12f, 1f);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        gl.UseProgram(program);
        gl.BindVertexArray(vao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        gl.BindVertexArray(0);
    }

    private uint Compile(ShaderType type, string source)
    {
        uint s = gl.CreateShader(type);
        gl.ShaderSource(s, source);
        gl.CompileShader(s);
        gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0)
            throw new InvalidOperationException($"{type} compile failed: {gl.GetShaderInfoLog(s)}");
        return s;
    }

    private uint Link(uint vs, uint fs)
    {
        uint p = gl.CreateProgram();
        gl.AttachShader(p, vs);
        gl.AttachShader(p, fs);
        gl.LinkProgram(p);
        gl.GetProgram(p, ProgramPropertyARB.LinkStatus, out int ok);
        if (ok == 0)
            throw new InvalidOperationException($"Program link failed: {gl.GetProgramInfoLog(p)}");
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        return p;
    }

    public void Dispose()
    {
        gl.DeleteBuffer(vbo);
        gl.DeleteVertexArray(vao);
        gl.DeleteProgram(program);
    }
}
