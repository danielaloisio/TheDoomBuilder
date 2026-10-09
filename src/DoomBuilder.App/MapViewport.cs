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
    private bool gles;

    /// <summary>The backend the Core renders with. It exists before the GL context does.</summary>
    public GlRenderBackend Backend { get; private set; } = new GlRenderBackend();

    /// <summary>Raised when a lost GL context was replaced and the backend put back what it had copies of.</summary>
    public event Action ContextRestored;

    /// <summary>
    /// The backend for a map that is being opened. Closing a map disposes the backend its render device used, and a disposed backend never
    /// draws again (no context, so no frame): the next map gets a new one, which takes the window's GL context on its first frame.
    /// </summary>
    public GlRenderBackend RenewBackend()
    {
        if (!Backend.IsDisposed) return Backend;

        Backend.FrameRequested -= OnBackendFrameRequested;
        Backend.ContextRestored -= OnBackendContextRestored;
        Backend = new GlRenderBackend();
        Backend.FrameRequested += OnBackendFrameRequested;
        Backend.ContextRestored += OnBackendContextRestored;
        UpdateSurfaceSize();
        return Backend;
    }

    // Modes that draw straight from mouse events present outside a frame: the frame that shows it is asked for here
    private void OnBackendFrameRequested() => Dispatcher.UIThread.Post(RequestRedraw);

    private void OnBackendContextRestored() => ContextRestored?.Invoke();

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
        Backend.FrameRequested += OnBackendFrameRequested;
        Backend.ContextRestored += OnBackendContextRestored;
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

    /// <summary>The window moved to a screen with another scale (or the system scale changed): the surface has a new size in device pixels.</summary>
    public void OnScaleChanged()
    {
        UpdateSurfaceSize();
        RequestRedraw();
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
            gles = gl.ContextInfo.Version.Type == GlProfileType.OpenGLES;
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
        // A backend made for a map opened after another one was closed meets the context here, where it is current
        if (!Backend.HasContext && silk != null)
        {
            try { Backend.AttachContext(silk, gles); }
            catch (Exception e) { Console.Error.WriteLine("[GL] context setup failed: " + e); }
        }
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
