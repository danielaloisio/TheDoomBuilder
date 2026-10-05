using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using DoomBuilder.Rendering;
using Silk.NET.OpenGL;

namespace DoomBuilder.App;

/// <summary>
/// The map display. Avalonia owns the GL context and only makes it current inside <see cref="OnOpenGlRender"/>, so this control
/// brackets every frame with <see cref="GlRenderBackend.BeginFrame"/>/<see cref="GlRenderBackend.EndFrame"/> and asks the
/// editor to paint through <see cref="Paint"/>. Everything the editor does to the GPU between frames is queued by the backend.
/// </summary>
public class MapViewport : OpenGlControlBase
{
    private GL silk;

    /// <summary>The backend the Core renders with. It exists before the GL context does.</summary>
    public GlRenderBackend Backend { get; } = new GlRenderBackend();

    /// <summary>Raised inside a frame (context current): the editor draws the map here.</summary>
    public event Action Paint;

    /// <summary>Raised once, with the reason, if the GL context could not be set up.</summary>
    public event Action<string> ContextFailed;

    /// <summary>Raised after the first frame that finished painting (used by the screenshot hook and status line).</summary>
    public event Action<GL, int, PixelSize> FramePainted;

    public string GlInfo { get { return Backend.GlInfo; } }

    /// <summary>
    /// The element that receives the pointer events for this display. A GL control is not hit-testable on its own, so the
    /// window puts a transparent panel around it; anything that listens to the pointer (exclusive mouse mode) must use this.
    /// </summary>
    public Control InputSurface { get; set; }

    public MapViewport()
    {
        // Modes that draw straight from mouse events present outside a frame: the frame that shows it is asked for here
        Backend.FrameRequested += () => Dispatcher.UIThread.Post(RequestRedraw);
        ClipToBounds = true;
        Focusable = true;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateSurfaceSize();
        RequestRedraw();
    }

    /// <summary>Size in device pixels (what GL draws into), not in layout units.</summary>
    public PixelSize PixelSize
    {
        get
        {
            double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
            return new PixelSize(Math.Max(1, (int)Math.Round(Bounds.Width * scale)), Math.Max(1, (int)Math.Round(Bounds.Height * scale)));
        }
    }

    private void UpdateSurfaceSize()
    {
        PixelSize size = PixelSize;
        Backend.SetSurfaceSize(new System.Drawing.Size(size.Width, size.Height));
    }

    /// <summary>How many frames were asked for (diagnostics and tests).</summary>
    public int FrameRequests { get; private set; }

    public void RequestRedraw()
    {
        FrameRequests++;
        RequestNextFrameRendering();
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        try
        {
            silk = GL.GetApi(gl.GetProcAddress);
            bool gles = gl.ContextInfo.Version.Type == GlProfileType.OpenGLES;
            Backend.AttachContext(silk, gles);
            UpdateSurfaceSize();
            Console.WriteLine("[GL] " + Backend.GlInfo);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("[GL] context setup failed: " + e);
            Dispatcher.UIThread.Post(() => ContextFailed?.Invoke(e.Message));
            return;
        }
        RequestRedraw();
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (!Backend.HasContext) return;

        PixelSize size = PixelSize;
        Backend.BeginFrame(fb, new System.Drawing.Size(size.Width, size.Height));
        try
        {
            Paint?.Invoke();
            FramePainted?.Invoke(silk, fb, size);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("[GL] frame failed: " + e);
        }
        finally
        {
            Backend.EndFrame();
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        Backend.DetachContext();
        silk = null;
    }
}
