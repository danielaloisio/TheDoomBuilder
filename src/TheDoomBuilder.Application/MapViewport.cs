using System;
using System.Collections.Generic;
using CodeImp.DoomBuilder;
using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using TheDoomBuilder.Rendering;
using Silk.NET.OpenGL;

namespace TheDoomBuilder.Application;

/// <summary>
/// The map display. Avalonia owns the GL context and only makes it current inside <see cref="OnOpenGlRender"/>, so this control
/// brackets every frame with <see cref="GlRenderBackend.BeginFrame"/>/<see cref="GlRenderBackend.EndFrame"/> and asks the
/// editor to paint through <see cref="Paint"/>. Everything the editor does to the GPU between frames is queued by the backend.
/// </summary>
public class MapViewport : OpenGlControlBase
{
    private GL silk;
    private bool firstFrameLogged;
    private bool initCalled;
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
        Backend.CommandFailed -= OnBackendProblem;
        Backend.Diagnostic -= OnBackendProblem;
        Backend = new GlRenderBackend();
        Backend.FrameRequested += OnBackendFrameRequested;
        Backend.ContextRestored += OnBackendContextRestored;
        Backend.CommandFailed += OnBackendProblem;
        Backend.Diagnostic += OnBackendProblem;
        UpdateSurfaceSize();
        return Backend;
    }

    // Modes that draw straight from mouse events present outside a frame: the frame that shows it is asked for here
    private void OnBackendFrameRequested() => Dispatcher.UIThread.Post(RequestRedraw);

    private void OnBackendContextRestored() => ContextRestored?.Invoke();

    private void OnBackendProblem(string message) => LogOnce("error: " + message);

    // What reaches stderr is lost when the app is opened from the desktop (macOS .app, Windows), so what is needed to tell
    // why nothing is drawn goes to the log file too. Each message once, a frame loop would repeat it 60 times a second.
    private static readonly HashSet<string> logged = new HashSet<string>();

    private static void LogOnce(string message)
    {
        lock (logged)
        {
            if (!logged.Add(message)) return;
        }
        Console.Error.WriteLine("[GL] " + message);
        try { General.WriteLogLine("[GL] " + message); }
        catch (Exception) { }
    }

    private string ContextDescription(GlInterface gl)
    {
        GlVersion v = gl.ContextInfo.Version;
        return Backend.GlInfo + " | context " + v.Type + " " + v.Major + "." + v.Minor + " | scale " +
               (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0);
    }

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
        Backend.CommandFailed += OnBackendProblem;
        Backend.Diagnostic += OnBackendProblem;
        LogOnce("viewport created");
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LogOnce("viewport attached to " + (TopLevel.GetTopLevel(this)?.GetType().Name ?? "nothing"));

        // The context is made by Avalonia once the control is shown: if nothing came of it, say what the control knows
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        watchdog.Tick += (s, a) =>
        {
            watchdog.Stop();
            if (!initCalled)
                LogOnce("OnOpenGlInit was not called 4 s after the viewport was attached (visible " + IsEffectivelyVisible +
                        ", bounds " + Bounds.Width + "x" + Bounds.Height + ", attached " + IsAttachedToVisualTree() + ")");
        };
        watchdog.Start();
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) != null;

    protected override void OnOpenGlLost()
    {
        string version;
        try { version = GlVersion.ToString(); }
        catch (Exception) { version = "unknown"; }
        LogOnce("Avalonia reports the OpenGL context as lost or unavailable (version " + version + ")");
        base.OnOpenGlLost();
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        initCalled = true;
        try
        {
            silk = GL.GetApi(gl.GetProcAddress);
            gles = gl.ContextInfo.Version.Type == GlProfileType.OpenGLES;
            Backend.AttachContext(silk, gles);
            UpdateSurfaceSize();
            LogOnce("context ready: " + ContextDescription(gl));
        }
        catch (Exception e)
        {
            LogOnce("context setup failed: " + e);
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
            try
            {
                Backend.AttachContext(silk, gles);
                LogOnce("context ready (new backend): " + ContextDescription(gl));
            }
            catch (Exception e) { LogOnce("context setup failed: " + e); }
        }
        if (!Backend.HasContext) return;

        PixelSize size = PixelSize;
        if (!firstFrameLogged)
        {
            firstFrameLogged = true;
            LogOnce("first frame: framebuffer " + fb + ", surface " + size.Width + "x" + size.Height);
        }
        Backend.BeginFrame(fb, new System.Drawing.Size(size.Width, size.Height));
        try
        {
            Paint?.Invoke();
            FramePainted?.Invoke(silk, fb, size);
        }
        catch (Exception e)
        {
            LogOnce("frame failed: " + e);
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
