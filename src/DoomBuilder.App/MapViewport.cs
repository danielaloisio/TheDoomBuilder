using System;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using DoomBuilder.Rendering;
using Silk.NET.OpenGL;

namespace DoomBuilder.App;

/// <summary>
/// Fase 0 prototype of the map viewport. Binds Silk.NET to the context Avalonia owns.
/// </summary>
public class MapViewport : OpenGlControlBase
{
    private GL? silk;
    private TriangleRenderer? renderer;

    public string? GlInfo { get; private set; }
    public string? Error { get; private set; }
    public event Action? InfoChanged;

    protected override void OnOpenGlInit(GlInterface gl)
    {
        try
        {
            silk = GL.GetApi(gl.GetProcAddress);
            bool gles = gl.ContextInfo.Version.Type == GlProfileType.OpenGLES;
            renderer = new TriangleRenderer(silk, gles);
            GlInfo = renderer.Info;
        }
        catch (Exception e)
        {
            Error = e.Message;
        }
        Console.WriteLine(Error is null ? $"[GL] {GlInfo}" : $"[GL] ERRO: {Error}");
        Dispatcher.UIThread.Post(() => InfoChanged?.Invoke());
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        renderer?.Render((int)(Bounds.Width * scale), (int)(Bounds.Height * scale));
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        renderer?.Dispose();
        renderer = null;
        silk?.Dispose();
        silk = null;
    }
}
