using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Geometry;

namespace DoomBuilder.App.Input;

/// <summary>
/// Exclusive mouse mode on a viewport: hides the pointer and reports its movement as relative deltas by warping it back to the
/// center of the view after every movement (see <see cref="RelativePointerTracker"/>).
/// </summary>
public sealed class ViewportMouseCapture : IMouseCapture
{
    private readonly MapViewport viewport;
    private readonly IPointerWarp warp;
    private readonly RelativePointerTracker tracker = new RelativePointerTracker();
    private readonly IRelativeMotionSource raw;
    private readonly Cursor previouscursor;
    private readonly Control surface;
    private PixelPoint screencenter;

    /// <param name="raw">Device movement that does not depend on the pointer's position (X11/XWayland); the capture owns it. Without one, the movement is measured from the pointer's positions.</param>
    public ViewportMouseCapture(MapViewport viewport, IPointerWarp warp, IRelativeMotionSource raw = null)
    {
        this.viewport = viewport;
        this.warp = warp;
        this.raw = raw;

        tracker.WarpGivenUp += () => CodeImp.DoomBuilder.General.WriteLogLine("Mouse look: moving the pointer has no effect in this session (XWayland, for one), so movement is read between positions instead.");
        tracker.UsesWarp = warp.Supported;     // where the pointer cannot be moved, the movement is read between successive positions
        // The cursor that shows is the one of the control that receives the pointer (the transparent panel over the GL view), not the GL view's
        surface = viewport.InputSurface ?? viewport;
        previouscursor = surface.Cursor;
        surface.Cursor = new Cursor(StandardCursorType.None);
        viewport.Cursor = surface.Cursor;

        UpdateCenter();
        warp.MoveTo(screencenter.X, screencenter.Y);
        tracker.Begin();

        if (raw == null) surface.PointerMoved += OnPointerMoved;     // with raw motion the positions are not needed at all (UDB ignores them too)
        viewport.SizeChanged += OnSizeChanged;
    }

    // Center of the view in view pixels (what the tracker works in) and in screen pixels (what the warp needs)
    private void UpdateCenter()
    {
        PixelSize size = viewport.PixelSize;
        tracker.SetCenter(size.Width / 2.0, size.Height / 2.0);

        double scale = TopLevel.GetTopLevel(viewport)?.RenderScaling ?? 1.0;
        screencenter = viewport.PointToScreen(new Point(size.Width / 2.0 / scale, size.Height / 2.0 / scale));
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateCenter();

    private void OnPointerMoved(object sender, PointerEventArgs e)
    {
        double scale = TopLevel.GetTopLevel(viewport)?.RenderScaling ?? 1.0;
        Point p = e.GetPosition(surface);
        if (tracker.Feed(p.X * scale, p.Y * scale))
            warp.MoveTo(screencenter.X, screencenter.Y);
    }

    public Vector2D Poll() => raw != null ? raw.Poll() : tracker.Poll();

    public void Dispose()
    {
        surface.PointerMoved -= OnPointerMoved;
        raw?.Dispose();
        viewport.SizeChanged -= OnSizeChanged;
        surface.Cursor = previouscursor;
        viewport.Cursor = null;

        // Like UDB: leave the pointer in the middle of the view
        warp.MoveTo(screencenter.X, screencenter.Y);
    }
}
